using System.Collections.Generic;
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
        public const int MaximumRunDifficultyStage = 4;

        [Header("Identity")]
        [SerializeField] private int id = MinimumMapId;
        [SerializeField] private string displayName;
        [SerializeField] private bool selectable = true;

        [Header("Difficulty And Reward")]
        [FormerlySerializedAs("difficulty")]
        [SerializeField] private PHSMapDifficultyTier difficultyTier =
            PHSMapDifficultyTier.Normal;
        [SerializeField, Min(0)] private int debrisAmount;
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

        [Header("Team Event Schedule")]
        [FormerlySerializedAs("eventWeights")]
        [SerializeField] private List<PHSMapEventWeight> externalThreatWeights = new();
        [FormerlySerializedAs("eventIntervalMinSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMinSeconds = 30f;
        [FormerlySerializedAs("eventIntervalMaxSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMaxSeconds = 60f;
        [FormerlySerializedAs("maximumActiveEvents")]
        [Tooltip("0 = unlimited. Selectable maps must author an explicit positive cap.")]
        [SerializeField, Min(0)] private int maximumActiveExternalThreats;

        [Header("Team Event Impact")]
        [SerializeField, Min(0.01f)] private float internalModuleDamageMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float internalShipDamageMultiplier = 1f;

        public int MapId => id;
        public string DisplayName => displayName;
        public bool Selectable => selectable;
        public int Difficulty => (int)difficultyTier;
        public PHSMapDifficultyTier DifficultyTier => difficultyTier;
        public string DifficultyLabel => difficultyTier switch
        {
            PHSMapDifficultyTier.Easy => "하",
            PHSMapDifficultyTier.Normal => "중",
            PHSMapDifficultyTier.Hard => "상",
            _ => "-"
        };
        public float DifficultyIntervalMultiplier => difficultyTier switch
        {
            PHSMapDifficultyTier.Easy => 1.2f,
            PHSMapDifficultyTier.Normal => 1f,
            PHSMapDifficultyTier.Hard => 0.8f,
            _ => 1f
        };
        public float DifficultyDamageMultiplier => difficultyTier switch
        {
            PHSMapDifficultyTier.Easy => 0.85f,
            PHSMapDifficultyTier.Normal => 1f,
            PHSMapDifficultyTier.Hard => 1.25f,
            _ => 1f
        };
        public float DifficultyRewardMultiplier => difficultyTier switch
        {
            PHSMapDifficultyTier.Easy => 0.9f,
            PHSMapDifficultyTier.Normal => 1f,
            PHSMapDifficultyTier.Hard => 1.25f,
            _ => 1f
        };
        public int DebrisAmount => debrisAmount;
        public string DebrisAmountLabel => debrisAmount.ToString();
        public float StageTimeLimitSeconds => stageTimeLimitSeconds;
        public int ClearRewardCredits => Mathf.RoundToInt(
            clearRewardCredits * DifficultyRewardMultiplier);
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
        public IReadOnlyList<PHSMapEventWeight> ExternalThreatWeights => externalThreatWeights;
        public float ExternalThreatIntervalMinSeconds =>
            externalThreatIntervalMinSeconds * DifficultyIntervalMultiplier;
        public float ExternalThreatIntervalMaxSeconds =>
            externalThreatIntervalMaxSeconds * DifficultyIntervalMultiplier;
        public int MaximumActiveExternalThreats => maximumActiveExternalThreats;
        public float InternalModuleDamageMultiplier =>
            internalModuleDamageMultiplier * DifficultyDamageMultiplier;
        public float InternalShipDamageMultiplier =>
            internalShipDamageMultiplier * DifficultyDamageMultiplier;

        public static int GetRunDifficultyStage(int clearedZones)
        {
            return Mathf.Clamp(clearedZones + 1, 1, MaximumRunDifficultyStage);
        }

        public float GetExternalThreatIntervalMinSeconds(int clearedZones)
        {
            return ExternalThreatIntervalMinSeconds
                * GetRunIntervalMultiplier(clearedZones);
        }

        public float GetExternalThreatIntervalMaxSeconds(int clearedZones)
        {
            return ExternalThreatIntervalMaxSeconds
                * GetRunIntervalMultiplier(clearedZones);
        }

        public float GetInternalModuleDamageMultiplier(int clearedZones)
        {
            return InternalModuleDamageMultiplier
                * GetRunDamageMultiplier(clearedZones);
        }

        public float GetInternalShipDamageMultiplier(int clearedZones)
        {
            return InternalShipDamageMultiplier
                * GetRunDamageMultiplier(clearedZones);
        }

        private static float GetRunIntervalMultiplier(int clearedZones)
        {
            return 1f - (GetRunDifficultyStage(clearedZones) - 1) * 0.1f;
        }

        private static float GetRunDamageMultiplier(int clearedZones)
        {
            return 1f + (GetRunDifficultyStage(clearedZones) - 1) * 0.1f;
        }

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

            if (!System.Enum.IsDefined(
                    typeof(PHSMapDifficultyTier),
                    difficultyTier))
            {
                reason = $"difficulty_tier_invalid:value={(byte)difficultyTier}";
                return false;
            }

            if (allowsDebrisGeneration && debrisAmount <= 0)
            {
                reason = $"debris_amount_required:value={debrisAmount}";
                return false;
            }

            if (!allowsDebrisGeneration && debrisAmount != 0)
            {
                reason = $"debris_amount_must_be_zero:value={debrisAmount}";
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
                if (entry.EventId == EventId.GravityGeneratorFailure)
                {
                    reason = "team_event_excluded:gravity_generator_failure";
                    return false;
                }

                if (eventValue < (int)SM.EventType.Internal
                    || eventValue >= (int)SM.EventType.Environment)
                {
                    reason = $"team_event_channel_mismatch:event={entry.EventId}";
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

            // Zero is the explicit unlimited setting.  Concurrent incidents must
            // remain visible together instead of being serialized by the profile.

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
