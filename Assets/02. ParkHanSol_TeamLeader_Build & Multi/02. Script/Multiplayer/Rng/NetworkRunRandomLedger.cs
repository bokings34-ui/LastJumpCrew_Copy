using System;
using System.Security.Cryptography;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Persistent server-owned run seed and deterministic semantic-scope factory.
    /// Creating a scope is stateless and never mutates the synchronized snapshot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRunRandomLedger :
        NetworkBehaviour,
        IRunRandomLedger
    {
        public const uint CurrentAlgorithmVersion = 1U;
        private const ulong GoldenRunSeed = 0x0123456789ABCDEFUL;
        private const ulong GoldenScopeKey = 42UL;
        private const ulong GoldenScopeSeed = 0xD3F231980735186FUL;
        private const ulong GoldenFirstValue = 0x6EB4818D0A3987DFUL;
        private const ulong GoldenSecondValue = 0x1255028B52389303UL;

        private readonly NetworkVariable<NetworkRunRandomSnapshot> synchronizedSnapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkRunRandomSnapshot Snapshot => synchronizedSnapshot.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!TryValidateAlgorithmContract(out var algorithmReason))
            {
                Debug.LogError(
                    $"PHS_RUN_RANDOM_SETUP_FAILED reason={algorithmReason}",
                    this);
                enabled = false;
                return;
            }

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_RUN_RANDOM_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            if (IsServer && !TryInitializeServerSnapshot(out var reason))
            {
                Debug.LogError(
                    $"PHS_RUN_RANDOM_SETUP_FAILED reason={reason}",
                    this);
                enabled = false;
                return;
            }

            Debug.Log(
                $"PHS_RUN_RANDOM_READY server={IsServer} seed={Snapshot.RunSeed} " +
                $"algorithm={Snapshot.AlgorithmVersion} revision={Snapshot.Revision}",
                this);
        }

        public static bool TryValidateAlgorithmContract(out string reason)
        {
            var random = new PHSDeterministicRandom(
                GoldenRunSeed,
                CurrentAlgorithmVersion,
                NetworkRunRandomStream.MapChoice,
                GoldenScopeKey);
            if (random.ScopeSeed != GoldenScopeSeed)
            {
                reason =
                    $"random_golden_scope_seed_mismatch:" +
                    $"{random.ScopeSeed:X16}!={GoldenScopeSeed:X16}";
                return false;
            }

            var firstValue = random.NextUInt64();
            var secondValue = random.NextUInt64();
            if (firstValue != GoldenFirstValue || secondValue != GoldenSecondValue)
            {
                reason =
                    $"random_golden_draw_mismatch:" +
                    $"{firstValue:X16},{secondValue:X16}!=" +
                    $"{GoldenFirstValue:X16},{GoldenSecondValue:X16}";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryCreateServerScope(
            NetworkRunRandomStream stream,
            ulong scopeKey,
            out PHSDeterministicRandom random,
            out string reason)
        {
            random = null;
            if (!IsSpawned || !IsServer)
            {
                reason = "server_authority_required";
                return false;
            }

            if (!IsSupportedStream(stream))
            {
                reason = $"random_stream_invalid:{(uint)stream}";
                return false;
            }

            var snapshot = Snapshot;
            if (!snapshot.IsInitialized)
            {
                reason = "random_snapshot_uninitialized";
                return false;
            }

            if (snapshot.AlgorithmVersion != CurrentAlgorithmVersion)
            {
                reason =
                    $"random_algorithm_version_mismatch:" +
                    $"{snapshot.AlgorithmVersion}!={CurrentAlgorithmVersion}";
                return false;
            }

            random = new PHSDeterministicRandom(
                snapshot.RunSeed,
                snapshot.AlgorithmVersion,
                stream,
                scopeKey);
            reason = null;
            return true;
        }

        private bool TryInitializeServerSnapshot(out string reason)
        {
            var current = Snapshot;
            if (current.IsInitialized)
            {
                if (current.AlgorithmVersion != CurrentAlgorithmVersion)
                {
                    reason =
                        $"random_algorithm_version_mismatch:" +
                        $"{current.AlgorithmVersion}!={CurrentAlgorithmVersion}";
                    return false;
                }

                reason = null;
                return true;
            }

            if (current.RunSeed != 0UL
                || current.AlgorithmVersion != 0U
                || current.Revision != 0U)
            {
                reason = "random_snapshot_partially_initialized";
                return false;
            }

            ulong runSeed;
            try
            {
                runSeed = CreateNonZeroRunSeed();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                reason = "run_seed_generation_failed";
                return false;
            }

            synchronizedSnapshot.Value = new NetworkRunRandomSnapshot(
                runSeed,
                CurrentAlgorithmVersion,
                1U);
            reason = null;
            return true;
        }

        private static ulong CreateNonZeroRunSeed()
        {
            var bytes = new byte[sizeof(ulong)];
            using var generator = RandomNumberGenerator.Create();
            for (var attempt = 0; attempt < 16; attempt++)
            {
                generator.GetBytes(bytes);
                var seed =
                    (ulong)bytes[0]
                    | ((ulong)bytes[1] << 8)
                    | ((ulong)bytes[2] << 16)
                    | ((ulong)bytes[3] << 24)
                    | ((ulong)bytes[4] << 32)
                    | ((ulong)bytes[5] << 40)
                    | ((ulong)bytes[6] << 48)
                    | ((ulong)bytes[7] << 56);
                if (seed != 0UL)
                {
                    return seed;
                }
            }

            throw new CryptographicException(
                "Failed to generate a nonzero run seed.");
        }

        private static bool IsSupportedStream(NetworkRunRandomStream stream)
        {
            return stream == NetworkRunRandomStream.MapChoice
                || stream == NetworkRunRandomStream.ExternalThreat
                || stream == NetworkRunRandomStream.InternalAccident
                || stream == NetworkRunRandomStream.InternalAccidentAnchor
                || stream == NetworkRunRandomStream.IncidentConsequence
                || stream == NetworkRunRandomStream.DebrisLayout
                || stream == NetworkRunRandomStream.DebrisRecycle
                || stream == NetworkRunRandomStream.ShopStock
                || stream == NetworkRunRandomStream.IncidentSpread;
        }
    }
}
