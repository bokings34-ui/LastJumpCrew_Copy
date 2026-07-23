using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IRunEconomyLedger
    {
        NetworkRunEconomySnapshot Snapshot { get; }
        int Credits { get; }
        uint Revision { get; }
        int DeliveryEntryCount { get; }

        event Action<
            NetworkRunEconomySnapshot,
            NetworkRunEconomySnapshot> SnapshotChanged;
        event Action<NetworkListEvent<NetworkDeliveryEntry>> DeliveryChanged;

        NetworkDeliveryEntry GetDeliveryEntryAt(int index);
        bool TryGetDeliveryEntry(uint entryId, out NetworkDeliveryEntry entry);
        bool TryGetNextPendingDelivery(out NetworkDeliveryEntry entry);

        bool TryAddCreditsServer(
            string transactionId,
            int amount,
            NetworkRunEconomyTransactionKind transactionKind,
            ulong actorClientId,
            out string reason);

        bool TrySpendCreditsServer(
            string transactionId,
            int amount,
            NetworkRunEconomyTransactionKind transactionKind,
            ulong actorClientId,
            out string reason);

        bool TryCommitPurchaseServer(
            string transactionId,
            IReadOnlyList<string> purchaseIds,
            IReadOnlyList<string> itemIds,
            int totalPrice,
            ulong purchaserClientId,
            out string reason);

        bool TryClaimNextDeliveryServer(
            string boxId,
            out NetworkDeliveryEntry entry,
            out string reason);

        bool TryClaimDeliveryServer(
            uint entryId,
            string boxId,
            out NetworkDeliveryEntry entry,
            out string reason);

        bool TryReleaseDeliveryClaimServer(
            uint entryId,
            string boxId,
            out string reason);

        bool TryCompleteDeliveryServer(
            uint entryId,
            string boxId,
            out string reason);
    }
}
