#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHS0723RuntimeRepairAuthoring
    {
        private const string NoPlayerInteractLayerName = "NoPlayerInteract";
        private const string LegacyFireEffectPrefabPath =
            "Assets/04. NohSeokMin_Game Event/03_Prefab/Fire/Effect_Fire.prefab";
        private const string TeamFirePresentationPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/" +
            "Props/Prefabs/IncidentFire/PHS_TeamFirePatchPresentation.prefab";

        private static readonly string[] ScenePaths =
        {
            "Assets/01. MainGame/01. MainScene/Beta/ParkHanSol_LobbyScene.unity",
            "Assets/01. MainGame/01. MainScene/Beta/PHS_Map_ver1.unity",
            "Assets/01. MainGame/01. MainScene/Beta/PHS_ExteriorShopScene.unity",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/test/PHS_FeatureInspectionScene.unity"
        };

        [MenuItem("Tools/ParkHanSol/Repair 0723 Runtime Assets")]
        public static void RepairRuntimeAssets()
        {
            PHS0723OxygenZoneAuthoring.AuthorRuntimePrefab();
            DisableLegacyFirePlaceholder();

            var noPlayerInteractLayer = LayerMask.NameToLayer(
                NoPlayerInteractLayerName);
            if (noPlayerInteractLayer < 0)
            {
                throw new InvalidOperationException(
                    "PHS_0723_RUNTIME_REPAIR_FAILED " +
                    "reason=no_player_interact_layer_missing");
            }

            var networkObjectCount = 0;
            var gravityVolumeCount = 0;
            var fireZoneCount = 0;
            var teamFirePresentation = AssetDatabase.LoadAssetAtPath<GameObject>(
                TeamFirePresentationPrefabPath);
            if (teamFirePresentation == null)
            {
                throw new InvalidOperationException(
                    "PHS_0723_RUNTIME_REPAIR_FAILED " +
                    "reason=team_fire_presentation_missing");
            }

            foreach (var scenePath in ScenePaths)
            {
                RepairScene(
                    scenePath,
                    noPlayerInteractLayer,
                    teamFirePresentation,
                    ref networkObjectCount,
                    ref gravityVolumeCount,
                    ref fireZoneCount);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"PHS_0723_RUNTIME_REPAIR_OK scenes={ScenePaths.Length} " +
                $"networkObjects={networkObjectCount} " +
                $"gravityVolumes={gravityVolumeCount} " +
                $"fireZones={fireZoneCount}");
        }

        private static void RepairScene(
            string scenePath,
            int noPlayerInteractLayer,
            GameObject teamFirePresentation,
            ref int networkObjectCount,
            ref int gravityVolumeCount,
            ref int fireZoneCount)
        {
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    "PHS_0723_RUNTIME_REPAIR_FAILED reason=scene_missing",
                    scenePath);
            }

            var scene = EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var networkObjects = roots
                .SelectMany(root =>
                    root.GetComponentsInChildren<NetworkObject>(true))
                .ToArray();
            var validateMethod = typeof(NetworkObject).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (validateMethod == null)
            {
                throw new MissingMethodException(
                    typeof(NetworkObject).FullName,
                    "OnValidate");
            }

            foreach (var networkObject in networkObjects)
            {
                validateMethod.Invoke(networkObject, null);
                EditorUtility.SetDirty(networkObject);
            }

            var gravityVolumes = roots
                .SelectMany(root =>
                    root.GetComponentsInChildren<NetworkPlayerGravityArea>(true))
                .ToArray();
            foreach (var gravityVolume in gravityVolumes)
            {
                gravityVolume.gameObject.layer = noPlayerInteractLayer;
                EditorUtility.SetDirty(gravityVolume.gameObject);
            }

            var fireZones = roots
                .SelectMany(root =>
                    root.GetComponentsInChildren<PHSFireZone>(true))
                .ToArray();
            foreach (var fireZone in fireZones)
            {
                var serializedFireZone = new SerializedObject(fireZone);
                var presentationProperty = serializedFireZone.FindProperty(
                    "patchPresentationPrefab");
                if (presentationProperty == null)
                {
                    throw new InvalidOperationException(
                        "PHS_0723_RUNTIME_REPAIR_FAILED " +
                        $"reason=fire_presentation_property_missing zone={fireZone.name}");
                }

                presentationProperty.objectReferenceValue = teamFirePresentation;
                serializedFireZone.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fireZone);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"PHS_0723_RUNTIME_REPAIR_FAILED " +
                    $"reason=scene_save_failed path={scenePath}");
            }

            networkObjectCount += networkObjects.Length;
            gravityVolumeCount += gravityVolumes.Length;
            fireZoneCount += fireZones.Length;
            Debug.Log(
                $"PHS_0723_SCENE_REPAIRED path={scenePath} " +
                $"networkObjects={networkObjects.Length} " +
                $"gravityVolumes={gravityVolumes.Length} " +
                $"fireZones={fireZones.Length}");
        }

        private static void DisableLegacyFirePlaceholder()
        {
            var root = PrefabUtility.LoadPrefabContents(
                LegacyFireEffectPrefabPath);
            try
            {
                var placeholderRenderer = root.GetComponent<MeshRenderer>();
                if (placeholderRenderer == null)
                {
                    throw new InvalidOperationException(
                        "PHS_0723_RUNTIME_REPAIR_FAILED " +
                        "reason=legacy_fire_renderer_missing");
                }

                placeholderRenderer.enabled = false;
                EditorUtility.SetDirty(placeholderRenderer);
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    LegacyFireEffectPrefabPath,
                    out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "PHS_0723_RUNTIME_REPAIR_FAILED " +
                        "reason=legacy_fire_prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
