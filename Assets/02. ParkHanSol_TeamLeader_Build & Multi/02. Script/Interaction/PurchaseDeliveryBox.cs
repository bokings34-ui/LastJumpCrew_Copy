using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Shop;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Scene-placed ship delivery box that mirrors Host-confirmed deliveries to every client.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PurchaseDeliveryBox : NetworkBehaviour
    {
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private UtilityToolBoxStorageSlotInteractable[] deliverySlots;
        [SerializeField] private Transform[] overflowDropPoints;

        private readonly NetworkList<FixedString64Bytes> deliveredItemIds = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private int nextOverflowDropPoint;
        private int appliedNetworkDeliveryCount;

        private void Start()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                DeliverPendingItems();
            }
        }

        public override void OnNetworkSpawn()
        {
            deliveredItemIds.OnListChanged += HandleDeliveredItemsChanged;
            if (IsServer)
            {
                DeliverPendingItems();
                appliedNetworkDeliveryCount = deliveredItemIds.Count;
            }
            else
            {
                ApplyPendingNetworkDeliveries();
            }
        }

        public override void OnNetworkDespawn()
        {
            deliveredItemIds.OnListChanged -= HandleDeliveredItemsChanged;
            base.OnNetworkDespawn();
        }

        public bool TryReceive(UtilityItemPrefabData itemPrefabData)
        {
            if (!ValidateSetup() || itemPrefabData == null)
            {
                return false;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return TryApplyDelivery(itemPrefabData);
            }

            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_FAILED reason=server_required box={name}", this);
                return false;
            }

            if (!TryApplyDelivery(itemPrefabData))
            {
                return false;
            }

            deliveredItemIds.Add(new FixedString64Bytes(itemPrefabData.ItemId));
            appliedNetworkDeliveryCount = deliveredItemIds.Count;
            return true;
        }

        private void DeliverPendingItems()
        {
            if (!ValidateSetup())
            {
                return;
            }

            var deliveryService = SessionPurchaseDeliveryService.Instance;
            if (deliveryService == null)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_FAILED reason=service_missing box={name}", this);
                return;
            }

            deliveryService.DeliverTo(this);
        }

        private void HandleDeliveredItemsChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
        {
            if (!IsServer)
            {
                ApplyPendingNetworkDeliveries();
            }
        }

        private void ApplyPendingNetworkDeliveries()
        {
            while (appliedNetworkDeliveryCount < deliveredItemIds.Count)
            {
                var itemId = deliveredItemIds[appliedNetworkDeliveryCount].ToString();
                if (!TryResolveItemData(itemId, out var itemPrefabData))
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_SYNC_FAILED reason=item_missing box={name} item={itemId}",
                        this);
                    return;
                }

                if (!TryApplyDelivery(itemPrefabData))
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_SYNC_FAILED reason=client_apply_rejected box={name} item={itemId}",
                        this);
                    return;
                }

                appliedNetworkDeliveryCount++;
            }
        }

        private bool TryApplyDelivery(UtilityItemPrefabData itemPrefabData)
        {
            foreach (var slot in deliverySlots)
            {
                if (slot != null && slot.TryReceiveDelivery(itemPrefabData))
                {
                    return true;
                }
            }

            return TryDropOverflowItem(itemPrefabData);
        }

        private bool TryResolveItemData(string itemId, out UtilityItemPrefabData itemPrefabData)
        {
            itemPrefabData = null;
            if (catalog == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            foreach (var product in catalog.Products)
            {
                if (product != null &&
                    product.ItemPrefabData != null &&
                    product.ItemPrefabData.ItemId == itemId)
                {
                    itemPrefabData = product.ItemPrefabData;
                    return true;
                }
            }

            return false;
        }

        private bool TryDropOverflowItem(UtilityItemPrefabData itemPrefabData)
        {
            if (overflowDropPoints == null || overflowDropPoints.Length == 0)
            {
                Debug.LogWarning(
                    $"PHS_PURCHASE_DELIVERY_WAITING reason=delivery_box_full box={name} item={itemPrefabData.ItemId}");
                return false;
            }

            if (!itemPrefabData.HasDroppedPrefab)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_OVERFLOW_FAILED reason=dropped_prefab_missing box={name}");
                return false;
            }

            var dropPoint = overflowDropPoints[nextOverflowDropPoint % overflowDropPoints.Length];
            nextOverflowDropPoint++;
            if (dropPoint == null)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_OVERFLOW_FAILED reason=drop_point_missing box={name}");
                return false;
            }

            var droppedItem = Instantiate(itemPrefabData.DroppedPrefab, dropPoint.position, dropPoint.rotation);
            droppedItem.name = $"PHS_OverflowDelivery_{itemPrefabData.ItemId}";
            if (droppedItem.TryGetComponent<UtilityItemObject>(out var itemObject))
            {
                itemObject.OnDropped(dropPoint.position);
            }

            Debug.Log(
                $"PHS_PURCHASE_DELIVERY_OVERFLOW_DROPPED box={name} item={itemPrefabData.ItemId} point={dropPoint.name}");
            return true;
        }

        private bool ValidateSetup()
        {
            var valid = true;
            if (catalog == null)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_SETUP_FAILED reason=catalog_missing box={name}", this);
                valid = false;
            }

            if (deliverySlots == null || deliverySlots.Length == 0)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_SETUP_FAILED reason=slots_missing box={name}", this);
                valid = false;
            }

            return valid;
        }
    }
}
