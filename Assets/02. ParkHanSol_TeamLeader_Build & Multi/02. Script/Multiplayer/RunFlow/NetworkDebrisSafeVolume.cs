using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(SphereCollider))]
    public sealed class NetworkDebrisSafeVolume : MonoBehaviour
    {
        [SerializeField] private NetworkDebrisCollectionZone collectionZone;
        [SerializeField] private SphereCollider safeTrigger;

        private void Awake()
        {
            if (safeTrigger == null)
            {
                safeTrigger = GetComponent<SphereCollider>();
            }

            if (collectionZone == null || safeTrigger == null || !safeTrigger.isTrigger)
            {
                Debug.LogError($"PHS_DEBRIS_SAFE_VOLUME_SETUP_FAILED volume={name}", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            SetSafeState(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            SetSafeState(other, false);
        }

        private void SetSafeState(Collider other, bool isInside)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }

            if (collectionZone == null)
            {
                Debug.LogError($"PHS_DEBRIS_SAFE_VOLUME_FAILED reason=collection_zone_missing volume={name}", this);
                return;
            }

            if (other.GetComponent<CharacterController>() == null)
            {
                return;
            }

            var player = other.GetComponent<NetworkPlayerController>();
            if (player != null && player.IsSpawned)
            {
                collectionZone.SetPlayerInInnerSafeVolume(player, isInside);
            }
        }
    }
}
