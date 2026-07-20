using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Fixed integer deterministic generator for one semantic run scope.
    /// Instances are derived only from run seed, algorithm version, stream, and scope key.
    /// </summary>
    public sealed class PHSDeterministicRandom
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong ScopeDomainSalt = 0xD1B54A32D192ED03UL;
        private const ulong ScopeKeySalt = 0x94D049BB133111EBUL;
        private const ulong NonZeroScopeSeed = 0xA0761D6478BD642FUL;

        private ulong state;

        internal PHSDeterministicRandom(
            ulong runSeed,
            uint algorithmVersion,
            NetworkRunRandomStream stream,
            ulong scopeKey)
        {
            if (runSeed == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runSeed),
                    "Run seed must be nonzero.");
            }

            if (algorithmVersion == 0U)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(algorithmVersion),
                    "Algorithm version must be nonzero.");
            }

            RunSeed = runSeed;
            AlgorithmVersion = algorithmVersion;
            Stream = stream;
            ScopeKey = scopeKey;
            ScopeSeed = DeriveScopeSeed(
                runSeed,
                algorithmVersion,
                stream,
                scopeKey);
            state = ScopeSeed;
        }

        public ulong RunSeed { get; }
        public uint AlgorithmVersion { get; }
        public NetworkRunRandomStream Stream { get; }
        public ulong ScopeKey { get; }
        public ulong ScopeSeed { get; }
        public ulong DrawCount { get; private set; }

        public ulong NextUInt64()
        {
            unchecked
            {
                state += GoldenGamma;
                DrawCount++;
                return Mix64(state);
            }
        }

        public uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        public int NextInt(int maxExclusive)
        {
            return NextInt(0, maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    $"Range must satisfy min < max. min={minInclusive} max={maxExclusive}");
            }

            var range = (ulong)((long)maxExclusive - minInclusive);
            var rejectionThreshold = unchecked(0UL - range) % range;
            ulong sample;
            do
            {
                sample = NextUInt64();
            }
            while (sample < rejectionThreshold);

            return (int)((long)minInclusive + (long)(sample % range));
        }

        private static ulong DeriveScopeSeed(
            ulong runSeed,
            uint algorithmVersion,
            NetworkRunRandomStream stream,
            ulong scopeKey)
        {
            unchecked
            {
                var streamAndVersion =
                    ((ulong)(uint)stream << 32)
                    | algorithmVersion;
                var derived = Mix64(runSeed ^ ScopeDomainSalt);
                derived = Mix64(derived ^ streamAndVersion);
                derived = Mix64(derived ^ scopeKey ^ ScopeKeySalt);
                return derived == 0UL ? NonZeroScopeSeed : derived;
            }
        }

        private static ulong Mix64(ulong value)
        {
            unchecked
            {
                value ^= value >> 30;
                value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27;
                value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return value;
            }
        }
    }
}
