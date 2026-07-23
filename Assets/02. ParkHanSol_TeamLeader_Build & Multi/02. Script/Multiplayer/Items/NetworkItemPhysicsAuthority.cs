using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetworkItemPhysicsAuthority : NetworkBehaviour
    {
        [SerializeField] private Rigidbody targetRigidbody;

        private bool initialIsKinematic;
        private bool initialStateCaptured;

        private void Awake()
        {
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody>();
            }

            if (targetRigidbody == null)
            {
                return;
            }

            initialIsKinematic = targetRigidbody.isKinematic;
            initialStateCaptured = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            ApplyAuthorityState();
        }

        public override void OnNetworkDespawn()
        {
            if (targetRigidbody != null && initialStateCaptured)
            {
                targetRigidbody.isKinematic = initialIsKinematic;
            }

            base.OnNetworkDespawn();
        }

        private void ApplyAuthorityState()
        {
            if (targetRigidbody == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_PHYSICS_FAILED reason=rigidbody_missing item={name}",
                    this);
                return;
            }

            // Server simulates shared item physics. Clients keep colliders enabled
            // for queries and presentation while NetworkTransform supplies movement.
            targetRigidbody.isKinematic = !IsServer;
        }
    }
}
