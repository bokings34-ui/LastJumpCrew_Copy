using LastJumpCrew.ParkHanSol.Experiments.MudPrototype;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    public sealed class PHS_CuteWhiteGhostRagdollStateController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private MonoBehaviour[] disableOnDown;
        [SerializeField] private Rigidbody impulseBody;
        [SerializeField] private Rigidbody[] ragdollBodies;
        [SerializeField] private Collider[] ragdollColliders;
        [SerializeField] private SquishyRagdollJellyBounceBody[] jellyBounceBodies;
        [SerializeField] private Transform[] poseBones;
        [SerializeField] private Vector3 testLaunchVelocity = new(3.8f, 5.4f, 0f);
        [SerializeField] private Vector3 testLaunchImpulse = new(0.8f, 0.9f, 0f);
        [SerializeField] private Vector3 testLaunchTorque = new(0f, 0f, -3.2f);
        [SerializeField, Min(0f)] private float downDampingDelaySeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float downDampingRampSeconds = 2.2f;
        [SerializeField, Min(0f)] private float downLinearDamping = 4.8f;
        [SerializeField, Min(0f)] private float downAngularDamping = 6.4f;

        private Vector3[] initialLocalPositions;
        private Quaternion[] initialLocalRotations;
        private float[] initialLinearDampings;
        private float[] initialAngularDampings;
        private bool isDown;
        private float downElapsedSeconds;
        private bool setupErrorReported;

        private void Awake()
        {
            if (!ValidateSetup())
            {
                return;
            }

            EnsurePoseCaptured();
            CaptureDamping();
            SetRagdollActive(false);
        }

        [ContextMenu("PHS Test Enter Down")]
        public void TestEnterDown()
        {
            EnterDown(testLaunchVelocity, testLaunchImpulse, testLaunchTorque);
        }

        [ContextMenu("PHS Restore From Down")]
        public void RestoreFromDown()
        {
            if (!ValidateSetup())
            {
                return;
            }

            SetRagdollActive(false);
            EnsurePoseCaptured();
            RestorePose();

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.Update(0f);
            }

            SetDownScriptsEnabled(true);
            isDown = false;
            downElapsedSeconds = 0f;
        }

        public void EnterDown(Vector3 launchVelocity, Vector3 launchImpulse, Vector3 launchTorque)
        {
            if (isDown || !ValidateSetup())
            {
                return;
            }

            SetDownScriptsEnabled(false);

            if (animator != null)
            {
                animator.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            SetRagdollActive(true);
            ApplyLaunch(launchVelocity, launchImpulse, launchTorque);
            isDown = true;
            downElapsedSeconds = 0f;
            Debug.Log($"PHS_RAGDOLL_ENTER_DOWN target={name} launchVelocity={launchVelocity} launchImpulse={launchImpulse}");
        }

        private void FixedUpdate()
        {
            if (!isDown || !ValidateSetup())
            {
                return;
            }

            downElapsedSeconds += Time.fixedDeltaTime;
            ApplyDownDamping();
        }

        private void SetRagdollActive(bool active)
        {
            foreach (var jellyBounceBody in jellyBounceBodies)
            {
                if (jellyBounceBody != null)
                {
                    jellyBounceBody.SetJellyActive(active);
                }
            }

            foreach (var ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider != null)
                {
                    ragdollCollider.enabled = active;
                }
            }

            foreach (var ragdollBody in ragdollBodies)
            {
                if (ragdollBody == null)
                {
                    continue;
                }

                if (active)
                {
                    ragdollBody.isKinematic = false;
                    ragdollBody.detectCollisions = true;
                    RestoreDamping(ragdollBody);
                    ragdollBody.linearVelocity = Vector3.zero;
                    ragdollBody.angularVelocity = Vector3.zero;
                    ragdollBody.WakeUp();
                    continue;
                }

                if (!ragdollBody.isKinematic)
                {
                    ragdollBody.linearVelocity = Vector3.zero;
                    ragdollBody.angularVelocity = Vector3.zero;
                }

                ragdollBody.detectCollisions = false;
                RestoreDamping(ragdollBody);
                ragdollBody.isKinematic = true;
            }
        }

        private void ApplyDownDamping()
        {
            if (downElapsedSeconds < downDampingDelaySeconds)
            {
                return;
            }

            var t = Mathf.Clamp01((downElapsedSeconds - downDampingDelaySeconds) / downDampingRampSeconds);
            for (var i = 0; i < ragdollBodies.Length; i++)
            {
                var ragdollBody = ragdollBodies[i];
                if (ragdollBody == null || ragdollBody.isKinematic)
                {
                    continue;
                }

                var initialLinearDamping = GetInitialLinearDamping(i);
                var initialAngularDamping = GetInitialAngularDamping(i);
                ragdollBody.linearDamping = Mathf.Lerp(initialLinearDamping, downLinearDamping, t);
                ragdollBody.angularDamping = Mathf.Lerp(initialAngularDamping, downAngularDamping, t);
            }
        }

        private void CaptureDamping()
        {
            initialLinearDampings = new float[ragdollBodies.Length];
            initialAngularDampings = new float[ragdollBodies.Length];

            for (var i = 0; i < ragdollBodies.Length; i++)
            {
                var ragdollBody = ragdollBodies[i];
                if (ragdollBody == null)
                {
                    continue;
                }

                initialLinearDampings[i] = ragdollBody.linearDamping;
                initialAngularDampings[i] = ragdollBody.angularDamping;
            }
        }

        private void RestoreDamping(Rigidbody ragdollBody)
        {
            var index = System.Array.IndexOf(ragdollBodies, ragdollBody);
            ragdollBody.linearDamping = GetInitialLinearDamping(index);
            ragdollBody.angularDamping = GetInitialAngularDamping(index);
        }

        private float GetInitialLinearDamping(int index)
        {
            if (initialLinearDampings == null || index < 0 || index >= initialLinearDampings.Length)
            {
                return 0f;
            }

            return initialLinearDampings[index];
        }

        private float GetInitialAngularDamping(int index)
        {
            if (initialAngularDampings == null || index < 0 || index >= initialAngularDampings.Length)
            {
                return 0.05f;
            }

            return initialAngularDampings[index];
        }

        private void ApplyLaunch(Vector3 launchVelocity, Vector3 launchImpulse, Vector3 launchTorque)
        {
            foreach (var ragdollBody in ragdollBodies)
            {
                if (ragdollBody != null)
                {
                    ragdollBody.linearVelocity = launchVelocity;
                }
            }

            impulseBody.AddForce(launchImpulse, ForceMode.Impulse);
            impulseBody.AddTorque(launchTorque, ForceMode.Impulse);
        }

        private void SetDownScriptsEnabled(bool active)
        {
            foreach (var behaviour in disableOnDown)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = active;
                }
            }
        }

        private void CapturePose()
        {
            initialLocalPositions = new Vector3[poseBones.Length];
            initialLocalRotations = new Quaternion[poseBones.Length];

            for (var i = 0; i < poseBones.Length; i++)
            {
                initialLocalPositions[i] = poseBones[i].localPosition;
                initialLocalRotations[i] = poseBones[i].localRotation;
            }
        }

        private void EnsurePoseCaptured()
        {
            if (initialLocalRotations != null && initialLocalRotations.Length == poseBones.Length)
            {
                return;
            }

            CapturePose();
        }

        private void RestorePose()
        {
            if (initialLocalRotations == null || initialLocalRotations.Length != poseBones.Length)
            {
                Debug.LogError($"PHS_RAGDOLL_RESTORE_FAILED reason=pose_not_captured target={name}");
                return;
            }

            for (var i = 0; i < poseBones.Length; i++)
            {
                poseBones[i].localPosition = initialLocalPositions[i];
                poseBones[i].localRotation = initialLocalRotations[i];
            }
        }

        private bool ValidateSetup()
        {
            if (animator == null)
            {
                LogSetupError("animator_missing");
                return false;
            }

            if (characterController == null)
            {
                LogSetupError("characterController_missing");
                return false;
            }

            if (impulseBody == null)
            {
                LogSetupError("impulseBody_missing");
                return false;
            }

            if (ragdollBodies == null || ragdollBodies.Length == 0)
            {
                LogSetupError("ragdollBodies_missing");
                return false;
            }

            if (ragdollColliders == null || ragdollColliders.Length == 0)
            {
                LogSetupError("ragdollColliders_missing");
                return false;
            }

            if (jellyBounceBodies == null || jellyBounceBodies.Length == 0)
            {
                LogSetupError("jellyBounceBodies_missing");
                return false;
            }

            if (poseBones == null || poseBones.Length == 0)
            {
                LogSetupError("poseBones_missing");
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
            Debug.LogError($"PHS_RAGDOLL_STATE_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
