using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Persistent, server-authoritative party wallet and purchase-delivery ledger.
    /// Purchase debit and delivery append commit through one server API.
    /// A failed delivery apply must release its claim before another box can retry it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRunEconomyLedger :
        NetworkBehaviour,
        IRunEconomyLedger
    {
        [Header("Run Economy")]
        [SerializeField, Min(0)] private int startingCredits = 500;
        [SerializeField, Min(1)] private int maximumDeliveryEntries = 128;

        private readonly NetworkVariable<NetworkRunEconomySnapshot> synchronizedSnapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkDeliveryEntry> synchronizedDeliveries = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly HashSet<string> committedTransactionIds = new(
            StringComparer.Ordinal);
        private readonly HashSet<string> committedPurchaseIds = new(
            StringComparer.Ordinal);
        private readonly Queue<NetworkListEvent<NetworkDeliveryEntry>>
            pendingDeliveryEvents = new();

        private bool isSubscribed;

        public NetworkRunEconomySnapshot Snapshot => synchronizedSnapshot.Value;
        public int Credits => Snapshot.Credits;
        public uint Revision => Snapshot.Revision;
        public int DeliveryEntryCount => synchronizedDeliveries.Count;

        public event Action<
            NetworkRunEconomySnapshot,
            NetworkRunEconomySnapshot> SnapshotChanged;
        public event Action<NetworkListEvent<NetworkDeliveryEntry>> DeliveryChanged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_RUN_ECONOMY_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            Subscribe();
            if (IsServer)
            {
                InitializeServerState();
            }

            Debug.Log(
                $"PHS_RUN_ECONOMY_READY server={IsServer} credits={Credits} revision={Revision} deliveries={DeliveryEntryCount}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            Unsubscribe();
            committedTransactionIds.Clear();
            committedPurchaseIds.Clear();
            pendingDeliveryEvents.Clear();
            base.OnNetworkDespawn();
        }

        public NetworkDeliveryEntry GetDeliveryEntryAt(int index)
        {
            if (index < 0 || index >= synchronizedDeliveries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return synchronizedDeliveries[index];
        }

        public bool TryGetDeliveryEntry(
            uint entryId,
            out NetworkDeliveryEntry entry)
        {
            if (TryFindDeliveryIndex(entryId, out var index))
            {
                entry = synchronizedDeliveries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryGetNextPendingDelivery(out NetworkDeliveryEntry entry)
        {
            for (var index = 0; index < synchronizedDeliveries.Count; index++)
            {
                if (synchronizedDeliveries[index].State != NetworkDeliveryState.Pending)
                {
                    continue;
                }

                entry = synchronizedDeliveries[index];
                return true;
            }

            entry = default;
            return false;
        }

        public bool TryAddCreditsServer(
            string transactionId,
            int amount,
            NetworkRunEconomyTransactionKind transactionKind,
            ulong actorClientId,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryPrepareTransaction(
                    transactionId,
                    amount,
                    transactionKind,
                    true,
                    out var fixedTransactionId,
                    out var transactionKey,
                    out reason))
            {
                return false;
            }

            if (amount > int.MaxValue - Credits)
            {
                reason = "credit_overflow";
                return false;
            }

            CommitWalletMutation(
                fixedTransactionId,
                transactionKey,
                transactionKind,
                amount,
                actorClientId);
            reason = null;
            return true;
        }

        public bool TrySpendCreditsServer(
            string transactionId,
            int amount,
            NetworkRunEconomyTransactionKind transactionKind,
            ulong actorClientId,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryPrepareTransaction(
                    transactionId,
                    amount,
                    transactionKind,
                    false,
                    out var fixedTransactionId,
                    out var transactionKey,
                    out reason))
            {
                return false;
            }

            if (Credits < amount)
            {
                reason = "insufficient_credits";
                return false;
            }

            CommitWalletMutation(
                fixedTransactionId,
                transactionKey,
                transactionKind,
                -amount,
                actorClientId);
            reason = null;
            return true;
        }

        public bool TryCommitPurchaseServer(
            string transactionId,
            IReadOnlyList<string> purchaseIds,
            IReadOnlyList<string> itemIds,
            int totalPrice,
            ulong purchaserClientId,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryCreateFixedTransactionId(
                    transactionId,
                    out var fixedTransactionId,
                    out var transactionKey,
                    out reason))
            {
                return false;
            }

            if (committedTransactionIds.Contains(transactionKey))
            {
                reason = "transaction_already_committed";
                return false;
            }

            if (totalPrice <= 0)
            {
                reason = "positive_purchase_price_required";
                return false;
            }

            if (purchaseIds == null
                || itemIds == null
                || purchaseIds.Count == 0
                || purchaseIds.Count != itemIds.Count)
            {
                reason = "purchase_items_required";
                return false;
            }

            if (itemIds.Count > maximumDeliveryEntries - synchronizedDeliveries.Count)
            {
                reason = "delivery_ledger_full";
                return false;
            }

            var current = Snapshot;
            var commitRevision = IncrementNonZero(current.Revision);
            var nextEntryId = current.NextDeliveryEntryId == 0U
                ? 1U
                : current.NextDeliveryEntryId;
            var stagedEntries = new NetworkDeliveryEntry[itemIds.Count];
            var stagedPurchaseKeys = new string[itemIds.Count];
            var requestPurchaseIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < itemIds.Count; index++)
            {
                if (!TryCreateFixedTransactionId(
                        purchaseIds[index],
                        out var fixedPurchaseId,
                        out var purchaseKey,
                        out reason))
                {
                    reason = $"purchase_id_invalid:{reason}";
                    return false;
                }

                if (!requestPurchaseIds.Add(purchaseKey)
                    || committedPurchaseIds.Contains(purchaseKey))
                {
                    reason = "purchase_already_committed";
                    return false;
                }

                if (!TryCreateFixedItemId(
                    itemIds[index],
                    out var fixedItemId,
                    out reason))
                {
                    return false;
                }

                if (TryFindDeliveryIndex(nextEntryId, out _))
                {
                    reason = "delivery_entry_id_exhausted";
                    return false;
                }

                stagedEntries[index] = new NetworkDeliveryEntry(
                    nextEntryId,
                    fixedTransactionId,
                    fixedPurchaseId,
                    fixedItemId,
                    purchaserClientId,
                    NetworkDeliveryState.Pending,
                    default,
                    commitRevision,
                    commitRevision);
                stagedPurchaseKeys[index] = purchaseKey;
                nextEntryId = IncrementNonZero(nextEntryId);
            }

            if (Credits < totalPrice)
            {
                reason = "insufficient_credits";
                return false;
            }

            var appendedCount = 0;
            try
            {
                for (var index = 0; index < stagedEntries.Length; index++)
                {
                    synchronizedDeliveries.Add(stagedEntries[index]);
                    appendedCount++;
                }
            }
            catch (Exception exception)
            {
                while (appendedCount > 0)
                {
                    synchronizedDeliveries.RemoveAt(
                        synchronizedDeliveries.Count - 1);
                    appendedCount--;
                }
                pendingDeliveryEvents.Clear();

                Debug.LogError(
                    $"PHS_RUN_ECONOMY_INVARIANT_FAILED operation=purchase_append exception={exception.GetType().Name}",
                    this);
                reason = "delivery_append_failed";
                return false;
            }

            committedTransactionIds.Add(transactionKey);
            for (var index = 0; index < stagedPurchaseKeys.Length; index++)
            {
                committedPurchaseIds.Add(stagedPurchaseKeys[index]);
            }
            synchronizedSnapshot.Value = new NetworkRunEconomySnapshot(
                current.Credits - totalPrice,
                commitRevision,
                IncrementNonZero(current.WalletRevision),
                IncrementNonZero(current.DeliveryRevision),
                nextEntryId,
                current.PendingDeliveryCount + stagedEntries.Length,
                current.ClaimedDeliveryCount,
                current.DeliveredCount,
                fixedTransactionId,
                NetworkRunEconomyTransactionKind.PurchaseDebit,
                -totalPrice,
                purchaserClientId);
            reason = null;
            Debug.Log(
                $"PHS_RUN_ECONOMY_PURCHASE_COMMITTED transaction={fixedTransactionId} purchaser={purchaserClientId} price={totalPrice} items={stagedEntries.Length} credits={Credits} revision={Revision}",
                this);
            return true;
        }

        public bool TryClaimNextDeliveryServer(
            string boxId,
            out NetworkDeliveryEntry entry,
            out string reason)
        {
            entry = default;
            if (!RequireServer(out reason)
                || !TryCreateFixedBoxId(boxId, out var fixedBoxId, out reason))
            {
                return false;
            }

            for (var index = 0; index < synchronizedDeliveries.Count; index++)
            {
                if (synchronizedDeliveries[index].State != NetworkDeliveryState.Pending)
                {
                    continue;
                }

                return TryClaimAtIndexServer(
                    index,
                    fixedBoxId,
                    out entry,
                    out reason);
            }

            reason = "pending_delivery_missing";
            return false;
        }

        public bool TryClaimDeliveryServer(
            uint entryId,
            string boxId,
            out NetworkDeliveryEntry entry,
            out string reason)
        {
            entry = default;
            if (!RequireServer(out reason)
                || !TryCreateFixedBoxId(boxId, out var fixedBoxId, out reason))
            {
                return false;
            }

            if (!TryFindDeliveryIndex(entryId, out var index))
            {
                reason = "delivery_entry_missing";
                return false;
            }

            return TryClaimAtIndexServer(
                index,
                fixedBoxId,
                out entry,
                out reason);
        }

        public bool TryReleaseDeliveryClaimServer(
            uint entryId,
            string boxId,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryCreateFixedBoxId(boxId, out var fixedBoxId, out reason))
            {
                return false;
            }

            if (!TryFindDeliveryIndex(entryId, out var index))
            {
                reason = "delivery_entry_missing";
                return false;
            }

            var currentEntry = synchronizedDeliveries[index];
            if (currentEntry.State != NetworkDeliveryState.Claimed)
            {
                reason = $"delivery_not_claimed:{currentEntry.State}";
                return false;
            }

            if (!currentEntry.DeliveryBoxId.Equals(fixedBoxId))
            {
                reason = "delivery_claim_owner_mismatch";
                return false;
            }

            CommitDeliveryState(
                index,
                currentEntry.WithState(
                    NetworkDeliveryState.Pending,
                    default,
                    IncrementNonZero(Snapshot.Revision)),
                "claim_released");
            reason = null;
            return true;
        }

        public bool TryCompleteDeliveryServer(
            uint entryId,
            string boxId,
            out string reason)
        {
            if (!RequireServer(out reason)
                || !TryCreateFixedBoxId(boxId, out var fixedBoxId, out reason))
            {
                return false;
            }

            if (!TryFindDeliveryIndex(entryId, out var index))
            {
                reason = "delivery_entry_missing";
                return false;
            }

            var currentEntry = synchronizedDeliveries[index];
            if (currentEntry.State == NetworkDeliveryState.Delivered
                && currentEntry.DeliveryBoxId.Equals(fixedBoxId))
            {
                reason = null;
                return true;
            }

            if (currentEntry.State != NetworkDeliveryState.Claimed)
            {
                reason = $"delivery_not_claimed:{currentEntry.State}";
                return false;
            }

            if (!currentEntry.DeliveryBoxId.Equals(fixedBoxId))
            {
                reason = "delivery_claim_owner_mismatch";
                return false;
            }

            CommitDeliveryState(
                index,
                currentEntry.WithState(
                    NetworkDeliveryState.Delivered,
                    fixedBoxId,
                    IncrementNonZero(Snapshot.Revision)),
                "delivered");
            reason = null;
            return true;
        }

        private void InitializeServerState()
        {
            startingCredits = Mathf.Max(0, startingCredits);
            maximumDeliveryEntries = Mathf.Max(1, maximumDeliveryEntries);
            committedTransactionIds.Clear();
            committedPurchaseIds.Clear();
            pendingDeliveryEvents.Clear();
            synchronizedDeliveries.Clear();
            pendingDeliveryEvents.Clear();
            synchronizedSnapshot.Value = new NetworkRunEconomySnapshot(
                startingCredits,
                1U,
                1U,
                1U,
                1U,
                0,
                0,
                0,
                default,
                NetworkRunEconomyTransactionKind.None,
                0,
                NetworkManager.ServerClientId);
        }

        private bool TryPrepareTransaction(
            string transactionId,
            int amount,
            NetworkRunEconomyTransactionKind transactionKind,
            bool isCredit,
            out FixedString128Bytes fixedTransactionId,
            out string transactionKey,
            out string reason)
        {
            fixedTransactionId = default;
            transactionKey = null;
            if (!TryCreateFixedTransactionId(
                transactionId,
                out fixedTransactionId,
                out transactionKey,
                out reason))
            {
                return false;
            }

            if (committedTransactionIds.Contains(transactionKey))
            {
                reason = "transaction_already_committed";
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_amount_required";
                return false;
            }

            if (isCredit && !IsCreditKind(transactionKind))
            {
                reason = $"credit_transaction_kind_required:{transactionKind}";
                return false;
            }

            if (!isCredit && !IsDebitKind(transactionKind))
            {
                reason = transactionKind == NetworkRunEconomyTransactionKind.PurchaseDebit
                    ? "use_purchase_commit_api"
                    : $"debit_transaction_kind_required:{transactionKind}";
                return false;
            }

            reason = null;
            return true;
        }

        private void CommitWalletMutation(
            FixedString128Bytes transactionId,
            string transactionKey,
            NetworkRunEconomyTransactionKind transactionKind,
            int creditDelta,
            ulong actorClientId)
        {
            var current = Snapshot;
            committedTransactionIds.Add(transactionKey);
            synchronizedSnapshot.Value = new NetworkRunEconomySnapshot(
                current.Credits + creditDelta,
                IncrementNonZero(current.Revision),
                IncrementNonZero(current.WalletRevision),
                current.DeliveryRevision,
                current.NextDeliveryEntryId,
                current.PendingDeliveryCount,
                current.ClaimedDeliveryCount,
                current.DeliveredCount,
                transactionId,
                transactionKind,
                creditDelta,
                actorClientId);
            Debug.Log(
                $"PHS_RUN_ECONOMY_WALLET_COMMITTED transaction={transactionId} kind={transactionKind} delta={creditDelta} actor={actorClientId} credits={Credits} revision={Revision}",
                this);
        }

        private bool TryClaimAtIndexServer(
            int index,
            FixedString64Bytes boxId,
            out NetworkDeliveryEntry entry,
            out string reason)
        {
            var currentEntry = synchronizedDeliveries[index];
            if (currentEntry.State != NetworkDeliveryState.Pending)
            {
                entry = default;
                reason = $"delivery_not_pending:{currentEntry.State}";
                return false;
            }

            entry = currentEntry.WithState(
                NetworkDeliveryState.Claimed,
                boxId,
                IncrementNonZero(Snapshot.Revision));
            CommitDeliveryState(index, entry, "claimed");
            reason = null;
            return true;
        }

        private void CommitDeliveryState(
            int index,
            NetworkDeliveryEntry nextEntry,
            string operation)
        {
            synchronizedDeliveries[index] = nextEntry;
            CountDeliveryStates(
                out var pendingCount,
                out var claimedCount,
                out var deliveredCount);

            var current = Snapshot;
            synchronizedSnapshot.Value = new NetworkRunEconomySnapshot(
                current.Credits,
                nextEntry.StateRevision,
                current.WalletRevision,
                IncrementNonZero(current.DeliveryRevision),
                current.NextDeliveryEntryId,
                pendingCount,
                claimedCount,
                deliveredCount,
                current.LastTransactionId,
                current.LastTransactionKind,
                current.LastCreditDelta,
                current.LastActorClientId);
            Debug.Log(
                $"PHS_RUN_ECONOMY_DELIVERY_STATE operation={operation} entry={nextEntry.EntryId} box={nextEntry.DeliveryBoxId} state={nextEntry.State} revision={Revision}",
                this);
        }

        private void CountDeliveryStates(
            out int pendingCount,
            out int claimedCount,
            out int deliveredCount)
        {
            pendingCount = 0;
            claimedCount = 0;
            deliveredCount = 0;
            for (var index = 0; index < synchronizedDeliveries.Count; index++)
            {
                switch (synchronizedDeliveries[index].State)
                {
                    case NetworkDeliveryState.Pending:
                        pendingCount++;
                        break;
                    case NetworkDeliveryState.Claimed:
                        claimedCount++;
                        break;
                    case NetworkDeliveryState.Delivered:
                        deliveredCount++;
                        break;
                }
            }
        }

        private bool TryFindDeliveryIndex(uint entryId, out int index)
        {
            if (entryId != 0U)
            {
                for (var candidate = 0; candidate < synchronizedDeliveries.Count; candidate++)
                {
                    if (synchronizedDeliveries[candidate].EntryId == entryId)
                    {
                        index = candidate;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        private bool RequireServer(out string reason)
        {
            if (IsSpawned
                && IsServer
                && OwnerClientId == NetworkManager.ServerClientId)
            {
                reason = null;
                return true;
            }

            reason = "server_required";
            return false;
        }

        private static bool TryCreateFixedTransactionId(
            string value,
            out FixedString128Bytes fixedValue,
            out string normalizedValue,
            out string reason)
        {
            fixedValue = default;
            normalizedValue = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
            if (normalizedValue == null)
            {
                reason = "transaction_id_required";
                return false;
            }

            if (fixedValue.CopyFrom(normalizedValue) != CopyError.None)
            {
                fixedValue = default;
                reason = "transaction_id_too_long";
                return false;
            }

            normalizedValue = fixedValue.ToString();
            reason = null;
            return true;
        }

        private static bool TryCreateFixedItemId(
            string value,
            out FixedString64Bytes fixedValue,
            out string reason)
        {
            return TryCreateFixedString64(
                value,
                "item_id",
                out fixedValue,
                out reason);
        }

        private static bool TryCreateFixedBoxId(
            string value,
            out FixedString64Bytes fixedValue,
            out string reason)
        {
            return TryCreateFixedString64(
                value,
                "box_id",
                out fixedValue,
                out reason);
        }

        private static bool TryCreateFixedString64(
            string value,
            string fieldName,
            out FixedString64Bytes fixedValue,
            out string reason)
        {
            fixedValue = default;
            var normalized = string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
            if (normalized == null)
            {
                reason = $"{fieldName}_required";
                return false;
            }

            if (fixedValue.CopyFrom(normalized) != CopyError.None)
            {
                fixedValue = default;
                reason = $"{fieldName}_too_long";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsCreditKind(
            NetworkRunEconomyTransactionKind transactionKind)
        {
            return transactionKind == NetworkRunEconomyTransactionKind.SaleCredit
                || transactionKind == NetworkRunEconomyTransactionKind.RewardCredit
                || transactionKind == NetworkRunEconomyTransactionKind.RefundCredit;
        }

        private static bool IsDebitKind(
            NetworkRunEconomyTransactionKind transactionKind)
        {
            return transactionKind == NetworkRunEconomyTransactionKind.RepairDebit
                || transactionKind == NetworkRunEconomyTransactionKind.PenaltyDebit;
        }

        private static uint IncrementNonZero(uint value)
        {
            value++;
            return value == 0U ? 1U : value;
        }

        private void Subscribe()
        {
            if (isSubscribed)
            {
                return;
            }

            synchronizedSnapshot.OnValueChanged += HandleSnapshotChanged;
            synchronizedDeliveries.OnListChanged += HandleDeliveryChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed)
            {
                return;
            }

            synchronizedSnapshot.OnValueChanged -= HandleSnapshotChanged;
            synchronizedDeliveries.OnListChanged -= HandleDeliveryChanged;
            isSubscribed = false;
        }

        private void HandleSnapshotChanged(
            NetworkRunEconomySnapshot previousValue,
            NetworkRunEconomySnapshot currentValue)
        {
            InvokeSnapshotChanged(previousValue, currentValue);
            FlushDeliveryEvents(currentValue.Revision);
        }

        private void HandleDeliveryChanged(
            NetworkListEvent<NetworkDeliveryEntry> changeEvent)
        {
            var requiredRevision = changeEvent.Value.StateRevision;
            if (requiredRevision != 0U && requiredRevision > Snapshot.Revision)
            {
                pendingDeliveryEvents.Enqueue(changeEvent);
                return;
            }

            InvokeDeliveryChanged(changeEvent);
        }

        private void FlushDeliveryEvents(uint snapshotRevision)
        {
            while (pendingDeliveryEvents.Count > 0)
            {
                var changeEvent = pendingDeliveryEvents.Peek();
                var requiredRevision = changeEvent.Value.StateRevision;
                if (requiredRevision != 0U && requiredRevision > snapshotRevision)
                {
                    return;
                }

                pendingDeliveryEvents.Dequeue();
                InvokeDeliveryChanged(changeEvent);
            }
        }

        private void InvokeSnapshotChanged(
            NetworkRunEconomySnapshot previousValue,
            NetworkRunEconomySnapshot currentValue)
        {
            var handlers = SnapshotChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<
                         NetworkRunEconomySnapshot,
                         NetworkRunEconomySnapshot> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(previousValue, currentValue);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_RUN_ECONOMY_OBSERVER_FAILED event=snapshot observer={handler.Method.Name} exception={exception.GetType().Name}",
                        this);
                }
            }
        }

        private void InvokeDeliveryChanged(
            NetworkListEvent<NetworkDeliveryEntry> changeEvent)
        {
            var handlers = DeliveryChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<NetworkListEvent<NetworkDeliveryEntry>> handler
                     in handlers.GetInvocationList())
            {
                try
                {
                    handler(changeEvent);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_RUN_ECONOMY_OBSERVER_FAILED event=delivery observer={handler.Method.Name} exception={exception.GetType().Name}",
                        this);
                }
            }
        }
    }
}
