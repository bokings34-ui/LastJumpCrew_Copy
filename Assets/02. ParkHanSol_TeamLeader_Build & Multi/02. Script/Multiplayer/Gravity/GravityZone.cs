using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Collider))]
    public sealed class GravityZone : MonoBehaviour, IGravitySource
    {
        [SerializeField] private GravityMode gravityMode = GravityMode.ShipGravity;
        [SerializeField] private bool canToggleShipGravity;
        [SerializeField] private bool shipGravityEnabled = true;
        [SerializeField] private int priority;
        [SerializeField] private Vector3 gravityDirection = Vector3.down;
        [SerializeField, Min(0f)] private float gravityStrength = 18f;

        public GravityState CurrentGravityState => new(
            GetEffectiveMode(),
            priority,
            gravityDirection,
            gravityStrength);

        public void SetShipGravityEnabled(bool isEnabled)
        {
            if (!canToggleShipGravity)
            {
                Debug.LogError($"PHS_GRAVITY_ZONE_TOGGLE_FAILED reason=not_toggleable zone={name}");
                return;
            }

            shipGravityEnabled = isEnabled;
            RefreshAffectablesInside();
            Debug.Log($"PHS_GRAVITY_ZONE_STATE zone={name} gravity={shipGravityEnabled}");
        }

        private GravityMode GetEffectiveMode()
        {
            if (!canToggleShipGravity)
            {
                return gravityMode;
            }

            return shipGravityEnabled ? GravityMode.ShipGravity : GravityMode.ShipZeroGravity;
        }

        private void Reset()
        {
            EnsureTriggerCollider();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
            if (gravityDirection.sqrMagnitude <= 0.0001f)
            {
                gravityDirection = Vector3.down;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            EnterAffectables(other);
        }

        private void OnTriggerStay(Collider other)
        {
            EnterAffectables(other);
        }

        private void OnTriggerExit(Collider other)
        {
            foreach (var affectable in FindAffectables(other))
            {
                affectable.ExitGravitySource(this);
            }
        }

        private void RefreshAffectablesInside()
        {
            foreach (var affectable in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (affectable is IGravityAffectable gravityAffectable)
                {
                    gravityAffectable.RefreshGravitySource(this);
                }
            }
        }

        private void EnsureTriggerCollider()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void EnterAffectables(Collider other)
        {
            foreach (var affectable in FindAffectables(other))
            {
                affectable.EnterGravitySource(this);
            }
        }

        private static IGravityAffectable[] FindAffectables(Collider other)
        {
            var affectables = other.GetComponentsInParent<IGravityAffectable>();
            if (affectables.Length > 0)
            {
                return affectables;
            }

            return other.GetComponentsInChildren<IGravityAffectable>();
        }
    }
}
