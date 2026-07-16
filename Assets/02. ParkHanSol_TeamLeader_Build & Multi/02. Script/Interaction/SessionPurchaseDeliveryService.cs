using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Shop;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Persists Host-confirmed shop purchases while gameplay scenes change.</summary>
    [DefaultExecutionOrder(-190)]
    public sealed class SessionPurchaseDeliveryService : MonoBehaviour, IShopDeliveryService
    {
        public static SessionPurchaseDeliveryService Instance { get; private set; }

        // Network scene transitions can recreate the scene's online-session object.
        // Keep the Host queue independent from that scene instance until delivery succeeds.
        private static readonly Queue<UtilityItemPrefabData> pendingItems = new();

        public int PendingCount => pendingItems.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            pendingItems.Clear();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryQueueDelivery(UtilityItemPrefabData itemPrefabData)
        {
            return TryQueueDeliveries(new[] { itemPrefabData });
        }

        public bool CanQueueDeliveries(IReadOnlyList<UtilityItemPrefabData> itemPrefabData)
        {
            if (itemPrefabData == null || itemPrefabData.Count == 0)
            {
                Debug.LogError($"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=items_missing service={name}");
                return false;
            }

            for (var index = 0; index < itemPrefabData.Count; index++)
            {
                if (itemPrefabData[index] == null)
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=item_missing service={name} index={index}");
                    return false;
                }
            }

            return true;
        }

        public bool TryQueueDeliveries(IReadOnlyList<UtilityItemPrefabData> itemPrefabData)
        {
            if (!CanQueueDeliveries(itemPrefabData))
            {
                return false;
            }

            foreach (var item in itemPrefabData)
            {
                pendingItems.Enqueue(item);
            }

            Debug.Log(
                $"PHS_PURCHASE_DELIVERY_BATCH_QUEUED service={name} count={itemPrefabData.Count} pending={pendingItems.Count}");
            return true;
        }

        public void QueueDelivery(UtilityItemPrefabData itemPrefabData)
        {
            TryQueueDelivery(itemPrefabData);
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
