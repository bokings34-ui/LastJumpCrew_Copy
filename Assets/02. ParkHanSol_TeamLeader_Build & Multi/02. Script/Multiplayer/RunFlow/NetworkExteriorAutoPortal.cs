using LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NetworkExteriorAutoPortal : MonoBehaviour
    {
        [SerializeField] private ExteriorTestTeleportInteractable portal;
        [SerializeField] private NetworkDebrisCollectionZone debrisCollectionZone;

        private void Awake()
        {
            if (portal == null || debrisCollectionZone == null)
            {
                Debug.LogError($"PHS_EXTERIOR_AUTO_PORTAL_SETUP_FAILED portal={portal != null} debrisZone={debrisCollectionZone != null}", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }

            if (other.GetComponent<CharacterController>() == null
                || !other.TryGetComponent(out NetworkPlayerController player)
                || !player.IsSpawned)
            {
                return;
            }

            player.RequestLocalPortalTeleport(portal.name);
        }
    }
}
