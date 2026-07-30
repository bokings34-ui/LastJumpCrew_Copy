using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NetworkDebrisCollectionZone : MonoBehaviour
    {
        [SerializeField] private SphereCollider collectionTrigger;
        [SerializeField, Min(0.1f)] private float deadZoneWarningSeconds = 5f;

        private readonly HashSet<ulong> playersInside = new();
        private readonly HashSet<ulong> playersInsideSafeVolume = new();

        public bool IsPlayerInside(ulong clientId) => playersInside.Contains(clientId);

        private void Awake()
        {
            if (collectionTrigger == null)
            {
                collectionTrigger = GetComponent<SphereCollider>();
            }

            if (collectionTrigger == null || !collectionTrigger.isTrigger)
            {
                Debug.LogError($"PHS_DEBRIS_ZONE_SETUP_FAILED reason=outer_trigger_invalid zone={name}", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetServerPlayer(other, out var player, out var lifeState))
            {
                return;
            }

            playersInside.Add(player.OwnerClientId);
            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError($"PHS_DEBRIS_ZONE_FAILED reason=coordinator_missing zone={name}", this);
                return;
            }

            coordinator.SetPlayerInsideDebrisZone(player.OwnerClientId, true);
            if (playersInsideSafeVolume.Contains(player.OwnerClientId))
            {
                lifeState.CancelDeadZoneWarning();
            }
            else
            {
                lifeState.BeginDeadZoneWarning(deadZoneWarningSeconds);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetServerPlayer(other, out var player, out var lifeState))
            {
                return;
            }

            playersInside.Remove(player.OwnerClientId);
            playersInsideSafeVolume.Remove(player.OwnerClientId);
            lifeState.BeginDeadZoneWarning(deadZoneWarningSeconds);
            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError($"PHS_DEBRIS_ZONE_FAILED reason=coordinator_missing zone={name}", this);
                return;
            }

            coordinator.SetPlayerInsideDebrisZone(player.OwnerClientId, false);
        }

        public void SetPlayerInInnerSafeVolume(NetworkPlayerController player, bool isInside)
        {
            if (player == null)
            {
                return;
            }

            if (isInside)
            {
                playersInsideSafeVolume.Add(player.OwnerClientId);
            }
            else
            {
                playersInsideSafeVolume.Remove(player.OwnerClientId);
            }

            if (!playersInside.Contains(player.OwnerClientId))
            {
                return;
            }

            var lifeState = player.GetComponent<NetworkPlayerLifeState>();
            if (lifeState == null)
            {
                Debug.LogError($"PHS_DEBRIS_ZONE_FAILED reason=life_state_missing player={player.name}", player);
                return;
            }

            if (isInside)
            {
                lifeState.CancelDeadZoneWarning();
            }
            else
            {
                lifeState.BeginDeadZoneWarning(deadZoneWarningSeconds);
            }
        }

        private static bool TryGetServerPlayer(
            Collider other,
            out NetworkPlayerController player,
            out NetworkPlayerLifeState lifeState)
        {
            player = null;
            lifeState = null;
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return false;
            }

            if (other.GetComponent<CharacterController>() == null)
            {
                return false;
            }

            player = other.GetComponent<NetworkPlayerController>();
            if (player == null || !player.IsSpawned)
            {
                return false;
            }

            lifeState = player.GetComponent<NetworkPlayerLifeState>();
            if (lifeState != null)
            {
                return true;
            }

            Debug.LogError($"PHS_DEBRIS_ZONE_FAILED reason=life_state_missing player={player.name}", player);
            return false;
        }
    }
}
