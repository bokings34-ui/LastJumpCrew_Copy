using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    public sealed class NetworkEventLifecycleAudioPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkEventCoordinator eventCoordinator;
        [SerializeField] private MonoBehaviour cuePlayerSource;

        private readonly HashSet<ulong> announcedEventInstances = new();
        private readonly List<NetworkEventLifecycleSnapshot> snapshotBuffer = new();
        private INetworkAudioCuePlayer cuePlayer;
        private bool isSubscribed;
        private bool initialSnapshotPassComplete;

        public bool HasRequiredReferences => eventCoordinator != null
            && cuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            cuePlayer = cuePlayerSource as INetworkAudioCuePlayer;
            if (HasRequiredReferences)
            {
                return;
            }

            Debug.LogError(
                $"PHS_EVENT_AUDIO_SETUP_FAILED reason=inspector_reference_missing presenter={name}",
                this);
            enabled = false;
        }

        private void OnEnable()
        {
            if (!enabled || eventCoordinator == null || isSubscribed)
            {
                return;
            }

            eventCoordinator.LifecycleSnapshotsChanged += HandleLifecycleSnapshotsChanged;
            isSubscribed = true;
            ReconcileActiveEvents();
        }

        private void Update()
        {
            if (initialSnapshotPassComplete
                || eventCoordinator == null
                || !eventCoordinator.IsSpawned)
            {
                return;
            }

            initialSnapshotPassComplete = true;
            ReconcileActiveEvents();
        }

        private void OnDisable()
        {
            if (!isSubscribed || eventCoordinator == null)
            {
                return;
            }

            eventCoordinator.LifecycleSnapshotsChanged -= HandleLifecycleSnapshotsChanged;
            isSubscribed = false;
        }

        private void HandleLifecycleSnapshotsChanged()
        {
            ReconcileActiveEvents();
        }

        private void ReconcileActiveEvents()
        {
            if (eventCoordinator == null || !eventCoordinator.IsSpawned)
            {
                return;
            }

            eventCoordinator.CopySnapshotsTo(snapshotBuffer);
            foreach (var snapshot in snapshotBuffer)
            {
                if (snapshot.State != EventState.InProgress
                    || !IsScheduledEvent(snapshot.EventId)
                    || !announcedEventInstances.Add(snapshot.InstanceId))
                {
                    continue;
                }

                if (cuePlayer.TryPlay(NetworkAudioCue.AccidentAppeared, out var reason)
                    || reason == "cue_cooldown")
                {
                    continue;
                }

                Debug.LogError(
                    $"PHS_EVENT_AUDIO_PLAY_FAILED reason={reason} event={snapshot.EventId} instance={snapshot.InstanceId}",
                    this);
            }
        }

        private static bool IsScheduledEvent(EventId eventId)
        {
            return eventId is EventId.Fire
                or EventId.EnemySpawn
                or EventId.PowerOff
                or EventId.OxygenLeak
                or EventId.MicDestroy
                or EventId.HullBreach
                or EventId.EnemyScout
                or EventId.MeteorAttack
                or EventId.EmpAttack;
        }
    }
}
