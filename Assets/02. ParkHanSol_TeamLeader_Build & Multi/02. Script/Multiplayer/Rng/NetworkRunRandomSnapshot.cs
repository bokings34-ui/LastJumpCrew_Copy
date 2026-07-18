using System;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public struct NetworkRunRandomSnapshot :
        INetworkSerializable,
        IEquatable<NetworkRunRandomSnapshot>
    {
        public ulong RunSeed;
        public uint AlgorithmVersion;
        public uint Revision;

        public NetworkRunRandomSnapshot(
            ulong runSeed,
            uint algorithmVersion,
            uint revision)
        {
            RunSeed = runSeed;
            AlgorithmVersion = algorithmVersion;
            Revision = revision;
        }

        public bool IsInitialized =>
            RunSeed != 0UL
            && AlgorithmVersion != 0U
            && Revision != 0U;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref RunSeed);
            serializer.SerializeValue(ref AlgorithmVersion);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkRunRandomSnapshot other)
        {
            return RunSeed == other.RunSeed
                && AlgorithmVersion == other.AlgorithmVersion
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRunRandomSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RunSeed, AlgorithmVersion, Revision);
        }
    }
}
