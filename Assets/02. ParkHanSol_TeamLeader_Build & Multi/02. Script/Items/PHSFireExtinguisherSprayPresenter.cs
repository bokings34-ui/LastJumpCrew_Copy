using UnityEngine;
using UnityEngine.VFX;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    public sealed class PHSFireExtinguisherSprayPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject effectRoot;
        [SerializeField] private VisualEffect sprayEffect;

        private float hideAtTime;
        private bool setupErrorLogged;

        private void Awake()
        {
            ValidateSetup();
            Hide();
        }

        private void Update()
        {
            if (effectRoot != null
                && effectRoot.activeSelf
                && Time.time >= hideAtTime)
            {
                Hide();
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        public bool TryPlay(float duration)
        {
            if (!ValidateSetup())
            {
                return false;
            }

            hideAtTime = Time.time + Mathf.Max(0.05f, duration);
            effectRoot.SetActive(true);
            sprayEffect.Reinit();
            sprayEffect.Play();
            return true;
        }

        private void Hide()
        {
            if (sprayEffect != null)
            {
                sprayEffect.Stop();
            }

            if (effectRoot != null)
            {
                effectRoot.SetActive(false);
            }
        }

        private bool ValidateSetup()
        {
            if (effectRoot != null && sprayEffect != null)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError(
                    $"PHS_EXTINGUISHER_SPRAY_SETUP_FAILED item={name} root={effectRoot != null} effect={sprayEffect != null}",
                    this);
            }

            return false;
        }
    }
}
