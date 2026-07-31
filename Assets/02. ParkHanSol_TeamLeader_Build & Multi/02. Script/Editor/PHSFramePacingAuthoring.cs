using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSFramePacingAuthoring
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkOwnerPauseUI.prefab"
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Frame Pacing Options")]
        public static void Author()
        {
            foreach (var prefabPath in PrefabPaths)
            {
                AuthorPrefab(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "PHS_FRAME_PACING_AUTHOR_OK prefabs=4 vsyncDefault=true " +
                "frameRates=30,60,120,unlimited defaultFrameRate=60");
        }

        private static void AuthorPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var controller = root.GetComponentInChildren<ParkHanSolGameSettingsController>(true);
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_FRAME_PACING_AUTHOR_FAILED reason=controller_missing path={prefabPath}");
                }

                var serialized = new SerializedObject(controller);
                var vSyncToggle = serialized.FindProperty("vSyncToggle").objectReferenceValue as Toggle;
                var qualityDropdown = serialized.FindProperty("qualityDropdown").objectReferenceValue as TMP_Dropdown;
                if (vSyncToggle == null || qualityDropdown == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_FRAME_PACING_AUTHOR_FAILED reason=source_controls_missing path={prefabPath}");
                }

                var graphicsPanel = vSyncToggle.transform.parent;
                var existing = FindDirectChild(graphicsPanel, "FRAME RATE");
                var frameRateRow = existing != null
                    ? existing.gameObject
                    : UnityEngine.Object.Instantiate(
                        qualityDropdown.transform.parent.gameObject,
                        graphicsPanel,
                        false);
                frameRateRow.name = "FRAME RATE";
                frameRateRow.transform.SetSiblingIndex(vSyncToggle.transform.GetSiblingIndex());

                var labelTransform = frameRateRow.transform.childCount > 0
                    ? frameRateRow.transform.GetChild(0)
                    : null;
                var label = labelTransform?.GetComponent<TMP_Text>();
                var dropdown = frameRateRow.GetComponentInChildren<TMP_Dropdown>(true);
                if (label == null || dropdown == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_FRAME_PACING_AUTHOR_FAILED reason=cloned_controls_invalid path={prefabPath}");
                }

                labelTransform.name = "FRAME RATE Label";
                label.text = "FPS LIMIT";
                dropdown.gameObject.name = "Frame Rate Dropdown";
                serialized.FindProperty("targetFrameRateDropdown").objectReferenceValue = dropdown;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
