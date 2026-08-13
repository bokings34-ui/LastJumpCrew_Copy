#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Items;
using SM;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSTeamEventStabilityAuthoring
    {
        private const string EnemyHitEffectMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/05. Material/Items/Feedback/PHS_WrenchSpark.mat";
        private const string EnemyScoutSamplePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventSamples/PHS_EnemyScoutEventContentSample.prefab";
        private const string RunSessionRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/" +
            "PHS_NetworkRunSessionRoot.prefab";
        private const float DeviceEnemyAttackRange = 3f;
        private const float DeviceEnemyStoppingDistance = 1.2f;

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/04. NohSeokMin_Game Event/03_Prefab/Enemy/Enemy_AT_Player.prefab",
            "Assets/04. NohSeokMin_Game Event/03_Prefab/Enemy/Enemy_AT_Device.prefab"
        };

        private static readonly string[] EnemyPresentationPrefabPaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_PlayerAttackEnemyPresentation.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_DeviceAttackEnemyPresentation.prefab"
        };

        private static readonly string[] ProfilePaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps/PHS_Map_8001_WasteOrbit.asset",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps/PHS_Map_8002_AsteroidField.asset",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps/PHS_Map_8003_BrokenSatellites.asset",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps/PHS_Map_8004_NebulaDebris.asset"
        };

        public static void Author()
        {
            foreach (var profilePath in ProfilePaths)
            {
                var profile = AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>(profilePath);
                if (profile == null)
                {
                    Debug.LogError($"PHS_TEAM_EVENT_STABILITY_FAILED reason=profile_missing path={profilePath}");
                    return;
                }

                var serializedProfile = new SerializedObject(profile);
                var internalWeights = serializedProfile.FindProperty("internalAccidentWeights");
                var maximumInternal = serializedProfile.FindProperty("maximumActiveInternalAccidents");
                if (internalWeights == null || maximumInternal == null)
                {
                    Debug.LogError($"PHS_TEAM_EVENT_STABILITY_FAILED reason=legacy_profile_property_missing path={profilePath}");
                    return;
                }

                internalWeights.arraySize = 0;
                maximumInternal.intValue = 0;
                serializedProfile.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);

                if (!profile.TryValidate(out var profileReason))
                {
                    Debug.LogError($"PHS_TEAM_EVENT_STABILITY_FAILED reason=profile_invalid path={profilePath}:{profileReason}");
                    return;
                }
            }

            AssetDatabase.SaveAssets();
            PHSShipMapRealtimeAuthoring.Author();
            AlignEventRoomsToMap();
            RepairEnemyGameplayStats();
            RepairEnemyOrangeVisuals();
            RepairEnemyHitEffectVisuals();
            RepairEnemyPresentationHitFeedbackVisuals();
            ValidateMapRuntimeReferences();
            Debug.Log("PHS_TEAM_EVENT_STABILITY_AUTHORING_OK profiles=4 map_render_rig=authored");
        }

        public static void RepairEnemyHitEffectVisuals()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EnemyHitEffectMaterialPath);
            if (material == null)
            {
                throw new System.InvalidOperationException(
                    $"PHS_ENEMY_HIT_VFX_REPAIR_FAILED reason=material_missing path={EnemyHitEffectMaterialPath}");
            }

            foreach (var prefabPath in EnemyPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var enemy = root.GetComponent<EnemyBase>();
                    if (enemy == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_VFX_REPAIR_FAILED reason=enemy_component_missing path={prefabPath}");
                    }

                    var serializedEnemy = new SerializedObject(enemy);
                    var effect = serializedEnemy.FindProperty("hitEffect")?.objectReferenceValue as ParticleSystem;
                    if (effect == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_VFX_REPAIR_FAILED reason=hit_effect_missing path={prefabPath}");
                    }

                    var particleRenderer = effect.GetComponent<ParticleSystemRenderer>();
                    if (particleRenderer == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_VFX_REPAIR_FAILED reason=particle_renderer_missing path={prefabPath}");
                    }

                    particleRenderer.sharedMaterial = material;
                    var main = effect.main;
                    effect.transform.localPosition = new Vector3(0f, 1.1f, 0.38f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.055f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
                    EditorUtility.SetDirty(effect);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            ValidateEnemyHitEffectVisuals();
            Debug.Log("PHS_ENEMY_HIT_VFX_REPAIR_OK prefabs=2 size=0.055 lifetime=0.30");
        }

        public static void RepairEnemyCombatFeedback()
        {
            RepairEnemyGameplayStats();
            RepairEnemyOrangeVisuals();
            RepairEnemyHitEffectVisuals();
            RepairEnemyPresentationHitFeedbackVisuals();
            Debug.Log("PHS_ENEMY_COMBAT_FEEDBACK_REPAIR_OK prefabs=4 max_health=30");
        }

        public static void RepairEnemyOrangeVisuals()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EnemyHitEffectMaterialPath);
            if (material == null)
            {
                throw new System.InvalidOperationException(
                    $"PHS_ENEMY_ORANGE_REPAIR_FAILED reason=hit_material_missing path={EnemyHitEffectMaterialPath}");
            }

            material.SetColor("_BaseColor", TeamRepairToolVisualPalette.EnemyOrange);
            material.SetColor("_Color", TeamRepairToolVisualPalette.EnemyOrange);
            material.SetColor("_EmissionColor", TeamRepairToolVisualPalette.EnemyOrange * 2.5f);
            EditorUtility.SetDirty(material);

            var root = PrefabUtility.LoadPrefabContents(EnemyScoutSamplePrefabPath);
            try
            {
                var lights = root.GetComponentsInChildren<Light>(true);
                if (lights.Length == 0)
                {
                    throw new System.InvalidOperationException(
                        $"PHS_ENEMY_ORANGE_REPAIR_FAILED reason=sample_light_missing path={EnemyScoutSamplePrefabPath}");
                }

                foreach (var light in lights)
                {
                    light.color = TeamRepairToolVisualPalette.EnemyOrange;
                    EditorUtility.SetDirty(light);
                }

                PrefabUtility.SaveAsPrefabAsset(root, EnemyScoutSamplePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            ValidateEnemyOrangeVisuals();
            Debug.Log("PHS_ENEMY_ORANGE_REPAIR_OK hit_vfx=true sample_lighting=true");
        }

        public static void RepairEnemyGameplayStats()
        {
            foreach (var prefabPath in EnemyPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var enemy = root.GetComponent<EnemyBase>();
                    if (enemy == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_STATS_REPAIR_FAILED reason=enemy_component_missing path={prefabPath}");
                    }

                    var serializedEnemy = new SerializedObject(enemy);
                    serializedEnemy.FindProperty("maxHealth").floatValue = 30f;
                    if (enemy is DeviceAttackEnemy)
                    {
                        serializedEnemy.FindProperty("attackRange").floatValue =
                            DeviceEnemyAttackRange;
                        var agent = root.GetComponent<NavMeshAgent>();
                        if (agent == null)
                        {
                            throw new System.InvalidOperationException(
                                $"PHS_ENEMY_STATS_REPAIR_FAILED reason=navmesh_agent_missing path={prefabPath}");
                        }

                        agent.stoppingDistance = DeviceEnemyStoppingDistance;
                        EditorUtility.SetDirty(agent);
                    }
                    serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            ValidateEnemyGameplayStats();
            Debug.Log("PHS_ENEMY_STATS_REPAIR_OK prefabs=2 max_health=30");
        }

        public static void RepairEnemyPresentationHitFeedbackVisuals()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EnemyHitEffectMaterialPath);
            if (material == null)
            {
                throw new System.InvalidOperationException(
                    $"PHS_ENEMY_HIT_PRESENTATION_REPAIR_FAILED reason=material_missing path={EnemyHitEffectMaterialPath}");
            }

            foreach (var prefabPath in EnemyPresentationPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var view = root.GetComponent<EventEffectPresentationView>();
                    var animator = root.GetComponentInChildren<Animator>(true);
                    if (view == null || animator == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_PRESENTATION_REPAIR_FAILED reason=view_or_animator_missing path={prefabPath}");
                    }

                    var effectTransform = root.transform.Find("PHS_EnemyHitEffect");
                    if (effectTransform == null)
                    {
                        effectTransform = new GameObject("PHS_EnemyHitEffect").transform;
                        effectTransform.SetParent(root.transform, false);
                    }

                    effectTransform.localPosition = new Vector3(0f, 1.1f, 0.25f);
                    var effect = effectTransform.GetComponent<ParticleSystem>();
                    if (effect == null)
                    {
                        effect = effectTransform.gameObject.AddComponent<ParticleSystem>();
                    }

                    var main = effect.main;
                    main.loop = false;
                    main.duration = 0.15f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f);
                    main.maxParticles = 16;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    var emission = effect.emission;
                    emission.enabled = true;
                    emission.rateOverTime = 0f;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

                    var shape = effect.shape;
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.16f;

                    var particleRenderer = effect.GetComponent<ParticleSystemRenderer>();
                    particleRenderer.sharedMaterial = material;

                    var serializedView = new SerializedObject(view);
                    serializedView.FindProperty("enemyAnimator").objectReferenceValue = animator;
                    serializedView.FindProperty("enemyHitEffect").objectReferenceValue = effect;
                    serializedView.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            ValidateEnemyPresentationHitFeedbackVisuals();
            Debug.Log("PHS_ENEMY_HIT_PRESENTATION_REPAIR_OK prefabs=2 burst=12");
        }

        public static void ValidateMapRuntimeReferences()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity",
                OpenSceneMode.Single);
            var layout = Object.FindFirstObjectByType<PHSShipMapWorldLayout>(FindObjectsInactive.Include);
            var rig = Object.FindFirstObjectByType<PHSShipMapRenderRig>(FindObjectsInactive.Include);
            if (layout == null || rig == null || rig.MapCamera == null || rig.MapTexture == null
                || rig.MapTexture.width != 240 || rig.MapTexture.height != 720
                || rig.MapCamera.targetTexture != rig.MapTexture)
            {
                throw new System.InvalidOperationException(
                    "PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=scene_map_render_reference_invalid");
            }

            ValidateEventRoomMapPositions();
            ValidateEnemyHitEffectVisuals();
            ValidateEnemyOrangeVisuals();
            ValidateEnemyGameplayStats();
            ValidateEnemyPresentationHitFeedbackVisuals();

            var playerRoot = PrefabUtility.LoadPrefabContents(
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab");
            try
            {
                var controller = playerRoot.GetComponentInChildren<PHSHandheldShipMapController>(true);
                if (controller == null)
                {
                    throw new System.InvalidOperationException(
                        "PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=map_controller_missing");
                }

                var serializedController = new SerializedObject(controller);
                ValidateMapView(
                    serializedController.FindProperty("firstPersonView")?.objectReferenceValue as PHSHandheldShipMapView,
                    "first_person");
                ValidateMapView(
                    serializedController.FindProperty("worldView")?.objectReferenceValue as PHSHandheldShipMapView,
                    "world");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }

            Debug.Log("PHS_TEAM_EVENT_STABILITY_VALIDATE_PASS scene=map_ver1 views=2 texture=240x720");
        }

        private static void AlignEventRoomsToMap()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new System.InvalidOperationException(
                    "PHS_TEAM_EVENT_STABILITY_FAILED reason=active_scene_missing");
            }
            var runRoot = PrefabUtility.LoadPrefabContents(RunSessionRootPrefabPath);
            try
            {
                var rooms = runRoot.GetComponentsInChildren<ShipRoom>(true);
                foreach (var binding in EventRoomBindings)
                {
                    var matches = rooms.Where(room => room.RoomId == binding.RoomId).ToArray();
                    var station = FindTransformInScene(scene, binding.AnchorName);
                    if (matches.Length != 1 || station == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_TEAM_EVENT_STABILITY_FAILED reason=room_station_missing " +
                            $"room={binding.RoomId} station={binding.AnchorName} rooms={matches.Length}");
                    }

                    matches[0].transform.position = station.position;
                    EditorUtility.SetDirty(matches[0].transform);
                }

                PrefabUtility.SaveAsPrefabAsset(runRoot, RunSessionRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(runRoot);
            }
        }

        private static void ValidateEventRoomMapPositions()
        {
            var scene = SceneManager.GetActiveScene();
            var runRoot = AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPrefabPath);
            if (runRoot == null)
            {
                throw new System.InvalidOperationException(
                    "PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=persistent_run_root_missing");
            }

            var rooms = runRoot.GetComponentsInChildren<ShipRoom>(true);
            foreach (var binding in EventRoomBindings)
            {
                var matches = rooms.Where(room => room.RoomId == binding.RoomId).ToArray();
                var station = FindTransformInScene(scene, binding.AnchorName);
                if (matches.Length != 1 || station == null
                    || (matches[0].transform.position - station.position).sqrMagnitude > 0.0001f)
                {
                    throw new System.InvalidOperationException(
                        $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=room_map_position_invalid " +
                        $"room={binding.RoomId} station={binding.AnchorName} rooms={matches.Length}");
                }
            }
        }

        private static Transform FindTransformInScene(Scene scene, string objectName)
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == objectName)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private readonly struct EventRoomBinding
        {
            public EventRoomBinding(string roomId, string anchorName)
            {
                RoomId = roomId;
                AnchorName = anchorName;
            }

            public string RoomId { get; }
            public string AnchorName { get; }
        }

        private static readonly EventRoomBinding[] EventRoomBindings =
        {
            new("Room A", "PHS_Utility_BatteryStation_RoomA"),
            new("Room B", "PHS_Utility_BatteryStation"),
            new("Room C", "PHS_Utility_BatteryStation_RoomC"),
            new("중앙 복도", "PHS_Utility_BatteryStation_RoomD")
        };

        private static void ValidateMapView(PHSHandheldShipMapView view, string kind)
        {
            if (view == null)
            {
                throw new System.InvalidOperationException(
                    $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=view_reference_missing view={kind}");
            }

            var serializedView = new SerializedObject(view);
            var requiredReferences = new[]
            {
                "deviceRoot", "mapImage", "markerRoot", "markerTemplate", "markerGlyphTemplate",
                "markerLabelTemplate", "currentMapText", "mapDetailText", "runPhaseText", "warpFill",
                "warpValueText", "shipHpFill", "shipHpValueText", "eventOverflowText"
            };
            foreach (var propertyName in requiredReferences)
            {
                if (serializedView.FindProperty(propertyName)?.objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=view_property_missing view={kind} property={propertyName}");
                }
            }

            ValidateObjectArray(serializedView, "eventRows", kind);
            ValidateObjectArray(serializedView, "eventIcons", kind);
            var iconProperties = new[]
            {
                "fireIcon", "powerFailureIcon", "deviceFailureIcon", "hullBreachIcon", "steamLeakIcon",
                "oxygenFailureIcon", "gravityFailureIcon", "enemySpawnIcon", "patrolZoneIcon", "meteorZoneIcon",
                "nebulaZoneIcon", "planetZoneIcon", "powerSyncIcon", "cannonIcon", "wireFixIcon", "warpIcon",
                "batteryIcon", "wrenchIcon", "fireExtinguisherIcon"
            };
            foreach (var propertyName in iconProperties)
            {
                if (serializedView.FindProperty(propertyName)?.objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=view_icon_missing view={kind} property={propertyName}");
                }
            }
        }

        private static void ValidateEnemyHitEffectVisuals()
        {
            var expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(EnemyHitEffectMaterialPath);
            foreach (var prefabPath in EnemyPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var enemy = root.GetComponent<EnemyBase>();
                    var effect = enemy == null
                        ? null
                        : new SerializedObject(enemy).FindProperty("hitEffect")?.objectReferenceValue as ParticleSystem;
                    var particleRenderer = effect == null
                        ? null
                        : effect.GetComponent<ParticleSystemRenderer>();
                    if (effect == null
                        || particleRenderer == null
                        || particleRenderer.sharedMaterial != expectedMaterial
                        || effect.main.startSize.constant < 0.05f
                        || effect.main.startLifetime.constant < 0.25f)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_VFX_VALIDATE_FAILED path={prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateEnemyOrangeVisuals()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(EnemyHitEffectMaterialPath);
            if (material == null
                || material.GetColor("_BaseColor") != TeamRepairToolVisualPalette.EnemyOrange
                || material.GetColor("_Color") != TeamRepairToolVisualPalette.EnemyOrange)
            {
                throw new System.InvalidOperationException("PHS_ENEMY_ORANGE_VALIDATE_FAILED reason=hit_material_color_invalid");
            }

            var root = PrefabUtility.LoadPrefabContents(EnemyScoutSamplePrefabPath);
            try
            {
                var lights = root.GetComponentsInChildren<Light>(true);
                if (lights.Length == 0 || lights.Any(light => light.color != TeamRepairToolVisualPalette.EnemyOrange))
                {
                    throw new System.InvalidOperationException("PHS_ENEMY_ORANGE_VALIDATE_FAILED reason=sample_light_color_invalid");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateEnemyGameplayStats()
        {
            foreach (var prefabPath in EnemyPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var enemy = root.GetComponent<EnemyBase>();
                    var agent = root.GetComponent<NavMeshAgent>();
                    var deviceEnemyValid = enemy is not DeviceAttackEnemy
                        || (Mathf.Abs(enemy.AttackRange - DeviceEnemyAttackRange) <= 0.001f
                            && agent != null
                            && Mathf.Abs(
                                agent.stoppingDistance - DeviceEnemyStoppingDistance)
                                <= 0.001f);
                    if (enemy == null || enemy.MaxHealth != 30f || !deviceEnemyValid)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_STATS_VALIDATE_FAILED path={prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateEnemyPresentationHitFeedbackVisuals()
        {
            foreach (var prefabPath in EnemyPresentationPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var view = root.GetComponent<EventEffectPresentationView>();
                    var serializedView = view == null ? null : new SerializedObject(view);
                    if (serializedView?.FindProperty("enemyAnimator")?.objectReferenceValue == null
                        || serializedView.FindProperty("enemyHitEffect")?.objectReferenceValue == null)
                    {
                        throw new System.InvalidOperationException(
                            $"PHS_ENEMY_HIT_PRESENTATION_VALIDATE_FAILED path={prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateObjectArray(SerializedObject view, string propertyName, string kind)
        {
            var property = view.FindProperty(propertyName);
            if (property == null || property.arraySize == 0)
            {
                throw new System.InvalidOperationException(
                    $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=view_array_missing view={kind} property={propertyName}");
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                if (property.GetArrayElementAtIndex(index).objectReferenceValue == null)
                {
                    throw new System.InvalidOperationException(
                        $"PHS_TEAM_EVENT_STABILITY_VALIDATE_FAILED reason=view_array_element_missing view={kind} property={propertyName} index={index}");
                }
            }
        }
    }
}
#endif
