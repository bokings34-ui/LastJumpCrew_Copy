using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Shop;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>
    /// Scene-facing delivery adapter. Network sessions commit to the persistent
    /// run economy ledger; the static queue remains only for standalone scenes.
    /// </summary>
    [DefaultExecutionOrder(-190)]
    public sealed class SessionPurchaseDeliveryService :
        MonoBehaviour,
        IShopDeliveryService,
        IShopPurchaseTransactionService
    {
        public static SessionPurchaseDeliveryService Instance { get; private set; }

        private static readonly Queue<UtilityItemDataSO> offlinePendingItems = new();

        public int PendingCount
        {
            get
            {
                if (!IsNetworkSessionActive())
                {
                    return offlinePendingItems.Count;
                }

                return TryGetNetworkLedger(out var ledger)
                    ? ledger.Snapshot.PendingDeliveryCount
                    : 0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            offlinePendingItems.Clear();
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

        public bool TryQueueDelivery(UtilityItemDataSO itemPrefabData)
        {
            return TryQueueDeliveries(new[] { itemPrefabData });
        }

        public bool CanQueueDeliveries(IReadOnlyList<UtilityItemDataSO> itemPrefabData)
        {
            if (!ValidateItems(itemPrefabData))
            {
                return false;
            }

            if (!IsNetworkSessionActive())
            {
                return true;
            }

            if (!TryGetNetworkLedger(out _))
            {
                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=run_economy_ledger_missing service={name}",
                    this);
                return false;
            }

            return true;
        }

        public bool TryQueueDeliveries(IReadOnlyList<UtilityItemDataSO> itemPrefabData)
        {
            if (!CanQueueDeliveries(itemPrefabData))
            {
                return false;
            }

            if (IsNetworkSessionActive())
            {
                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=atomic_purchase_commit_required service={name}",
                    this);
                return false;
            }

            foreach (var item in itemPrefabData)
            {
                offlinePendingItems.Enqueue(item);
            }

            Debug.Log(
                $"PHS_PURCHASE_DELIVERY_BATCH_QUEUED service={name} count={itemPrefabData.Count} pending={offlinePendingItems.Count}",
                this);
            return true;
        }

        public bool TryCommitPurchase(
            string transactionId,
            int totalPrice,
            IReadOnlyList<ShopPurchaseDeliveryRequest> deliveries,
            ulong purchaserClientId,
            out string reason)
        {
            if (!IsNetworkSessionActive())
            {
                reason = "network_session_required";
                return false;
            }

            if (!TryGetNetworkLedger(out var ledger))
            {
                reason = "run_economy_ledger_missing";
                return false;
            }

            if (deliveries == null || deliveries.Count == 0)
            {
                reason = "purchase_items_required";
                return false;
            }

            var purchaseIds = new string[deliveries.Count];
            var itemIds = new string[deliveries.Count];
            for (var index = 0; index < deliveries.Count; index++)
            {
                var delivery = deliveries[index];
                if (string.IsNullOrWhiteSpace(delivery.PurchaseId)
                    || delivery.ItemPrefabData == null
                    || string.IsNullOrWhiteSpace(delivery.ItemPrefabData.ItemId))
                {
                    reason = $"purchase_delivery_invalid:{index}";
                    return false;
                }

                purchaseIds[index] = delivery.PurchaseId;
                itemIds[index] = delivery.ItemPrefabData.ItemId;
            }

            return ledger.TryCommitPurchaseServer(
                transactionId,
                purchaseIds,
                itemIds,
                totalPrice,
                purchaserClientId,
                out reason);
        }

        public void QueueDelivery(UtilityItemDataSO itemPrefabData)
        {
            TryQueueDelivery(itemPrefabData);
        }

        public void DeliverTo(PurchaseDeliveryBox deliveryBox)
        {
            if (deliveryBox == null || IsNetworkSessionActive())
            {
                return;
            }

            while (offlinePendingItems.Count > 0
                   && deliveryBox.TryReceive(offlinePendingItems.Peek()))
            {
                var delivered = offlinePendingItems.Dequeue();
                Debug.Log(
                    $"PHS_PURCHASE_DELIVERY_COMPLETED service={name} item={delivered.ItemId} pending={offlinePendingItems.Count}",
                    this);
            }
        }

        private bool ValidateItems(IReadOnlyList<UtilityItemDataSO> itemPrefabData)
        {
            if (itemPrefabData == null || itemPrefabData.Count == 0)
            {
                Debug.LogError(
                    $"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=items_missing service={name}",
                    this);
                return false;
            }

            for (var index = 0; index < itemPrefabData.Count; index++)
            {
                var item = itemPrefabData[index];
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                {
                    Debug.LogError(
                        $"PHS_PURCHASE_DELIVERY_QUEUE_FAILED reason=item_missing service={name} index={index}",
                        this);
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetNetworkLedger(out NetworkRunEconomyLedger ledger)
        {
            ledger = NetworkRunSessionRoot.Instance?.Economy;
            return ledger != null
                && ledger.IsSpawned
                && ledger.Revision > 0U;
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }
    }
}
