using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    public sealed class NetworkRunWarningAudioPresenter : MonoBehaviour
    {
        private static readonly float[] WarningThresholds = { 30f, 10f, 5f };

        [SerializeField] private NetworkRunIncidentLedger incidentLedger;
        [SerializeField] private NetworkRunStageClock stageClock;
        [SerializeField] private MonoBehaviour cuePlayerSource;

        private readonly HashSet<string> announcedIncidentStates = new();
        private readonly HashSet<float> announcedClockThresholds = new();
        private INetworkAudioCuePlayer cuePlayer;
        private uint observedStageSequence;
        private float previousRemainingSeconds;
        private bool hasClockBaseline;
        private bool isSubscribed;

        public bool HasRequiredReferences =>
            incidentLedger != null
            && stageClock != null
            && cuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            cuePlayer = cuePlayerSource as INetworkAudioCuePlayer;
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    $"PHS_NETWORK_WARNING_AUDIO_SETUP_FAILED reason=inspector_reference_missing presenter={name}",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!enabled || isSubscribed)
            {
                return;
            }

            incidentLedger.CommandChanged += HandleIncidentCommandChanged;
            stageClock.SnapshotChanged += HandleStageClockSnapshotChanged;
            isSubscribed = true;
            ResetClockBaseline(stageClock.Snapshot);
        }

        private void OnDisable()
        {
            if (!isSubscribed)
            {
                return;
            }

            incidentLedger.CommandChanged -= HandleIncidentCommandChanged;
            stageClock.SnapshotChanged -= HandleStageClockSnapshotChanged;
            isSubscribed = false;
        }

        private void Update()
        {
            if (!hasClockBaseline
                || stageClock.State != NetworkRunStageClockState.Running)
            {
                return;
            }

            if (stageClock.StageSequence != observedStageSequence)
            {
                ResetClockBaseline(stageClock.Snapshot);
                return;
            }

            var currentRemainingSeconds = stageClock.RemainingSeconds;
            var crossedThreshold = false;
            foreach (var threshold in WarningThresholds)
            {
                if (!announcedClockThresholds.Contains(threshold)
                    && previousRemainingSeconds > threshold
                    && currentRemainingSeconds <= threshold)
                {
                    announcedClockThresholds.Add(threshold);
                    crossedThreshold = true;
                }
            }

            previousRemainingSeconds = currentRemainingSeconds;
            if (crossedThreshold)
            {
                PlayCue(NetworkAudioCue.Warning, "stage_clock_threshold");
            }
        }

        private void HandleIncidentCommandChanged(
            NetworkListEvent<NetworkRunIncidentCommand> changeEvent)
        {
            var command = changeEvent.Value;
            if (command.State != NetworkRunIncidentCommandState.Active)
            {
                return;
            }

            var key = $"{command.CommandId}:{command.StateRevision}";
            if (announcedIncidentStates.Add(key))
            {
                PlayCue(NetworkAudioCue.AccidentAppeared, "incident_active");
            }
        }

        private void HandleStageClockSnapshotChanged(
            NetworkRunStageClockSnapshot previous,
            NetworkRunStageClockSnapshot current)
        {
            if (previous.StageSequence != current.StageSequence
                || previous.State != current.State)
            {
                ResetClockBaseline(current);
            }
        }

        private void ResetClockBaseline(NetworkRunStageClockSnapshot snapshot)
        {
            observedStageSequence = snapshot.StageSequence;
            announcedClockThresholds.Clear();
            announcedIncidentStates.Clear();
            previousRemainingSeconds = stageClock.RemainingSeconds;
            hasClockBaseline = snapshot.State == NetworkRunStageClockState.Running;
        }

        private void PlayCue(NetworkAudioCue cue, string trigger)
        {
            if (cuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_WARNING_AUDIO_PLAY_FAILED reason=cue_player_missing trigger={trigger}",
                    this);
                return;
            }

            if (cuePlayer.TryPlay(cue, out var reason)
                || reason == "cue_cooldown")
            {
                return;
            }

            Debug.LogError(
                $"PHS_NETWORK_WARNING_AUDIO_PLAY_FAILED reason={reason} trigger={trigger}",
                this);
        }
    }
}
