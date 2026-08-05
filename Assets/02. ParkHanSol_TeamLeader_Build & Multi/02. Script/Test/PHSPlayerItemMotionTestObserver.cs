using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Testing
{
    public sealed class PHSPlayerItemMotionTestObserver : MonoBehaviour
    {
        [SerializeField] private Camera firstPersonCamera;
        [SerializeField] private AudioListener firstPersonAudioListener;
        [SerializeField] private Transform playerVisualRoot;

        private Renderer[] playerRenderers;

        private void Awake()
        {
            if (firstPersonCamera == null ||
                firstPersonAudioListener == null ||
                playerVisualRoot == null)
            {
                Debug.LogError($"PHS_TEST_OBSERVER_SETUP_FAILED observer={name}", this);
                enabled = false;
                return;
            }

            playerRenderers = playerVisualRoot.GetComponentsInChildren<Renderer>(true);
            ApplyThirdPersonPresentation();
        }

        private void LateUpdate()
        {
            ApplyThirdPersonPresentation();
        }

        private void ApplyThirdPersonPresentation()
        {
            firstPersonCamera.enabled = false;
            firstPersonAudioListener.enabled = false;

            foreach (var playerRenderer in playerRenderers)
            {
                if (playerRenderer != null)
                {
                    playerRenderer.enabled = true;
                }
            }
        }
    }
}
