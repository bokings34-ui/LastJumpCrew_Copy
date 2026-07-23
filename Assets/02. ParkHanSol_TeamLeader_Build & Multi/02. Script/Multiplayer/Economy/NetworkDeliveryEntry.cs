using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkDeliveryState : byte
    {
        Pending = 0,
        Claimed = 1,
        Delivered = 2
    }

    public struct NetworkDeliveryEntry :
        INetworkSerializable,
        IEquatable<NetworkDeliveryEntry>
    {
        public uint EntryId;
        public FixedString128Bytes PurchaseTransactionId;
        public FixedString128Bytes PurchaseId;
        public FixedString64Bytes ItemId;
        public ulong PurchaserClientId;
        public NetworkDeliveryState State;
        public FixedString64Bytes DeliveryBoxId;
        public uint CreatedRevision;
        public uint StateRevision;

        public NetworkDeliveryEntry(
            uint entryId,
            FixedString128Bytes purchaseTransactionId,
            FixedString128Bytes purchaseId,
            FixedString64Bytes itemId,
            ulong purchaserClientId,
            NetworkDeliveryState state,
            FixedString64Bytes deliveryBoxId,
            uint createdRevision,
            uint stateRevision)
        {
            EntryId = entryId;
            PurchaseTransactionId = purchaseTransactionId;
            PurchaseId = purchaseId;
            ItemId = itemId;
            PurchaserClientId = purchaserClientId;
            State = state;
            DeliveryBoxId = deliveryBoxId;
            CreatedRevision = createdRevision;
            StateRevision = stateRevision;
        }

        public NetworkDeliveryEntry WithState(
            NetworkDeliveryState state,
            FixedString64Bytes deliveryBoxId,
            uint stateRevision)
        {
            return new NetworkDeliveryEntry(
                EntryId,
                PurchaseTransactionId,
                PurchaseId,
                ItemId,
                PurchaserClientId,
                state,
                deliveryBoxId,
                CreatedRevision,
                stateRevision);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref EntryId);
            serializer.SerializeValue(ref PurchaseTransactionId);
            serializer.SerializeValue(ref PurchaseId);
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref PurchaserClientId);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref DeliveryBoxId);
            serializer.SerializeValue(ref CreatedRevision);
            serializer.SerializeValue(ref StateRevision);
        }

        public bool Equals(NetworkDeliveryEntry other)
        {
            return EntryId == other.EntryId
                && PurchaseTransactionId.Equals(other.PurchaseTransactionId)
                && PurchaseId.Equals(other.PurchaseId)
                && ItemId.Equals(other.ItemId)
                && PurchaserClientId == other.PurchaserClientId
                && State == other.State
                && DeliveryBoxId.Equals(other.DeliveryBoxId)
                && CreatedRevision == other.CreatedRevision
                && StateRevision == other.StateRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkDeliveryEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)EntryId;
                hash = (hash * 397) ^ PurchaseTransactionId.GetHashCode();
                hash = (hash * 397) ^ PurchaseId.GetHashCode();
                hash = (hash * 397) ^ ItemId.GetHashCode();
                hash = (hash * 397) ^ PurchaserClientId.GetHashCode();
                hash = (hash * 397) ^ (byte)State;
                hash = (hash * 397) ^ DeliveryBoxId.GetHashCode();
                hash = (hash * 397) ^ (int)CreatedRevision;
                hash = (hash * 397) ^ (int)StateRevision;
                return hash;
            }
        }
    }
}
