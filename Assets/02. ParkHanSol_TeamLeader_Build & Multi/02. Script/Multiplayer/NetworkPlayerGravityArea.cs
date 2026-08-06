using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkPlayerGravityArea : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerGravityMode gravityMode = NetworkPlayerGravityMode.ShipGravity;
        [SerializeField] private bool canToggleShipGravity;
        [SerializeField] private bool shipGravityEnabled = true;
        [SerializeField] private int priority;

        public int Priority => priority;

        public bool ContainsPosition(Vector3 position)
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null || !triggerCollider.enabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            return (triggerCollider.ClosestPoint(position) - position).sqrMagnitude <= 0.0001f;
        }

        public NetworkPlayerGravityMode EffectiveGravityMode
        {
            get
            {
                if (!canToggleShipGravity)
                {
                    return gravityMode;
                }

                return shipGravityEnabled
                    ? NetworkPlayerGravityMode.ShipGravity
                    : NetworkPlayerGravityMode.ShipZeroGravity;
            }
        }

        public void SetShipGravityEnabled(bool isEnabled)
        {
            if (!canToggleShipGravity)
            {
                Debug.LogError($"PHS_GRAVITY_AREA_TOGGLE_FAILED reason=not_toggleable area={name}");
                return;
            }

            shipGravityEnabled = isEnabled;
            ApplyToPlayersInside();
            Debug.Log($"PHS_GRAVITY_AREA_STATE area={name} gravity={shipGravityEnabled}");
        }

        private void Reset()
        {
            var triggerCollider = GetComponent<Collider>();
            triggerCollider.isTrigger = true;
        }

        private void OnValidate()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider == null)
            {
                return;
            }

            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayer(other, out var player))
            {
                return;
            }

            player.EnterGravityArea(this);
        }

        private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayer(other, out var player))
            {
                return;
            }

            player.ExitGravityArea(this);
        }

        private void ApplyToPlayersInside()
        {
            foreach (var player in FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None))
            {
                player.RefreshGravityArea(this);
            }
        }

        private static bool TryGetPlayer(Collider other, out NetworkPlayerController player)
        {
            player = null;
            if (other.GetComponent<CharacterController>() == null)
            {
                return false;
            }

            player = other.GetComponent<NetworkPlayerController>();
            return player != null;
        }
    }
}
