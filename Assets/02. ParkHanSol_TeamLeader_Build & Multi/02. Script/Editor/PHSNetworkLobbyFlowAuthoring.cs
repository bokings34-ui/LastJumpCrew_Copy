using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using LastJumpCrew.ParkHanSol.Multiplayer.RunFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkLobbyFlowAuthoring
    {
        private const string LobbyPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab";
        private const string SinglePlayButtonName = "Single Play Button";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Lobby Tutorial Customize Single Flow")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(LobbyPrefabPath);
            try
            {
                var frontend = RequireSingle<
                    NetworkLobbyCustomizationFrontendController>(root);
                frontend.gameObject.SetActive(true);
                var panel = frontend.transform.Find("CustomizationPanel");
                if (panel == null)
                {
                    throw Failure("customization_panel_missing");
                }

                panel.gameObject.SetActive(false);
                var frontendSerialized = new SerializedObject(frontend);
                var catalog = frontendSerialized
                    .FindProperty("catalog")
                    ?.objectReferenceValue as CosmeticCatalog;
                if (catalog == null)
                {
                    throw Failure("catalog_missing");
                }

                var localService = frontend.GetComponent<
                    LobbyLocalCustomizationService>();
                if (localService == null)
                {
                    localService = frontend.gameObject.AddComponent<
                        LobbyLocalCustomizationService>();
                }

                var serviceSerialized = new SerializedObject(localService);
                serviceSerialized.FindProperty("catalog").objectReferenceValue =
                    catalog;
                serviceSerialized.FindProperty("startingCredits").intValue = 300;
                serviceSerialized.FindProperty("maximumCredits").intValue =
                    999999;
                serviceSerialized.ApplyModifiedPropertiesWithoutUndo();

                frontendSerialized.Update();
                frontendSerialized.FindProperty("localService")
                    .objectReferenceValue = localService;
                frontendSerialized.ApplyModifiedPropertiesWithoutUndo();

                foreach (var staleLauncher in root.GetComponentsInChildren<
                             LocalHostGameSessionLauncher>(true))
                {
                    if (staleLauncher.gameObject != root)
                    {
                        UnityEngine.Object.DestroyImmediate(staleLauncher);
                    }
                }

                var launcher = root.GetComponent<
                    LocalHostGameSessionLauncher>();
                if (launcher == null)
                {
                    launcher = root.AddComponent<
                        LocalHostGameSessionLauncher>();
                }

                var singlePlayButton = root
                    .GetComponentsInChildren<Button>(true)
                    .SingleOrDefault(button =>
                        button.name == SinglePlayButtonName);
                if (singlePlayButton == null)
                {
                    throw Failure("single_play_button_missing");
                }

                var launcherSerialized = new SerializedObject(launcher);
                launcherSerialized.FindProperty("singlePlayButton")
                    .objectReferenceValue = singlePlayButton;
                launcherSerialized.FindProperty("playSceneName")
                    .stringValue = "PHS_Map_ver1";
                launcherSerialized.FindProperty("launchTimeoutSeconds")
                    .floatValue = 15f;
                launcherSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
                Debug.Log(
                    "PHS_NETWORK_LOBBY_FLOW_AUTHORING_OK " +
                    $"prefab={LobbyPrefabPath} " +
                    $"frontendActive={frontend.gameObject.activeSelf} " +
                    $"panelActive={panel.gameObject.activeSelf} " +
                    $"singleButton={singlePlayButton.name}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T RequireSingle<T>(GameObject root)
            where T : Component
        {
            var matches = root.GetComponentsInChildren<T>(true);
            if (matches.Length != 1)
            {
                throw Failure(
                    $"component_count type={typeof(T).Name} count={matches.Length}");
            }

            return matches[0];
        }

        private static InvalidOperationException Failure(string reason)
        {
            return new InvalidOperationException(
                $"PHS_NETWORK_LOBBY_FLOW_AUTHORING_FAILED reason={reason}");
        }
    }
}
