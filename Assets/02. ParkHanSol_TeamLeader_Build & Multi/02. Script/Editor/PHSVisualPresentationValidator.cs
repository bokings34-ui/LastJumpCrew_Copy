using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Keeps visual presentation validation focused on required references and
    /// missing scripts. Layout, object counts, and tuning remain editable.
    /// </summary>
    public static class PHSVisualPresentationValidator
    {
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string GameOverPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/GameOver/PHS_GameOverCinematicPresentation.prefab";
        private const string RunSessionRootPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";

        private static readonly string[] EventPresentationPrefabPaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_FireEventPresentation.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_OxygenLeakEventPresentation.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_PlayerAttackEnemyPresentation.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_DeviceAttackEnemyPresentation.prefab"
        };

        [MenuItem("Tools/ParkHanSol/Validate Visual Presentations")]
        public static void Validate()
        {
            var errors = new List<string>();
            CollectErrors(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_VISUAL_PRESENTATION_VALIDATE_FAILED count={errors.Count}\n- {string.Join("\n- ", errors)}");
            }

            Debug.Log("PHS_VISUAL_PRESENTATION_VALIDATE_OK game_over_refs=7 event_prefabs=4");
        }

        internal static void CollectErrors(ICollection<string> errors)
        {
            ValidateGameOverPrefab(errors);
            ValidateRunSessionRoot(errors);
            ValidateEventPresentationPrefabs(errors);
            ValidateMapScene(errors);
        }

        private static void ValidateGameOverPrefab(ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameOverPrefabPath);
            var presenter = prefab == null
                ? null
                : prefab.GetComponent<NetworkGameOverSequencePresenter>();
            Require(presenter != null, "visual_game_over_presenter_missing", errors);
            if (presenter == null)
            {
                return;
            }

            RequireNoMissingScripts(prefab, "visual_game_over_prefab", errors);
            var serialized = new SerializedObject(presenter);
            foreach (var propertyName in new[]
                     {
                         "visualRoot",
                         "cinematicCamera",
                         "playerShipRoot",
                         "enemyFleetRoot",
                         "fleetArrivalEffectRoot",
                         "barrageEffectRoot",
                         "explosionEffectRoot"
                     })
            {
                Require(
                    serialized.FindProperty(propertyName)?.objectReferenceValue != null,
                    $"visual_game_over_reference_missing property={propertyName}",
                    errors);
            }
        }

        private static void ValidateRunSessionRoot(ICollection<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunSessionRootPath);
            var coordinator = prefab == null
                ? null
                : prefab.GetComponent<NetworkGameOverSequenceCoordinator>();
            Require(coordinator != null, "visual_game_over_coordinator_missing", errors);
            if (prefab != null)
            {
                Require(prefab.GetComponent<Unity.Netcode.NetworkObject>() != null,
                    "visual_run_root_network_object_missing", errors);
                RequireNoMissingScripts(prefab, "visual_run_root_prefab", errors);
            }
        }

        private static void ValidateEventPresentationPrefabs(ICollection<string> errors)
        {
            foreach (var path in EventPresentationPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(prefab != null, $"visual_event_prefab_missing path={path}", errors);
                if (prefab != null)
                {
                    RequireNoMissingScripts(prefab, $"visual_event_prefab path={path}", errors);
                }
            }
        }

        private static void ValidateMapScene(ICollection<string> errors)
        {
            var originalSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
                var presenter = UnityEngine.Object.FindAnyObjectByType<NetworkEventEffectMirrorPresenter>(
                    FindObjectsInactive.Include);
                Require(presenter != null, "visual_event_mirror_presenter_missing", errors);
                if (presenter != null)
                {
                    var serialized = new SerializedObject(presenter);
                    foreach (var propertyName in new[]
                             {
                                 "firePresentationPrefab",
                                 "oxygenLeakPresentationPrefab",
                                 "playerAttackEnemyPresentationPrefab",
                                 "deviceAttackEnemyPresentationPrefab",
                                 "presentationRoot"
                             })
                    {
                        Require(
                            serialized.FindProperty(propertyName)?.objectReferenceValue != null,
                            $"visual_event_mirror_reference_missing property={propertyName}",
                            errors);
                    }
                }

                var gameOverRoot = GameObject.Find("PHS_GameOverPresentationRoot");
                Require(gameOverRoot != null, "visual_game_over_scene_root_missing", errors);
                if (gameOverRoot != null)
                {
                    Require(
                        gameOverRoot.GetComponent<NetworkGameOverSequencePresenter>() != null,
                        "visual_game_over_scene_presenter_missing",
                        errors);
                    RequireNoMissingScripts(gameOverRoot, "visual_game_over_scene_root", errors);
                }
            }
            finally
            {
                if (originalSceneSetup.Any(setup => setup.isLoaded && setup.isActive))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSceneSetup);
                }
            }
        }

        private static void RequireNoMissingScripts(
            GameObject root,
            string label,
            ICollection<string> errors)
        {
            var count = root.GetComponentsInChildren<Transform>(true)
                .Sum(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject));
            Require(count == 0, $"{label}_missing_scripts count={count}", errors);
        }

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }
    }
}
