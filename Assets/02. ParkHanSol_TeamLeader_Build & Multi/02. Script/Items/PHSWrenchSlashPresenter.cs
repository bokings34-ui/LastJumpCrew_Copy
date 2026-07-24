using System.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    public sealed class PHSWrenchSlashPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject effectRoot;
        [SerializeField] private ParticleSystem[] particles;
        [SerializeField, Min(0.05f)] private float visibleDuration = 0.42f;
        [SerializeField, Min(0.1f)] private float simulationSpeed = 3.2f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            effectRoot.SetActive(false);
        }

        public void Play()
        {
            if (!ValidateReferences())
                return;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);

            effectRoot.SetActive(true);
            foreach (var particle in particles)
            {
                var main = particle.main;
                main.simulationSpeed = simulationSpeed;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(visibleDuration);
            effectRoot.SetActive(false);
            hideRoutine = null;
        }

        private bool ValidateReferences()
        {
            if (effectRoot != null
                && particles != null
                && particles.Length > 0)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_WRENCH_SLASH_FAILED reason=reference_missing item={name}",
                this);
            return false;
        }
    }
}
