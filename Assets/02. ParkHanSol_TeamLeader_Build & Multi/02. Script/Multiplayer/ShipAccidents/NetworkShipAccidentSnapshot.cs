using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    public struct NetworkShipAccidentSnapshot :
        INetworkSerializable,
        IEquatable<NetworkShipAccidentSnapshot>
    {
        public uint InstanceId;
        public PHSShipAccidentId AccidentId;
        public FixedString64Bytes AnchorId;
        public int RepairProgress;
        public int RequiredRepairProgress;
        public uint Revision;

        public NetworkShipAccidentSnapshot(
            uint instanceId,
            PHSShipAccidentId accidentId,
            FixedString64Bytes anchorId,
            int repairProgress,
            int requiredRepairProgress,
            uint revision)
        {
            InstanceId = instanceId;
            AccidentId = accidentId;
            AnchorId = anchorId;
            RepairProgress = repairProgress;
            RequiredRepairProgress = requiredRepairProgress;
            Revision = revision;
        }

        public bool IsRepairComplete => RepairProgress >= RequiredRepairProgress;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref AccidentId);
            serializer.SerializeValue(ref AnchorId);
            serializer.SerializeValue(ref RepairProgress);
            serializer.SerializeValue(ref RequiredRepairProgress);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkShipAccidentSnapshot other)
        {
            return InstanceId == other.InstanceId
                && AccidentId == other.AccidentId
                && AnchorId.Equals(other.AnchorId)
                && RepairProgress == other.RepairProgress
                && RequiredRepairProgress == other.RequiredRepairProgress
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkShipAccidentSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                InstanceId,
                (ushort)AccidentId,
                AnchorId,
                RepairProgress,
                RequiredRepairProgress,
                Revision);
        }
    }
}
