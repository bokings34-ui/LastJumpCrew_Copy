using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkRunEconomyTransactionKind : byte
    {
        None = 0,
        SaleCredit = 1,
        RewardCredit = 2,
        RefundCredit = 3,
        RepairDebit = 10,
        PenaltyDebit = 11,
        PurchaseDebit = 20
    }

    public struct NetworkRunEconomySnapshot :
        INetworkSerializable,
        IEquatable<NetworkRunEconomySnapshot>
    {
        public int Credits;
        public uint Revision;
        public uint WalletRevision;
        public uint DeliveryRevision;
        public uint NextDeliveryEntryId;
        public int PendingDeliveryCount;
        public int ClaimedDeliveryCount;
        public int DeliveredCount;
        public FixedString128Bytes LastTransactionId;
        public NetworkRunEconomyTransactionKind LastTransactionKind;
        public int LastCreditDelta;
        public ulong LastActorClientId;

        public NetworkRunEconomySnapshot(
            int credits,
            uint revision,
            uint walletRevision,
            uint deliveryRevision,
            uint nextDeliveryEntryId,
            int pendingDeliveryCount,
            int claimedDeliveryCount,
            int deliveredCount,
            FixedString128Bytes lastTransactionId,
            NetworkRunEconomyTransactionKind lastTransactionKind,
            int lastCreditDelta,
            ulong lastActorClientId)
        {
            Credits = credits;
            Revision = revision;
            WalletRevision = walletRevision;
            DeliveryRevision = deliveryRevision;
            NextDeliveryEntryId = nextDeliveryEntryId;
            PendingDeliveryCount = pendingDeliveryCount;
            ClaimedDeliveryCount = claimedDeliveryCount;
            DeliveredCount = deliveredCount;
            LastTransactionId = lastTransactionId;
            LastTransactionKind = lastTransactionKind;
            LastCreditDelta = lastCreditDelta;
            LastActorClientId = lastActorClientId;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Credits);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref WalletRevision);
            serializer.SerializeValue(ref DeliveryRevision);
            serializer.SerializeValue(ref NextDeliveryEntryId);
            serializer.SerializeValue(ref PendingDeliveryCount);
            serializer.SerializeValue(ref ClaimedDeliveryCount);
            serializer.SerializeValue(ref DeliveredCount);
            serializer.SerializeValue(ref LastTransactionId);
            serializer.SerializeValue(ref LastTransactionKind);
            serializer.SerializeValue(ref LastCreditDelta);
            serializer.SerializeValue(ref LastActorClientId);
        }

        public bool Equals(NetworkRunEconomySnapshot other)
        {
            return Credits == other.Credits
                && Revision == other.Revision
                && WalletRevision == other.WalletRevision
                && DeliveryRevision == other.DeliveryRevision
                && NextDeliveryEntryId == other.NextDeliveryEntryId
                && PendingDeliveryCount == other.PendingDeliveryCount
                && ClaimedDeliveryCount == other.ClaimedDeliveryCount
                && DeliveredCount == other.DeliveredCount
                && LastTransactionId.Equals(other.LastTransactionId)
                && LastTransactionKind == other.LastTransactionKind
                && LastCreditDelta == other.LastCreditDelta
                && LastActorClientId == other.LastActorClientId;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunEconomySnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Credits;
                hash = (hash * 397) ^ (int)Revision;
                hash = (hash * 397) ^ (int)WalletRevision;
                hash = (hash * 397) ^ (int)DeliveryRevision;
                hash = (hash * 397) ^ (int)NextDeliveryEntryId;
                hash = (hash * 397) ^ PendingDeliveryCount;
                hash = (hash * 397) ^ ClaimedDeliveryCount;
                hash = (hash * 397) ^ DeliveredCount;
                hash = (hash * 397) ^ LastTransactionId.GetHashCode();
                hash = (hash * 397) ^ (byte)LastTransactionKind;
                hash = (hash * 397) ^ LastCreditDelta;
                hash = (hash * 397) ^ LastActorClientId.GetHashCode();
                return hash;
            }
        }
    }
}
