using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSMapVer3RedesignValidator
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string FireExtinguisherDataPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FireExtinguisherItemPrefabData.asset";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Map Ver3 Redesign")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var errors = new List<string>();
            Require(Find(scene, "PHS_Map_Runtime/MapEnvironmentRoot/THJ_Map_Ver3_ShipRoot")?.gameObject.activeInHierarchy == true, "map_ver3_root_inactive", errors);
            Require(Find(scene, "Cube")?.gameObject.activeSelf == false, "legacy_cube_active", errors);
            Require(Find(scene, "PHS_ShipAccessRetrofit/PHS_EntryWing_A")?.gameObject.activeSelf == false, "legacy_entry_a_active", errors);
            Require(Find(scene, "PHS_ShipAccessRetrofit/PHS_EntryWing_B")?.gameObject.activeSelf == false, "legacy_entry_b_active", errors);
            var legacyExteriorShell = Find(
                scene,
                "PHS_ShipAccessRetrofit/PHS_ExteriorCollisionShell");
            Require(
                legacyExteriorShell == null || !legacyExteriorShell.gameObject.activeSelf,
                "legacy_exterior_collision_shell_active",
                errors);
            Require(
                Find(
                    scene,
                    "PHS_Map_Runtime/ExteriorDebrisSector/GameplayCluster/PHS_ExteriorCollisionShell")
                    ?.gameObject.activeInHierarchy == true,
                "exterior_collision_shell_inactive",
                errors);

            var gravityAreas = Find(scene, "PHS_Map_Runtime/GravityZones")
                .GetComponentsInChildren<NetworkPlayerGravityArea>(true);
            Require(gravityAreas.Length == 7, $"interior_gravity_area_count:{gravityAreas.Length}", errors);
            var serviceArea = gravityAreas.SingleOrDefault(area => area.name == "PHS_ServiceGravityArea");
            Require(serviceArea != null, "service_gravity_missing", errors);
            if (serviceArea != null)
            {
                var state = new SerializedObject(serviceArea);
                Require(state.FindProperty("priority").intValue == 1000, "service_gravity_priority", errors);
                Require(!state.FindProperty("canToggleShipGravity").boolValue, "service_gravity_toggleable", errors);
            }

            var interiorColliders = gravityAreas
                .Select(area => area.GetComponent<BoxCollider>())
                .Where(collider => collider != null)
                .ToArray();
            Require(interiorColliders.Length == 7, $"interior_gravity_count:{interiorColliders.Length}", errors);
            ValidateInteriorContainment(scene, errors);

            var anchors = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSShipAccidentAnchor>(true))
                .ToArray();
            Require(anchors.Length == 12, $"accident_anchor_count:{anchors.Length}", errors);
            Require(anchors.Select(anchor => anchor.AnchorId).Distinct(StringComparer.Ordinal).Count() == anchors.Length, "accident_anchor_duplicate", errors);
            foreach (var anchor in anchors)
            {
                Require(interiorColliders.Any(collider => collider.bounds.Contains(anchor.transform.position)), $"anchor_outside_gravity:{anchor.AnchorId}:{anchor.transform.position}", errors);
            }

            var locations = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSIncidentLocationAnchor>(true))
                .ToArray();
            Require(locations.Length == 20, $"incident_location_count:{locations.Length}", errors);
            Require(locations.Select(location => location.LocationId).Distinct(StringComparer.Ordinal).Count() == locations.Length, "incident_location_duplicate", errors);
            var fireZones = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSFireZone>(true))
                .ToArray();
            Require(fireZones.Length == 4, $"fire_zone_count:{fireZones.Length}", errors);
            ValidateFeedbackTuning(scene, errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"PHS_MAP_VER3_REDESIGN_VALIDATION_FAILED reason={error}");
                }
                throw new InvalidOperationException($"PHS_MAP_VER3_REDESIGN_VALIDATION_FAILED count={errors.Count}");
            }

            Debug.Log(
                "PHS_MAP_VER3_REDESIGN_VALIDATION_PASS interiorGravityAreas=7 " +
                "interiorGravity=7 accidentAnchors=12 incidentLocations=20 fireZones=4 " +
                "debrisFlow=bidirectional extinguisherEffects=SO");
        }

        private static void ValidateInteriorContainment(Scene scene, ICollection<string> errors)
        {
            var envelope = Find(
                scene,
                "PHS_Map_Runtime/PHS_InteriorContainment/PHS_InteriorSafetyEnvelope");
            var collider = envelope?.GetComponent<BoxCollider>();
            var volume = envelope?.GetComponent<NetworkInteriorContainmentVolume>();
            Require(collider != null && collider.isTrigger, "interior_safety_envelope_trigger_invalid", errors);
            Require(volume != null, "interior_safety_envelope_volume_missing", errors);
            if (collider == null)
            {
                return;
            }

            Require(collider.bounds.size.x >= 82f, "interior_safety_envelope_width_small", errors);
            Require(collider.bounds.size.y >= 22f, "interior_safety_envelope_height_small", errors);
            Require(collider.bounds.size.z >= 152f, "interior_safety_envelope_length_small", errors);
        }

        private static void ValidateFeedbackTuning(Scene scene, ICollection<string> errors)
        {
            var debrisStream = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSRandomDebrisStream>(true))
                .SingleOrDefault();
            Require(debrisStream != null, "debris_stream_missing", errors);
            if (debrisStream != null)
            {
                var streamState = new SerializedObject(debrisStream);
                Require(streamState.FindProperty("oppositeFlowChance").floatValue > 0f, "debris_opposite_flow_disabled", errors);
            }

            var extinguisher = AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(
                FireExtinguisherDataPath);
            Require(extinguisher != null, "extinguisher_data_missing", errors);
            if (extinguisher != null)
            {
                Require(
                    extinguisher.UseType == ItemUseType.Spray,
                    "extinguisher_use_type_invalid",
                    errors);
                Require(
                    extinguisher.HitEffects.Any(effect =>
                        effect.EffectType == ItemEffectType.Knockback
                        && effect.Amount > 0f),
                    "extinguisher_knockback_missing",
                    errors);
            }
        }

        private static Transform Find(Scene scene, string path)
        {
            var segments = path.Split('/');
            var root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == segments[0]);
            if (root == null)
            {
                return null;
            }

            var current = root.transform;
            for (var index = 1; index < segments.Length && current != null; index++)
            {
                current = current.Cast<Transform>().FirstOrDefault(child => child.name == segments[index]);
            }
            return current;
        }

        private static void Require(bool condition, string reason, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(reason);
            }
        }
    }
}
