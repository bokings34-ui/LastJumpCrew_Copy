using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHS0717ValidationBuildExporter
    {
        private const string OutputPath = "Builds/PHS0717Validation/LastJumpCrew.exe";
        private const string InputDeviceSmokeOutputPath = "Builds/PHSInputDeviceSmoke/LastJumpCrew.exe";
        private const string TutorialLobbyOutputPath =
            "Builds/TutorialLobby/LastJumpCrew.exe";
        private static readonly string[] TutorialLobbyScenePaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity"
        };

        public static void BuildTutorialLobbyPlayer()
        {
            PHS20260812ReleaseValidator.Validate();
            var absoluteOutputPath = Path.GetFullPath(TutorialLobbyOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)
                                      ?? throw new InvalidOperationException(
                                          "PHS_TUTORIAL_LOBBY_BUILD_FAILED reason=output_directory_missing"));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = TutorialLobbyScenePaths,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"PHS_TUTORIAL_LOBBY_BUILD_FAILED result={report.summary.result} errors={report.summary.totalErrors}");
            }

            Debug.Log(
                $"PHS_TUTORIAL_LOBBY_BUILD_OK path={TutorialLobbyOutputPath} size={report.summary.totalSize}");
        }

        [MenuItem("Tools/ParkHanSol/Build 0717 P0 Validation Player")]
        public static void BuildValidationPlayer()
        {
            PHS20260812ReleaseValidator.Validate();

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "PHS_0717_VALIDATION_BUILD_FAILED reason=enabled_scenes_missing");
            }

            var absoluteOutputPath = Path.GetFullPath(OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)
                                      ?? throw new InvalidOperationException(
                                          "PHS_0717_VALIDATION_BUILD_FAILED reason=output_directory_missing"));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"PHS_0717_VALIDATION_BUILD_FAILED result={report.summary.result} errors={report.summary.totalErrors}");
            }

            Debug.Log(
                $"PHS_0717_VALIDATION_BUILD_OK path={OutputPath} size={report.summary.totalSize}");
        }

        [MenuItem("Tools/ParkHanSol/Build Input Device Smoke Player")]
        public static void BuildInputDeviceSmokePlayer()
        {
            PHS20260812ReleaseValidator.Validate();
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException(
                    "PHS_INPUT_DEVICE_BUILD_FAILED reason=enabled_scenes_missing");
            }

            var absoluteOutputPath = Path.GetFullPath(InputDeviceSmokeOutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath)
                                      ?? throw new InvalidOperationException(
                                          "PHS_INPUT_DEVICE_BUILD_FAILED reason=output_directory_missing"));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"PHS_INPUT_DEVICE_BUILD_FAILED result={report.summary.result} errors={report.summary.totalErrors}");
            }

            Debug.Log(
                $"PHS_INPUT_DEVICE_BUILD_OK path={InputDeviceSmokeOutputPath} size={report.summary.totalSize}");
        }
    }
}
