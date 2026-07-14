using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Persists Host-confirmed shop purchases while gameplay scenes change.</summary>
    [DefaultExecutionOrder(-190)]
    public sealed class SessionPurchaseDeliveryService : MonoBehaviour
    {
        public static SessionPurchaseDeliveryService Instance { get; private set; }

        // Network scene transitions can recreate the scene's online-session object.
        // Keep the Host queue independent from that scene instance until delivery succeeds.
        private static readonly Queue<UtilityItemPrefabData> pendingItems = new();

        public int PendingCount => pendingItems.Count;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void QueueDelivery(UtilityItemPrefabData itemPrefabData)
        {
            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=item_missing service={name}");
                return;
            }

            pendingItems.Enqueue(itemPrefabData);
            Debug.Log($"PHS_PURCHASE_DELIVERY_QUEUED service={name} item={itemPrefabData.ItemId} pending={pendingItems.Count}");
        }

        public void DeliverTo(PurchaseDeliveryBox deliveryBox)
        {
            if (deliveryBox == null)
            {
                return;
            }

            while (pendingItems.Count > 0 && deliveryBox.TryReceive(pendingItems.Peek()))
            {
                var delivered = pendingItems.Dequeue();
                Debug.Log($"PHS_PURCHASE_DELIVERY_COMPLETED service={name} item={delivered.ItemId} pending={pendingItems.Count}");
            }
        }
    }
}
