using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSPlayHudSingleSourceAuthoring
    {
        private const string HudPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab";
        private const string NetworkHudPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab";
        private const string NetworkHudGuid =
            "07d62e5473408144e8beaf1dc528b2bc";
        private const string CanonicalEnglishFontPath =
            "Assets/99. DownloadAssets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Canonical Play HUD")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var controller = root.GetComponentInChildren<PHSHudFeedbackController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=feedback_controller_missing");
                var economy = Find(root.transform, "Economy Cluster") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=economy_cluster_missing");
                ConfigureEconomyCluster(economy);

                var warpText = RequireText(root, "Warp Gauge Text");
                var shipHpText = RequireText(root, "Ship HP Text");
                ConfigureWarpText(warpText);
                ConfigureShipHpText(shipHpText);
                var warpGauge = EnsureGauge(
                    warpText,
                    "Warp Gauge Bar",
                    new Vector2(0f, -148f),
                    new Vector2(180f, 10f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Color(0.44f, 0.1f, 0.025f, 1f),
                    new Color(1f, 0.31f, 0.08f, 1f));
                var shipHpGauge = EnsureGauge(
                    shipHpText,
                    "Ship HP Bar",
                    new Vector2(0f, -5f),
                    new Vector2(160f, 9f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Color(0.04f, 0.16f, 0.22f, 1f),
                    new Color(0.27f, 0.81f, 1f, 1f));

                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "economyRoot", economy.gameObject);
                SetReference(controllerData, "warpGaugeMotion", warpGauge);
                SetReference(controllerData, "shipHpGaugeMotion", shipHpGauge);
                controllerData.ApplyModifiedPropertiesWithoutUndo();

                RemoveUnavailableModular3DText(root);
                EnsureEventAlertIcon(root);
                EnsureShopProductPanel(root);
                NormalizeEnglishFonts(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPath, ImportAssetOptions.ForceSynchronousImport);
            MigrateNetworkHudToCanonicalVariant();
            ValidateOrThrow();
            Debug.Log(
                "PHS_HUD_SSO_AUTHOR_OK gauges=2 economySafe=true " +
                "englishFontUnified=true networkVariant=true");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Migrate Network HUD To Canonical Variant")]
        public static void MigrateNetworkHudToCanonicalVariant()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=play_mode_active");
            }

            var canonicalHud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=canonical_hud_missing path={HudPath}");
            var existingGuid = AssetDatabase.AssetPathToGUID(NetworkHudPath);
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject variantRoot = null;
            try
            {
                variantRoot = PrefabUtility.InstantiatePrefab(
                    canonicalHud,
                    previewScene) as GameObject;
                if (variantRoot == null)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=canonical_hud_instantiate_failed");
                }

                variantRoot.name = "PHS_NetworkPlayHudUI";
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    variantRoot,
                    NetworkHudPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=network_hud_variant_save_failed");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }

            AssetDatabase.ImportAsset(
                NetworkHudPath,
                ImportAssetOptions.ForceSynchronousImport);
            var savedGuid = AssetDatabase.AssetPathToGUID(NetworkHudPath);
            if ((!string.IsNullOrWhiteSpace(existingGuid)
                    && !string.Equals(existingGuid, savedGuid, StringComparison.Ordinal))
                || !string.Equals(savedGuid, NetworkHudGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=network_hud_guid_changed " +
                    $"expected={NetworkHudGuid} before={existingGuid} after={savedGuid}");
            }

            PHSNetworkOptionsAuthoring.ConfigurePlayHudForCanonicalVariant();
            AssetDatabase.SaveAssets();
            ValidateNetworkHudVariantOrThrow();
            Debug.Log(
                "PHS_NETWORK_HUD_VARIANT_AUTHOR_OK " +
                $"source={HudPath} target={NetworkHudPath}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Canonical Play HUD")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"PHS_HUD_SSO_VALIDATION_FAILED reason=prefab_missing path={HudPath}");
            }

            var controller = prefab.GetComponentInChildren<PHSHudFeedbackController>(true);
            Require(controller != null, "feedback_controller_missing", errors);
            var gauges = prefab.GetComponentsInChildren<ParkHanSolHudGaugeMotion>(true);
            Require(gauges.Length == 2, $"gauge_count actual={gauges.Length}", errors);
            foreach (var gauge in gauges)
            {
                var data = new SerializedObject(gauge);
                var fill = data.FindProperty("fillImage")?.objectReferenceValue as Image;
                Require(fill != null, $"gauge_fill_missing name={gauge.name}", errors);
                if (fill != null)
                {
                    Require(fill.type == Image.Type.Filled,
                        $"gauge_fill_type name={gauge.name} actual={fill.type}", errors);
                }
            }

            var warpGauge = Find(prefab.transform, "Warp Gauge Bar") as RectTransform;
            var shipHpGauge = Find(prefab.transform, "Ship HP Bar") as RectTransform;
            Require(warpGauge != null, "warp_gauge_missing", errors);
            Require(shipHpGauge != null, "ship_hp_gauge_missing", errors);
            if (warpGauge != null)
            {
                Require(Approximately(warpGauge.sizeDelta, new Vector2(180f, 10f)),
                    $"warp_gauge_size_invalid actual={warpGauge.sizeDelta}", errors);
                var fill = Find(warpGauge, "Fill")?.GetComponent<Image>();
                Require(fill != null && Approximately(
                        fill.color,
                        new Color(1f, 0.31f, 0.08f, 1f)),
                    "warp_gauge_fill_not_orange", errors);
            }

            if (shipHpGauge != null)
            {
                Require(Approximately(shipHpGauge.sizeDelta, new Vector2(160f, 9f)),
                    $"ship_hp_gauge_size_invalid actual={shipHpGauge.sizeDelta}", errors);
                Require(shipHpGauge.anchorMin == shipHpGauge.anchorMax,
                    "ship_hp_gauge_stretch_anchor_present", errors);
            }

            var economy = Find(prefab.transform, "Economy Cluster") as RectTransform;
            if (controller != null)
            {
                var data = new SerializedObject(controller);
                Require(data.FindProperty("warpGaugeMotion")?.objectReferenceValue != null,
                    "warp_gauge_reference_missing", errors);
                Require(data.FindProperty("shipHpGaugeMotion")?.objectReferenceValue != null,
                    "ship_hp_gauge_reference_missing", errors);
                Require(data.FindProperty("economyRoot")?.objectReferenceValue == economy?.gameObject,
                    "economy_root_reference_missing", errors);
            }

            Require(economy != null, "economy_cluster_missing", errors);
            if (economy != null)
            {
                Require(economy.anchorMin == Vector2.one && economy.anchorMax == Vector2.one,
                    "economy_anchor_not_top_right", errors);
                Require(economy.anchoredPosition.x >= -120f && economy.anchoredPosition.x <= -16f,
                    $"economy_safe_margin_invalid x={economy.anchoredPosition.x}", errors);
                Require(economy.gameObject.activeSelf,
                    "economy_cluster_inactive", errors);
                var bankText = Find(economy, "Bank Text");
                Require(bankText != null && bankText.gameObject.activeSelf,
                    "bank_text_inactive_or_missing", errors);
            }

            var eventView = prefab.GetComponentInChildren<PHSNetworkEventHudView>(true);
            Require(eventView != null, "event_hud_view_missing", errors);
            if (eventView != null)
            {
                var eventData = new SerializedObject(eventView);
                Require(eventData.FindProperty("eventAlertIcon")?.objectReferenceValue != null,
                    "event_alert_icon_reference_missing", errors);
                Require(Find(prefab.transform, "PHS Event Alert Text") == null,
                    "event_alert_text_still_present", errors);
                Require(Find(prefab.transform, "PHS Event Alert Icon") != null,
                    "event_alert_icon_missing", errors);
            }

            var canonicalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CanonicalEnglishFontPath);
            Require(canonicalFont != null, "canonical_english_font_missing", errors);
            if (canonicalFont != null)
            {
                foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true)
                             .Where(text => !ContainsHangul(text.text)))
                {
                    Require(text.font == canonicalFont,
                        $"english_font_mismatch text={GetPath(text.transform)}", errors);
                }
            }

            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                Require(
                    transform.name != "PHS_M3D_Extrusion",
                    $"removed_m3d_extrusion_present path={GetPath(transform)}",
                    errors);
                Require(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject) == 0,
                    $"missing_script path={GetPath(transform)}",
                    errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_HUD_SSO_VALIDATION_FAILED\n- " + string.Join("\n- ", errors));
            }

            ValidateNetworkHudVariantOrThrow();

            Debug.Log(
                "PHS_HUD_SSO_VALIDATION_OK gauges=2 economySafe=true " +
                "englishFontUnified=true networkVariant=true");
        }

        private static void ValidateNetworkHudVariantOrThrow()
        {
            var canonicalHud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            var networkHud = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkHudPath);
            var errors = new List<string>();
            Require(networkHud != null, "network_hud_missing", errors);
            if (canonicalHud != null && networkHud != null)
            {
                Require(
                    AssetDatabase.AssetPathToGUID(NetworkHudPath) == NetworkHudGuid,
                    "network_hud_guid_changed",
                    errors);
                Require(
                    PrefabUtility.GetPrefabAssetType(networkHud) == PrefabAssetType.Variant,
                    "network_hud_not_variant",
                    errors);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(networkHud);
                Require(
                    source == canonicalHud,
                    $"network_hud_source_invalid actual={AssetDatabase.GetAssetPath(source)}",
                    errors);
                Require(
                    networkHud.GetComponentsInChildren<ParkHanSolHudGaugeMotion>(true).Length == 2,
                    "network_hud_gauge_count_invalid",
                    errors);
                foreach (var transform in networkHud.GetComponentsInChildren<Transform>(true))
                {
                    Require(
                        GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                            transform.gameObject) == 0,
                        $"network_hud_missing_script path={GetPath(transform)}",
                        errors);
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_HUD_VARIANT_VALIDATION_FAILED\n- " +
                    string.Join("\n- ", errors));
            }
        }

        private static void EnsureEventAlertIcon(GameObject root)
        {
            var eventHudView = root.GetComponentInChildren<PHSNetworkEventHudView>(true)
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_hud_view_missing");
            var viewData = new SerializedObject(eventHudView);
            var alertRootProperty = viewData.FindProperty("eventAlertRoot")
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_alert_root_property_missing");
            var alertRoot = alertRootProperty.objectReferenceValue as GameObject
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_alert_root_missing");

            var oldText = Find(alertRoot.transform, "PHS Event Alert Text");
            if (oldText != null)
            {
                UnityEngine.Object.DestroyImmediate(oldText.gameObject);
            }

            var alertRect = alertRoot.GetComponent<RectTransform>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_alert_rect_missing");
            alertRect.anchorMin = Vector2.one;
            alertRect.anchorMax = Vector2.one;
            alertRect.pivot = Vector2.one;
            alertRect.anchoredPosition = new Vector2(-42f, -154f);
            alertRect.sizeDelta = new Vector2(72f, 72f);

            var iconTransform = Find(alertRoot.transform, "PHS Event Alert Icon");
            var iconObject = iconTransform == null
                ? new GameObject(
                    "PHS Event Alert Icon",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Outline))
                : iconTransform.gameObject;
            var iconRect = iconObject.GetComponent<RectTransform>();
            if (iconTransform == null)
            {
                iconRect.SetParent(alertRoot.transform, false);
            }

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(46f, 46f);
            iconRect.localScale = Vector3.one;
            iconRect.localEulerAngles = new Vector3(0f, 0f, 45f);

            var iconBackground = iconObject.GetComponent<Image>();
            iconBackground.color = new Color(1f, 0.31f, 0.08f, 0.94f);
            iconBackground.raycastTarget = false;
            var iconOutline = iconObject.GetComponent<Outline>();
            iconOutline.effectColor = new Color(0.02f, 0.055f, 0.075f, 0.96f);
            iconOutline.effectDistance = new Vector2(3f, -3f);

            var markTransform = Find(iconRect, "Icon Mark");
            var markObject = markTransform == null
                ? new GameObject(
                    "Icon Mark",
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI))
                : markTransform.gameObject;
            var markRect = markObject.GetComponent<RectTransform>();
            if (markTransform == null)
            {
                markRect.SetParent(iconRect, false);
            }

            markRect.anchorMin = Vector2.zero;
            markRect.anchorMax = Vector2.one;
            markRect.offsetMin = Vector2.zero;
            markRect.offsetMax = Vector2.zero;
            markRect.localScale = Vector3.one;
            markRect.localEulerAngles = new Vector3(0f, 0f, -45f);

            var mark = markObject.GetComponent<TextMeshProUGUI>();
            mark.text = "!";
            mark.alignment = TextAlignmentOptions.Center;
            mark.fontSize = 32f;
            mark.fontStyle = FontStyles.Bold;
            mark.color = Color.white;
            mark.raycastTarget = false;
            SetReference(viewData, "eventAlertIcon", iconObject);
            viewData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eventHudView);
        }

        private static void ConfigureEconomyCluster(RectTransform economy)
        {
            economy.anchorMin = Vector2.one;
            economy.anchorMax = Vector2.one;
            economy.pivot = Vector2.one;
            economy.anchoredPosition = new Vector2(-36f, -24f);
            economy.sizeDelta = new Vector2(240f, 40f);
            economy.localScale = Vector3.one;
            economy.gameObject.SetActive(true);

            var bankTransform = Find(economy, "Bank Text") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=bank_text_missing");
            bankTransform.anchorMin = Vector2.zero;
            bankTransform.anchorMax = Vector2.one;
            bankTransform.pivot = new Vector2(0.5f, 0.5f);
            bankTransform.anchoredPosition = Vector2.zero;
            bankTransform.sizeDelta = Vector2.zero;
            bankTransform.localScale = Vector3.one;
            bankTransform.gameObject.SetActive(true);

            var bankText = bankTransform.GetComponent<TMP_Text>();
            bankText.alignment = TextAlignmentOptions.Right;
            bankText.fontSize = 30f;
            bankText.color = new Color(0.27f, 0.81f, 1f, 1f);
        }

        private static void ConfigureWarpText(TMP_Text warpText)
        {
            var rect = warpText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -114f);
            rect.sizeDelta = new Vector2(180f, 28f);
            rect.localScale = Vector3.one;
            warpText.alignment = TextAlignmentOptions.Left;
            warpText.fontSize = 26f;
            warpText.color = new Color(1f, 0.31f, 0.08f, 1f);
        }

        private static void ConfigureShipHpText(TMP_Text shipHpText)
        {
            var root = shipHpText.transform.parent as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=ship_hp_root_missing");
            root.anchorMin = Vector2.one;
            root.anchorMax = Vector2.one;
            root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(0f, -72f);
            root.sizeDelta = new Vector2(260f, 32f);
            root.localScale = Vector3.one;

            var textRect = shipHpText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.localScale = Vector3.one;
            shipHpText.alignment = TextAlignmentOptions.Right;
            shipHpText.fontSize = 26f;
            shipHpText.color = new Color(0.92f, 0.96f, 1f, 1f);
        }

        private static ParkHanSolHudGaugeMotion EnsureGauge(
            TMP_Text valueText,
            string gaugeName,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor,
            Vector2 pivot,
            Color emptyColor,
            Color fullColor)
        {
            var parent = valueText.transform.parent as RectTransform
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=gauge_parent_missing text={valueText.name}");
            var matchingGauges = parent.Cast<Transform>()
                .Where(candidate => candidate.name == gaugeName)
                .ToArray();
            var existing = matchingGauges.FirstOrDefault();
            foreach (var duplicate in matchingGauges.Skip(1))
            {
                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            }

            var gaugeObject = existing == null
                ? new GameObject(gaugeName, typeof(RectTransform), typeof(Image),
                    typeof(ParkHanSolHudGaugeMotion))
                : existing.gameObject;
            var gaugeRect = gaugeObject.GetComponent<RectTransform>();
            if (existing == null)
            {
                gaugeRect.SetParent(parent, false);
            }

            gaugeRect.anchorMin = anchor;
            gaugeRect.anchorMax = anchor;
            gaugeRect.pivot = pivot;
            gaugeRect.anchoredPosition = anchoredPosition;
            gaugeRect.sizeDelta = size;
            gaugeRect.localScale = Vector3.one;

            var background = gaugeObject.GetComponent<Image>() ?? gaugeObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.11f, 0.82f);
            background.raycastTarget = false;

            var fillTransform = Find(gaugeRect, "Fill");
            var fillObject = fillTransform == null
                ? new GameObject("Fill", typeof(RectTransform), typeof(Image))
                : fillTransform.gameObject;
            var fillRect = fillObject.GetComponent<RectTransform>();
            if (fillTransform == null)
            {
                fillRect.SetParent(gaugeRect, false);
            }

            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.localScale = Vector3.one;
            var fill = fillObject.GetComponent<Image>() ?? fillObject.AddComponent<Image>();
            fill.color = fullColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;

            var motion = gaugeObject.GetComponent<ParkHanSolHudGaugeMotion>()
                ?? gaugeObject.AddComponent<ParkHanSolHudGaugeMotion>();
            var data = new SerializedObject(motion);
            SetReference(data, "gaugeRoot", gaugeRect);
            SetReference(data, "fillImage", fill);
            SetReference(data, "valueText", valueText);
            data.FindProperty("emptyValueColor").colorValue = emptyColor;
            data.FindProperty("fullValueColor").colorValue = fullColor;
            data.ApplyModifiedPropertiesWithoutUndo();
            return motion;
        }

        private static void EnsureShopProductPanel(GameObject root)
        {
            var panelTransform = Find(root.transform, "PHS Shop Product Panel");
            var panelObject = panelTransform == null
                ? new GameObject(
                    "PHS Shop Product Panel",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(CanvasGroup))
                : panelTransform.gameObject;
            var panelRect = panelObject.GetComponent<RectTransform>();
            if (panelTransform == null)
            {
                panelRect.SetParent(root.transform, false);
            }

            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -118f);
            panelRect.sizeDelta = new Vector2(420f, 126f);
            panelRect.localScale = Vector3.one;
            var background = panelObject.GetComponent<Image>();
            background.color = new Color(0.02f, 0.07f, 0.11f, 0.9f);
            background.raycastTarget = false;
            var canvasGroup = panelObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var nameText = EnsureShopText(
                panelRect,
                "Product Name",
                new Vector2(0f, 34f),
                30f,
                FontStyles.Bold);
            var priceText = EnsureShopText(
                panelRect,
                "Price",
                Vector2.zero,
                24f,
                FontStyles.Normal);
            var pickupText = EnsureShopText(
                panelRect,
                "Pickup Prompt",
                new Vector2(0f, -34f),
                22f,
                FontStyles.Bold);

            var presenters = root.GetComponentsInChildren<
                ShopLocalProductHudPresenter>(true);
            if (presenters.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=shop_presenter_count_invalid actual={presenters.Length}");
            }

            var presenter = presenters.Length == 1
                ? presenters[0]
                : root.AddComponent<ShopLocalProductHudPresenter>();
            var presenterData = new SerializedObject(presenter);
            SetReference(presenterData, "productPanel", canvasGroup);
            SetReference(presenterData, "productNameText", nameText);
            SetReference(presenterData, "priceText", priceText);
            SetReference(presenterData, "pickupPromptText", pickupText);
            presenterData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presenter);
        }

        private static TextMeshProUGUI EnsureShopText(
            RectTransform parent,
            string objectName,
            Vector2 anchoredPosition,
            float fontSize,
            FontStyles fontStyle)
        {
            var existing = Find(parent, objectName);
            var textObject = existing == null
                ? new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI))
                : existing.gameObject;
            var rect = textObject.GetComponent<RectTransform>();
            if (existing == null)
            {
                rect.SetParent(parent, false);
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(388f, 34f);
            rect.localScale = Vector3.one;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = string.Empty;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void NormalizeEnglishFonts(GameObject root)
        {
            var canonicalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CanonicalEnglishFontPath)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=font_missing path={CanonicalEnglishFontPath}");
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (ContainsHangul(text.text))
                {
                    continue;
                }

                text.font = canonicalFont;
                text.fontSharedMaterial = canonicalFont.material;
                EditorUtility.SetDirty(text);
            }
        }

        internal static void RemoveUnavailableModular3DText(GameObject root)
        {
            var extrusionTransforms = root
                .GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == "PHS_M3D_Extrusion")
                .ToArray();
            var mirrorOwners = new HashSet<GameObject>();

            foreach (var extrusion in extrusionTransforms)
            {
                var missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        extrusion.gameObject);
                if (missingCount <= 0)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED " +
                        $"reason=m3d_contract_changed path={GetPath(extrusion)}");
                }

                if (extrusion.parent != null)
                {
                    mirrorOwners.Add(extrusion.parent.gameObject);
                }

                UnityEngine.Object.DestroyImmediate(extrusion.gameObject);
            }

            foreach (var owner in mirrorOwners)
            {
                var missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(owner);
                if (missingCount != 1)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED " +
                        $"reason=m3d_mirror_contract_changed object={owner.name} " +
                        $"missingCount={missingCount}");
                }

                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(owner);
            }

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var missingCount =
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject);
                if (missingCount > 0)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED " +
                        $"reason=unknown_missing_script path={GetPath(transform)} " +
                        $"missingCount={missingCount}");
                }
            }
        }

        private static TMP_Text RequireText(GameObject root, string objectName)
        {
            var target = Find(root.transform, objectName);
            var text = target == null ? null : target.GetComponent<TMP_Text>();
            return text ?? throw new InvalidOperationException(
                $"PHS_HUD_SSO_AUTHOR_FAILED reason=text_missing name={objectName}");
        }

        private static Transform Find(Transform root, string objectName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == objectName);
        }

        private static void SetReference(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = target.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=property_missing property={propertyName}");
            property.objectReferenceValue = value;
        }

        private static bool ContainsHangul(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.Any(character => character >= '\uAC00' && character <= '\uD7A3');
        }

        private static string GetPath(Transform target)
        {
            var names = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static bool Approximately(Vector2 actual, Vector2 expected)
        {
            return (actual - expected).sqrMagnitude <= 0.01f;
        }

        private static bool Approximately(Color actual, Color expected)
        {
            return Mathf.Abs(actual.r - expected.r) <= 0.01f
                && Mathf.Abs(actual.g - expected.g) <= 0.01f
                && Mathf.Abs(actual.b - expected.b) <= 0.01f
                && Mathf.Abs(actual.a - expected.a) <= 0.01f;
        }

        private static void Require(bool condition, string error, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }
    }
}
