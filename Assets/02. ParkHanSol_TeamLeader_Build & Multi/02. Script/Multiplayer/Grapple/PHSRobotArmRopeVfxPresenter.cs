using UnityEngine;
using UnityEngine.VFX;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class PHSRobotArmRopeVfxPresenter : MonoBehaviour
    {
        [SerializeField] private Transform effectRoot;
        [SerializeField] private VisualEffect ropeEffect;
        [SerializeField, Min(0.01f)] private float sourceLength = 3f;
        [SerializeField, Min(0.01f)] private float transverseScale = 0.16f;

        private bool visible;
        private bool setupErrorLogged;

        private void Awake()
        {
            ValidateSetup();
            SetVisible(false);
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        public void SetEndpoints(Vector3 origin, Vector3 end)
        {
            if (!ValidateSetup())
            {
                return;
            }

            var offset = end - origin;
            var distance = offset.magnitude;
            if (distance <= 0.001f)
            {
                SetVisible(false);
                return;
            }

            effectRoot.SetPositionAndRotation(
                origin,
                Quaternion.FromToRotation(Vector3.up, offset / distance));
            effectRoot.localScale = new Vector3(
                transverseScale,
                distance / sourceLength,
                transverseScale);
        }

        public void SetVisible(bool shouldShow)
        {
            if (!ValidateSetup())
            {
                return;
            }

            if (visible == shouldShow
                && effectRoot.gameObject.activeSelf == shouldShow)
            {
                return;
            }

            visible = shouldShow;
            if (!shouldShow)
            {
                ropeEffect.Stop();
                effectRoot.gameObject.SetActive(false);
                return;
            }

            effectRoot.gameObject.SetActive(true);
            ropeEffect.Reinit();
            ropeEffect.Play();
        }

        private bool ValidateSetup()
        {
            if (effectRoot != null && ropeEffect != null && sourceLength > 0f)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError(
                    $"PHS_GRAPPLE_ROPE_VFX_SETUP_FAILED player={name} root={effectRoot != null} effect={ropeEffect != null} sourceLength={sourceLength:F2}",
                    this);
            }

            return false;
        }
    }
}
