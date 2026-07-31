using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkPlayerSector
    {
        Interior = 0,
        Transition = 1,
        AuthorizedExterior = 2
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerSectorState : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkPlayerSector> synchronizedSector = new(
            NetworkPlayerSector.Interior,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkPlayerSector sectorBeforeTransition = NetworkPlayerSector.Interior;
        private NetworkPlayerSector pendingDestination = NetworkPlayerSector.Interior;
        private bool transitionPending;

        public NetworkPlayerSector CurrentSector => synchronizedSector.Value;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ResetToInteriorServer("network_spawn");
            }
        }

        public override void OnNetworkDespawn()
        {
            transitionPending = false;
            sectorBeforeTransition = NetworkPlayerSector.Interior;
            pendingDestination = NetworkPlayerSector.Interior;
            base.OnNetworkDespawn();
        }

        public bool TryBeginTransitionServer(NetworkPlayerSector destination, out string reason)
        {
            if (!RequireServer(nameof(TryBeginTransitionServer), out reason))
            {
                return false;
            }

            if (destination == NetworkPlayerSector.Transition)
            {
                reason = "destination_transition_invalid";
                return false;
            }

            if (transitionPending || synchronizedSector.Value == NetworkPlayerSector.Transition)
            {
                reason = "transition_already_pending";
                return false;
            }

            sectorBeforeTransition = synchronizedSector.Value;
            pendingDestination = destination;
            transitionPending = true;
            synchronizedSector.Value = NetworkPlayerSector.Transition;
            reason = null;
            Debug.Log(
                $"PHS_PLAYER_SECTOR_TRANSITION_BEGIN player={name} clientId={OwnerClientId} " +
                $"from={sectorBeforeTransition} destination={pendingDestination}",
                this);
            return true;
        }

        public bool TryCompleteTransitionServer(out string reason)
        {
            if (!RequireServer(nameof(TryCompleteTransitionServer), out reason))
            {
                return false;
            }

            if (!transitionPending || synchronizedSector.Value != NetworkPlayerSector.Transition)
            {
                reason = "transition_not_pending";
                return false;
            }

            var completedDestination = pendingDestination;
            transitionPending = false;
            sectorBeforeTransition = completedDestination;
            pendingDestination = completedDestination;
            synchronizedSector.Value = completedDestination;
            reason = null;
            Debug.Log(
                $"PHS_PLAYER_SECTOR_TRANSITION_COMPLETE player={name} clientId={OwnerClientId} " +
                $"sector={completedDestination}",
                this);
            return true;
        }

        public void CancelTransitionServer(string reason)
        {
            if (!RequireServer(nameof(CancelTransitionServer), out _))
            {
                return;
            }

            if (!transitionPending && synchronizedSector.Value != NetworkPlayerSector.Transition)
            {
                return;
            }

            var restoredSector = sectorBeforeTransition;
            transitionPending = false;
            pendingDestination = restoredSector;
            synchronizedSector.Value = restoredSector;
            Debug.LogWarning(
                $"PHS_PLAYER_SECTOR_TRANSITION_CANCELLED player={name} clientId={OwnerClientId} " +
                $"sector={restoredSector} reason={reason}",
                this);
        }

        public void ResetToInteriorServer(string reason)
        {
            if (!RequireServer(nameof(ResetToInteriorServer), out _))
            {
                return;
            }

            transitionPending = false;
            sectorBeforeTransition = NetworkPlayerSector.Interior;
            pendingDestination = NetworkPlayerSector.Interior;
            synchronizedSector.Value = NetworkPlayerSector.Interior;
            Debug.Log(
                $"PHS_PLAYER_SECTOR_RESET player={name} clientId={OwnerClientId} sector=Interior reason={reason}",
                this);
        }

        private bool RequireServer(string operation, out string reason)
        {
            if (IsSpawned && IsServer)
            {
                reason = null;
                return true;
            }

            reason = "server_required";
            Debug.LogError(
                $"PHS_PLAYER_SECTOR_FAILED reason={reason} operation={operation} player={name}",
                this);
            return false;
        }
    }
}
