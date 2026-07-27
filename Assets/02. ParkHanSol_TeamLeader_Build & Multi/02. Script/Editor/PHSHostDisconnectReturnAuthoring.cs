using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSHostDisconnectReturnAuthoring
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Host Disconnect Return")]
        public static void Author()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var managers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NetworkManager>(true))
                .ToArray();
            if (managers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_HOST_DISCONNECT_RETURN_AUTHOR_FAILED reason=network_manager_count:{managers.Length}");
            }

            var manager = managers[0];
            var controller = manager.GetComponent<NetworkHostDisconnectReturnController>()
                ?? manager.gameObject.AddComponent<NetworkHostDisconnectReturnController>();
            var state = new SerializedObject(controller);
            state.FindProperty("networkManager").objectReferenceValue = manager;
            state.FindProperty("lobbySceneName").stringValue = "ParkHanSol_LobbyScene";
            state.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    "PHS_HOST_DISCONNECT_RETURN_AUTHOR_FAILED reason=scene_save_failed");
            }

            Debug.Log(
                "PHS_HOST_DISCONNECT_RETURN_AUTHOR_OK controller=1 " +
                "scene=ParkHanSol_LobbyScene");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Host Disconnect Return")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<NetworkHostDisconnectReturnController>(true))
                .ToArray();
            if (controllers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_HOST_DISCONNECT_RETURN_VALIDATION_FAILED reason=controller_count:{controllers.Length}");
            }

            var state = new SerializedObject(controllers[0]);
            var manager = state.FindProperty("networkManager").objectReferenceValue as NetworkManager;
            var lobbySceneName = state.FindProperty("lobbySceneName").stringValue;
            if (manager == null || manager.gameObject != controllers[0].gameObject)
            {
                throw new InvalidOperationException(
                    "PHS_HOST_DISCONNECT_RETURN_VALIDATION_FAILED reason=network_manager_reference");
            }

            if (lobbySceneName != "ParkHanSol_LobbyScene")
            {
                throw new InvalidOperationException(
                    $"PHS_HOST_DISCONNECT_RETURN_VALIDATION_FAILED reason=lobby_scene:{lobbySceneName}");
            }

            Debug.Log(
                "PHS_HOST_DISCONNECT_RETURN_VALIDATION_PASS controller=1 " +
                "networkManager=same_object lobby=ParkHanSol_LobbyScene");
        }
    }
}
