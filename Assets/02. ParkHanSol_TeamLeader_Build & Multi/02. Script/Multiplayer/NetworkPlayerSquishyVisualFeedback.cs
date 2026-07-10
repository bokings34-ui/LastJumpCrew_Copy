using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerSquishyVisualFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private CharacterController characterController;
        [SerializeField, Min(0f)] private float movementStretchPerSpeed = 0.018f;
        [SerializeField, Min(0f)] private float impactStrengthPerSpeed = 0.08f;
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 1.8f;
        [SerializeField, Range(0f, 0.45f)] private float maxSquash = 0.2f;
        [SerializeField, Range(0f, 0.45f)] private float maxStretch = 0.16f;
        [SerializeField, Min(0.01f)] private float springSmoothTime = 0.08f;
        [SerializeField, Min(0.01f)] private float impactRecoverySpeed = 4.5f;

        private Vector3 baseScale;
        private Vector3 scaleVelocity;
        private Vector3 previousPosition;
        private Vector3 previousVelocity;
        private Vector3 currentVelocity;
        private float impactAmount;
        private bool setupComplete;
        private bool setupErrorReported;

        private void Awake()
        {
            Setup();
        }

        private void OnEnable()
        {
            if (!setupComplete)
            {
                Setup();
            }
        }

        private void Setup()
        {
            if (!ValidateSetup())
            {
                return;
            }

            setupComplete = true;
            setupErrorReported = false;
            baseScale = visualRoot.localScale;
            previousPosition = transform.position;
            previousVelocity = Vector3.zero;
            currentVelocity = Vector3.zero;
            impactAmount = 0f;
        }

        private void LateUpdate()
        {
            if (!setupComplete)
            {
                return;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            currentVelocity = (transform.position - previousPosition) / deltaTime;

            var horizontalSpeed = new Vector2(currentVelocity.x, currentVelocity.z).magnitude;
            var velocityChange = (currentVelocity - previousVelocity).magnitude;
            AddImpact(velocityChange);

            var movementStretch = Mathf.Clamp(horizontalSpeed * movementStretchPerSpeed, 0f, maxStretch);
            var squash = Mathf.Clamp(impactAmount * maxSquash, 0f, maxSquash);
            var stretch = Mathf.Clamp(movementStretch + impactAmount * maxStretch, 0f, maxStretch);

            var horizontalScale = 1f + squash - movementStretch * 0.35f;
            var verticalScale = 1f + stretch - squash;
            var targetScale = new Vector3(
                baseScale.x * horizontalScale,
                baseScale.y * verticalScale,
                baseScale.z * horizontalScale);

            visualRoot.localScale = Vector3.SmoothDamp(
                visualRoot.localScale,
                targetScale,
                ref scaleVelocity,
                springSmoothTime,
                Mathf.Infinity,
                deltaTime);

            impactAmount = Mathf.MoveTowards(impactAmount, 0f, impactRecoverySpeed * deltaTime);
            previousVelocity = currentVelocity;
            previousPosition = transform.position;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!setupComplete)
            {
                return;
            }

            var controllerVelocity = characterController.velocity;
            var impactSpeed = Mathf.Abs(Vector3.Dot(controllerVelocity, hit.normal));
            AddImpact(impactSpeed);
        }

        private void AddImpact(float impactSpeed)
        {
            if (impactSpeed < minimumImpactSpeed)
            {
                return;
            }

            impactAmount = Mathf.Clamp01(impactAmount + (impactSpeed - minimumImpactSpeed) * impactStrengthPerSpeed);
        }

        private bool ValidateSetup()
        {
            if (visualRoot == null)
            {
                LogSetupError("visualRoot_missing");
                return false;
            }

            if (characterController == null)
            {
                LogSetupError("characterController_missing");
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
            Debug.LogError($"PHS_PLAYER_SQUISHY_SETUP_FAILED reason={reason} target={name}");
        }

        private void Reset()
        {
            characterController = GetComponent<CharacterController>();
        }
    }
}
