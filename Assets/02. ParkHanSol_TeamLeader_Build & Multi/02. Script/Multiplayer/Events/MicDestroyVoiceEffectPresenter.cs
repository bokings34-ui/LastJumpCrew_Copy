using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class MicDestroyVoiceEffectPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkEventCoordinator eventCoordinator;

        private IVoiceCommunicationSuppression voiceSuppression;
        private bool isSuppressionActive;

        public bool IsSuppressionActive => isSuppressionActive;

        private void OnEnable()
        {
            if (eventCoordinator == null)
            {
                Debug.LogError(
                    "PHS_MIC_DESTROY_VOICE_DISABLED reason=event_coordinator_missing",
                    this);
                enabled = false;
                return;
            }

            eventCoordinator.LifecycleSnapshotsChanged += RefreshSuppression;
            ProximityVoiceChatSession.ActiveSessionChanged += HandleActiveSessionChanged;
            SetVoiceSession(ProximityVoiceChatSession.ActiveSession);
        }

        private void OnDisable()
        {
            if (eventCoordinator != null)
            {
                eventCoordinator.LifecycleSnapshotsChanged -= RefreshSuppression;
            }

            ProximityVoiceChatSession.ActiveSessionChanged -= HandleActiveSessionChanged;
            SetVoiceSession(null);
        }

        private void HandleActiveSessionChanged(ProximityVoiceChatSession session)
        {
            SetVoiceSession(session);
        }

        private void SetVoiceSession(ProximityVoiceChatSession session)
        {
            voiceSuppression?.SetEventInputSuppressed(false);
            voiceSuppression = session;
            isSuppressionActive = false;
            RefreshSuppression();
        }

        private void RefreshSuppression()
        {
            if (voiceSuppression == null || eventCoordinator == null)
            {
                return;
            }

            isSuppressionActive = eventCoordinator.IsEventActive(EventId.MicDestroy);
            voiceSuppression.SetEventInputSuppressed(isSuppressionActive);
        }
    }
}
