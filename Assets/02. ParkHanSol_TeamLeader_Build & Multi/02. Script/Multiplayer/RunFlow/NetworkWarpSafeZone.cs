using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NetworkWarpSafeZone : MonoBehaviour
    {
        [SerializeField] private BoxCollider safeTrigger;
        private bool setupErrorLogged;

        private void Awake()
        {
            ValidateSetup();
        }

        private void OnTriggerEnter(Collider other)
        {
            SetPlayerSafeState(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            SetPlayerSafeState(other, false);
        }

        private void SetPlayerSafeState(Collider other, bool isInside)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }

            if (!ValidateSetup())
            {
                return;
            }

            if (other.GetComponent<CharacterController>() == null)
            {
                return;
            }

            var player = other.GetComponent<NetworkPlayerController>();
            if (player == null || !player.IsSpawned)
            {
                return;
            }

            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError($"PHS_WARP_SAFE_ZONE_FAILED reason=coordinator_missing zone={name}", this);
                return;
            }

            coordinator.SetPlayerInsideSafeZone(player.OwnerClientId, isInside);
            Debug.Log($"PHS_WARP_SAFE_ZONE_PLAYER zone={name} clientId={player.OwnerClientId} inside={isInside}");
        }

        private bool ValidateSetup()
        {
            if (safeTrigger == null)
            {
                safeTrigger = GetComponent<BoxCollider>();
            }

            if (safeTrigger != null && safeTrigger.isTrigger)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_WARP_SAFE_ZONE_SETUP_FAILED reason=trigger_invalid zone={name}", this);
            }

            return false;
        }
    }
}
