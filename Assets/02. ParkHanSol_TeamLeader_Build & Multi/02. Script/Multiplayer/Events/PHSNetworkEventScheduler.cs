using System;
using System.Collections.Generic;
using SM;
using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSNetworkEventScheduler : MonoBehaviour, IEventScheduleConfigurator
    {
        [SerializeField] private NetworkEventCoordinator coordinator;
        [SerializeField] private PHSNetworkEventChannel channel = PHSNetworkEventChannel.LegacyMixed;
        [SerializeField] private WeightedEventScheduleEntry[] weightedEvents =
        {
            new(EventId.Fire, 1f),
            new(EventId.EnemySpawn, 1f),
            new(EventId.OxygenLeak, 1f),
            new(EventId.PowerOff, 1f),
            new(EventId.EngineBreak, 1f),
            new(EventId.EnemyScout, 1f),
            new(EventId.MeteorAttack, 1f),
            new(EventId.EmpAttack, 1f)
        };
        [FormerlySerializedAs("spawnIntervalSeconds")]
        [SerializeField, Min(1f)] private float intervalMinSeconds = 30f;
        [SerializeField, Min(1f)] private float intervalMaxSeconds = 30f;
        [Tooltip("0 = no limit")]
        [SerializeField, Min(0)] private int maximumActiveEvents;

        private float nextSpawnTime;
        private bool isRunning;
        private bool setupValid;
        private bool scheduleValid;

        private void Awake()
        {
            setupValid = ValidateCoordinator();
            scheduleValid = ValidateSchedule(
                channel,
                weightedEvents,
                intervalMinSeconds,
                intervalMaxSeconds,
                maximumActiveEvents,
                out var reason);
            if (!scheduleValid)
            {
                Debug.LogError($"PHS_EVENT_SCHEDULER_SETUP_FAILED reason={reason}", this);
            }

            enabled = setupValid && scheduleValid;
        }

        private void Update()
        {
            if (!isRunning || !coordinator.IsAuthoritative || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + RollNextInterval();
            if (maximumActiveEvents > 0 && CountActiveEvents() >= maximumActiveEvents)
            {
                return;
            }

            if (!TrySelectWeightedEvent(out var eventId, out var selectionReason))
            {
                Debug.LogWarning(
                    $"PHS_EVENT_SCHEDULER_SPAWN_SKIPPED reason={selectionReason}",
                    this);
                return;
            }

            if (!coordinator.TrySpawnEventServer(eventId, out var instanceId))
            {
                Debug.LogError(
                    $"PHS_EVENT_SCHEDULER_SPAWN_FAILED reason=coordinator_rejected event={eventId}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_EVENT_SCHEDULER_SPAWNED event={eventId} instance={instanceId}",
                this);
        }

        public void StartScheduler()
        {
            if (!TryStartServer(out var reason))
            {
                Debug.LogError($"PHS_EVENT_SCHEDULER_START_FAILED reason={reason}", this);
            }
        }

        public void StopScheduler()
        {
            if (!TryStopServer(out var reason))
            {
                Debug.LogError($"PHS_EVENT_SCHEDULER_STOP_FAILED reason={reason}", this);
            }
        }

        public void ResetScheduler()
        {
            isRunning = false;
            nextSpawnTime = 0f;
        }

        public bool TryConfigureServer(
            PHSNetworkEventChannel newChannel,
            WeightedEventScheduleEntry[] entries,
            float newIntervalMinSeconds,
            float newIntervalMaxSeconds,
            int newMaximumActiveEvents,
            out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (isRunning)
            {
                reason = "scheduler_running";
                return false;
            }

            if (!ValidateSchedule(
                    newChannel,
                    entries,
                    newIntervalMinSeconds,
                    newIntervalMaxSeconds,
                    newMaximumActiveEvents,
                    out reason))
            {
                return false;
            }

            channel = newChannel;
            weightedEvents = (WeightedEventScheduleEntry[])entries.Clone();
            intervalMinSeconds = newIntervalMinSeconds;
            intervalMaxSeconds = newIntervalMaxSeconds;
            maximumActiveEvents = newMaximumActiveEvents;
            scheduleValid = true;
            enabled = true;
            Debug.Log(
                $"PHS_EVENT_SCHEDULER_CONFIGURED channel={channel} entries={weightedEvents.Length} interval={intervalMinSeconds:0.###}-{intervalMaxSeconds:0.###} maxActive={maximumActiveEvents}",
                this);
            reason = null;
            return true;
        }

        public bool TryStartServer(out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (!scheduleValid)
            {
                reason = "schedule_invalid";
                return false;
            }

            if (isRunning)
            {
                reason = null;
                return true;
            }

            isRunning = true;
            nextSpawnTime = Time.time + RollNextInterval();
            reason = null;
            return true;
        }

        public bool TryStopServer(out string reason)
        {
            if (!CanExecuteServerCommand(out reason))
            {
                return false;
            }

            if (!isRunning)
            {
                reason = null;
                return true;
            }

            isRunning = false;
            nextSpawnTime = 0f;
            reason = null;
            return true;
        }

        private int CountActiveEvents()
        {
            var count = 0;
            for (var index = 0; index < coordinator.SnapshotCount; index++)
            {
                if (coordinator.TryGetSnapshotAt(index, out var snapshot) && !snapshot.IsTerminal)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TrySelectWeightedEvent(out EventId selectedEventId, out string reason)
        {
            var availableEntries = new List<WeightedEventScheduleEntry>(weightedEvents.Length);
            var totalWeight = 0f;
            foreach (var entry in weightedEvents)
            {
                if (coordinator.IsEventActive(entry.eventId))
                {
                    continue;
                }

                availableEntries.Add(entry);
                totalWeight += entry.weight;
            }

            if (availableEntries.Count == 0 || totalWeight <= 0f)
            {
                selectedEventId = default;
                reason = "no_available_event";
                return false;
            }

            var roll = UnityEngine.Random.value * totalWeight;
            foreach (var entry in availableEntries)
            {
                roll -= entry.weight;
                if (roll <= 0f)
                {
                    selectedEventId = entry.eventId;
                    reason = null;
                    return true;
                }
            }

            selectedEventId = default;
            reason = "weighted_roll_out_of_range";
            return false;
        }

        private float RollNextInterval()
        {
            return UnityEngine.Random.Range(intervalMinSeconds, intervalMaxSeconds);
        }

        private bool CanExecuteServerCommand(out string reason)
        {
            if (!setupValid)
            {
                setupValid = ValidateCoordinator();
            }

            if (!setupValid)
            {
                reason = "coordinator_missing";
                return false;
            }

            if (!coordinator.IsAuthoritative)
            {
                reason = "server_authority_required";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateCoordinator()
        {
            if (coordinator == null)
            {
                Debug.LogError("PHS_EVENT_SCHEDULER_SETUP_FAILED reason=coordinator_missing", this);
                return false;
            }

            return true;
        }

        private static bool ValidateSchedule(
            PHSNetworkEventChannel candidateChannel,
            WeightedEventScheduleEntry[] entries,
            float candidateIntervalMinSeconds,
            float candidateIntervalMaxSeconds,
            int candidateMaximumActiveEvents,
            out string reason)
        {
            if (!Enum.IsDefined(typeof(PHSNetworkEventChannel), candidateChannel))
            {
                reason = $"event_channel_invalid:{(byte)candidateChannel}";
                return false;
            }

            if (entries == null || entries.Length == 0)
            {
                reason = "event_entries_empty";
                return false;
            }

            var eventIds = new HashSet<EventId>();
            foreach (var entry in entries)
            {
                if (!Enum.IsDefined(typeof(EventId), entry.eventId))
                {
                    reason = $"event_invalid:{entry.eventId}";
                    return false;
                }

                if (!IsEventInChannel(entry.eventId, candidateChannel))
                {
                    reason = $"event_channel_mismatch:channel={candidateChannel}:event={entry.eventId}";
                    return false;
                }

                if (!eventIds.Add(entry.eventId))
                {
                    reason = $"event_duplicate:{entry.eventId}";
                    return false;
                }

                if (entry.weight <= 0f || float.IsNaN(entry.weight) || float.IsInfinity(entry.weight))
                {
                    reason = $"event_weight_invalid:{entry.eventId}";
                    return false;
                }
            }

            if (candidateIntervalMinSeconds <= 0f
                || float.IsNaN(candidateIntervalMinSeconds)
                || float.IsInfinity(candidateIntervalMinSeconds))
            {
                reason = "interval_min_invalid";
                return false;
            }

            if (candidateIntervalMaxSeconds < candidateIntervalMinSeconds
                || float.IsNaN(candidateIntervalMaxSeconds)
                || float.IsInfinity(candidateIntervalMaxSeconds))
            {
                reason = "interval_max_invalid";
                return false;
            }

            if (candidateMaximumActiveEvents < 0)
            {
                reason = "maximum_active_events_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsEventInChannel(
            EventId eventId,
            PHSNetworkEventChannel candidateChannel)
        {
            var value = (int)eventId;
            return candidateChannel switch
            {
                PHSNetworkEventChannel.LegacyMixed =>
                    value >= (int)SM.EventType.Internal && value < (int)SM.EventType.Environment,
                PHSNetworkEventChannel.ExternalThreat =>
                    value >= (int)SM.EventType.External && value < (int)SM.EventType.Environment,
                PHSNetworkEventChannel.LegacyInternal =>
                    value >= (int)SM.EventType.Internal && value < (int)SM.EventType.External,
                _ => false
            };
        }
    }
}
