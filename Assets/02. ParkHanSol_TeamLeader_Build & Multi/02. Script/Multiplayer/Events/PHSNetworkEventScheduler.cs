using System;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSNetworkEventScheduler : MonoBehaviour
    {
        [SerializeField] private NetworkEventCoordinator coordinator;
        [SerializeField] private EventId[] eventPool =
        {
            EventId.Fire,
            EventId.EnemySpawn,
            EventId.OxygenLeak,
            EventId.EnemyScout,
            EventId.MeteorAttack,
            EventId.EmpAttack
        };
        [SerializeField, Min(1f)] private float spawnIntervalSeconds = 30f;
        [SerializeField, Min(1)] private int maximumActiveEvents = 2;

        private float nextSpawnTime;
        private bool isRunning;
        private bool setupValid;

        private void Awake()
        {
            setupValid = ValidateSetup();
            enabled = setupValid;
        }

        private void Update()
        {
            if (!isRunning || !coordinator.IsAuthoritative || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + spawnIntervalSeconds;
            if (CountActiveEvents() >= maximumActiveEvents)
            {
                return;
            }

            var startIndex = UnityEngine.Random.Range(0, eventPool.Length);
            for (var offset = 0; offset < eventPool.Length; offset++)
            {
                var eventId = eventPool[(startIndex + offset) % eventPool.Length];
                if (coordinator.IsEventActive(eventId))
                {
                    continue;
                }

                if (coordinator.TrySpawnEventServer(eventId, out var instanceId))
                {
                    Debug.Log(
                        $"PHS_EVENT_SCHEDULER_SPAWNED event={eventId} instance={instanceId}",
                        this);
                    return;
                }
            }

            Debug.LogWarning("PHS_EVENT_SCHEDULER_SPAWN_SKIPPED reason=no_available_event", this);
        }

        public void StartScheduler()
        {
            if (!setupValid || !coordinator.IsAuthoritative)
            {
                Debug.LogError("PHS_EVENT_SCHEDULER_START_FAILED reason=setup_or_authority", this);
                return;
            }

            isRunning = true;
            nextSpawnTime = Time.time + spawnIntervalSeconds;
        }

        public void StopScheduler()
        {
            isRunning = false;
        }

        public void ResetScheduler()
        {
            isRunning = false;
            nextSpawnTime = 0f;
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

        private bool ValidateSetup()
        {
            if (coordinator == null)
            {
                Debug.LogError("PHS_EVENT_SCHEDULER_SETUP_FAILED reason=coordinator_missing", this);
                return false;
            }

            if (eventPool == null || eventPool.Length == 0)
            {
                Debug.LogError("PHS_EVENT_SCHEDULER_SETUP_FAILED reason=event_pool_empty", this);
                return false;
            }

            foreach (var eventId in eventPool)
            {
                if (!Enum.IsDefined(typeof(EventId), eventId))
                {
                    Debug.LogError($"PHS_EVENT_SCHEDULER_SETUP_FAILED reason=event_invalid event={eventId}", this);
                    return false;
                }
            }

            return true;
        }
    }
}
