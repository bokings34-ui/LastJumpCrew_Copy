using System.Collections;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSFirePresentationRuntimeAdapter :
        MonoBehaviour,
        IShipAccidentPresentation
    {
        [SerializeField] private FirePresentationController presentationController;
        [SerializeField] private FireIntensity initialIntensity = FireIntensity.Small;
        [SerializeField, Min(0f)] private float telegraphSeconds = 1.5f;
        [SerializeField, Min(0f)] private float extinguishSeconds = 1.2f;

        private Coroutine activationRoutine;
        private FireIntensity targetIntensity;
        private bool isActivated;

        private void Awake()
        {
            if (presentationController == null)
            {
                presentationController = GetComponent<FirePresentationController>();
            }
        }

        private void OnEnable()
        {
            if (presentationController == null)
            {
                Debug.LogError("PHS_FIRE_PRESENTATION_FAILED reason=controller_missing", this);
                return;
            }

            targetIntensity = initialIntensity;
            isActivated = false;
            if (telegraphSeconds <= 0f)
            {
                presentationController.Telegraph();
                ActivateTargetIntensity();
                return;
            }

            presentationController.Telegraph();
            activationRoutine = StartCoroutine(ActivateAfterTelegraph());
        }

        private void OnDisable()
        {
            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
                activationRoutine = null;
            }

            presentationController?.ResetPresentation();
            isActivated = false;
        }

        public void ApplySnapshot(in NetworkShipAccidentSnapshot snapshot)
        {
            targetIntensity = ResolveIntensity(snapshot);
            if (isActivated)
            {
                presentationController.SetIntensity(targetIntensity);
            }
        }

        public float BeginClear()
        {
            if (activationRoutine != null)
            {
                StopCoroutine(activationRoutine);
                activationRoutine = null;
            }

            isActivated = false;
            presentationController.Extinguish();
            return extinguishSeconds;
        }

        private IEnumerator ActivateAfterTelegraph()
        {
            yield return new WaitForSeconds(telegraphSeconds);
            activationRoutine = null;
            ActivateTargetIntensity();
        }

        private void ActivateTargetIntensity()
        {
            presentationController.Activate(targetIntensity);
            isActivated = true;
        }

        private static FireIntensity ResolveIntensity(
            in NetworkShipAccidentSnapshot snapshot)
        {
            if (snapshot.RequiredRepairProgress <= 0)
            {
                return FireIntensity.Small;
            }

            var remainingRatio = 1f - Mathf.Clamp01(
                snapshot.RepairProgress
                / (float)snapshot.RequiredRepairProgress);
            if (remainingRatio > 0.66f)
            {
                return FireIntensity.Large;
            }

            return remainingRatio > 0.33f
                ? FireIntensity.Medium
                : FireIntensity.Small;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (presentationController == null)
            {
                presentationController = GetComponent<FirePresentationController>();
            }
        }
#endif
    }
}
