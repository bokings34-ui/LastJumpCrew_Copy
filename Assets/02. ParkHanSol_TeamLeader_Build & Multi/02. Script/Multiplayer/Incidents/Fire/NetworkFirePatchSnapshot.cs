using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    public struct NetworkFirePatchSnapshot :
        INetworkSerializable,
        IEquatable<NetworkFirePatchSnapshot>
    {
        public uint AccidentInstanceId;
        public FixedString64Bytes LocationId;
        public ushort PatchId;
        public PHSFireIntensity Intensity;
        public ushort Heat;
        public uint Revision;
        public double ChangedAtServerTime;

        public NetworkFirePatchSnapshot(
            uint accidentInstanceId,
            FixedString64Bytes locationId,
            ushort patchId,
            PHSFireIntensity intensity,
            ushort heat,
            uint revision,
            double changedAtServerTime)
        {
            AccidentInstanceId = accidentInstanceId;
            LocationId = locationId;
            PatchId = patchId;
            Intensity = intensity;
            Heat = heat;
            Revision = revision;
            ChangedAtServerTime = changedAtServerTime;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref AccidentInstanceId);
            serializer.SerializeValue(ref LocationId);
            serializer.SerializeValue(ref PatchId);
            serializer.SerializeValue(ref Intensity);
            serializer.SerializeValue(ref Heat);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref ChangedAtServerTime);
        }

        public bool Equals(NetworkFirePatchSnapshot other)
        {
            return AccidentInstanceId == other.AccidentInstanceId
                && LocationId.Equals(other.LocationId)
                && PatchId == other.PatchId
                && Intensity == other.Intensity
                && Heat == other.Heat
                && Revision == other.Revision
                && ChangedAtServerTime.Equals(other.ChangedAtServerTime);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkFirePatchSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                AccidentInstanceId,
                LocationId,
                PatchId,
                Intensity,
                Heat,
                Revision,
                ChangedAtServerTime);
        }
    }
}
