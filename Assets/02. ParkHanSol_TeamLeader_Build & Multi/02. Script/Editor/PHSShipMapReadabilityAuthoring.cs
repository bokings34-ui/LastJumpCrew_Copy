using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSShipMapReadabilityAuthoring
    {
        private const string HudPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/" +
            "ParkHanSol_PlayHudUI.prefab";
        private const string FontPath = PHSUIFontPaths.SuiteSemiBold;

        private static readonly Dictionary<string, (Vector2 position, Vector2 size)> RoomLayout =
            new(StringComparer.Ordinal)
            {
                ["Room A"] = (new Vector2(-360f, 20f), new Vector2(300f, 210f)),
                ["Room B"] = (new Vector2(0f, 225f), new Vector2(260f, 170f)),
                ["Room C"] = (new Vector2(360f, 20f), new Vector2(300f, 210f)),
                ["Center Corridor"] = (new Vector2(0f, 0f), new Vector2(300f, 250f))
            };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Ship Map Readability")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var view = root.GetComponentInChildren<PHSNetworkEventHudView>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_SHIP_MAP_AUTHOR_FAILED reason=view_missing");
                var binder = root.GetComponentInChildren<PHSNetworkEventHudBinder>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_SHIP_MAP_AUTHOR_FAILED reason=binder_missing");
                var binderData = new SerializedObject(binder);
                binderData.FindProperty("enableLegacyShipMapInput").boolValue = false;
                binderData.FindProperty("shipMapInputMode").enumValueIndex = 0;
                binderData.ApplyModifiedPropertiesWithoutUndo();

                var viewData = new SerializedObject(view);
                var shipMap = viewData.FindProperty("shipMapRoot")?.objectReferenceValue as GameObject
                    ?? throw new InvalidOperationException(
                        "PHS_SHIP_MAP_AUTHOR_FAILED reason=map_root_missing");
                ConfigureMapFrame(shipMap.GetComponent<RectTransform>());
                ConfigureRooms(viewData.FindProperty("roomViews"));
                EnsureConnector(shipMap.transform, "Port Connector", new Vector2(-195f, 20f), new Vector2(90f, 32f));
                EnsureConnector(shipMap.transform, "Starboard Connector", new Vector2(195f, 20f), new Vector2(90f, 32f));
                EnsureConnector(shipMap.transform, "Cockpit Connector", new Vector2(0f, 118f), new Vector2(34f, 68f));
                EnsureLegend(shipMap.transform);
                PHSUIFontAssetAuthoring.ApplyTypography(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            PHSPlayHudSingleSourceAuthoring.MigrateNetworkHudToCanonicalVariant();
            Validate();
            Debug.Log(
                "PHS_SHIP_MAP_AUTHOR_PASS input=tab_hold frame=1180x720 " +
                "rooms=4 silhouette=beaver_map_ver3 event_manager=untouched");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Ship Map Readability")]
        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath)
                ?? throw new InvalidOperationException(
                    "PHS_SHIP_MAP_VALIDATION_FAILED reason=prefab_missing");
            var binder = prefab.GetComponentInChildren<PHSNetworkEventHudBinder>(true);
            var view = prefab.GetComponentInChildren<PHSNetworkEventHudView>(true);
            Require(binder != null, "binder_missing");
            Require(view != null, "view_missing");

            var binderData = new SerializedObject(binder);
            Require(!binderData.FindProperty("enableLegacyShipMapInput").boolValue,
                "legacy_tab_input_enabled");
            Require(binderData.FindProperty("shipMapInputMode").enumValueIndex == 0,
                "tab_mode_not_hold");
            var viewData = new SerializedObject(view);
            var shipMap = viewData.FindProperty("shipMapRoot")?.objectReferenceValue as GameObject;
            Require(shipMap != null, "map_root_missing");
            Require(shipMap.GetComponent<RectTransform>().sizeDelta == new Vector2(1180f, 720f),
                "map_frame_size_invalid");
            Require(Find(shipMap.transform, "Port Connector") != null &&
                Find(shipMap.transform, "Starboard Connector") != null &&
                Find(shipMap.transform, "Cockpit Connector") != null,
                "ship_silhouette_connectors_missing");
        }

        private static void ConfigureMapFrame(RectTransform map)
        {
            map.anchorMin = new Vector2(0.5f, 0.5f);
            map.anchorMax = new Vector2(0.5f, 0.5f);
            map.pivot = new Vector2(0.5f, 0.5f);
            map.anchoredPosition = Vector2.zero;
            map.sizeDelta = new Vector2(1180f, 720f);
            map.localScale = Vector3.one;
            var background = map.GetComponent<Image>() ?? map.gameObject.AddComponent<Image>();
            background.color = new Color(0.008f, 0.025f, 0.055f, 0.97f);
            background.raycastTarget = false;
            var outline = map.GetComponent<Outline>() ?? map.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.8f, 1f, 0.9f);
            outline.effectDistance = new Vector2(4f, -4f);
            outline.useGraphicAlpha = false;
        }

        private static void ConfigureRooms(SerializedProperty rooms)
        {
            for (var index = 0; index < rooms.arraySize; index++)
            {
                var entry = rooms.GetArrayElementAtIndex(index);
                var roomId = entry.FindPropertyRelative("roomId").stringValue;
                var roomRoot = entry.FindPropertyRelative("roomRoot").objectReferenceValue as GameObject;
                if (roomRoot == null || !RoomLayout.TryGetValue(roomId, out var layout))
                {
                    continue;
                }

                var rect = roomRoot.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = layout.position;
                rect.sizeDelta = layout.size;
                rect.localScale = Vector3.one;
                var image = roomRoot.GetComponent<Image>() ?? roomRoot.AddComponent<Image>();
                image.color = roomId == "Center Corridor"
                    ? new Color(0.04f, 0.24f, 0.34f, 0.96f)
                    : new Color(0.025f, 0.14f, 0.24f, 0.96f);
                image.raycastTarget = false;
                var outline = roomRoot.GetComponent<Outline>() ?? roomRoot.AddComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.86f, 1f, 0.82f);
                outline.effectDistance = new Vector2(3f, -3f);
                outline.useGraphicAlpha = false;
            }
        }

        private static void EnsureConnector(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size)
        {
            var existing = Find(parent, name);
            var connector = existing == null
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : existing.gameObject;
            var rect = connector.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.SetSiblingIndex(0);
            var image = connector.GetComponent<Image>();
            image.color = new Color(0.04f, 0.32f, 0.42f, 0.96f);
            image.raycastTarget = false;
        }

        private static void EnsureLegend(Transform parent)
        {
            var existing = Find(parent, "Ship Map Controls");
            var labelObject = existing == null
                ? new GameObject(
                    "Ship Map Controls",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI))
                : existing.gameObject;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.rectTransform.SetParent(parent, false);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 0f);
            label.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            label.rectTransform.sizeDelta = new Vector2(900f, 42f);
            label.rectTransform.localScale = Vector3.one;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            label.text = "HOLD TAB  |  SHIP SYSTEM MAP  |  BRIGHT MARKERS = ACTIVE INCIDENT";
            label.fontSize = 24f;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.7f, 0.92f, 1f, 1f);
            label.raycastTarget = false;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var result = Find(child, name);
                if (result != null) return result;
            }

            return null;
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_VALIDATION_FAILED reason={reason}");
            }
        }
    }
}
