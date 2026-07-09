using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype
{
    public sealed class SquishyRagdollPhysicsPreviewDriver : MonoBehaviour
    {
        private enum PreviewState
        {
            Waiting,
            WindingUp,
            Ragdoll
        }

        [SerializeField] private Transform targetRoot;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private CharacterController targetController;
        [SerializeField] private Transform paddle;
        [SerializeField] private Rigidbody impulseBody;
        [SerializeField] private Rigidbody[] ragdollBodies;
        [SerializeField] private Collider[] ragdollColliders;
        [SerializeField] private SquishyRagdollJellyBounceBody[] jellyBounceBodies;
        [SerializeField] private Transform[] poseBones;
        [SerializeField] private Vector3 targetStartPosition = new(-1f, -0.95f, 1.15f);
        [SerializeField] private Vector3 targetStartEulerAngles = new(0f, 180f, 0f);
        [SerializeField] private Vector3 paddleStartPosition = new(-3.45f, -0.05f, 1.15f);
        [SerializeField] private Vector3 paddleHitPosition = new(-1.65f, -0.05f, 1.15f);
        [SerializeField, Min(0.05f)] private float waitSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.22f;
        [SerializeField, Min(0.25f)] private float resetAfterSeconds = 4.5f;
        [SerializeField] private Vector3 launchVelocity = new(7.25f, 3.2f, 0f);
        [SerializeField] private Vector3 launchImpulse = new(2.4f, 0.9f, 0f);
        [SerializeField] private Vector3 launchTorque = new(0f, 0f, -4.5f);
        [SerializeField] private bool autoStart = true;

        private PreviewState state;
        private float stateTime;
        private bool setupComplete;
        private bool setupErrorReported;
        private Vector3[] initialLocalPositions;
        private Quaternion[] initialLocalRotations;

        private void Awake()
        {
            setupComplete = ValidateSetup();
            if (!setupComplete)
            {
                return;
            }

            CapturePose();
            IgnoreSelfCollisions();
            ResetPreview();
        }

        private void OnEnable()
        {
            if (!setupComplete)
            {
                setupComplete = ValidateSetup();
            }

            if (setupComplete && initialLocalRotations == null)
            {
                CapturePose();
                IgnoreSelfCollisions();
            }

            if (setupComplete && autoStart)
            {
                ResetPreview();
            }
        }

        private void Update()
        {
            if (!setupComplete)
            {
                return;
            }

            stateTime += Time.deltaTime;

            switch (state)
            {
                case PreviewState.Waiting:
                    if (stateTime >= waitSeconds)
                    {
                        SetState(PreviewState.WindingUp);
                    }
                    break;

                case PreviewState.WindingUp:
                    MovePaddleToHit();
                    if (stateTime >= windupSeconds)
                    {
                        ReleaseRagdoll();
                        SetState(PreviewState.Ragdoll);
                    }
                    break;

                case PreviewState.Ragdoll:
                    if (stateTime >= resetAfterSeconds)
                    {
                        ResetPreview();
                    }
                    break;
            }
        }

        private void MovePaddleToHit()
        {
            var t = Mathf.Clamp01(stateTime / windupSeconds);
            t = 1f - (1f - t) * (1f - t);
            paddle.position = Vector3.Lerp(paddleStartPosition, paddleHitPosition, t);
        }

        private void ReleaseRagdoll()
        {
            if (targetAnimator != null)
            {
                targetAnimator.enabled = false;
            }

            if (targetController != null)
            {
                targetController.enabled = false;
            }

            SetRagdollActive(true);
            ApplyLaunchVelocity();
            impulseBody.AddForce(launchImpulse, ForceMode.Impulse);
            impulseBody.AddTorque(launchTorque, ForceMode.Impulse);
        }

        private void ApplyLaunchVelocity()
        {
            foreach (var ragdollBody in ragdollBodies)
            {
                if (ragdollBody == null)
                {
                    continue;
                }

                ragdollBody.linearVelocity = launchVelocity;
            }
        }

        [ContextMenu("Reset Preview")]
        public void ResetPreview()
        {
            if (!setupComplete)
            {
                return;
            }

            SetRagdollActive(false);
            RestorePose();
            targetRoot.SetPositionAndRotation(targetStartPosition, Quaternion.Euler(targetStartEulerAngles));
            paddle.position = paddleStartPosition;

            if (targetController != null)
            {
                targetController.enabled = true;
            }

            if (targetAnimator != null)
            {
                targetAnimator.enabled = true;
                targetAnimator.Update(0f);
            }

            SetState(PreviewState.Waiting);
        }

        private void SetRagdollActive(bool active)
        {
            if (jellyBounceBodies != null)
            {
                foreach (var jellyBounceBody in jellyBounceBodies)
                {
                    if (jellyBounceBody != null)
                    {
                        jellyBounceBody.SetJellyActive(active);
                    }
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

                ragdollBody.isKinematic = true;
                ragdollBody.detectCollisions = false;
            }
        }

        private void IgnoreSelfCollisions()
        {
            for (var i = 0; i < ragdollColliders.Length; i++)
            {
                var first = ragdollColliders[i];
                if (first == null)
                {
                    continue;
                }

                for (var j = i + 1; j < ragdollColliders.Length; j++)
                {
                    var second = ragdollColliders[j];
                    if (second != null)
                    {
                        Physics.IgnoreCollision(first, second, true);
                    }
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

        private void RestorePose()
        {
            if (initialLocalRotations == null || initialLocalRotations.Length != poseBones.Length)
            {
                return;
            }

            for (var i = 0; i < poseBones.Length; i++)
            {
                poseBones[i].localPosition = initialLocalPositions[i];
                poseBones[i].localRotation = initialLocalRotations[i];
            }
        }

        private void SetState(PreviewState nextState)
        {
            state = nextState;
            stateTime = 0f;
        }

        private bool ValidateSetup()
        {
            if (targetRoot == null)
            {
                LogSetupError("targetRoot_missing");
                return false;
            }

            if (targetAnimator == null)
            {
                LogSetupError("targetAnimator_missing");
                return false;
            }

            if (paddle == null)
            {
                LogSetupError("paddle_missing");
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
            Debug.LogError($"PHS_RAGDOLL_PREVIEW_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
