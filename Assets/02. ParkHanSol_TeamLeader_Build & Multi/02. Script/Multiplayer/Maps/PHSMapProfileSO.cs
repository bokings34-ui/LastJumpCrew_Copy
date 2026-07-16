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

        [Header("Scene And Environment")]
        [SerializeField] private PHSMapSceneMode sceneMode = PHSMapSceneMode.SharedSceneEnvironment;
        [SerializeField] private string sceneName = "PHS_Map_ver1";
        [SerializeField] private GameObject environmentRootPrefab;
        [SerializeField] private Material gameplaySkybox;
        [SerializeField] private Material arrivalSkybox;

        [Header("External Threat Schedule")]
        [FormerlySerializedAs("eventWeights")]
        [SerializeField] private List<PHSMapEventWeight> externalThreatWeights = new();
        [FormerlySerializedAs("eventIntervalMinSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMinSeconds = 30f;
        [FormerlySerializedAs("eventIntervalMaxSeconds")]
        [SerializeField, Min(0.1f)] private float externalThreatIntervalMaxSeconds = 60f;
        [FormerlySerializedAs("maximumActiveEvents")]
        [SerializeField, Min(1)] private int maximumActiveExternalThreats = 1;

        [Header("Internal Accident Schedule")]
        [SerializeField] private List<PHSMapShipAccidentWeight> internalAccidentWeights = new();
        [SerializeField, Min(0.1f)] private float internalAccidentIntervalMinSeconds = 35f;
        [SerializeField, Min(0.1f)] private float internalAccidentIntervalMaxSeconds = 55f;
        [SerializeField, Min(1)] private int maximumActiveInternalAccidents = 2;
        [SerializeField, Min(0.01f)] private float internalModuleDamageMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float internalShipDamageMultiplier = 1f;

        public int MapId => id;
        public string DisplayName => displayName;
        public bool Selectable => selectable;
        public int Difficulty => difficulty;
        public float StageTimeLimitSeconds => stageTimeLimitSeconds;
        public int ClearRewardCredits => clearRewardCredits;
        public PHSMapSceneMode SceneMode => sceneMode;
        public string SceneName => sceneName;
        public GameObject EnvironmentRootPrefab => environmentRootPrefab;
        public Material GameplaySkybox => gameplaySkybox;
        public Material ArrivalSkybox => arrivalSkybox;
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

            if (gameplaySkybox == null)
            {
                reason = "gameplay_skybox_missing";
                return false;
            }

            if (arrivalSkybox == null)
            {
                reason = "arrival_skybox_missing";
                return false;
            }

            if (externalThreatWeights == null || externalThreatWeights.Count == 0)
            {
                reason = "external_threat_weights_missing";
                return false;
            }

            var eventIds = new HashSet<EventId>();
            for (var index = 0; index < externalThreatWeights.Count; index++)
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

            if (maximumActiveExternalThreats <= 0)
            {
                reason = $"maximum_active_external_threats_not_positive:value={maximumActiveExternalThreats}";
                return false;
            }

            if (internalAccidentWeights == null || internalAccidentWeights.Count == 0)
            {
                reason = "internal_accident_weights_missing";
                return false;
            }

            var accidentIds = new HashSet<PHSShipAccidentId>();
            for (var index = 0; index < internalAccidentWeights.Count; index++)
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

            if (maximumActiveInternalAccidents <= 0)
            {
                reason = $"maximum_active_internal_accidents_not_positive:value={maximumActiveInternalAccidents}";
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
