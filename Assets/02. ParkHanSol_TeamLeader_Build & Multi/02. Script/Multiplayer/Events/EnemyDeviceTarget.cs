using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class EnemyDeviceTarget : MonoBehaviour, IDevice, IDamageable
    {
        [SerializeField, Min(1)] private int maximumHealth = 10;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private PHSShipAccidentId destructionAccident;
        [SerializeField] private string requestedAnchorId;

        private Renderer[] renderers;
        private bool[] initialRendererStates;
        private int currentHealth;
        private bool isRegistered;

        public Transform Transform => transform;
        public bool IsAlive => currentHealth > 0;

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
            currentHealth = maximumHealth;
            SetVisualsAlive(true);
            DeviceRegistry.Instance.Register(this);
            isRegistered = true;
        }

        private void OnDisable()
        {
            Unregister();
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

            currentHealth = Mathf.Max(0, currentHealth - amount);
            if (currentHealth > 0)
            {
                return;
            }

            Unregister();
            SetVisualsAlive(false);
            TriggerDestructionAccident();
            Debug.Log($"PHS_ENEMY_DEVICE_DESTROYED target={name} attacker={attacker?.name ?? "unknown"}", this);
        }

        private void TriggerDestructionAccident()
        {
            if (destructionAccident == PHSShipAccidentId.None)
            {
                return;
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
                Debug.LogWarning(
                    $"PHS_ENEMY_DEVICE_ACCIDENT_FAILED target={name} accident={destructionAccident} reason={reason}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_ENEMY_DEVICE_ACCIDENT_STARTED target={name} accident={destructionAccident} instance={instanceId}",
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
