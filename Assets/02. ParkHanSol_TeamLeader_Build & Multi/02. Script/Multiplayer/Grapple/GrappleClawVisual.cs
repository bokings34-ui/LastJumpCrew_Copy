using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum GrappleClawPhase : byte
    {
        Hidden,
        Flying,
        Latched
    }

    public sealed class GrappleClawVisual : MonoBehaviour
    {
        [Header("Finger Pivots")]
        [SerializeField] private Transform rightFingerPivot;
        [SerializeField] private Transform leftFingerPivot;
        [SerializeField] private Transform topFingerPivot;
        [Header("Closed Pose Offsets")]
        [SerializeField] private Vector3 rightClosedEuler = new(0f, 0f, -35f);
        [SerializeField] private Vector3 leftClosedEuler = new(0f, 0f, 35f);
        [SerializeField] private Vector3 topClosedEuler = new(35f, 0f, 0f);
        [SerializeField, Range(0f, 1f)] private float gripClosure = 0.63f;
        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float closeDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float openDuration = 0.1f;
        [SerializeField, Min(0.01f)] private float gripDuration = 0.14f;

        private readonly Quaternion[] initialRotations = new Quaternion[3];
        private GrappleClawPhase phase = GrappleClawPhase.Hidden;
        private float phaseElapsed;
        private bool setupValid;
        private bool setupErrorLogged;

        private void Awake()
        {
            setupValid = ValidateSetup();
            if (!setupValid)
            {
                return;
            }

            initialRotations[0] = rightFingerPivot.localRotation;
            initialRotations[1] = leftFingerPivot.localRotation;
            initialRotations[2] = topFingerPivot.localRotation;
            ResetPose();
        }

        private void OnEnable()
        {
            if (!setupValid)
            {
                setupValid = ValidateSetup();
            }

            if (setupValid)
            {
                ResetPose();
            }
        }

        private void Update()
        {
            if (!setupValid)
            {
                return;
            }

            phaseElapsed += Time.deltaTime;
            switch (phase)
            {
                case GrappleClawPhase.Flying:
                    AnimateToward(1f, closeDuration);
                    break;
                case GrappleClawPhase.Latched:
                    AnimateLatchedPose();
                    break;
            }
        }

        public void SetPhase(GrappleClawPhase nextPhase)
        {
            if (!setupValid || phase == nextPhase)
            {
                return;
            }

            phase = nextPhase;
            phaseElapsed = 0f;
            if (phase == GrappleClawPhase.Hidden)
            {
                ResetPose();
            }
        }

        private void AnimateLatchedPose()
        {
            if (phaseElapsed <= openDuration)
            {
                var progress = Mathf.Clamp01(phaseElapsed / openDuration);
                SetPoseClosure(Mathf.Lerp(1f, 0f, Smooth(progress)));
                return;
            }

            var gripProgress = Mathf.Clamp01((phaseElapsed - openDuration) / gripDuration);
            SetPoseClosure(Mathf.Lerp(0f, gripClosure, Smooth(gripProgress)));
        }

        private void AnimateToward(float targetClosure, float duration)
        {
            var progress = Mathf.Clamp01(phaseElapsed / duration);
            SetPoseClosure(Mathf.Lerp(0f, targetClosure, Smooth(progress)));
        }

        private void ResetPose()
        {
            phase = GrappleClawPhase.Hidden;
            phaseElapsed = 0f;
            SetPoseClosure(0f);
        }

        private void SetPoseClosure(float closure)
        {
            rightFingerPivot.localRotation = initialRotations[0]
                * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(rightClosedEuler), closure);
            leftFingerPivot.localRotation = initialRotations[1]
                * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(leftClosedEuler), closure);
            topFingerPivot.localRotation = initialRotations[2]
                * Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.Euler(topClosedEuler), closure);
        }

        private bool ValidateSetup()
        {
            var valid = rightFingerPivot != null
                && leftFingerPivot != null
                && topFingerPivot != null;
            if (!valid && !setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_GRAPPLE_CLAW_SETUP_FAILED object={name} rightPivot={rightFingerPivot != null} leftPivot={leftFingerPivot != null} topPivot={topFingerPivot != null}");
            }

            return valid;
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }
    }
}
