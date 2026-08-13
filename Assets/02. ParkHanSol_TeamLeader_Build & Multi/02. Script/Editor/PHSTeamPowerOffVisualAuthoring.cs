using System;
using System.Linq;
using SM;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSTeamPowerOffVisualAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Team Power Off Visual")]
        public static void Author()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var visual = UnityEngine.Object.FindFirstObjectByType<TeamPowerOffNetworkVisual>(
                FindObjectsInactive.Include);
            if (visual == null)
            {
                throw new InvalidOperationException("PHS_TEAM_POWER_OFF_AUTHOR_FAILED reason=visual_missing");
            }

            var emergencyLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(light => light != null && light.gameObject.name == "PHS_EmergencyLighting")
                .OrderBy(light => light.transform.position.x)
                .ThenBy(light => light.transform.position.z)
                .ToArray();
            if (emergencyLights.Length != 4)
            {
                throw new InvalidOperationException(
                    $"PHS_TEAM_POWER_OFF_AUTHOR_FAILED reason=emergency_count count={emergencyLights.Length}");
            }

            var serializedVisual = new SerializedObject(visual);
            var controlledLights = serializedVisual.FindProperty("controlledLights");
            var serializedEmergencyLights = serializedVisual.FindProperty("emergencyLights");
            if (controlledLights == null || controlledLights.arraySize == 0 || serializedEmergencyLights == null)
            {
                throw new InvalidOperationException("PHS_TEAM_POWER_OFF_AUTHOR_FAILED reason=serialized_reference_missing");
            }

            serializedEmergencyLights.arraySize = emergencyLights.Length;
            for (var index = 0; index < emergencyLights.Length; index++)
            {
                serializedEmergencyLights.GetArrayElementAtIndex(index).objectReferenceValue = emergencyLights[index];
            }

            serializedVisual.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"PHS_TEAM_POWER_OFF_AUTHOR_OK controlled={controlledLights.arraySize} emergency={emergencyLights.Length} object={visual.name}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Team Power Off Visual")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var visual = UnityEngine.Object.FindFirstObjectByType<TeamPowerOffNetworkVisual>(
                FindObjectsInactive.Include);
            var serializedVisual = visual == null ? null : new SerializedObject(visual);
            var controlledLights = serializedVisual?.FindProperty("controlledLights");
            var emergencyLights = serializedVisual?.FindProperty("emergencyLights");
            if (visual == null || controlledLights == null || controlledLights.arraySize == 0
                || emergencyLights == null || emergencyLights.arraySize != 4)
            {
                throw new InvalidOperationException("PHS_TEAM_POWER_OFF_VALIDATE_FAILED reason=references_invalid");
            }

            for (var index = 0; index < emergencyLights.arraySize; index++)
            {
                if (emergencyLights.GetArrayElementAtIndex(index).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_TEAM_POWER_OFF_VALIDATE_FAILED reason=emergency_reference_missing index={index}");
                }
            }

            Debug.Log(
                $"PHS_TEAM_POWER_OFF_VALIDATE_OK controlled={controlledLights.arraySize} emergency={emergencyLights.arraySize} object={visual.name}");
        }
    }
}
