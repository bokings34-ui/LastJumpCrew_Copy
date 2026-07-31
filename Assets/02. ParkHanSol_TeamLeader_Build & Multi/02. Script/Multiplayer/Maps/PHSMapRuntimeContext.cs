using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSMapRuntimeContext : MonoBehaviour, IMapRuntimeContext
    {
        [Header("Inspector References")]
        [SerializeField] private PHSMapCatalogSO mapCatalog;
        [SerializeField] private PHSMapProfileSO warpMaintenanceProfile;
        [SerializeField] private PHSMapProfileSO shopPortalProfile;
        [SerializeField] private Transform environmentRoot;
        [SerializeField] private WarpTransitionPresenter warpTransitionPresenter;
        [SerializeField] private PHSNetworkEventScheduler externalThreatScheduler;
        [SerializeField] private PHSNetworkShipAccidentCoordinator internalAccidentCoordinator;
        [SerializeField] private PHSMapIncidentCommandConsumer incidentCommandConsumer;
        [SerializeField] private PHSRandomDebrisStream debrisStream;
        [SerializeField] private GameObject shopPortalRoot;
        [SerializeField] private GameObject exteriorTravelRoot;
        [SerializeField] private GameObject safeAreaWarpEffectRoot;
        [SerializeField] private bool keepShopPortalAlwaysActive = true;

        [Header("Runtime Binding")]
        [SerializeField, Min(0.1f)] private float bindTimeoutSeconds = 5f;

        private NetworkRunFlowCoordinator runFlowCoordinator;
        private GameObject environmentInstance;
        private float bindStartedAt;
        private bool setupValid;
        private bool bindErrorLogged;
        private bool initialApplyPending;
        private PHSMapProfileSO pendingIncidentScheduleProfile;
        private float pendingIncidentScheduleStartedAt;
        private bool pendingIncidentScheduleErrorLogged;

        public PHSMapProfileSO CurrentProfile { get; private set; }
        public bool KeepShopPortalAlwaysActive => keepShopPortalAlwaysActive;

        public event Action<PHSMapProfileSO> CurrentProfileChanged;

        private void Awake()
        {
            setupValid = ValidateSetup();
            if (shopPortalRoot != null)
            {
                shopPortalRoot.SetActive(keepShopPortalAlwaysActive);
            }

            if (exteriorTravelRoot != null)
            {
                exteriorTravelRoot.SetActive(false);
            }

            if (safeAreaWarpEffectRoot != null)
            {
                safeAreaWarpEffectRoot.SetActive(false);
            }

            enabled = setupValid;
        }

        private void OnEnable()
        {
            bindStartedAt = Time.unscaledTime;
            initialApplyPending = false;
            TryBindRunFlow();
        }

        private void OnDisable()
        {
            if (IsServer())
            {
                TryCancelIncidentSchedule("map_runtime_disabled");
            }

            ClearPendingIncidentSchedule();
            UnbindRunFlow();
        }

        private void Update()
        {
            if (!setupValid)
            {
                return;
            }

            if (runFlowCoordinator == null)
            {
                var networkManager = NetworkManager.Singleton;
                if (networkManager == null
                    || !networkManager.IsListening
                    || networkManager.ShutdownInProgress)
                {
                    bindStartedAt = Time.unscaledTime;
                    bindErrorLogged = false;
                    return;
                }

                TryBindRunFlow();
                if (runFlowCoordinator == null
                    && !bindErrorLogged
                    && Time.unscaledTime - bindStartedAt >= bindTimeoutSeconds)
                {
                    bindErrorLogged = true;
                    Debug.LogError("PHS_MAP_RUNTIME_BIND_FAILED reason=run_flow_missing", this);
                }

                return;
            }

            if (initialApplyPending)
            {
                initialApplyPending = false;
                ApplyCurrentPhaseState();
            }

            TryConfigurePendingIncidentSchedule();
        }

        private void TryBindRunFlow()
        {
            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator == null)
            {
                return;
            }

            runFlowCoordinator = coordinator;
            runFlowCoordinator.ActiveMapCommitted += HandleActiveMapCommitted;
            runFlowCoordinator.PhaseChanged += HandlePhaseChanged;
            bindErrorLogged = false;

            if (runFlowCoordinator.ActiveMapId <= 0)
            {
                Debug.LogError("PHS_MAP_RUNTIME_APPLY_FAILED reason=active_map_not_initialized", this);
                return;
            }

            // Scene object Awake order is undefined. Apply on the first Update after every
            // referenced runtime component has completed Awake/OnEnable, then reconcile
            // the current phase in case its transition event happened before this bind.
            initialApplyPending = true;
        }

        private void ApplyCurrentPhaseState()
        {
            var currentPhase = runFlowCoordinator.Phase;
            if (currentPhase == NetworkRunPhase.WarpArrival)
            {
                HandlePhaseChanged(currentPhase, currentPhase);
                TryApplyMap(runFlowCoordinator.ActiveMapId);
                return;
            }

            if (currentPhase == NetworkRunPhase.WarpSafe)
            {
                // A newly loaded or re-enabled context has no environment instance yet.
                // Restore the active map presentation before applying maintenance mode.
                TryApplyMap(runFlowCoordinator.ActiveMapId);
                HandlePhaseChanged(currentPhase, currentPhase);
                return;
            }

            if (currentPhase == NetworkRunPhase.Shop
                || currentPhase == NetworkRunPhase.FinalShop
                || currentPhase == NetworkRunPhase.Clear
                || currentPhase == NetworkRunPhase.GameOver)
            {
                HandlePhaseChanged(currentPhase, currentPhase);
                return;
            }

            TryApplyMap(runFlowCoordinator.ActiveMapId);
            HandlePhaseChanged(currentPhase, currentPhase);
        }

        private void UnbindRunFlow()
        {
            if (runFlowCoordinator == null)
            {
                return;
            }

            runFlowCoordinator.ActiveMapCommitted -= HandleActiveMapCommitted;
            runFlowCoordinator.PhaseChanged -= HandlePhaseChanged;
            runFlowCoordinator = null;
            initialApplyPending = false;
        }

        private void HandleActiveMapCommitted(int currentMapId)
        {
            ClearPendingIncidentSchedule();
            initialApplyPending = true;
        }

        private void HandlePhaseChanged(NetworkRunPhase previousPhase, NetworkRunPhase currentPhase)
        {
            if (IsServer()
                && (currentPhase == NetworkRunPhase.WarpArrival
                    || currentPhase == NetworkRunPhase.Shop
                    || currentPhase == NetworkRunPhase.FinalShop
                    || currentPhase == NetworkRunPhase.Clear
                    || currentPhase == NetworkRunPhase.GameOver))
            {
                TerminateIncidentRuntimeForPhase(currentPhase);
            }

            if (currentPhase == NetworkRunPhase.WarpSafe)
            {
                if (!TryApplyProfile(warpMaintenanceProfile))
                {
                    Debug.LogError("PHS_MAP_MAINTENANCE_APPLY_FAILED", this);
                }

                return;
            }

            if (currentPhase == NetworkRunPhase.Shop || currentPhase == NetworkRunPhase.FinalShop)
            {
                if (!TryApplyProfile(shopPortalProfile))
                {
                    Debug.LogError("PHS_MAP_SHOP_PORTAL_APPLY_FAILED", this);
                }

                return;
            }

            if (currentPhase == NetworkRunPhase.WarpReady
                && CurrentProfile != null
                && CurrentProfile.IsWarpMaintenance)
            {
                TryApplyMap(runFlowCoordinator.ActiveMapId);
            }

            if (!IsServer())
            {
                return;
            }

            if (currentPhase == NetworkRunPhase.WarpReady)
            {
                if (!TrySetIncidentSchedulingEnabled(false, out var pauseReason))
                {
                    Debug.LogError(
                        $"PHS_MAP_INCIDENT_SCHEDULE_PAUSE_FAILED reason={pauseReason}",
                        this);
                }

                return;
            }

            if (currentPhase == NetworkRunPhase.Charging && CurrentProfile != null)
            {
                TryConfigurePendingIncidentSchedule();
                if (!internalAccidentCoordinator.TrySetMaintenancePausedServer(false, out var maintenanceReason))
                {
                    Debug.LogError($"PHS_MAP_MAINTENANCE_RESUME_FAILED reason={maintenanceReason}", this);
                }

                if (!externalThreatScheduler.TryStopServer(out var externalStopReason))
                {
                    Debug.LogError(
                        $"PHS_MAP_EXTERNAL_THREAT_STOP_FAILED reason={externalStopReason} mapId={CurrentProfile.MapId}",
                        this);
                }

                if (!internalAccidentCoordinator.TryStopServer(out var internalStopReason))
                {
                    Debug.LogError(
                        $"PHS_MAP_INTERNAL_ACCIDENT_STOP_FAILED reason={internalStopReason} mapId={CurrentProfile.MapId}",
                        this);
                }

                if (CurrentProfile.AllowsEventGeneration
                    && !TrySetIncidentSchedulingEnabled(true, out var scheduleReason))
                {
                    Debug.LogError(
                        $"PHS_MAP_INCIDENT_SCHEDULE_START_FAILED reason={scheduleReason} mapId={CurrentProfile.MapId}",
                        this);
                }
            }
        }

        private bool TryApplyMap(int mapId)
        {
            if (!mapCatalog.TryResolve(mapId, out var profile))
            {
                Debug.LogError($"PHS_MAP_RUNTIME_APPLY_FAILED reason=profile_missing mapId={mapId}", this);
                return false;
            }

            return TryApplyProfile(profile);
        }

        private bool TryApplyProfile(PHSMapProfileSO profile)
        {
            if (profile == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_APPLY_FAILED reason=profile_missing", this);
                return false;
            }

            var mapId = profile.MapId;
            if (!profile.TryValidate(out var validationReason))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason=profile_invalid mapId={mapId} detail={validationReason}",
                    this);
                return false;
            }

            if (!TryApplyEnvironment(profile, out var environmentReason))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason={environmentReason} mapId={mapId}",
                    this);
                return false;
            }

            if (!TryResolveGameplaySkybox(profile, out var gameplaySkybox, out var skyboxReason))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason={skyboxReason} mapId={mapId}",
                    this);
                return false;
            }

            if (!warpTransitionPresenter.TryConfigureMapPresentation(
                    gameplaySkybox,
                    profile.ArrivalSkybox,
                    out var presentationReason))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason={presentationReason} mapId={mapId}",
                    this);
                return false;
            }

            if (IsServer() && !TryConfigureSchedules(profile, out var scheduleReason))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason={scheduleReason} mapId={mapId}",
                    this);
                return false;
            }

            debrisStream.SetSimulationEnabled(profile.AllowsDebrisGeneration);
            if (IsServer()
                && !internalAccidentCoordinator.TrySetMaintenancePausedServer(
                    profile.IsWarpMaintenance || profile.IsShopPortalProfile,
                    out var maintenanceReason))
            {
                Debug.LogError($"PHS_MAP_MAINTENANCE_STATE_FAILED reason={maintenanceReason} mapId={mapId}", this);
                return false;
            }

            shopPortalRoot.SetActive(keepShopPortalAlwaysActive || profile.AllowsShopPortal);
            exteriorTravelRoot.SetActive(!profile.AllowsShopPortal);
            safeAreaWarpEffectRoot.SetActive(profile.AllowsShopPortal);
            CurrentProfile = profile;
            CurrentProfileChanged?.Invoke(profile);
            Debug.Log($"PHS_MAP_RUNTIME_APPLIED mapId={mapId} name={profile.DisplayName}", this);
            return true;
        }

        private static bool TryResolveGameplaySkybox(
            PHSMapProfileSO profile,
            out Material gameplaySkybox,
            out string reason)
        {
            switch (profile.SkyboxMode)
            {
                case PHSMapSkyboxMode.ProfileMaterials:
                    gameplaySkybox = profile.GameplaySkybox;
                    reason = null;
                    return true;
                case PHSMapSkyboxMode.DedicatedSceneGameplayWithProfileArrival:
                    gameplaySkybox = RenderSettings.skybox;
                    if (gameplaySkybox == null)
                    {
                        reason = "dedicated_scene_render_settings_skybox_missing";
                        return false;
                    }

                    reason = null;
                    return true;
                default:
                    gameplaySkybox = null;
                    reason = $"skybox_mode_unsupported:{profile.SkyboxMode}";
                    return false;
            }
        }

        private bool TryApplyEnvironment(PHSMapProfileSO profile, out string reason)
        {
            if (profile.IsWarpMaintenance || profile.IsShopPortalProfile)
            {
                reason = null;
                return true;
            }

            if (environmentInstance != null)
            {
                Destroy(environmentInstance);
                environmentInstance = null;
            }

            if (profile.SceneMode == PHSMapSceneMode.SeparateScene)
            {
                reason = null;
                return true;
            }

            if (profile.SceneMode != PHSMapSceneMode.SharedSceneEnvironment)
            {
                reason = $"scene_mode_unsupported:{profile.SceneMode}";
                return false;
            }

            environmentInstance = Instantiate(profile.EnvironmentRootPrefab, environmentRoot);
            environmentInstance.name = $"PHS_ActiveMap_{profile.MapId}_{profile.EnvironmentRootPrefab.name}";
            reason = null;
            return true;
        }

        private bool TryConfigureSchedules(PHSMapProfileSO profile, out string reason)
        {
            if (!externalThreatScheduler.TryStopServer(out var externalStopReason))
            {
                reason = $"external_threat_scheduler_stop_failed:{externalStopReason}";
                return false;
            }

            if (!internalAccidentCoordinator.TryStopServer(out var internalStopReason))
            {
                reason = $"internal_accident_scheduler_stop_failed:{internalStopReason}";
                return false;
            }

            if (!profile.AllowsEventGeneration)
            {
                ClearPendingIncidentSchedule();
                var director =
                    NetworkRunSessionRoot.Instance?.IncidentDirector;
                if (director != null
                    && director.IsConfigured
                    && !director.ScheduleCancelled
                    && !director.TrySetSchedulingEnabledServer(
                        false,
                        out var pauseReason))
                {
                    reason =
                        $"incident_schedule_pause_failed:{pauseReason}";
                    return false;
                }

                reason = null;
                return true;
            }

            var externalWeights = profile.ExternalThreatWeights;
            var externalEntries = new WeightedEventScheduleEntry[externalWeights.Count];
            for (var index = 0; index < externalWeights.Count; index++)
            {
                externalEntries[index] = new WeightedEventScheduleEntry(
                    externalWeights[index].EventId,
                    externalWeights[index].Weight);
            }

            if (!externalThreatScheduler.TryConfigureServer(
                    PHSNetworkEventChannel.ExternalThreat,
                    externalEntries,
                    profile.ExternalThreatIntervalMinSeconds,
                    profile.ExternalThreatIntervalMaxSeconds,
                    profile.MaximumActiveExternalThreats,
                    out var externalConfigureReason))
            {
                reason = $"external_threat_scheduler_configure_failed:{externalConfigureReason}";
                return false;
            }

            var internalWeights = profile.InternalAccidentWeights;
            var internalEntries = new PHSMapShipAccidentWeight[internalWeights.Count];
            for (var index = 0; index < internalWeights.Count; index++)
            {
                internalEntries[index] = internalWeights[index];
            }

            if (!internalAccidentCoordinator.TryConfigureServer(
                    internalEntries,
                    profile.InternalAccidentIntervalMinSeconds,
                    profile.InternalAccidentIntervalMaxSeconds,
                    profile.MaximumActiveInternalAccidents,
                    profile.InternalModuleDamageMultiplier,
                    profile.InternalShipDamageMultiplier,
                    out var internalConfigureReason))
            {
                reason = $"internal_accident_scheduler_configure_failed:{internalConfigureReason}";
                return false;
            }

            var root = NetworkRunSessionRoot.Instance;
            if (root == null
                || root.IncidentDirector == null
                || root.StageClock == null)
            {
                reason = "incident_root_not_ready";
                return false;
            }

            if (!IsIncidentStageReady(profile, root.StageClock))
            {
                SetPendingIncidentSchedule(profile);
                reason = null;
                return true;
            }

            return TryConfigureIncidentDirector(profile, root, out reason);
        }

        private bool TryConfigureIncidentDirector(
            PHSMapProfileSO profile,
            NetworkRunSessionRoot root,
            out string reason)
        {
            if (!TryBuildIncidentScheduleDefinition(
                    profile,
                    root.StageClock.StageSequence,
                    out var definition,
                    out reason))
            {
                return false;
            }

            if (!root.IncidentDirector.TryConfigureServer(definition, out reason))
            {
                reason = $"incident_director_configure_failed:{reason}";
                return false;
            }

            if (runFlowCoordinator != null
                && runFlowCoordinator.Phase == NetworkRunPhase.Charging
                && !root.IncidentDirector.TrySetSchedulingEnabledServer(
                    true,
                    out var enableReason))
            {
                reason =
                    $"incident_schedule_start_failed:{enableReason}";
                return false;
            }

            ClearPendingIncidentSchedule();
            reason = null;
            return true;
        }

        private void SetPendingIncidentSchedule(PHSMapProfileSO profile)
        {
            if (pendingIncidentScheduleProfile == profile)
            {
                return;
            }

            pendingIncidentScheduleProfile = profile;
            pendingIncidentScheduleStartedAt = Time.unscaledTime;
            pendingIncidentScheduleErrorLogged = false;
            var stageClock = NetworkRunSessionRoot.Instance?.StageClock;
            Debug.Log(
                $"PHS_MAP_INCIDENT_SCHEDULE_DEFERRED mapId={profile.MapId} " +
                $"clock={stageClock?.MapId ?? 0} " +
                $"sequence={stageClock?.StageSequence ?? 0U}",
                this);
        }

        private void ClearPendingIncidentSchedule()
        {
            pendingIncidentScheduleProfile = null;
            pendingIncidentScheduleStartedAt = 0f;
            pendingIncidentScheduleErrorLogged = false;
        }

        private void TryConfigurePendingIncidentSchedule()
        {
            var profile = pendingIncidentScheduleProfile;
            if (profile == null || !IsServer())
            {
                return;
            }

            var root = NetworkRunSessionRoot.Instance;
            if (root == null
                || root.IncidentDirector == null
                || root.StageClock == null)
            {
                LogPendingIncidentScheduleFailure("incident_root_not_ready");
                return;
            }

            if (!IsIncidentStageReady(profile, root.StageClock))
            {
                LogPendingIncidentScheduleFailure(
                    $"incident_stage_mismatch:" +
                    $"profile={profile.MapId}:" +
                    $"clock={root.StageClock.MapId}:" +
                    $"sequence={root.StageClock.StageSequence}:" +
                    $"state={root.StageClock.State}");
                return;
            }

            if (!TryConfigureIncidentDirector(profile, root, out var reason))
            {
                LogPendingIncidentScheduleFailure(reason);
                return;
            }

            Debug.Log(
                $"PHS_MAP_INCIDENT_SCHEDULE_READY mapId={profile.MapId} " +
                $"sequence={root.StageClock.StageSequence}",
                this);
            if (runFlowCoordinator != null
                && runFlowCoordinator.Phase == NetworkRunPhase.Charging
                && !TrySetIncidentSchedulingEnabled(true, out var enableReason))
            {
                Debug.LogError(
                    $"PHS_MAP_INCIDENT_SCHEDULE_START_FAILED " +
                    $"reason={enableReason} mapId={profile.MapId}",
                    this);
            }
        }

        private static bool IsIncidentStageReady(
            PHSMapProfileSO profile,
            NetworkRunStageClock stageClock)
        {
            return profile != null
                && stageClock != null
                && stageClock.MapId == profile.MapId
                && stageClock.StageSequence != 0U
                && (stageClock.State == NetworkRunStageClockState.Running
                    || stageClock.State
                        == NetworkRunStageClockState.Paused);
        }

        private void LogPendingIncidentScheduleFailure(string reason)
        {
            if (pendingIncidentScheduleErrorLogged
                || runFlowCoordinator == null
                || runFlowCoordinator.Phase != NetworkRunPhase.Charging
                || Time.unscaledTime - pendingIncidentScheduleStartedAt
                    < bindTimeoutSeconds)
            {
                return;
            }

            pendingIncidentScheduleErrorLogged = true;
            Debug.LogError(
                $"PHS_MAP_INCIDENT_SCHEDULE_PENDING_FAILED " +
                $"reason={reason ?? "unknown"}",
                this);
        }

        private static bool TryBuildIncidentScheduleDefinition(
            PHSMapProfileSO profile,
            uint stageSequence,
            out RunIncidentScheduleDefinition definition,
            out string reason)
        {
            var externalWeights = profile.ExternalThreatWeights;
            var externalEntries = new RunIncidentWeightedEntry[externalWeights.Count];
            for (var index = 0; index < externalWeights.Count; index++)
            {
                var entry = externalWeights[index];
                if (!TryResolveExternalIncidentFamily(
                        entry.EventId,
                        out var family))
                {
                    definition = null;
                    reason = $"external_incident_family_missing:{entry.EventId}";
                    return false;
                }

                externalEntries[index] = new RunIncidentWeightedEntry(
                    (int)entry.EventId,
                    family,
                    entry.Weight,
                    1,
                    entry.WarpChargeMultiplier);
            }

            var internalWeights = profile.InternalAccidentWeights;
            var internalEntries = new RunIncidentWeightedEntry[internalWeights.Count];
            for (var index = 0; index < internalWeights.Count; index++)
            {
                var entry = internalWeights[index];
                if (!TryResolveInternalIncidentFamily(
                        entry.Definition.Id,
                        out var family))
                {
                    definition = null;
                    reason =
                        $"internal_incident_family_missing:" +
                        $"{entry.Definition.Id}";
                    return false;
                }

                internalEntries[index] = new RunIncidentWeightedEntry(
                    (int)entry.Definition.Id,
                    family,
                    entry.Weight,
                    1,
                    entry.WarpChargeMultiplier);
            }

            definition = new RunIncidentScheduleDefinition(
                profile.MapId,
                stageSequence,
                (ushort)profile.IncidentPressureCapacity,
                (byte)profile.MaximumActiveExternalThreats,
                (byte)profile.MaximumActiveInternalAccidents,
                profile.ExternalThreatIntervalMinSeconds,
                profile.ExternalThreatIntervalMaxSeconds,
                profile.InternalAccidentIntervalMinSeconds,
                profile.InternalAccidentIntervalMaxSeconds,
                externalEntries,
                internalEntries);
            if (!definition.TryValidate(out reason))
            {
                definition = null;
                return false;
            }

            reason = null;
            return true;
        }

        private static bool TryResolveExternalIncidentFamily(
            SM.EventId eventId,
            out NetworkRunIncidentFamily family)
        {
            switch (eventId)
            {
                case SM.EventId.EnemyScout:
                    family = NetworkRunIncidentFamily.Enemy;
                    return true;
                case SM.EventId.MeteorAttack:
                    family = NetworkRunIncidentFamily.Meteor;
                    return true;
                case SM.EventId.EmpAttack:
                    family = NetworkRunIncidentFamily.EMP;
                    return true;
                default:
                    family = NetworkRunIncidentFamily.None;
                    return false;
            }
        }

        private static bool TryResolveInternalIncidentFamily(
            PHSShipAccidentId accidentId,
            out NetworkRunIncidentFamily family)
        {
            switch (accidentId)
            {
                case PHSShipAccidentId.Fire:
                    family = NetworkRunIncidentFamily.Fire;
                    return true;
                case PHSShipAccidentId.PowerFailure:
                    family = NetworkRunIncidentFamily.Power;
                    return true;
                case PHSShipAccidentId.DeviceFailure:
                    family = NetworkRunIncidentFamily.Device;
                    return true;
                case PHSShipAccidentId.HullBreach:
                    family = NetworkRunIncidentFamily.Hull;
                    return true;
                case PHSShipAccidentId.SteamLeak:
                    family = NetworkRunIncidentFamily.Steam;
                    return true;
                case PHSShipAccidentId.OxygenFailure:
                    family = NetworkRunIncidentFamily.Oxygen;
                    return true;
                case PHSShipAccidentId.GravityGeneratorFailure:
                    family = NetworkRunIncidentFamily.Gravity;
                    return true;
                default:
                    family = NetworkRunIncidentFamily.None;
                    return false;
            }
        }

        private void TerminateIncidentRuntimeForPhase(
            NetworkRunPhase currentPhase)
        {
            ClearPendingIncidentSchedule();
            if (!externalThreatScheduler.TryStopServer(
                    out var externalStopReason))
            {
                Debug.LogError(
                    $"PHS_MAP_EXTERNAL_THREAT_STOP_FAILED " +
                    $"reason={externalStopReason} phase={currentPhase}",
                    this);
            }

            if (!internalAccidentCoordinator.TryStopServer(
                    out var internalStopReason))
            {
                Debug.LogError(
                    $"PHS_MAP_INTERNAL_ACCIDENT_STOP_FAILED " +
                    $"reason={internalStopReason} phase={currentPhase}",
                    this);
            }

            if (!incidentCommandConsumer.TryTerminateAllServer(
                    $"phase_{currentPhase}",
                    out var terminateReason))
            {
                Debug.LogError(
                    $"PHS_MAP_INCIDENT_RUNTIME_TERMINATE_FAILED " +
                    $"reason={terminateReason} phase={currentPhase}",
                    this);
            }

            TryCancelIncidentSchedule($"phase_{currentPhase}");
        }

        private bool TrySetIncidentSchedulingEnabled(
            bool schedulingEnabled,
            out string reason)
        {
            var director = NetworkRunSessionRoot.Instance?.IncidentDirector;
            if (director == null)
            {
                reason = "incident_director_missing";
                return false;
            }

            return director.TrySetSchedulingEnabledServer(
                schedulingEnabled,
                out reason);
        }

        private void TryCancelIncidentSchedule(string cause)
        {
            var director = NetworkRunSessionRoot.Instance?.IncidentDirector;
            if (director == null || !director.IsConfigured)
            {
                return;
            }

            if (!director.TryCancelScheduleServer(cause, out var reason))
            {
                Debug.LogError(
                    $"PHS_MAP_INCIDENT_SCHEDULE_CANCEL_FAILED reason={reason} cause={cause}",
                    this);
            }
        }

        private bool ValidateSetup()
        {
            if (mapCatalog == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=map_catalog_missing", this);
                return false;
            }

            if (warpMaintenanceProfile == null || !warpMaintenanceProfile.IsWarpMaintenance)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=warp_maintenance_profile_missing", this);
                return false;
            }

            if (shopPortalProfile == null || !shopPortalProfile.IsShopPortalProfile)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=shop_portal_profile_missing", this);
                return false;
            }

            if (shopPortalRoot == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=shop_portal_root_missing", this);
                return false;
            }

            if (exteriorTravelRoot == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=exterior_travel_root_missing", this);
                return false;
            }

            if (safeAreaWarpEffectRoot == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=safe_area_warp_effect_root_missing", this);
                return false;
            }

            if (!mapCatalog.TryValidate(out var catalogReason))
            {
                Debug.LogError($"PHS_MAP_RUNTIME_SETUP_FAILED reason=catalog_invalid detail={catalogReason}", this);
                return false;
            }

            if (environmentRoot == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=environment_root_missing", this);
                return false;
            }

            if (warpTransitionPresenter == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=warp_presenter_missing", this);
                return false;
            }

            if (externalThreatScheduler == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=external_threat_scheduler_missing", this);
                return false;
            }

            if (internalAccidentCoordinator == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=internal_accident_coordinator_missing", this);
                return false;
            }

            if (incidentCommandConsumer == null)
            {
                Debug.LogError(
                    "PHS_MAP_RUNTIME_SETUP_FAILED reason=incident_command_consumer_missing",
                    this);
                return false;
            }

            if (debrisStream == null)
            {
                Debug.LogError("PHS_MAP_RUNTIME_SETUP_FAILED reason=debris_stream_missing", this);
                return false;
            }

            return true;
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
