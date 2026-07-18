using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [CreateAssetMenu(
        fileName = "PHS_MapProfile_New",
        menuName = "LastJumpCrew/ParkHanSol/Map Profile")]
    public sealed class PHSMapProfileSO : ScriptableObject, IMapProfile
    {
        public const int MinimumMapId = 8000;
        public const int MaximumMapId = 8999;

        [Header("Identity")]
        [SerializeField] private int id = MinimumMapId;
        [SerializeField] private string displayName;
        [SerializeField] private bool selectable = true;

        [Header("Difficulty And Reward")]
        [SerializeField, Min(1)] private int difficulty = 1;
        [SerializeField, Min(1f)] private float stageTimeLimitSeconds = 180f;
        [SerializeField, Min(0)] private int clearRewardCredits;

        [Header("Runtime Rules")]
        [SerializeField] private bool isWarpMaintenance;
        [SerializeField] private bool isShopPortalProfile;
        [SerializeField] private bool advancesStageTime = true;
        [SerializeField] private bool allowsEventGeneration = true;
        [SerializeField] private bool allowsDebrisGeneration = true;
        [SerializeField] private bool allowsShopPortal;

        [Header("Map Loading")]
        [Tooltip("Shared: 공용 맵 씬에 Environment Prefab을 교체합니다. Separate: Scene Name의 전용 우주 맵 씬을 로드합니다.")]
        [SerializeField] private PHSMapSceneMode sceneMode = PHSMapSceneMode.SharedSceneEnvironment;
        [SerializeField] private string sceneName = "PHS_Map_ver1";
        [Tooltip("SharedSceneEnvironment에서만 사용합니다. 맵 외형 루트 프리팹을 드래그해 교체합니다.")]
        [SerializeField] private GameObject environmentRootPrefab;

        [Header("Skybox Materials - Drag And Drop")]
        [Tooltip("ProfileMaterials: 아래 두 재질을 사용합니다. DedicatedSceneGameplayWithProfileArrival: 전용 씬의 RenderSettings.skybox를 플레이 배경으로 사용합니다.")]
        [SerializeField] private PHSMapSkyboxMode skyboxMode = PHSMapSkyboxMode.ProfileMaterials;
        [Tooltip("ProfileMaterials에서 플레이 중 사용할 Skybox Material입니다.")]
        [SerializeField] private Material gameplaySkybox;
        [Tooltip("워프 도착 연출에 사용할 Skybox Material입니다. 모든 모드에서 필수입니다.")]
        [SerializeField] private Material arrivalSkybox;

        [Header("External Threat Schedule")]
        [SerializeField, Range(1, 8)] private int incidentPressureCapacity = 3;
        [FormerlySerializedAs("eventWeights")]
        [SerializeField] private List<PHSMapEventWeight> externalThreatWeights = new();
        [FormerlySerializedAs("eventIntervalMinSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMinSeconds = 30f;
        [FormerlySerializedAs("eventIntervalMaxSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMaxSeconds = 60f;
        [FormerlySerializedAs("maximumActiveEvents")]
        [Tooltip("0 = disabled. Production selectable maps use 1.")]
        [SerializeField, Min(0)] private int maximumActiveExternalThreats;

        [Header("Internal Accident Schedule")]
        [SerializeField] private List<PHSMapShipAccidentWeight> internalAccidentWeights = new();
        [SerializeField, Min(0.1f)] private float internalAccidentIntervalMinSeconds = 35f;
        [SerializeField, Min(0.1f)] private float internalAccidentIntervalMaxSeconds = 55f;
        [Tooltip("0 = disabled. Production selectable maps use 2.")]
        [SerializeField, Min(0)] private int maximumActiveInternalAccidents;
        [SerializeField, Min(0.01f)] private float internalModuleDamageMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float internalShipDamageMultiplier = 1f;

        public int MapId => id;
        public string DisplayName => displayName;
        public bool Selectable => selectable;
        public int Difficulty => difficulty;
        public float StageTimeLimitSeconds => stageTimeLimitSeconds;
        public int ClearRewardCredits => clearRewardCredits;
        public bool IsWarpMaintenance => isWarpMaintenance;
        public bool IsShopPortalProfile => isShopPortalProfile;
        public bool AdvancesStageTime => advancesStageTime;
        public bool AllowsEventGeneration => allowsEventGeneration;
        public bool AllowsDebrisGeneration => allowsDebrisGeneration;
        public bool AllowsShopPortal => allowsShopPortal;
        public PHSMapSceneMode SceneMode => sceneMode;
        public string SceneName => sceneName;
        public GameObject EnvironmentRootPrefab => environmentRootPrefab;
        public PHSMapSkyboxMode SkyboxMode => skyboxMode;
        public Material GameplaySkybox => gameplaySkybox;
        public Material ArrivalSkybox => arrivalSkybox;
        public int IncidentPressureCapacity => incidentPressureCapacity;
        public IReadOnlyList<PHSMapEventWeight> ExternalThreatWeights => externalThreatWeights;
        public float ExternalThreatIntervalMinSeconds => externalThreatIntervalMinSeconds;
        public float ExternalThreatIntervalMaxSeconds => externalThreatIntervalMaxSeconds;
        public int MaximumActiveExternalThreats => maximumActiveExternalThreats;
        public IReadOnlyList<PHSMapShipAccidentWeight> InternalAccidentWeights => internalAccidentWeights;
        public float InternalAccidentIntervalMinSeconds => internalAccidentIntervalMinSeconds;
        public float InternalAccidentIntervalMaxSeconds => internalAccidentIntervalMaxSeconds;
        public int MaximumActiveInternalAccidents => maximumActiveInternalAccidents;
        public float InternalModuleDamageMultiplier => internalModuleDamageMultiplier;
        public float InternalShipDamageMultiplier => internalShipDamageMultiplier;

        private void OnValidate()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_MAP_PROFILE_INVALID asset={name} reason={reason}",
                    this);
            }
        }

        public bool TryValidate(out string reason)
        {
            if (id < MinimumMapId || id > MaximumMapId)
            {
                reason = $"id_out_of_range:id={id}:required={MinimumMapId}-{MaximumMapId}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                reason = "display_name_missing";
                return false;
            }

            if (difficulty <= 0)
            {
                reason = $"difficulty_not_positive:value={difficulty}";
                return false;
            }

            if (stageTimeLimitSeconds <= 0f)
            {
                reason = $"stage_time_not_positive:value={stageTimeLimitSeconds}";
                return false;
            }

            if (clearRewardCredits < 0)
            {
                reason = $"clear_reward_negative:value={clearRewardCredits}";
                return false;
            }

            if (isWarpMaintenance
                && (selectable || advancesStageTime || allowsEventGeneration || allowsDebrisGeneration))
            {
                reason = "warp_maintenance_rules_invalid";
                return false;
            }

            if (isShopPortalProfile
                && (selectable || advancesStageTime || allowsEventGeneration
                    || allowsDebrisGeneration || !allowsShopPortal))
            {
                reason = "shop_portal_profile_rules_invalid";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(PHSMapSceneMode), sceneMode))
            {
                reason = $"scene_mode_invalid:value={(int)sceneMode}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                reason = "scene_name_missing";
                return false;
            }

            if (sceneMode == PHSMapSceneMode.SharedSceneEnvironment && environmentRootPrefab == null)
            {
                reason = "environment_root_prefab_missing_for_shared_scene";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(PHSMapSkyboxMode), skyboxMode))
            {
                reason = $"skybox_mode_invalid:value={(int)skyboxMode}";
                return false;
            }

            if (skyboxMode == PHSMapSkyboxMode.DedicatedSceneGameplayWithProfileArrival
                && sceneMode != PHSMapSceneMode.SeparateScene)
            {
                reason = "dedicated_scene_skybox_mode_requires_separate_scene";
                return false;
            }

            if (skyboxMode == PHSMapSkyboxMode.ProfileMaterials && gameplaySkybox == null)
            {
                reason = "gameplay_skybox_missing";
                return false;
            }

            if (arrivalSkybox == null)
            {
                reason = "arrival_skybox_missing";
                return false;
            }

            if (allowsEventGeneration && (externalThreatWeights == null || externalThreatWeights.Count == 0))
            {
                reason = "external_threat_weights_missing";
                return false;
            }

            if (incidentPressureCapacity <= 0 || incidentPressureCapacity > byte.MaxValue)
            {
                reason = $"incident_pressure_capacity_invalid:value={incidentPressureCapacity}";
                return false;
            }

            var eventIds = new HashSet<EventId>();
            for (var index = 0; index < (externalThreatWeights?.Count ?? 0); index++)
            {
                var entry = externalThreatWeights[index];
                if (entry == null)
                {
                    reason = $"event_weight_entry_missing:index={index}";
                    return false;
                }

                if (!entry.TryValidate(out var entryReason))
                {
                    reason = $"event_weight_invalid:index={index}:{entryReason}";
                    return false;
                }

                var eventValue = (int)entry.EventId;
                if (eventValue < (int)SM.EventType.External || eventValue >= (int)SM.EventType.Environment)
                {
                    reason = $"external_threat_channel_mismatch:event={entry.EventId}";
                    return false;
                }

                if (!eventIds.Add(entry.EventId))
                {
                    reason = $"event_weight_duplicate:event={entry.EventId}";
                    return false;
                }
            }

            if (externalThreatIntervalMinSeconds <= 0f)
            {
                reason = $"external_threat_interval_min_not_positive:value={externalThreatIntervalMinSeconds}";
                return false;
            }

            if (externalThreatIntervalMaxSeconds < externalThreatIntervalMinSeconds)
            {
                reason = $"external_threat_interval_range_invalid:min={externalThreatIntervalMinSeconds}:max={externalThreatIntervalMaxSeconds}";
                return false;
            }

            if (maximumActiveExternalThreats < 0)
            {
                reason = $"maximum_active_external_threats_negative:value={maximumActiveExternalThreats}";
                return false;
            }

            if (allowsEventGeneration && maximumActiveExternalThreats <= 0)
            {
                reason = "maximum_active_external_threats_required";
                return false;
            }

            if (allowsEventGeneration && (internalAccidentWeights == null || internalAccidentWeights.Count == 0))
            {
                reason = "internal_accident_weights_missing";
                return false;
            }

            var accidentIds = new HashSet<PHSShipAccidentId>();
            for (var index = 0; index < (internalAccidentWeights?.Count ?? 0); index++)
            {
                var entry = internalAccidentWeights[index];
                if (entry == null)
                {
                    reason = $"internal_accident_weight_entry_missing:index={index}";
                    return false;
                }

                if (!entry.TryValidate(out var entryReason))
                {
                    reason = $"internal_accident_weight_invalid:index={index}:{entryReason}";
                    return false;
                }

                if (!accidentIds.Add(entry.Definition.Id))
                {
                    reason = $"internal_accident_weight_duplicate:accident={entry.Definition.Id}";
                    return false;
                }
            }

            if (internalAccidentIntervalMinSeconds <= 0f
                || internalAccidentIntervalMaxSeconds < internalAccidentIntervalMinSeconds)
            {
                reason = $"internal_accident_interval_range_invalid:min={internalAccidentIntervalMinSeconds}:max={internalAccidentIntervalMaxSeconds}";
                return false;
            }

            if (maximumActiveInternalAccidents < 0)
            {
                reason = $"maximum_active_internal_accidents_negative:value={maximumActiveInternalAccidents}";
                return false;
            }

            if (allowsEventGeneration && maximumActiveInternalAccidents <= 0)
            {
                reason = "maximum_active_internal_accidents_required";
                return false;
            }

            if (maximumActiveExternalThreats > incidentPressureCapacity
                || maximumActiveInternalAccidents > incidentPressureCapacity
                || maximumActiveExternalThreats + maximumActiveInternalAccidents
                    > incidentPressureCapacity)
            {
                reason =
                    $"incident_channel_capacity_invalid:" +
                    $"external={maximumActiveExternalThreats}:" +
                    $"internal={maximumActiveInternalAccidents}:" +
                    $"capacity={incidentPressureCapacity}";
                return false;
            }

            if (internalModuleDamageMultiplier <= 0f
                || float.IsNaN(internalModuleDamageMultiplier)
                || float.IsInfinity(internalModuleDamageMultiplier))
            {
                reason = $"internal_module_damage_multiplier_invalid:value={internalModuleDamageMultiplier}";
                return false;
            }

            if (internalShipDamageMultiplier <= 0f
                || float.IsNaN(internalShipDamageMultiplier)
                || float.IsInfinity(internalShipDamageMultiplier))
            {
                reason = $"internal_ship_damage_multiplier_invalid:value={internalShipDamageMultiplier}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
