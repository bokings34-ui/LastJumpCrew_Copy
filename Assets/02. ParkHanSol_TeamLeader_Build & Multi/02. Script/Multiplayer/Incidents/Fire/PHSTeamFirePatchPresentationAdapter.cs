using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSTeamFirePatchPresentationAdapter :
        MonoBehaviour
    {
        [SerializeField]
        private FirePresentationController presentationController;
        [SerializeField] private Light presentationLight;
        [SerializeField] private AudioSource fireAudio;

        private PHSFireIntensity appliedIntensity;
        private bool isActive;

        private void OnEnable()
        {
            ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        public void ApplyState(
            PHSFireIntensity intensity,
            bool allowAudio)
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_TEAM_FIRE_PATCH_PRESENTATION_FAILED " +
                    $"reason={reason}",
                    this);
                return;
            }

            if (intensity == PHSFireIntensity.None)
            {
                ResetPresentation();
                return;
            }

            var teamIntensity = ConvertIntensity(intensity);
            if (!isActive)
            {
                presentationController.Activate(teamIntensity);
            }
            else if (appliedIntensity != intensity)
            {
                presentationController.SetIntensity(teamIntensity);
            }

            // PHSFirePatchRuntimeTarget owns the one gameplay light per patch.
            // The team prefab light remains a required controller reference,
            // but must not multiply once per visual socket and spread bridge.
            presentationLight.enabled = false;
            presentationLight.intensity = 0f;
            if (!allowAudio)
            {
                fireAudio.Stop();
                fireAudio.volume = 0f;
            }

            appliedIntensity = intensity;
            isActive = true;
        }

        public bool TryValidate(out string reason)
        {
            if (presentationController == null)
            {
                reason = "controller_missing";
                return false;
            }

            if (fireAudio == null)
            {
                reason = "audio_missing";
                return false;
            }

            if (presentationLight == null)
            {
                reason = "light_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void ResetPresentation()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_TEAM_FIRE_PATCH_PRESENTATION_FAILED " +
                    $"reason={reason}",
                    this);
                return;
            }

            presentationController.ResetPresentation();
            fireAudio.Stop();
            fireAudio.volume = 0f;
            appliedIntensity = PHSFireIntensity.None;
            isActive = false;
        }

        private static FireIntensity ConvertIntensity(
            PHSFireIntensity intensity)
        {
            return intensity switch
            {
                PHSFireIntensity.Small => FireIntensity.Small,
                PHSFireIntensity.Medium => FireIntensity.Medium,
                PHSFireIntensity.Large => FireIntensity.Large,
                _ => FireIntensity.Small
            };
        }
    }
}
