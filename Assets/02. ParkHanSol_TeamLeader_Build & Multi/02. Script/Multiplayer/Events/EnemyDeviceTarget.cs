using LastJumpCrew.Common;
using SM;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class EnemyDeviceTarget : NetworkBehaviour, IDevice, IDamageable
    {
        [SerializeField, Min(1)] private int maximumHealth = 10;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private EventId destroyedEvent = EventId.PowerOff;

        private Renderer[] renderers;
        private bool[] initialRendererStates;
        private int localCurrentHealth;
        private bool isRegistered;
        private NetworkEventCoordinator boundEventCoordinator;
        private ulong activeBreakdownEventInstanceId;
        private readonly NetworkVariable<int> synchronizedHealth = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public Transform Transform => transform;
        public bool IsAlive => IsSpawned
            ? synchronizedHealth.Value > 0
            : localCurrentHealth > 0;
        public bool IsBroken => !IsAlive;

        private void Awake()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            initialRendererStates = new bool[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
            {
                initialRendererStates[index] = renderers[index].enabled;
            }
        }

        private void OnEnable()
        {
            localCurrentHealth = maximumHealth;
            RestoreVisuals();
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsListening)
            {
                RegisterTarget();
            }
        }

        private void OnDisable()
        {
            UnbindBreakdownEvent();
            Unregister();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            synchronizedHealth.OnValueChanged += HandleHealthChanged;
            if (IsServer)
            {
                synchronizedHealth.Value = maximumHealth;
                localCurrentHealth = maximumHealth;
                RegisterTarget();
            }
            else
            {
                Unregister();
            }

            RestoreVisuals();
        }

        public override void OnNetworkDespawn()
        {
            synchronizedHealth.OnValueChanged -= HandleHealthChanged;
            UnbindBreakdownEvent();
            Unregister();
            base.OnNetworkDespawn();
        }

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (amount <= 0 || !IsAlive)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                return;
            }

            var networked = IsSpawned
                && NetworkManager != null
                && NetworkManager.IsListening;
            var remainingHealth = Mathf.Max(
                0,
                (networked ? synchronizedHealth.Value : localCurrentHealth) - amount);
            localCurrentHealth = remainingHealth;
            if (networked)
            {
                synchronizedHealth.Value = remainingHealth;
            }
            else
            {
                RestoreVisuals();
            }

            if (remainingHealth > 0)
            {
                return;
            }

            Unregister();
            TryStartDestroyedEvent();
            Debug.Log($"TEAM_ENEMY_DEVICE_BROKEN target={name} attacker={attacker?.name ?? "unknown"}", this);
        }

        public bool TryRestoreFromBatteryServer()
        {
            if (!IsServer || !IsBroken || destroyedEvent != EventId.PowerOff)
            {
                return false;
            }

            localCurrentHealth = maximumHealth;
            synchronizedHealth.Value = maximumHealth;
            RegisterTarget();
            return true;
        }

        private void TryStartDestroyedEvent()
        {
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsAuthoritative)
            {
                Debug.LogError(
                    $"TEAM_ENEMY_DEVICE_EVENT_FAILED reason=coordinator_server_missing target={name} event={destroyedEvent}",
                    this);
                return;
            }

            if (!coordinator.TrySpawnEventServer(destroyedEvent, out var instanceId))
            {
                if (TryFindActiveEventInstance(
                        coordinator,
                        destroyedEvent,
                        out instanceId))
                {
                    BindBreakdownEvent(coordinator, instanceId);
                    Debug.Log(
                        $"TEAM_ENEMY_DEVICE_EVENT_JOINED target={name} event={destroyedEvent} instance={instanceId}",
                        this);
                    return;
                }

                Debug.LogWarning(
                    $"TEAM_ENEMY_DEVICE_EVENT_REJECTED target={name} event={destroyedEvent}",
                    this);
                return;
            }

            BindBreakdownEvent(coordinator, instanceId);
            Debug.Log(
                $"TEAM_ENEMY_DEVICE_EVENT_STARTED target={name} event={destroyedEvent} instance={instanceId}",
                this);
        }

        private static bool TryFindActiveEventInstance(
            NetworkEventCoordinator coordinator,
            EventId eventId,
            out ulong instanceId)
        {
            for (var index = 0; index < coordinator.SnapshotCount; index++)
            {
                var snapshot = coordinator.GetLifecycleSnapshotAt(index);
                if (snapshot.EventId == eventId && !snapshot.IsTerminal)
                {
                    instanceId = snapshot.InstanceId;
                    return instanceId != 0UL;
                }
            }

            instanceId = 0UL;
            return false;
        }

        private void BindBreakdownEvent(
            NetworkEventCoordinator coordinator,
            ulong eventInstanceId)
        {
            UnbindBreakdownEvent();
            boundEventCoordinator = coordinator;
            activeBreakdownEventInstanceId = eventInstanceId;
            boundEventCoordinator.ServerEventFinished += HandleServerEventFinished;
        }

        private void UnbindBreakdownEvent()
        {
            if (boundEventCoordinator != null)
            {
                boundEventCoordinator.ServerEventFinished -= HandleServerEventFinished;
            }

            boundEventCoordinator = null;
            activeBreakdownEventInstanceId = 0UL;
        }

        private void HandleServerEventFinished(
            ulong eventInstanceId,
            EventId eventId,
            bool succeeded)
        {
            if (eventInstanceId != activeBreakdownEventInstanceId
                || eventId != destroyedEvent)
            {
                return;
            }

            UnbindBreakdownEvent();
            if (!succeeded)
            {
                Debug.LogWarning(
                    $"TEAM_ENEMY_DEVICE_RESTORE_REJECTED target={name} event={eventId} instance={eventInstanceId} reason=event_failed",
                    this);
                return;
            }

            if (!IsServer)
            {
                Debug.LogError(
                    $"TEAM_ENEMY_DEVICE_RESTORE_FAILED target={name} event={eventId} instance={eventInstanceId} reason=server_required",
                    this);
                return;
            }

            localCurrentHealth = maximumHealth;
            synchronizedHealth.Value = maximumHealth;
            RegisterTarget();
            Debug.Log(
                $"TEAM_ENEMY_DEVICE_RESTORED target={name} event={eventId} instance={eventInstanceId}",
                this);
        }

        private void Unregister()
        {
            if (!isRegistered)
            {
                return;
            }

            DeviceRegistry.Peek()?.Unregister(this);
            isRegistered = false;
        }

        private void RegisterTarget()
        {
            if (isRegistered || !isActiveAndEnabled || IsBroken)
            {
                return;
            }

            DeviceRegistry.Instance.Register(this);
            isRegistered = true;
        }

        private void HandleHealthChanged(int previousHealth, int currentHealth)
        {
            localCurrentHealth = currentHealth;
        }

        private void RestoreVisuals()
        {
            if (renderers == null)
            {
                return;
            }

            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = initialRendererStates[index];
                }
            }
        }
    }
}
