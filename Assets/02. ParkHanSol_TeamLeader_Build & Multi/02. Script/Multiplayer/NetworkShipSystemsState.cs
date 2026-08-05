using System;
using LastJumpCrew.SeoBoGyeong;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkShipSystemsState :
        NetworkBehaviour,
        IShipSystemsState,
        IShipSystemsCommands,
        IShipDockRepairCommands,
        IShipDockUpgradeCommands
    {
        [Header("Ship Defaults")]
        [SerializeField, Min(1)] private int maximumShipHp = 100;
        [SerializeField, Min(1)] private int defaultModuleMaximumHp = 100;

        private readonly NetworkVariable<int> synchronizedCurrentShipHp = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedMaximumShipHp = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedPowerEnabled = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedGravityEnabled = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedBatteryInstalled = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString64Bytes> synchronizedLastDamageCause = new(
            new FixedString64Bytes("none"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> synchronizedRevision = new(
            0U,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<NetworkShipModuleSnapshot> synchronizedModules = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool isRegisteredWithGameCore;
        private IShipStatus previousShipStatus;
        private float nextRegistrationAttemptTime;

        public static NetworkShipSystemsState Instance { get; private set; }

        public int CurrentShipHp => synchronizedCurrentShipHp.Value;
        public int MaximumShipHp => synchronizedMaximumShipHp.Value;
        public bool IsShipAlive => synchronizedCurrentShipHp.Value > 0;
        public bool IsPowerEnabled => synchronizedPowerEnabled.Value;
        public bool IsGravityEnabled => synchronizedGravityEnabled.Value;
        public bool IsBatteryInstalled => synchronizedBatteryInstalled.Value;
        public string LastDamageCause => synchronizedLastDamageCause.Value.ToString();
        public uint Revision => synchronizedRevision.Value;
        public int ModuleCount => synchronizedModules.Count;

        public event Action StateChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_SHIP_SYSTEMS_SETUP_FAILED reason=duplicate_server_replica current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            SubscribeToNetworkState();

            if (IsServer)
            {
                InitializeServerState();
            }

            TryRegisterWithGameCore();
            PublishStateChanged("network_spawn");
            Debug.Log(
                $"PHS_SHIP_SYSTEMS_READY server={IsServer} revision={Revision} modules={ModuleCount}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                UnsubscribeFromNetworkState();
                TryReleaseFromGameCore();
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || isRegisteredWithGameCore || Time.unscaledTime < nextRegistrationAttemptTime)
            {
                return;
            }

            nextRegistrationAttemptTime = Time.unscaledTime + 0.5f;
            TryRegisterWithGameCore();
        }

        public bool TryGetModuleSnapshot(
            NetworkShipModuleId moduleId,
            out NetworkShipModuleSnapshot snapshot)
        {
            for (var index = 0; index < synchronizedModules.Count; index++)
            {
                if (synchronizedModules[index].ModuleId == moduleId)
                {
                    snapshot = synchronizedModules[index];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        public NetworkShipModuleSnapshot GetModuleSnapshotAt(int index)
        {
            if (index < 0 || index >= synchronizedModules.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return synchronizedModules[index];
        }

        public bool TryApplyShipDamage(int amount, out string reason)
        {
            return TryApplyShipDamage(amount, "unspecified", out reason);
        }

        public bool TryApplyShipDamage(int amount, string cause, out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_damage_required";
                return false;
            }

            if (!IsShipAlive)
            {
                reason = "ship_destroyed";
                return false;
            }

            var previousHp = synchronizedCurrentShipHp.Value;
            synchronizedCurrentShipHp.Value = Mathf.Max(0, previousHp - amount);
            var normalizedCause = NormalizeDamageCause(cause);
            synchronizedLastDamageCause.Value = normalizedCause;
            if (synchronizedCurrentShipHp.Value == 0)
            {
                synchronizedPowerEnabled.Value = false;
                synchronizedGravityEnabled.Value = false;
            }

            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_DAMAGE_APPLIED amount={amount} cause={normalizedCause} hp={CurrentShipHp}/{MaximumShipHp} revision={Revision}",
                this);

            if (previousHp > 0 && synchronizedCurrentShipHp.Value == 0)
            {
                ReportGameOver(GameOverReason.ShipDestroyed);
            }

            return true;
        }

        public bool TryDestroyShip(
            GameOverReason gameOverReason,
            string cause,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (gameOverReason == GameOverReason.None)
            {
                reason = "game_over_reason_required";
                return false;
            }

            if (!IsShipAlive)
            {
                reason = "ship_destroyed";
                return false;
            }

            var previousHp = synchronizedCurrentShipHp.Value;
            var normalizedCause = NormalizeDamageCause(cause);
            synchronizedCurrentShipHp.Value = 0;
            synchronizedPowerEnabled.Value = false;
            synchronizedGravityEnabled.Value = false;
            synchronizedLastDamageCause.Value = normalizedCause;
            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_DESTROYED cause={normalizedCause} previousHp={previousHp} gameOverReason={gameOverReason} revision={Revision}",
                this);
            ReportGameOver(gameOverReason);
            return true;
        }

        public bool TryRestoreShipDurabilityAtDock(int amount, out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_repair_required";
                return false;
            }

            if (!IsShipAlive)
            {
                reason = "ship_destroyed";
                return false;
            }

            if (synchronizedCurrentShipHp.Value >= synchronizedMaximumShipHp.Value)
            {
                reason = "ship_durability_full";
                return false;
            }

            var previousHp = synchronizedCurrentShipHp.Value;
            synchronizedCurrentShipHp.Value = Mathf.Min(
                synchronizedMaximumShipHp.Value,
                previousHp + amount);
            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_DOCK_REPAIR_APPLIED amount={synchronizedCurrentShipHp.Value - previousHp} hp={CurrentShipHp}/{MaximumShipHp} revision={Revision}",
                this);
            return true;
        }

        public bool TryIncreaseMaximumShipHpAtDock(int amount, out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_maximum_increase_required";
                return false;
            }

            if (!IsShipAlive)
            {
                reason = "ship_destroyed";
                return false;
            }

            synchronizedMaximumShipHp.Value = checked(synchronizedMaximumShipHp.Value + amount);
            synchronizedCurrentShipHp.Value = Mathf.Min(
                synchronizedMaximumShipHp.Value,
                synchronizedCurrentShipHp.Value + amount);
            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_MAXIMUM_HP_INCREASED amount={amount} hp={CurrentShipHp}/{MaximumShipHp} revision={Revision}",
                this);
            return true;
        }

        public bool TryApplyModuleDamage(
            NetworkShipModuleId moduleId,
            int amount,
            bool causeFault,
            out string reason)
        {
            return TryApplyModuleDamage(
                moduleId,
                amount,
                causeFault,
                "unspecified",
                out reason);
        }

        public bool TryApplyModuleDamage(
            NetworkShipModuleId moduleId,
            int amount,
            bool causeFault,
            string cause,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_damage_required";
                return false;
            }

            if (!TryFindModuleIndex(moduleId, out var index))
            {
                reason = "module_missing";
                return false;
            }

            var current = synchronizedModules[index];
            var nextHp = Mathf.Max(0, current.CurrentHp - amount);
            var nextFault = current.IsFaulted || causeFault || nextHp == 0;
            var normalizedCause = NormalizeDamageCause(cause);
            synchronizedModules[index] = CreateModuleSnapshot(
                current.ModuleId,
                nextHp,
                current.MaximumHp,
                nextFault,
                normalizedCause,
                current.Revision + 1U);
            synchronizedLastDamageCause.Value = normalizedCause;
            ApplyModuleFailureToShipState(moduleId, nextHp, nextFault);
            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_MODULE_DAMAGED module={moduleId} amount={amount} cause={normalizedCause} hp={nextHp}/{current.MaximumHp} fault={nextFault} revision={Revision}",
                this);
            return true;
        }

        public bool TryRepairModule(
            NetworkShipModuleId moduleId,
            int amount,
            out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (amount <= 0)
            {
                reason = "positive_repair_required";
                return false;
            }

            if (!TryFindModuleIndex(moduleId, out var index))
            {
                reason = "module_missing";
                return false;
            }

            var current = synchronizedModules[index];
            if (current.CurrentHp >= current.MaximumHp && !current.IsFaulted)
            {
                reason = "module_already_operational";
                return false;
            }

            var nextHp = Mathf.Min(current.MaximumHp, current.CurrentHp + amount);
            var nextFault = current.IsFaulted && nextHp < current.MaximumHp;
            synchronizedModules[index] = CreateModuleSnapshot(
                current.ModuleId,
                nextHp,
                current.MaximumHp,
                nextFault,
                current.LastDamageCause,
                current.Revision + 1U);
            IncrementRevision();
            reason = null;
            Debug.Log(
                $"PHS_SHIP_MODULE_REPAIRED module={moduleId} amount={amount} hp={nextHp}/{current.MaximumHp} fault={nextFault} revision={Revision}",
                this);
            return true;
        }

        public bool TryPowerOff(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (!synchronizedPowerEnabled.Value && !synchronizedGravityEnabled.Value)
            {
                reason = "power_already_off";
                return false;
            }

            synchronizedPowerEnabled.Value = false;
            synchronizedGravityEnabled.Value = false;
            synchronizedBatteryInstalled.Value = false;
            var powerOffCause = new FixedString64Bytes("power_off");
            synchronizedLastDamageCause.Value = powerOffCause;
            SetModuleFault(NetworkShipModuleId.Power, true, powerOffCause);
            IncrementRevision();
            reason = null;
            Debug.Log($"PHS_SHIP_POWER_OFF revision={Revision}", this);
            return true;
        }

        public bool TryRestorePowerWithBattery(out string reason)
        {
            if (!CanRestorePowerWithBattery(out reason))
            {
                return false;
            }

            synchronizedBatteryInstalled.Value = true;
            synchronizedPowerEnabled.Value = true;
            synchronizedGravityEnabled.Value = true;
            SetModuleFault(NetworkShipModuleId.Power, false, null);
            SetModuleFault(NetworkShipModuleId.Gravity, false, null);
            IncrementRevision();
            reason = null;
            Debug.Log($"PHS_SHIP_POWER_RESTORED source=battery revision={Revision}", this);
            return true;
        }

        public bool CanRestorePowerWithBattery(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (!IsShipAlive)
            {
                reason = "ship_destroyed";
                return false;
            }

            if (synchronizedPowerEnabled.Value && synchronizedGravityEnabled.Value)
            {
                reason = "power_already_restored";
                return false;
            }

            if (!HasModuleIntegrity(NetworkShipModuleId.Power))
            {
                reason = "power_module_destroyed";
                return false;
            }

            if (!HasModuleIntegrity(NetworkShipModuleId.Gravity))
            {
                reason = "gravity_module_destroyed";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TrySetGravityEnabled(bool isEnabled, out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (isEnabled)
            {
                if (!synchronizedPowerEnabled.Value)
                {
                    reason = "power_required";
                    return false;
                }

                if (!CanOperateModule(NetworkShipModuleId.Gravity))
                {
                    reason = "gravity_module_unavailable";
                    return false;
                }
            }

            if (synchronizedGravityEnabled.Value == isEnabled)
            {
                reason = "gravity_state_unchanged";
                return false;
            }

            synchronizedGravityEnabled.Value = isEnabled;
            IncrementRevision();
            reason = null;
            Debug.Log($"PHS_SHIP_GRAVITY_STATE enabled={isEnabled} revision={Revision}", this);
            return true;
        }

        public bool TryRestoreGravityAfterRepair(out string reason)
        {
            if (!RequireServer(out reason))
            {
                return false;
            }

            if (!synchronizedPowerEnabled.Value)
            {
                reason = "power_required";
                return false;
            }

            if (!CanOperateModule(NetworkShipModuleId.Gravity))
            {
                reason = "gravity_module_unavailable";
                return false;
            }

            if (synchronizedGravityEnabled.Value)
            {
                reason = null;
                return true;
            }

            synchronizedGravityEnabled.Value = true;
            IncrementRevision();
            reason = null;
            Debug.Log($"PHS_SHIP_GRAVITY_RESTORED source=repair revision={Revision}", this);
            return true;
        }

        private void InitializeServerState()
        {
            maximumShipHp = Mathf.Max(1, maximumShipHp);
            defaultModuleMaximumHp = Mathf.Max(1, defaultModuleMaximumHp);
            synchronizedMaximumShipHp.Value = maximumShipHp;
            synchronizedCurrentShipHp.Value = maximumShipHp;
            synchronizedPowerEnabled.Value = true;
            synchronizedGravityEnabled.Value = true;
            synchronizedBatteryInstalled.Value = false;
            synchronizedLastDamageCause.Value = new FixedString64Bytes("none");
            synchronizedModules.Clear();
            AddInitialModule(NetworkShipModuleId.Power);
            AddInitialModule(NetworkShipModuleId.Gravity);
            AddInitialModule(NetworkShipModuleId.LifeSupport);
            AddInitialModule(NetworkShipModuleId.Engine);
            synchronizedRevision.Value = 1U;
        }

        private void AddInitialModule(NetworkShipModuleId moduleId)
        {
            synchronizedModules.Add(CreateModuleSnapshot(
                moduleId,
                defaultModuleMaximumHp,
                defaultModuleMaximumHp,
                false,
                new FixedString64Bytes("none"),
                1U));
        }

        private static NetworkShipModuleSnapshot CreateModuleSnapshot(
            NetworkShipModuleId moduleId,
            int currentHp,
            int maximumHp,
            bool isFaulted,
            FixedString64Bytes lastDamageCause,
            uint revision)
        {
            var condition = currentHp <= 0
                ? NetworkShipModuleRepairCondition.Destroyed
                : isFaulted
                    ? NetworkShipModuleRepairCondition.Faulted
                    : currentHp < maximumHp
                        ? NetworkShipModuleRepairCondition.Damaged
                        : NetworkShipModuleRepairCondition.Operational;
            return new NetworkShipModuleSnapshot(
                moduleId,
                currentHp,
                maximumHp,
                isFaulted,
                condition,
                lastDamageCause,
                revision);
        }

        private void ApplyModuleFailureToShipState(
            NetworkShipModuleId moduleId,
            int currentHp,
            bool isFaulted)
        {
            if (currentHp > 0 && !isFaulted)
            {
                return;
            }

            if (moduleId == NetworkShipModuleId.Power)
            {
                synchronizedPowerEnabled.Value = false;
                synchronizedGravityEnabled.Value = false;
                synchronizedBatteryInstalled.Value = false;
                return;
            }

            if (moduleId == NetworkShipModuleId.Gravity)
            {
                synchronizedGravityEnabled.Value = false;
            }
        }

        private void SetModuleFault(
            NetworkShipModuleId moduleId,
            bool isFaulted,
            FixedString64Bytes? damageCause)
        {
            if (!TryFindModuleIndex(moduleId, out var index))
            {
                return;
            }

            var current = synchronizedModules[index];
            if (current.IsFaulted == isFaulted)
            {
                return;
            }

            synchronizedModules[index] = CreateModuleSnapshot(
                current.ModuleId,
                current.CurrentHp,
                current.MaximumHp,
                isFaulted,
                damageCause ?? current.LastDamageCause,
                current.Revision + 1U);
        }

        private static FixedString64Bytes NormalizeDamageCause(string cause)
        {
            var normalized = string.IsNullOrWhiteSpace(cause)
                ? "unspecified"
                : cause.Trim();
            if (normalized.Length > 14)
            {
                normalized = normalized.Substring(0, 14);
            }

            return new FixedString64Bytes(normalized);
        }

        private bool CanOperateModule(NetworkShipModuleId moduleId)
        {
            return TryGetModuleSnapshot(moduleId, out var snapshot)
                && snapshot.IsOperational;
        }

        private bool HasModuleIntegrity(NetworkShipModuleId moduleId)
        {
            return TryGetModuleSnapshot(moduleId, out var snapshot)
                && snapshot.CurrentHp > 0;
        }

        private bool TryFindModuleIndex(NetworkShipModuleId moduleId, out int index)
        {
            for (var candidate = 0; candidate < synchronizedModules.Count; candidate++)
            {
                if (synchronizedModules[candidate].ModuleId == moduleId)
                {
                    index = candidate;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private bool RequireServer(out string reason)
        {
            if (IsSpawned && IsServer && OwnerClientId == NetworkManager.ServerClientId)
            {
                reason = null;
                return true;
            }

            reason = "server_required";
            return false;
        }

        private void IncrementRevision()
        {
            synchronizedRevision.Value++;
            if (synchronizedRevision.Value == 0U)
            {
                synchronizedRevision.Value = 1U;
            }
        }

        private void ReportGameOver(GameOverReason gameOverReason)
        {
            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null
                || !gameCore.Services.TryGet<IGameCommands>(out var commands))
            {
                Debug.LogError("PHS_SHIP_DESTROYED_REPORT_FAILED reason=game_commands_missing", this);
                return;
            }

            commands.ReportGameOver(gameOverReason);
            Debug.Log($"PHS_SHIP_DESTROYED_REPORTED reason={gameOverReason}", this);
        }

        private void TryRegisterWithGameCore()
        {
            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                return;
            }

            gameCore.Services.TryGet(out previousShipStatus);
            gameCore.Services.Register<IShipStatus>(this);
            isRegisteredWithGameCore = true;
            Debug.Log(
                $"PHS_SHIP_STATUS_CONNECTED provider={GetType().Name} server={IsServer}",
                this);
        }

        private void TryReleaseFromGameCore()
        {
            if (!isRegisteredWithGameCore)
            {
                return;
            }

            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                Debug.LogError("PHS_SHIP_STATUS_DISCONNECT_FAILED reason=service_registry_missing", this);
            }
            else if (!gameCore.Services.TryGet<IShipStatus>(out var currentShipStatus)
                     || !ReferenceEquals(currentShipStatus, this))
            {
                Debug.LogError("PHS_SHIP_STATUS_DISCONNECT_FAILED reason=provider_replaced", this);
            }
            else if (previousShipStatus == null)
            {
                Debug.LogError("PHS_SHIP_STATUS_DISCONNECT_FAILED reason=previous_provider_missing", this);
            }
            else
            {
                gameCore.Services.Register(previousShipStatus);
            }

            isRegisteredWithGameCore = false;
            previousShipStatus = null;
            Debug.LogWarning(
                $"PHS_SHIP_STATUS_DISCONNECTED provider={GetType().Name} waiting_for_reconnect=true",
                this);
        }

        private void SubscribeToNetworkState()
        {
            synchronizedCurrentShipHp.OnValueChanged += HandleIntChanged;
            synchronizedMaximumShipHp.OnValueChanged += HandleIntChanged;
            synchronizedPowerEnabled.OnValueChanged += HandleBoolChanged;
            synchronizedGravityEnabled.OnValueChanged += HandleBoolChanged;
            synchronizedBatteryInstalled.OnValueChanged += HandleBoolChanged;
            synchronizedLastDamageCause.OnValueChanged += HandleDamageCauseChanged;
            synchronizedRevision.OnValueChanged += HandleRevisionChanged;
            synchronizedModules.OnListChanged += HandleModulesChanged;
        }

        private void UnsubscribeFromNetworkState()
        {
            synchronizedCurrentShipHp.OnValueChanged -= HandleIntChanged;
            synchronizedMaximumShipHp.OnValueChanged -= HandleIntChanged;
            synchronizedPowerEnabled.OnValueChanged -= HandleBoolChanged;
            synchronizedGravityEnabled.OnValueChanged -= HandleBoolChanged;
            synchronizedBatteryInstalled.OnValueChanged -= HandleBoolChanged;
            synchronizedLastDamageCause.OnValueChanged -= HandleDamageCauseChanged;
            synchronizedRevision.OnValueChanged -= HandleRevisionChanged;
            synchronizedModules.OnListChanged -= HandleModulesChanged;
        }

        private void HandleIntChanged(int previousValue, int currentValue)
        {
            PublishStateChanged("int");
        }

        private void HandleBoolChanged(bool previousValue, bool currentValue)
        {
            PublishStateChanged("bool");
        }

        private void HandleRevisionChanged(uint previousValue, uint currentValue)
        {
            PublishStateChanged("revision");
        }

        private void HandleDamageCauseChanged(
            FixedString64Bytes previousValue,
            FixedString64Bytes currentValue)
        {
            PublishStateChanged("damage_cause");
        }

        private void HandleModulesChanged(
            NetworkListEvent<NetworkShipModuleSnapshot> changeEvent)
        {
            PublishStateChanged($"module_{changeEvent.Type}");
        }

        private void PublishStateChanged(string reason)
        {
            StateChanged?.Invoke();
            Debug.Log(
                $"PHS_SHIP_SYSTEMS_SNAPSHOT reason={reason} hp={CurrentShipHp}/{MaximumShipHp} power={IsPowerEnabled} gravity={IsGravityEnabled} battery={IsBatteryInstalled} damageCause={LastDamageCause} revision={Revision}",
                this);
        }
    }
}
