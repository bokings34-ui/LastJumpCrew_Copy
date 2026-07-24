using System;
using System.Collections.Generic;
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
        private const string IssueReportPath =
            "Builds/BEAVER_2026/BuildIssueReport.txt";

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
            var hasBuildReport = false;
            try
            {
                ValidateReleaseScenes();
                PHS0715IntegrationValidator.ValidateOrThrow();

                var absoluteOutputPath = Path.GetFullPath(OutputPath);
                var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
                if (string.IsNullOrWhiteSpace(outputDirectory))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED reason=output_directory_missing");
                }

                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = ReleaseScenePaths,
                    locationPathName = absoluteOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                hasBuildReport = true;
                WriteBuildIssueReport(report);

                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED result={report.summary.result} errors={report.summary.totalErrors}");
                }

                Debug.Log(
                    $"PHS_NETWORK_BEAVER_RELEASE_BUILD_OK path={OutputPath} size={report.summary.totalSize} issueReport={IssueReportPath}");
            }
            catch (Exception exception)
            {
                if (!hasBuildReport)
                {
                    WritePreflightIssueReport(exception);
                }

                throw;
            }
        }

        private static void WriteBuildIssueReport(BuildReport report)
        {
            var lines = new List<string>
            {
                "PHS BEAVER 2026 Build Issue Report",
                $"Result: {report.summary.result}",
                $"Errors: {report.summary.totalErrors}",
                $"Warnings: {report.summary.totalWarnings}",
                $"Output: {OutputPath}",
                string.Empty,
                "Issues:"
            };

            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error
                        || message.type == LogType.Exception
                        || message.type == LogType.Warning)
                    {
                        lines.Add($"[{message.type}] {step.name}: {message.content}");
                    }
                }
            }

            if (lines.Count == 7)
            {
                lines.Add("None");
            }

            WriteIssueReport(lines);
        }

        private static void WritePreflightIssueReport(Exception exception)
        {
            WriteIssueReport(new[]
            {
                "PHS BEAVER 2026 Build Issue Report",
                "Result: PreflightFailed",
                "Issues:",
                $"[Error] {exception.Message}"
            });
        }

        private static void WriteIssueReport(IEnumerable<string> lines)
        {
            var absoluteIssueReportPath = Path.GetFullPath(IssueReportPath);
            var issueReportDirectory = Path.GetDirectoryName(absoluteIssueReportPath);
            if (string.IsNullOrWhiteSpace(issueReportDirectory))
            {
                Debug.LogError("PHS_NETWORK_BEAVER_RELEASE_BUILD_FAILED reason=issue_report_directory_missing");
                return;
            }

            Directory.CreateDirectory(issueReportDirectory);
            File.WriteAllLines(absoluteIssueReportPath, lines);
            Debug.Log($"PHS_NETWORK_BEAVER_RELEASE_ISSUE_REPORT path={IssueReportPath}");
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
