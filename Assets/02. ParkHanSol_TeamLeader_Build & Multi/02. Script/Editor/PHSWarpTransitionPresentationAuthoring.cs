#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Keeps the warp transition prefab's serialized presentation contract explicit.
    /// Runtime must not silently continue when a status view is absent.
    /// </summary>
    public static class PHSWarpTransitionPresentationAuthoring
    {
        private const string PrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_WarpTransitionSystem.prefab";
        private const string FontPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/_ThirdParty/Fonts/SUIT/TMP/SUIT Korean Dynamic Fallback SDF.asset";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Warp Transition Presentation")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var presenter = root.GetComponentInChildren<WarpTransitionPresenter>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_WARP_PRESENTATION_AUTHOR_FAILED reason=presenter_missing");
                var transitionCanvas = Find(root.transform, "TransitionCanvas") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_WARP_PRESENTATION_AUTHOR_FAILED reason=transition_canvas_missing");
                var statusCard = Find(root.transform, "WarpStatusCard")
                    ?? throw new InvalidOperationException(
                        "PHS_WARP_PRESENTATION_AUTHOR_FAILED reason=warp_status_card_missing");
                var statusText = Find(statusCard, "StatusText")?.GetComponent<TMP_Text>()
                    ?? throw new InvalidOperationException(
                        "PHS_WARP_PRESENTATION_AUTHOR_FAILED reason=warp_status_text_missing");

                var safeCard = Find(transitionCanvas, "SafeZoneStatusCard") as RectTransform;
                if (safeCard == null)
                {
                    var safeObject = new GameObject(
                        "SafeZoneStatusCard",
                        typeof(RectTransform),
                        typeof(Image));
                    safeCard = safeObject.GetComponent<RectTransform>();
                    safeCard.SetParent(transitionCanvas, false);
                }

                safeCard.anchorMin = new Vector2(0.5f, 1f);
                safeCard.anchorMax = new Vector2(0.5f, 1f);
                safeCard.pivot = new Vector2(0.5f, 1f);
                safeCard.anchoredPosition = new Vector2(0f, -40f);
                safeCard.sizeDelta = new Vector2(420f, 78f);
                safeCard.localScale = Vector3.one;
                var safeImage = safeCard.GetComponent<Image>();
                safeImage.sprite = null;
                safeImage.color = new Color(0.02f, 0.07f, 0.11f, 0.92f);
                safeImage.raycastTarget = false;

                var safeTextTransform = Find(safeCard, "StatusText");
                var safeTextObject = safeTextTransform == null
                    ? new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                    : safeTextTransform.gameObject;
                var safeTextRect = safeTextObject.GetComponent<RectTransform>();
                if (safeTextTransform == null)
                {
                    safeTextRect.SetParent(safeCard, false);
                }

                safeTextRect.anchorMin = Vector2.zero;
                safeTextRect.anchorMax = Vector2.one;
                safeTextRect.offsetMin = new Vector2(16f, 8f);
                safeTextRect.offsetMax = new Vector2(-16f, -8f);
                var safeText = safeTextObject.GetComponent<TextMeshProUGUI>();
                safeText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                safeText.text = "안전구역";
                safeText.fontSize = 30f;
                safeText.fontStyle = FontStyles.Bold;
                safeText.alignment = TextAlignmentOptions.Center;
                safeText.color = new Color(0.12f, 0.88f, 0.9f, 1f);
                safeText.raycastTarget = false;
                safeCard.gameObject.SetActive(false);
                statusCard.gameObject.SetActive(false);

                var data = new SerializedObject(presenter);
                SetReference(data, "warpStatusCardRoot", statusCard.gameObject);
                SetReference(data, "warpStatusText", statusText);
                SetReference(data, "safeZoneStatusRoot", safeCard.gameObject);
                SetReference(data, "safeZoneStatusText", safeText);
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            ValidateOrThrow();
            Debug.Log("PHS_WARP_PRESENTATION_AUTHOR_OK status=warp,safe_zone");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Warp Transition Presentation")]
        public static void ValidateOrThrow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_WARP_PRESENTATION_VALIDATION_FAILED reason=prefab_missing");
            var presenter = prefab.GetComponentInChildren<WarpTransitionPresenter>(true)
                ?? throw new InvalidOperationException(
                    "PHS_WARP_PRESENTATION_VALIDATION_FAILED reason=presenter_missing");
            var data = new SerializedObject(presenter);
            foreach (var field in new[]
                     {
                         "transitionCanvasGroup", "warpVisualRoot", "warpStatusCardRoot",
                         "warpStatusText", "safeZoneStatusRoot", "safeZoneStatusText",
                         "normalSkybox", "warpSkybox", "arrivalSkybox"
                     })
            {
                if (data.FindProperty(field)?.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_WARP_PRESENTATION_VALIDATION_FAILED reason=reference_missing field={field}");
                }
            }

            Debug.Log("PHS_WARP_PRESENTATION_VALIDATION_OK");
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name) return candidate;
            }

            return null;
        }

        private static void SetReference(SerializedObject owner, string fieldName, UnityEngine.Object value)
        {
            var property = owner.FindProperty(fieldName)
                ?? throw new InvalidOperationException(
                    $"PHS_WARP_PRESENTATION_AUTHOR_FAILED reason=serialized_property_missing field={fieldName}");
            property.objectReferenceValue = value;
        }
    }
}
#endif
