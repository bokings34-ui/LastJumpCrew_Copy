using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    public sealed class NetworkRunWarpAudioPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkRunFlowCoordinator runFlowCoordinator;
        [SerializeField] private MonoBehaviour cuePlayerSource;

        private INetworkAudioCuePlayer cuePlayer;
        private bool warpStartPlayed;

        public bool HasRequiredReferences =>
            runFlowCoordinator != null
            && cuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            cuePlayer = cuePlayerSource as INetworkAudioCuePlayer;
            if (HasRequiredReferences)
            {
                return;
            }

            Debug.LogError(
                $"PHS_WARP_AUDIO_SETUP_FAILED root={name} " +
                $"runFlow={runFlowCoordinator != null} cuePlayer={cuePlayer != null}",
                this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (runFlowCoordinator != null)
            {
                runFlowCoordinator.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (runFlowCoordinator != null)
            {
                runFlowCoordinator.PhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(
            NetworkRunPhase previousPhase,
            NetworkRunPhase currentPhase)
        {
            if (previousPhase == NetworkRunPhase.WarpSafe
                && currentPhase == NetworkRunPhase.Warping)
            {
                Play(NetworkAudioCue.WarpStart);
                warpStartPlayed = true;
            }
            else if (warpStartPlayed
                && previousPhase == NetworkRunPhase.Warping
                && currentPhase != NetworkRunPhase.Warping)
            {
                Play(NetworkAudioCue.WarpEnd);
                warpStartPlayed = false;
            }
        }

        private void Play(NetworkAudioCue cue)
        {
            if (!cuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_WARP_AUDIO_PLAY_FAILED reason={reason} root={name} cue={cue}",
                    this);
            }
        }
    }
}
