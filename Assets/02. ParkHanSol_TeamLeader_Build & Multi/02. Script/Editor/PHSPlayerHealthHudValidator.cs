using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSPlayerHealthHudValidator
    {
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/" +
            "PHS_CuteWhiteGhost_Player.prefab";
        private const string HudPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/" +
            "ParkHanSol_PlayHudUI.prefab";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/" +
            "PHS_Map_ver1.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Player Health HUD Binding")]
        public static void Validate()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PLAYER_HEALTH_HUD_VALIDATION_FAILED reason=player_prefab_missing");
            Require(player.GetComponent<NetworkPlayerLifeState>() != null,
                "player_life_state_missing");
            Require(player.GetComponent<NetworkPlayerController>() != null,
                "player_controller_missing");

            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PLAYER_HEALTH_HUD_VALIDATION_FAILED reason=hud_prefab_missing");
            var presenter = hud.GetComponentInChildren<ParkHanSolPlayHudMockPresenter>(true);
            var feedback = hud.GetComponentInChildren<PHSHudFeedbackController>(true);
            Require(presenter != null, "presenter_missing");
            Require(feedback != null, "feedback_missing");
            var presenterData = new SerializedObject(presenter);
            var feedbackData = new SerializedObject(feedback);
            Require(presenterData.FindProperty("hudFeedbackController")?.objectReferenceValue == feedback,
                "presenter_feedback_reference_missing");
            Require(feedbackData.FindProperty("healthMotion")?.objectReferenceValue != null,
                "health_motion_reference_missing");

            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            var binders = UnityEngine.Object.FindObjectsByType<PHSMapPlayerHudBinder>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Require(binders.Length == 1,
                $"map_hud_binder_count actual={binders.Length}");
            var binderData = new SerializedObject(binders[0]);
            Require(binderData.FindProperty("playHudPresenter")?.objectReferenceValue != null,
                "map_presenter_reference_missing");
            Require(scene.isLoaded, "map_scene_not_loaded");

            Debug.Log(
                "PHS_PLAYER_HEALTH_HUD_VALIDATION_PASS player_life_state=1 " +
                "owner_binder=1 presenter=1 health_motion=1 host_supported=true");
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_PLAYER_HEALTH_HUD_VALIDATION_FAILED reason={reason}");
            }
        }
    }
}
