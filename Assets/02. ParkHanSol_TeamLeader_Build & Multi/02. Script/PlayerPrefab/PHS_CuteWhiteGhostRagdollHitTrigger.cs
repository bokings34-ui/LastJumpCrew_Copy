using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Collider))]
    public sealed class PHS_CuteWhiteGhostRagdollHitTrigger : MonoBehaviour
    {
        [SerializeField] private PHS_CuteWhiteGhostRagdollStateController target;
        [SerializeField] private string requiredPaddleName = "PHS_Lineup_HitPaddle";
        [SerializeField] private Vector3 launchVelocity = new(3.8f, 5.4f, 0f);
        [SerializeField] private Vector3 launchImpulse = new(0.8f, 0.9f, 0f);
        [SerializeField] private Vector3 launchTorque = new(0f, 0f, -3.2f);

        private bool setupErrorReported;

        private void Awake()
        {
            var hitCollider = GetComponent<Collider>();
            if (hitCollider != null)
            {
                hitCollider.isTrigger = true;
            }

            ValidateSetup();
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (!ValidateSetup())
            {
                return;
            }

            if (!other.name.Contains(requiredPaddleName))
            {
                return;
            }

            Debug.Log($"PHS_RAGDOLL_TRIGGER_HIT target={target.name} paddle={other.name}");
            target.EnterDown(launchVelocity, launchImpulse, launchTorque);
        }

        private bool ValidateSetup()
        {
            if (target == null)
            {
                LogSetupError("target_missing");
                return false;
            }

            return true;
        }

        private void LogSetupError(string reason)
        {
            if (setupErrorReported)
            {
                return;
            }

            setupErrorReported = true;
            Debug.LogError($"PHS_RAGDOLL_HIT_TRIGGER_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
