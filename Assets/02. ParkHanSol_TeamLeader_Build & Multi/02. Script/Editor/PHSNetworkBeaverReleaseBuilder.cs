using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkBeaverReleaseBuilder
    {
        private const string LobbyScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string OutputPath =
            "Builds/BEAVER_2026/LastJumpCrew_BEAVER_2026.exe";

        private static readonly string[] ReleaseScenePaths =
        {
            LobbyScenePath,
            TutorialScenePath,
            MapScenePath,
            ShopScenePath
        };

        [MenuItem("Tools/ParkHanSol/Build BEAVER 2026 Release Player")]
        public static void BuildReleasePlayer()
        {
            ValidateReleaseScenes();
            PHSRuntimeEditorOnlyComponentCleanup.Cleanup();
            PHS20260812ReleaseValidator.Validate();

            var absoluteOutputPath = Path.GetFullPath(OutputPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED reason=output_directory_missing");
            }

            Directory.CreateDirectory(outputDirectory);
            var previousStripEngineCode = PlayerSettings.stripEngineCode;
            PlayerSettings.stripEngineCode = false;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = ReleaseScenePaths,
                    locationPathName = absoluteOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.stripEngineCode = previousStripEngineCode;
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED result={report.summary.result} errors={report.summary.totalErrors}");
            }

            Debug.Log(
                $"PHS_NETWORK_BEAVER_RELEASE_BUILD_OK path={OutputPath} size={report.summary.totalSize}");
        }

        private static void ValidateReleaseScenes()
        {
            foreach (var scenePath in ReleaseScenePaths)
            {
                if (!File.Exists(Path.GetFullPath(scenePath)))
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED reason=scene_missing scene={scenePath}");
                }

                if (scenePath.Contains("FeatureInspection", StringComparison.OrdinalIgnoreCase)
                    || scenePath.Contains("DebrisCollection", StringComparison.OrdinalIgnoreCase)
                    || scenePath.Contains("/Legacy/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED reason=non_release_scene scene={scenePath}");
                }
            }
        }
    }
}
