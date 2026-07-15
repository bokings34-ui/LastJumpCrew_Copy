using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Scene-placed ship delivery box that accepts queued purchases into explicit Inspector slots.</summary>
    public sealed class PurchaseDeliveryBox : MonoBehaviour
    {
        [SerializeField] private UtilityToolBoxStorageSlotInteractable[] deliverySlots;
        [SerializeField] private Transform[] overflowDropPoints;

        private int nextOverflowDropPoint;

        private void Start()
        {
            if (SessionPurchaseDeliveryService.Instance != null)
            {
                SessionPurchaseDeliveryService.Instance.DeliverTo(this);
            }
        }

        public bool TryReceive(UtilityItemPrefabData itemPrefabData)
        {
            if (deliverySlots == null)
            {
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

        private bool TryDropOverflowItem(UtilityItemPrefabData itemPrefabData)
        {
            if (overflowDropPoints == null || overflowDropPoints.Length == 0)
            {
                Debug.LogWarning($"PHS_PURCHASE_DELIVERY_WAITING reason=delivery_box_full box={name} item={itemPrefabData.ItemId}");
                return false;
            }

            if (itemPrefabData == null || !itemPrefabData.HasDroppedPrefab)
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

            Debug.Log($"PHS_PURCHASE_DELIVERY_OVERFLOW_DROPPED box={name} item={itemPrefabData.ItemId} point={dropPoint.name}");
            return true;
        }
    }
}
