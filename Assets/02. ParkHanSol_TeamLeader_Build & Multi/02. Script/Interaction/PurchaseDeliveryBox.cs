using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
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
        [SerializeField] private string deliveryBoxId = "ship_delivery_box";

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

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                DeliverNetworkLedgerItems();
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

        private void DeliverNetworkLedgerItems()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            var ledger = NetworkRunSessionRoot.Instance?.Economy;
            if (ledger == null || !ledger.IsSpawned || ledger.Revision == 0U)
            {
                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_FAILED reason=run_economy_ledger_missing box={name}",
                    this);
                return;
            }

            var claimReason = string.Empty;
            while (ledger.TryClaimNextDeliveryServer(
                       deliveryBoxId,
                       out var entry,
                       out claimReason))
            {
                var itemId = entry.ItemId.ToString();
                if (!TryResolveItemData(itemId, out var itemPrefabData)
                    || !TryReceive(itemPrefabData))
                {
                    if (!ledger.TryReleaseDeliveryClaimServer(
                            entry.EntryId,
                            deliveryBoxId,
                            out var releaseReason))
                    {
                        Debug.LogError(
                            $"PHS_PURCHASE_DELIVERY_INVARIANT_FAILED reason=claim_release_failed box={name} entry={entry.EntryId} detail={releaseReason}",
                            this);
                    }

                    Debug.LogWarning(
                        $"PHS_PURCHASE_DELIVERY_WAITING reason=box_apply_rejected box={name} entry={entry.EntryId} item={itemId}",
                        this);
                    return;
                }

                if (!ledger.TryCompleteDeliveryServer(
                        entry.EntryId,
                        deliveryBoxId,
                        out var completeReason))
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_INVARIANT_FAILED reason=ledger_complete_failed box={name} entry={entry.EntryId} detail={completeReason}",
                        this);
                    return;
                }

                Debug.Log(
                    $"PHS_PURCHASE_DELIVERY_COMPLETED box={name} entry={entry.EntryId} item={itemId} pending={ledger.Snapshot.PendingDeliveryCount}",
                    this);
            }

            if (claimReason != "pending_delivery_missing")
            {
                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_FAILED reason=claim_rejected box={name} detail={claimReason}",
                    this);
            }
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
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !IsServer)
            {
                foreach (var slot in deliverySlots)
                {
                    if (slot != null && slot.IsNetworkManaged)
                    {
                        // The ToolBox NetworkList owns client slot presentation.
                        return true;
                    }
                }

                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_SYNC_FAILED reason=network_tool_box_missing box={name} item={itemPrefabData.ItemId}",
                    this);
                return false;
            }

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

            var networkManager = NetworkManager.Singleton;
            var networkSessionActive = networkManager != null && networkManager.IsListening;
            if (networkSessionActive && !IsServer)
            {
                // Server-spawned overflow object arrives through NGO. Client only advances delivery presentation state.
                return true;
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

            if (networkSessionActive)
            {
                if (!droppedItem.TryGetComponent<NetworkObject>(out var droppedNetworkObject))
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_OVERFLOW_FAILED reason=network_object_missing box={name} item={itemPrefabData.ItemId}");
                    Destroy(droppedItem);
                    return false;
                }

                droppedNetworkObject.Spawn();
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

            if (string.IsNullOrWhiteSpace(deliveryBoxId))
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_SETUP_FAILED reason=box_id_missing box={name}", this);
                valid = false;
            }

            return valid;
        }
    }
}
