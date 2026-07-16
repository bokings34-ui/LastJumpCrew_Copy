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
        [SerializeField] private PHSRandomDebrisStream debrisStream;
        [SerializeField] private GameObject shopPortalRoot;
        [SerializeField] private bool keepShopPortalAlwaysActive = true;

        [Header("Runtime Binding")]
        [SerializeField, Min(0.1f)] private float bindTimeoutSeconds = 5f;

        private NetworkRunFlowCoordinator runFlowCoordinator;
        private GameObject environmentInstance;
        private float bindStartedAt;
        private bool setupValid;
        private bool bindErrorLogged;
        private bool initialApplyPending;

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
                TryApplyMap(runFlowCoordinator.ActiveMapId);
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
            // referenced runtime component has completed Awake/OnEnable.
            initialApplyPending = true;
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
            TryApplyMap(currentMapId);
        }

        private void HandlePhaseChanged(NetworkRunPhase previousPhase, NetworkRunPhase currentPhase)
        {
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

            if (currentPhase == NetworkRunPhase.Warping
                || currentPhase == NetworkRunPhase.Shop
                || currentPhase == NetworkRunPhase.FinalShop
                || currentPhase == NetworkRunPhase.Clear
                || currentPhase == NetworkRunPhase.GameOver)
            {
                if (!externalThreatScheduler.TryStopServer(out var externalStopReason))
                {
                    Debug.LogError($"PHS_MAP_EXTERNAL_THREAT_STOP_FAILED reason={externalStopReason} phase={currentPhase}", this);
                }

                if (!internalAccidentCoordinator.TryStopServer(out var internalStopReason))
                {
                    Debug.LogError($"PHS_MAP_INTERNAL_ACCIDENT_STOP_FAILED reason={internalStopReason} phase={currentPhase}", this);
                }

                return;
            }

            if (currentPhase == NetworkRunPhase.Charging && CurrentProfile != null)
            {
                if (!internalAccidentCoordinator.TrySetMaintenancePausedServer(false, out var maintenanceReason))
                {
                    Debug.LogError($"PHS_MAP_MAINTENANCE_RESUME_FAILED reason={maintenanceReason}", this);
                }

                if (CurrentProfile.AllowsEventGeneration
                    && !externalThreatScheduler.TryStartServer(out var externalStartReason))
                {
                    Debug.LogError($"PHS_MAP_EXTERNAL_THREAT_START_FAILED reason={externalStartReason} mapId={CurrentProfile.MapId}", this);
                }

                if (CurrentProfile.AllowsEventGeneration
                    && !internalAccidentCoordinator.TryStartServer(out var internalStartReason))
                {
                    Debug.LogError($"PHS_MAP_INTERNAL_ACCIDENT_START_FAILED reason={internalStartReason} mapId={CurrentProfile.MapId}", this);
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

            reason = null;
            return true;
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
