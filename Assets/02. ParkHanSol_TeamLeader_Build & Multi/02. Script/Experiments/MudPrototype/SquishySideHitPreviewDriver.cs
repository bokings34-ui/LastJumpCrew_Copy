using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype
{
    public sealed class SquishySideHitPreviewDriver : MonoBehaviour
    {
        private enum PreviewState
        {
            Waiting,
            WindingUp,
            Flying
        }

        [SerializeField] private CharacterController targetController;
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Transform[] floppyBones;
        [SerializeField] private Transform paddle;
        [SerializeField] private Transform bounceWall;
        [SerializeField] private Vector3 targetStartPosition;
        [SerializeField] private Vector3 targetStartEulerAngles = new(0f, 180f, 0f);
        [SerializeField] private Vector3 targetFallEulerAngles = new(0f, 180f, -92f);
        [SerializeField] private Vector3 paddleStartPosition;
        [SerializeField] private Vector3 paddleHitPosition;
        [SerializeField] private float bounceWallX = 4.25f;
        [SerializeField, Min(0f)] private float wallBounceHorizontalMultiplier = 0.62f;
        [SerializeField, Min(0f)] private float wallBounceUpVelocity = 2.2f;
        [SerializeField, Min(0.1f)] private float waitSeconds = 0.75f;
        [SerializeField, Min(0.05f)] private float windupSeconds = 0.28f;
        [SerializeField, Min(0.05f)] private float fallRotateSeconds = 0.55f;
        [SerializeField, Min(0.1f)] private float resetAfterSeconds = 2.4f;
        [SerializeField] private Vector3 launchVelocity = new(7.5f, 3.8f, 0f);
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float groundY = -0.95f;
        [SerializeField, Min(0f)] private float fallenGroundLift = 0.68f;
        [SerializeField, Min(0f)] private float flopAngle = 34f;
        [SerializeField, Min(0f)] private float flopFrequency = 8f;
        [SerializeField, Min(0f)] private float flopDamping = 1.3f;
        [SerializeField] private bool autoStart = true;

        private PreviewState state;
        private Vector3 velocity;
        private float stateTime;
        private bool setupComplete;
        private bool setupErrorReported;
        private Quaternion[] initialBoneRotations;
        private bool bouncedFromWall;

        private void Awake()
        {
            setupComplete = ValidateSetup();
            if (!setupComplete)
            {
                return;
            }

            ResetPreview();
        }

        private void OnEnable()
        {
            if (!setupComplete)
            {
                setupComplete = ValidateSetup();
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
                        velocity = launchVelocity;
                        bouncedFromWall = false;
                        ReleaseAnimatorControl();
                        SetState(PreviewState.Flying);
                    }
                    break;

                case PreviewState.Flying:
                    MoveTarget();
                    ApplyFloppyBones();
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

        private void MoveTarget()
        {
            velocity.y += gravity * Time.deltaTime;
            var motion = velocity * Time.deltaTime;
            SetTargetPosition(targetRoot.position + motion);
            RotateTargetWhileFlying();
            BounceFromWall();

            var currentGroundY = GetCurrentGroundY();
            if (targetRoot.position.y < currentGroundY)
            {
                var position = targetRoot.position;
                position.y = currentGroundY;
                SetTargetPosition(position);
                velocity.y = Mathf.Abs(velocity.y) * 0.45f;
                velocity.x *= 0.78f;
            }
        }

        private void RotateTargetWhileFlying()
        {
            var t = Mathf.Clamp01(stateTime / fallRotateSeconds);
            t = t * t * (3f - 2f * t);
            var startRotation = Quaternion.Euler(targetStartEulerAngles);
            var fallRotation = Quaternion.Euler(targetFallEulerAngles);
            targetRoot.rotation = Quaternion.Slerp(startRotation, fallRotation, t);
        }

        private float GetCurrentGroundY()
        {
            var fallT = Mathf.Clamp01(stateTime / fallRotateSeconds);
            fallT = fallT * fallT * (3f - 2f * fallT);
            return groundY + fallenGroundLift * fallT;
        }

        private void BounceFromWall()
        {
            if (bouncedFromWall || targetRoot.position.x < bounceWallX)
            {
                return;
            }

            var position = targetRoot.position;
            position.x = bounceWallX;
            SetTargetPosition(position);
            velocity.x = -Mathf.Abs(velocity.x) * wallBounceHorizontalMultiplier;
            velocity.y = Mathf.Max(velocity.y, wallBounceUpVelocity);
            bouncedFromWall = true;
        }

        private void ReleaseAnimatorControl()
        {
            if (targetAnimator != null)
            {
                targetAnimator.enabled = false;
            }
        }

        private void RestoreAnimatorControl()
        {
            if (targetAnimator != null)
            {
                targetAnimator.enabled = true;
                targetAnimator.Update(0f);
            }
        }

        private void ApplyFloppyBones()
        {
            var damping = Mathf.Exp(-flopDamping * stateTime);
            var amount = flopAngle * damping;

            for (var i = 0; i < floppyBones.Length; i++)
            {
                var bone = floppyBones[i];
                if (bone == null)
                {
                    continue;
                }

                var phase = i * 1.37f;
                var x = Mathf.Sin(stateTime * flopFrequency + phase) * amount;
                var z = Mathf.Cos(stateTime * (flopFrequency * 0.73f) + phase) * amount * 0.65f;
                bone.localRotation = initialBoneRotations[i] * Quaternion.Euler(x, 0f, z);
            }
        }

        private void RestoreBones()
        {
            if (initialBoneRotations == null || initialBoneRotations.Length != floppyBones.Length)
            {
                return;
            }

            for (var i = 0; i < floppyBones.Length; i++)
            {
                if (floppyBones[i] != null)
                {
                    floppyBones[i].localRotation = initialBoneRotations[i];
                }
            }
        }

        [ContextMenu("Reset Preview")]
        public void ResetPreview()
        {
            if (!setupComplete)
            {
                return;
            }

            RestoreAnimatorControl();
            RestoreBones();
            SetTargetPose(targetStartPosition, Quaternion.Euler(targetStartEulerAngles));
            paddle.position = paddleStartPosition;
            if (bounceWall != null)
            {
                var wallPosition = bounceWall.position;
                wallPosition.x = bounceWallX;
                bounceWall.position = wallPosition;
            }

            velocity = Vector3.zero;
            bouncedFromWall = false;
            SetState(PreviewState.Waiting);
        }

        private void SetTargetPosition(Vector3 position)
        {
            SetTargetPose(position, targetRoot.rotation);
        }

        private void SetTargetPose(Vector3 position, Quaternion rotation)
        {
            var wasEnabled = targetController.enabled;
            targetController.enabled = false;
            targetRoot.position = position;
            targetRoot.rotation = rotation;
            targetController.enabled = wasEnabled;
        }

        private void SetState(PreviewState nextState)
        {
            state = nextState;
            stateTime = 0f;
        }

        private bool ValidateSetup()
        {
            if (targetController == null)
            {
                LogSetupError("targetController_missing");
                return false;
            }

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

            if (floppyBones == null || floppyBones.Length == 0)
            {
                LogSetupError("floppyBones_missing");
                return false;
            }

            if (paddle == null)
            {
                LogSetupError("paddle_missing");
                return false;
            }

            if (bounceWall == null)
            {
                LogSetupError("bounceWall_missing");
                return false;
            }

            initialBoneRotations = new Quaternion[floppyBones.Length];
            for (var i = 0; i < floppyBones.Length; i++)
            {
                if (floppyBones[i] == null)
                {
                    LogSetupError($"floppyBone_missing index={i}");
                    return false;
                }

                initialBoneRotations[i] = floppyBones[i].localRotation;
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
            Debug.LogError($"PHS_SIDE_HIT_PREVIEW_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
