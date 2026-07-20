using System;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public struct NetworkRunIncidentSnapshot :
        INetworkSerializable,
        IEquatable<NetworkRunIncidentSnapshot>
    {
        public int MapId;
        public uint StageSequence;
        public NetworkRunIncidentStageState State;
        public ushort PressureCapacity;
        public ushort ReservedPressure;
        public ushort ActivePressure;
        public byte ActiveExternalCount;
        public byte ActiveInternalCount;
        public uint StageIssuedCount;
        public uint StageResolvedCount;
        public ulong NextCommandId;
        public float ActiveWarpChargeMultiplier;
        public uint Revision;

        public NetworkRunIncidentSnapshot(
            int mapId,
            uint stageSequence,
            NetworkRunIncidentStageState state,
            ushort pressureCapacity,
            ushort reservedPressure,
            ushort activePressure,
            byte activeExternalCount,
            byte activeInternalCount,
            uint stageIssuedCount,
            uint stageResolvedCount,
            ulong nextCommandId,
            float activeWarpChargeMultiplier,
            uint revision)
        {
            MapId = mapId;
            StageSequence = stageSequence;
            State = state;
            PressureCapacity = pressureCapacity;
            ReservedPressure = reservedPressure;
            ActivePressure = activePressure;
            ActiveExternalCount = activeExternalCount;
            ActiveInternalCount = activeInternalCount;
            StageIssuedCount = stageIssuedCount;
            StageResolvedCount = stageResolvedCount;
            NextCommandId = nextCommandId;
            ActiveWarpChargeMultiplier = activeWarpChargeMultiplier;
            Revision = revision;
        }

        public ushort UsedPressure
        {
            get
            {
                var total = (uint)ReservedPressure + ActivePressure;
                return total > ushort.MaxValue
                    ? ushort.MaxValue
                    : (ushort)total;
            }
        }

        public ushort AvailablePressure =>
            UsedPressure >= PressureCapacity
                ? (ushort)0
                : (ushort)(PressureCapacity - UsedPressure);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref MapId);
            serializer.SerializeValue(ref StageSequence);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref PressureCapacity);
            serializer.SerializeValue(ref ReservedPressure);
            serializer.SerializeValue(ref ActivePressure);
            serializer.SerializeValue(ref ActiveExternalCount);
            serializer.SerializeValue(ref ActiveInternalCount);
            serializer.SerializeValue(ref StageIssuedCount);
            serializer.SerializeValue(ref StageResolvedCount);
            serializer.SerializeValue(ref NextCommandId);
            serializer.SerializeValue(ref ActiveWarpChargeMultiplier);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkRunIncidentSnapshot other)
        {
            return MapId == other.MapId
                && StageSequence == other.StageSequence
                && State == other.State
                && PressureCapacity == other.PressureCapacity
                && ReservedPressure == other.ReservedPressure
                && ActivePressure == other.ActivePressure
                && ActiveExternalCount == other.ActiveExternalCount
                && ActiveInternalCount == other.ActiveInternalCount
                && StageIssuedCount == other.StageIssuedCount
                && StageResolvedCount == other.StageResolvedCount
                && NextCommandId == other.NextCommandId
                && ActiveWarpChargeMultiplier.Equals(
                    other.ActiveWarpChargeMultiplier)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunIncidentSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MapId;
                hash = (hash * 397) ^ (int)StageSequence;
                hash = (hash * 397) ^ (byte)State;
                hash = (hash * 397) ^ PressureCapacity;
                hash = (hash * 397) ^ ReservedPressure;
                hash = (hash * 397) ^ ActivePressure;
                hash = (hash * 397) ^ ActiveExternalCount;
                hash = (hash * 397) ^ ActiveInternalCount;
                hash = (hash * 397) ^ (int)StageIssuedCount;
                hash = (hash * 397) ^ (int)StageResolvedCount;
                hash = (hash * 397) ^ NextCommandId.GetHashCode();
                hash = (hash * 397) ^ ActiveWarpChargeMultiplier.GetHashCode();
                hash = (hash * 397) ^ (int)Revision;
                return hash;
            }
        }
    }
}
