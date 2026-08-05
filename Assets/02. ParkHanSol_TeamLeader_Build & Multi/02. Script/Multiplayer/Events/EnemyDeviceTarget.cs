using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
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
        [SerializeField] private PHSShipAccidentId destructionAccident;
        [SerializeField] private string requestedAnchorId;

        private Renderer[] renderers;
        private bool[] initialRendererStates;
        private int localCurrentHealth;
        private bool isRegistered;
        private PHSNetworkShipAccidentCoordinator boundAccidentCoordinator;
        private uint activeBreakdownAccidentInstanceId;
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
            SetVisualsAlive(true);
            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsListening)
            {
                RegisterTarget();
            }
        }

        private void OnDisable()
        {
            UnbindAccidentCoordinator();
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

            SetVisualsAlive(synchronizedHealth.Value > 0);
        }

        public override void OnNetworkDespawn()
        {
            synchronizedHealth.OnValueChanged -= HandleHealthChanged;
            UnbindAccidentCoordinator();
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
                SetVisualsAlive(remainingHealth > 0);
            }

            if (remainingHealth > 0)
            {
                return;
            }

            Unregister();
            if (!TryTriggerBreakdownAccident())
            {
                return;
            }

            Debug.Log($"PHS_ENEMY_DEVICE_BROKEN target={name} attacker={attacker?.name ?? "unknown"}", this);
        }

        private bool TryTriggerBreakdownAccident()
        {
            if (destructionAccident == PHSShipAccidentId.None)
            {
                Debug.LogError(
                    $"PHS_ENEMY_DEVICE_BREAKDOWN_FAILED target={name} reason=accident_not_configured",
                    this);
                return false;
            }

            var coordinator = PHSNetworkShipAccidentCoordinator.Instance;
            var reason = coordinator == null ? "coordinator_missing" : null;
            uint instanceId = 0;
            if (coordinator == null
                || !coordinator.TrySpawnAccidentServer(
                    destructionAccident,
                    requestedAnchorId,
                    out instanceId,
                    out reason))
            {
                Debug.LogError(
                    $"PHS_ENEMY_DEVICE_ACCIDENT_FAILED target={name} accident={destructionAccident} reason={reason}",
                    this);
                return false;
            }

            BindAccidentCoordinator(coordinator, instanceId);
            Debug.Log(
                $"PHS_ENEMY_DEVICE_ACCIDENT_STARTED target={name} accident={destructionAccident} instance={instanceId}",
                this);
            return true;
        }

        private void BindAccidentCoordinator(
            PHSNetworkShipAccidentCoordinator coordinator,
            uint accidentInstanceId)
        {
            UnbindAccidentCoordinator();
            boundAccidentCoordinator = coordinator;
            activeBreakdownAccidentInstanceId = accidentInstanceId;
            boundAccidentCoordinator.ServerAccidentFinished += HandleServerAccidentFinished;
        }

        private void UnbindAccidentCoordinator()
        {
            if (boundAccidentCoordinator != null)
            {
                boundAccidentCoordinator.ServerAccidentFinished -= HandleServerAccidentFinished;
            }

            boundAccidentCoordinator = null;
            activeBreakdownAccidentInstanceId = 0U;
        }

        private void HandleServerAccidentFinished(
            uint accidentInstanceId,
            PHSShipAccidentId accidentId,
            bool resolved)
        {
            if (accidentInstanceId != activeBreakdownAccidentInstanceId)
            {
                return;
            }

            UnbindAccidentCoordinator();
            if (!resolved)
            {
                Debug.LogWarning(
                    $"PHS_ENEMY_DEVICE_REPAIR_FAILED target={name} accident={accidentId} instance={accidentInstanceId}",
                    this);
                return;
            }

            localCurrentHealth = maximumHealth;
            if (IsSpawned && IsServer)
            {
                synchronizedHealth.Value = maximumHealth;
            }
            else
            {
                SetVisualsAlive(true);
            }

            if (isActiveAndEnabled)
            {
                RegisterTarget();
            }

            Debug.Log(
                $"PHS_ENEMY_DEVICE_REPAIRED target={name} accident={accidentId} instance={accidentInstanceId}",
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
            SetVisualsAlive(currentHealth > 0);
        }

        private void SetVisualsAlive(bool alive)
        {
            if (renderers == null)
            {
                return;
            }

            for (var index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = alive && initialRendererStates[index];
                }
            }
        }
    }
}
