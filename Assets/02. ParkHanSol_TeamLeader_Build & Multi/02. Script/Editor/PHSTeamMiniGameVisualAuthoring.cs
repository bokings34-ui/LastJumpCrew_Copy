using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    // One-shot restoration only. This file deliberately has no palette authoring path.
    public static class PHSTeamMiniGameVisualAuthoring
    {
        private const string RuntimePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_MiniGameRuntimeSystem.prefab";

        public static void RestoreOriginalVisuals()
        {
            var root = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=prefab_missing");
            }

            try
            {
                var power = RequireSingle<PHSPowerSyncGame>(root);
                var wire = RequireSingle<PHSWireFixGame>(root);
                var cannon = RequireSingle<PHSCannonGame>(root);
                var manager = RequireSingle<PHSMiniGameManager>(root);

                power.normalSafeColor = new Color(1f, 0.48f, 0.08f, 0.52f);
                power.highlightSafeColor = new Color(0.22f, 0.95f, 0.46f, 0.82f);
                SetImage(power.safeZoneImage, power.normalSafeColor);
                SetImage(power.handleRect == null ? null : power.handleRect.GetComponent<Image>(), Color.white);
                SetImages(wire.leftPoints, Color.white);
                SetImages(wire.rightPoints, Color.white);
                RestoreCannonTargets(cannon.targetButtons);
                foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    text.color = Color.white;
                }

                SetImage(manager.flashScreen, Color.white);
                if (PrefabUtility.SaveAsPrefabAsset(root, RuntimePrefabPath) == null)
                {
                    throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=prefab_save_failed");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("PHS_MINIGAME_ORIGINAL_VISUALS_RESTORED power=orange_green_red wire=channel_runtime cannon=default flash=blue_red");
        }

        private static void RestoreCannonTargets(Button[] targets)
        {
            if (targets == null || targets.Length == 0)
            {
                throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=cannon_targets_missing");
            }

            foreach (var target in targets)
            {
                if (target == null)
                {
                    throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=cannon_target_null");
                }

                SetImage(target.GetComponent<Image>(), Color.white);
                target.colors = ColorBlock.defaultColorBlock;
            }
        }

        private static void SetImages(Image[] images, Color color)
        {
            if (images == null || images.Length == 0)
            {
                throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=wire_endpoints_missing");
            }

            foreach (var image in images)
            {
                SetImage(image, color);
            }
        }

        private static void SetImage(Image image, Color color)
        {
            if (image == null)
            {
                throw new InvalidOperationException("PHS_MINIGAME_RESTORE_FAILED reason=image_reference_missing");
            }

            image.color = color;
        }

        private static T RequireSingle<T>(GameObject root) where T : Component
        {
            var values = root.GetComponentsInChildren<T>(true);
            if (values.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_MINIGAME_RESTORE_FAILED reason=component_count type={typeof(T).Name} count={values.Length}");
            }

            return values.Single();
        }
    }
}
