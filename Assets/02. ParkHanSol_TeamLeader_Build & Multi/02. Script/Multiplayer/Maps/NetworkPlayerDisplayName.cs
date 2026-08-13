using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerDisplayName : NetworkBehaviour
    {
        private static readonly string[] TemporaryNames =
        {
            "Astra", "Bravo", "Comet", "Delta", "Echo", "Fable", "Gamma", "Helix"
        };

        private readonly NetworkVariable<FixedString32Bytes> synchronizedDisplayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string DisplayName => synchronizedDisplayName.Value.ToString();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                synchronizedDisplayName.Value = new FixedString32Bytes(
                    TemporaryNames[OwnerClientId % (ulong)TemporaryNames.Length]);
            }
        }

        public bool TryGetInitial(out string initial)
        {
            var displayName = DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                initial = null;
                return false;
            }

            initial = char.ToUpperInvariant(displayName[0]).ToString();
            return true;
        }
    }
}
