using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
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
        [SerializeField] private PHSRandomDebrisStream debrisStream;
        [SerializeField] private GameObject shopPortalRoot;

        [Header("Runtime Binding")]
        [SerializeField, Min(0.1f)] private float bindTimeoutSeconds = 5f;

        private NetworkRunFlowCoordinator runFlowCoordinator;
        private GameObject environmentInstance;
        private float bindStartedAt;
        private bool setupValid;
        private bool bindErrorLogged;
        private bool initialApplyPending;

        public PHSMapProfileSO CurrentProfile { get; private set; }

        public bool TryResolveGameplayProfile(
            int mapId,
            out PHSMapProfileSO profile)
        {
            profile = null;
            return mapCatalog != null && mapCatalog.TryResolve(mapId, out profile);
        }

        public event Action<PHSMapProfileSO> CurrentProfileChanged;

        private void Awake()
        {
            setupValid = ValidateSetup();
            SetShopPortalActive(false);

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
                    && NetworkRunSessionRoot.Instance != null
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
            initialApplyPending = true;
        }

        private void HandlePhaseChanged(NetworkRunPhase previousPhase, NetworkRunPhase currentPhase)
        {
            SetShopPortalActive(
                currentPhase == NetworkRunPhase.Shop
                || currentPhase == NetworkRunPhase.FinalShop);

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

            if (currentPhase == NetworkRunPhase.Charging && CurrentProfile != null)
            {
                if (CurrentProfile.AllowsEventGeneration)
                {
                    if (!TryGetPersistentEventScheduler(out var externalThreatScheduler))
                    {
                        Debug.LogError(
                            $"PHS_MAP_TEAM_EVENT_SCHEDULE_START_FAILED reason=persistent_scheduler_missing mapId={CurrentProfile.MapId}",
                            this);
                    }
                    else if (!externalThreatScheduler.TryStartServer(out var externalStartReason))
                    {
                        Debug.LogError(
                            $"PHS_MAP_TEAM_EVENT_SCHEDULE_START_FAILED reason={externalStartReason} mapId={CurrentProfile.MapId}",
                            this);
                    }
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

            if (debrisStream == null)
            {
                Debug.LogError($"PHS_MAP_RUNTIME_APPLY_FAILED reason=debris_stream_missing mapId={mapId}", this);
                return false;
            }

            if (!debrisStream.ConfigureTargetDebrisCount(profile.DebrisAmount))
            {
                Debug.LogError(
                    $"PHS_MAP_RUNTIME_APPLY_FAILED reason=debris_amount_rejected " +
                    $"mapId={mapId} amount={profile.DebrisAmount}",
                    this);
                return false;
            }

            debrisStream.SetSimulationEnabled(profile.AllowsDebrisGeneration);
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
            if (!TryGetPersistentEventScheduler(out var externalThreatScheduler))
            {
                reason = "persistent_event_scheduler_missing";
                return false;
            }

            if (!externalThreatScheduler.TryStopServer(out var externalStopReason))
            {
                reason = $"external_threat_scheduler_stop_failed:{externalStopReason}";
                return false;
            }

            if (!profile.AllowsEventGeneration)
            {
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

            var clearedZones = runFlowCoordinator == null
                ? 0
                : runFlowCoordinator.ClearedZoneCount;
            if (!externalThreatScheduler.TryConfigureServer(
                    PHSNetworkEventChannel.LegacyMixed,
                    externalEntries,
                    profile.GetExternalThreatIntervalMinSeconds(clearedZones),
                    profile.GetExternalThreatIntervalMaxSeconds(clearedZones),
                    profile.MaximumActiveExternalThreats,
                    out var externalConfigureReason))
            {
                reason = $"external_threat_scheduler_configure_failed:{externalConfigureReason}";
                return false;
            }

            var eventCoordinator = NetworkEventCoordinator.Instance;
            var eventImpactReason = eventCoordinator == null
                ? "coordinator_missing"
                : null;
            if (eventCoordinator == null
                || !eventCoordinator.TryConfigureShipModuleImpactServer(
                    profile.GetInternalModuleDamageMultiplier(clearedZones),
                    profile.GetInternalShipDamageMultiplier(clearedZones),
                    out eventImpactReason))
            {
                reason = $"event_module_impact_configure_failed:{eventImpactReason}";
                return false;
            }

            Debug.Log(
                $"PHS_MAP_TEAM_EVENT_AUTHORITY_READY mapId={profile.MapId} " +
                $"entries={externalEntries.Length} " +
                $"runDifficulty={PHSMapProfileSO.GetRunDifficultyStage(clearedZones)} " +
                $"cleared={clearedZones}",
                this);
            reason = null;
            return true;
        }

        private void TerminateIncidentRuntimeForPhase(
            NetworkRunPhase currentPhase)
        {
            if (!TryGetPersistentEventScheduler(out var externalThreatScheduler))
            {
                Debug.LogError(
                    $"PHS_MAP_EXTERNAL_THREAT_STOP_FAILED reason=persistent_scheduler_missing phase={currentPhase}",
                    this);
                return;
            }

            if (!externalThreatScheduler.TryStopServer(out var externalStopReason))
            {
                Debug.LogError(
                    $"PHS_MAP_EXTERNAL_THREAT_STOP_FAILED " +
                    $"reason={externalStopReason} phase={currentPhase}",
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

            return true;
        }

        private void SetShopPortalActive(bool active)
        {
            if (shopPortalRoot != null && shopPortalRoot.activeSelf != active)
            {
                shopPortalRoot.SetActive(active);
            }
        }

        private bool TryGetPersistentEventScheduler(
            out PHSNetworkEventScheduler scheduler)
        {
            scheduler = NetworkRunSessionRoot.Instance?.EventScheduler;
            if (scheduler != null)
            {
                return true;
            }

            Debug.LogError(
                "PHS_MAP_RUNTIME_EVENT_AUTHORITY_FAILED reason=persistent_scheduler_missing",
                this);
            return false;
        }

        private static bool IsServer()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
