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
        private const string CanonicalEnglishFontPath = PHSUIFontPaths.SuitRegular;
        private const string CanonicalLocalizedFontPath = PHSUIFontPaths.SuitRegular;
        private const string ShipHealthIconPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/VitalsIcons/PHS_Hud_ShipHealth.png";
        private const string WarpGaugeIconPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/VitalsIcons/PHS_Hud_WarpGauge.png";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Canonical Play HUD")]
        public static void Author()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                root.transform.localScale = Vector3.one;
                var controller = root.GetComponentInChildren<PHSHudFeedbackController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=feedback_controller_missing");
                var economy = Find(root.transform, "Economy Cluster") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_SSO_AUTHOR_FAILED reason=economy_cluster_missing");
                ConfigureEconomyCluster(economy);

                var gauges = ConfigureVitalsGauges(root);

                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "economyRoot", economy.gameObject);
                SetReference(controllerData, "shipHpMotion", null);
                SetReference(controllerData, "warpGaugeMotion", gauges.warp);
                SetReference(controllerData, "shipHpGaugeMotion", gauges.ship);
                controllerData.ApplyModifiedPropertiesWithoutUndo();

                RemoveUnavailableModular3DText(root);
                EnsureEventAlertIcon(root);
                ConfigureAlertIconLineup(root);
                EnsureShopProductPanel(root);
                NormalizeEnglishFonts(root);
                ConfigureLocalizedHudTypography(root);
                PHSUIFontAssetAuthoring.ApplyTypography(root);
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

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Vitals Gauge Readability")]
        public static void AuthorVitalsGaugeReadability()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                root.transform.localScale = Vector3.one;
                var gauges = ConfigureVitalsGauges(root);
                var controller = root.GetComponentInChildren<PHSHudFeedbackController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_GAUGE_AUTHOR_FAILED reason=feedback_controller_missing");
                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "warpGaugeMotion", gauges.warp);
                SetReference(controllerData, "shipHpGaugeMotion", gauges.ship);
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                PHSUIFontAssetAuthoring.ApplyTypography(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            MigrateNetworkHudToCanonicalVariant();
            ValidateVitalsGaugeOrThrow();
            Debug.Log(
                "PHS_HUD_GAUGE_AUTHOR_PASS bars=2 height=34 value_text=2 " +
                "ticks=6 change_trails=2 network_variant=true");
        }

        private static void ValidateVitalsGaugeOrThrow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath)
                ?? throw new InvalidOperationException(
                    "PHS_HUD_GAUGE_VALIDATION_FAILED reason=prefab_missing");
            var errors = new List<string>();
            Require(prefab.transform.localScale == Vector3.one,
                $"hud_root_scale_invalid actual={prefab.transform.localScale}", errors);
            foreach (var name in new[] { "Ship HP Bar", "Warp Gauge Bar" })
            {
                var bar = Find(prefab.transform, name) as RectTransform;
                Require(bar != null, $"bar_missing name={name}", errors);
                if (bar == null) continue;
                Require(Approximately(bar.sizeDelta, new Vector2(400f, 34f)),
                    $"bar_size_invalid name={name} actual={bar.sizeDelta}", errors);
                Require(Find(bar, "Change Trail")?.GetComponent<Image>() != null,
                    $"change_trail_missing name={name}", errors);
                Require(Find(bar, "Gauge Value Text")?.GetComponent<TMP_Text>() != null,
                    $"value_text_missing name={name}", errors);
                Require(CountGaugeTicks(bar) == 3,
                    $"tick_count_invalid name={name} actual={CountGaugeTicks(bar)}", errors);
                var motion = bar.GetComponent<ParkHanSolHudGaugeMotion>();
                var data = motion == null ? null : new SerializedObject(motion);
                Require(data?.FindProperty("changeImage")?.objectReferenceValue != null,
                    $"change_reference_missing name={name}", errors);
                Require(data?.FindProperty("valueText")?.objectReferenceValue != null,
                    $"value_reference_missing name={name}", errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_HUD_GAUGE_VALIDATION_FAILED\n- " + string.Join("\n- ", errors));
            }
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
                PHSUIFontAssetAuthoring.ApplyTypography(variantRoot);
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
            var presenter = prefab.GetComponentInChildren<ParkHanSolPlayHudMockPresenter>(true);
            var durabilitySegments = prefab.GetComponentInChildren<ParkHanSolHeldItemDurabilitySegments>(true);
            Require(presenter != null, "play_hud_presenter_missing", errors);
            Require(durabilitySegments != null, "held_item_durability_segments_missing", errors);
            if (presenter != null && durabilitySegments != null)
            {
                var presenterData = new SerializedObject(presenter);
                var segmentData = new SerializedObject(durabilitySegments);
                Require(
                    presenterData.FindProperty("heldItemDurabilitySegments")?.objectReferenceValue == durabilitySegments,
                    "held_item_durability_segments_reference_missing",
                    errors);
                Require(segmentData.FindProperty("segmentContainer")?.objectReferenceValue != null,
                    "held_item_durability_container_missing", errors);
                Require(segmentData.FindProperty("segmentGrid")?.objectReferenceValue != null,
                    "held_item_durability_grid_missing", errors);
                Require(segmentData.FindProperty("segmentTemplate")?.objectReferenceValue != null,
                    "held_item_durability_template_missing", errors);
                Require(segmentData.FindProperty("valueText")?.objectReferenceValue != null,
                    "held_item_durability_value_text_missing", errors);
            }
            var gauges = prefab.GetComponentsInChildren<ParkHanSolHudGaugeMotion>(true);
            Require(gauges.Length == 2, $"gauge_count actual={gauges.Length}", errors);
            foreach (var gauge in gauges)
            {
                var data = new SerializedObject(gauge);
                var fill = data.FindProperty("fillImage")?.objectReferenceValue as Image;
                var change = data.FindProperty("changeImage")?.objectReferenceValue as Image;
                var valueText = data.FindProperty("valueText")?.objectReferenceValue as TMP_Text;
                Require(fill != null, $"gauge_fill_missing name={gauge.name}", errors);
                Require(change != null, $"gauge_change_trail_missing name={gauge.name}", errors);
                Require(valueText != null, $"gauge_value_text_missing name={gauge.name}", errors);
                if (fill != null)
                {
                    Require(fill.sprite != null,
                        $"gauge_fill_sprite_missing name={gauge.name}", errors);
                    Require(fill.type == Image.Type.Filled,
                        $"gauge_fill_type name={gauge.name} actual={fill.type}", errors);
                    Require(fill.fillMethod == Image.FillMethod.Horizontal,
                        $"gauge_fill_method name={gauge.name} actual={fill.fillMethod}", errors);
                    Require(fill.fillOrigin == (int)Image.OriginHorizontal.Left,
                        $"gauge_fill_origin name={gauge.name} actual={fill.fillOrigin}", errors);
                }

                if (change != null)
                {
                    Require(change.sprite != null,
                        $"gauge_change_sprite_missing name={gauge.name}", errors);
                    Require(change.type == Image.Type.Filled,
                        $"gauge_change_type name={gauge.name} actual={change.type}", errors);
                    Require(change.fillMethod == Image.FillMethod.Horizontal,
                        $"gauge_change_method name={gauge.name} actual={change.fillMethod}", errors);
                    Require(change.fillOrigin == (int)Image.OriginHorizontal.Left,
                        $"gauge_change_origin name={gauge.name} actual={change.fillOrigin}", errors);
                }
            }

            var warpGauge = Find(prefab.transform, "Warp Gauge Bar") as RectTransform;
            var shipHpGauge = Find(prefab.transform, "Ship HP Bar") as RectTransform;
            Require(warpGauge != null, "warp_gauge_missing", errors);
            Require(shipHpGauge != null, "ship_hp_gauge_missing", errors);
            if (warpGauge != null)
            {
                Require(Approximately(warpGauge.sizeDelta, new Vector2(400f, 34f)),
                    $"warp_gauge_size_invalid actual={warpGauge.sizeDelta}", errors);
                var fill = Find(warpGauge, "Fill")?.GetComponent<Image>();
                Require(fill != null && Approximately(
                        fill.color,
                        new Color(1f, 0.36f, 0.06f, 1f)),
                    "warp_gauge_fill_not_orange", errors);
                Require(CountGaugeTicks(warpGauge) == 3,
                    $"warp_gauge_tick_count actual={CountGaugeTicks(warpGauge)}", errors);
            }

            if (shipHpGauge != null)
            {
                Require(Approximately(shipHpGauge.sizeDelta, new Vector2(400f, 34f)),
                    $"ship_hp_gauge_size_invalid actual={shipHpGauge.sizeDelta}", errors);
                Require(shipHpGauge.anchorMin == shipHpGauge.anchorMax,
                    "ship_hp_gauge_stretch_anchor_present", errors);
                Require(CountGaugeTicks(shipHpGauge) == 3,
                    $"ship_hp_gauge_tick_count actual={CountGaugeTicks(shipHpGauge)}", errors);
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
                var parentRect = economy.parent as RectTransform;
                var isTopRightRootLayout = economy.anchorMin == Vector2.one
                    && economy.anchorMax == Vector2.one
                    && economy.anchoredPosition.x >= -120f
                    && economy.anchoredPosition.x <= -16f;
                var isNestedVitalsLayout = parentRect != null
                    && parentRect.name == "Vitals Cluster"
                    && economy.anchorMin == new Vector2(0f, 1f)
                    && economy.anchorMax == new Vector2(0f, 1f)
                    && economy.anchoredPosition.x - economy.pivot.x * economy.rect.width >= 12f
                    && economy.anchoredPosition.x
                        + (1f - economy.pivot.x) * economy.rect.width
                        <= parentRect.rect.width - 12f;
                Require(
                    isTopRightRootLayout || isNestedVitalsLayout,
                    "economy_anchor_layout_invalid",
                    errors);
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
                Require(eventData.FindProperty("iconLineupRoot")?.objectReferenceValue != null,
                    "event_icon_lineup_reference_missing", errors);
                Require(eventData.FindProperty("accidentIconEntries")?.arraySize == 7,
                    "accident_icon_entry_count_invalid", errors);
                Require(eventData.FindProperty("miniGameIconEntries")?.arraySize == 3,
                    "minigame_icon_entry_count_invalid", errors);
                Require(Find(prefab.transform, "PHS Event Alert Text") != null,
                    "event_alert_text_missing", errors);
                Require(Find(prefab.transform, "Icon Mark") == null,
                    "event_alert_mark_still_present", errors);
                Require(Find(prefab.transform, "PHS Event Alert Icon") != null,
                    "event_alert_icon_missing", errors);
                var lineup = eventData.FindProperty("iconLineupRoot")
                    ?.objectReferenceValue as GameObject;
                Require(lineup != null
                        && lineup.GetComponent<VerticalLayoutGroup>() != null
                        && lineup.GetComponent<Image>() == null
                        && lineup.GetComponent<Outline>() == null,
                    "event_icon_lineup_should_be_vertical_and_background_free",
                    errors);
                if (lineup != null)
                {
                    foreach (Transform entry in lineup.transform)
                    {
                        var icon = Find(entry, "Icon") as RectTransform;
                        Require(icon != null
                                && Approximately(icon.sizeDelta, new Vector2(92f, 92f))
                                && entry.GetComponent<Image>() == null
                                && entry.GetComponent<Outline>() == null,
                            $"event_icon_entry_style_invalid name={entry.name}",
                            errors);
                    }
                }
            }

            var localizedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                CanonicalLocalizedFontPath);
            Require(localizedFont != null, "canonical_localized_font_missing", errors);
            var localizedTexts = prefab.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(UsesLocalizedHudTypography)
                .ToArray();
            Require(
                localizedTexts.Length == 5,
                $"localized_hud_text_count_invalid actual={localizedTexts.Length}",
                errors);
            foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Require(
                    IsProjectUiFont(text.font)
                        && text.fontSharedMaterial != null
                        && text.fontSharedMaterial.mainTexture == text.font.atlasTexture,
                    $"hud_typography_invalid text={GetPath(text.transform)}",
                    errors);
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

            var alertRect = alertRoot.GetComponent<RectTransform>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_alert_rect_missing");
            alertRect.anchorMin = Vector2.one;
            alertRect.anchorMax = Vector2.one;
            alertRect.pivot = Vector2.one;
            alertRect.anchoredPosition = new Vector2(-430f, -116f);
            alertRect.sizeDelta = new Vector2(250f, 92f);

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

            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-46f, 0f);
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
            if (markTransform != null)
            {
                UnityEngine.Object.DestroyImmediate(markTransform.gameObject);
            }

            var labelTransform = Find(
                alertRoot.transform,
                "PHS Event Alert Text");
            var labelObject = labelTransform == null
                ? new GameObject(
                    "PHS Event Alert Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI))
                : labelTransform.gameObject;
            var labelRect = labelObject.GetComponent<RectTransform>();
            if (labelTransform == null)
            {
                labelRect.SetParent(alertRoot.transform, false);
            }

            labelRect.anchorMin = new Vector2(1f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.anchoredPosition = new Vector2(-82f, 0f);
            labelRect.sizeDelta = new Vector2(150f, 42f);
            labelRect.localScale = Vector3.one;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "산소 유출";
            label.fontSize = 20f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.color = new Color(0.72f, 0.96f, 1f, 1f);
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            labelObject.SetActive(false);
            PHSUIFontPaths.ApplyResolved(label);

            SetReference(viewData, "eventAlertIcon", iconObject);
            SetReference(viewData, "eventAlertLabelText", label);
            viewData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eventHudView);
        }

        private static void ConfigureEconomyCluster(RectTransform economy)
        {
            economy.anchorMin = new Vector2(0f, 1f);
            economy.anchorMax = new Vector2(0f, 1f);
            economy.pivot = new Vector2(0f, 1f);
            economy.anchoredPosition = new Vector2(12f, -112f);
            economy.sizeDelta = new Vector2(260f, 32f);
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
            bankText.alignment = TextAlignmentOptions.Left;
            bankText.fontSize = 24f;
            bankText.color = new Color(0.27f, 0.81f, 1f, 1f);
        }

        private static (
            ParkHanSolHudGaugeMotion warp,
            ParkHanSolHudGaugeMotion ship) ConfigureVitalsGauges(GameObject root)
        {
            var mission = Find(root.transform, "Mission Status Cluster") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=mission_status_cluster_missing");
            mission.anchorMin = Vector2.one;
            mission.anchorMax = Vector2.one;
            mission.pivot = Vector2.one;
            mission.anchoredPosition = new Vector2(-32f, -28f);
            mission.sizeDelta = new Vector2(400f, 176f);

            var shipRoot = Find(root.transform, "Ship HP Root") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=ship_hp_root_missing");
            var obsoleteShipTextMotion =
                shipRoot.GetComponent<ParkHanSolHudTextMotion>();
            if (obsoleteShipTextMotion != null)
            {
                UnityEngine.Object.DestroyImmediate(obsoleteShipTextMotion);
            }
            shipRoot.SetParent(mission, false);
            ConfigureGaugeRow(shipRoot, new Vector2(0f, -66f));

            var warpRootTransform = Find(root.transform, "Warp Gauge Root");
            var warpRootObject = warpRootTransform == null
                ? new GameObject("Warp Gauge Root", typeof(RectTransform))
                : warpRootTransform.gameObject;
            var warpRoot = warpRootObject.GetComponent<RectTransform>();
            warpRoot.SetParent(mission, false);
            ConfigureGaugeRow(warpRoot, new Vector2(0f, -112f));

            var shipBar = Find(root.transform, "Ship HP Bar") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=ship_hp_bar_missing");
            var warpBar = Find(root.transform, "Warp Gauge Bar") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=warp_gauge_bar_missing");
            ConfigureGaugeBar(
                shipBar,
                shipRoot,
                "SHIP HP  100/100",
                new Color(1f, 0.18f, 0.16f, 1f),
                new Color(0.16f, 0.9f, 0.42f, 1f));
            ConfigureGaugeBar(
                warpBar,
                warpRoot,
                "WARP  0%",
                new Color(0.35f, 0.08f, 0.04f, 1f),
                new Color(1f, 0.36f, 0.06f, 1f));

            EnsureVitalsIcon(shipRoot, "Ship Health Icon", ShipHealthIconPath);
            EnsureVitalsIcon(warpRoot, "Warp Gauge Icon", WarpGaugeIconPath);

            var warpText = Find(root.transform, "Warp Gauge Text");
            if (warpText != null)
            {
                UnityEngine.Object.DestroyImmediate(warpText.gameObject);
            }

            var shipText = Find(root.transform, "Ship HP Text");
            if (shipText != null)
            {
                UnityEngine.Object.DestroyImmediate(shipText.gameObject);
            }

            return (
                warpBar.GetComponent<ParkHanSolHudGaugeMotion>(),
                shipBar.GetComponent<ParkHanSolHudGaugeMotion>());
        }

        private static void ConfigureGaugeRow(RectTransform row, Vector2 position)
        {
            row.anchorMin = Vector2.one;
            row.anchorMax = Vector2.one;
            row.pivot = Vector2.one;
            row.anchoredPosition = position;
            row.sizeDelta = new Vector2(400f, 40f);
            row.localScale = Vector3.one;
        }

        private static void ConfigureGaugeBar(
            RectTransform bar,
            RectTransform parent,
            string defaultValue,
            Color emptyColor,
            Color fullColor)
        {
            bar.SetParent(parent, false);
            bar.anchorMin = new Vector2(0f, 0.5f);
            bar.anchorMax = new Vector2(0f, 0.5f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(400f, 34f);
            bar.localScale = Vector3.one;

            var background = bar.GetComponent<Image>() ?? bar.gameObject.AddComponent<Image>();
            background.color = new Color(0.015f, 0.025f, 0.04f, 0.96f);
            background.raycastTarget = false;
            var outline = bar.GetComponent<Outline>() ?? bar.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.88f, 1f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            var fill = Find(bar, "Fill")?.GetComponent<Image>()
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=gauge_fill_missing bar={bar.name}");
            ConfigureFilledImage(fill, 3f, fullColor);

            var trailTransform = Find(bar, "Change Trail");
            var trailObject = trailTransform == null
                ? new GameObject("Change Trail", typeof(RectTransform), typeof(Image))
                : trailTransform.gameObject;
            var trailRect = trailObject.GetComponent<RectTransform>();
            trailRect.SetParent(bar, false);
            var trail = trailObject.GetComponent<Image>();
            ConfigureFilledImage(trail, 3f, Color.clear);
            trailRect.SetSiblingIndex(0);
            fill.rectTransform.SetSiblingIndex(1);

            for (var index = 1; index <= 3; index++)
            {
                EnsureGaugeTick(bar, index, index / 4f);
            }

            var valueTransform = Find(bar, "Gauge Value Text");
            var valueObject = valueTransform == null
                ? new GameObject(
                    "Gauge Value Text",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI))
                : valueTransform.gameObject;
            var valueText = valueObject.GetComponent<TextMeshProUGUI>();
            valueText.rectTransform.SetParent(bar, false);
            valueText.rectTransform.anchorMin = Vector2.zero;
            valueText.rectTransform.anchorMax = Vector2.one;
            valueText.rectTransform.offsetMin = new Vector2(8f, 0f);
            valueText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            valueText.rectTransform.localScale = Vector3.one;
            valueText.text = defaultValue;
            valueText.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CanonicalEnglishFontPath);
            valueText.fontSize = 19f;
            valueText.fontStyle = FontStyles.Normal;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.color = Color.white;
            valueText.raycastTarget = false;

            var motion = bar.GetComponent<ParkHanSolHudGaugeMotion>()
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=gauge_motion_missing bar={bar.name}");
            var data = new SerializedObject(motion);
            SetReference(data, "gaugeRoot", bar);
            SetReference(data, "fillImage", fill);
            SetReference(data, "changeImage", trail);
            SetReference(data, "valueText", valueText);
            data.FindProperty("emptyValueColor").colorValue = emptyColor;
            data.FindProperty("fullValueColor").colorValue = fullColor;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureFilledImage(Image image, float inset, Color color)
        {
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(
                "UI/Skin/UISprite.psd");
            if (image.sprite == null)
            {
                throw new InvalidOperationException(
                    $"PHS_HUD_GAUGE_AUTHOR_FAILED reason=fill_sprite_missing image={image.name}");
            }

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void EnsureGaugeTick(RectTransform bar, int index, float normalizedX)
        {
            var name = $"Gauge Tick {index}";
            var tickTransform = Find(bar, name);
            var tickObject = tickTransform == null
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : tickTransform.gameObject;
            var tickRect = tickObject.GetComponent<RectTransform>();
            tickRect.SetParent(bar, false);
            tickRect.anchorMin = new Vector2(normalizedX, 0f);
            tickRect.anchorMax = new Vector2(normalizedX, 1f);
            tickRect.pivot = new Vector2(0.5f, 0.5f);
            tickRect.anchoredPosition = Vector2.zero;
            tickRect.sizeDelta = new Vector2(2f, -6f);
            tickRect.localScale = Vector3.one;
            var image = tickObject.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.34f);
            image.raycastTarget = false;
        }

        private static int CountGaugeTicks(RectTransform bar)
        {
            return bar.Cast<Transform>().Count(child =>
                child.name.StartsWith("Gauge Tick ", StringComparison.Ordinal));
        }

        private static void EnsureVitalsIcon(
            RectTransform parent,
            string name,
            string spritePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=vitals_icon_missing path={spritePath}");
            var existing = Find(parent, name);
            var iconObject = existing == null
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : existing.gameObject;
            var iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(parent, false);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(32f, 0f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconRect.localScale = Vector3.one;
            var image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void ConfigureAlertIconLineup(GameObject root)
        {
            var view = root.GetComponentInChildren<PHSNetworkEventHudView>(true)
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_hud_view_missing");
            var data = new SerializedObject(view);
            var lineup = data.FindProperty("iconLineupRoot")
                ?.objectReferenceValue as GameObject
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_icon_lineup_missing");

            UnityEngine.Object.DestroyImmediate(lineup.GetComponent<Image>());
            UnityEngine.Object.DestroyImmediate(lineup.GetComponent<Outline>());
            UnityEngine.Object.DestroyImmediate(lineup.GetComponent<HorizontalLayoutGroup>());
            var vertical = lineup.GetComponent<VerticalLayoutGroup>()
                ?? lineup.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset();
            vertical.spacing = 8f;
            vertical.childAlignment = TextAnchor.UpperCenter;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = false;
            vertical.childForceExpandHeight = false;

            var lineupRect = lineup.GetComponent<RectTransform>();
            lineupRect.anchorMin = Vector2.one;
            lineupRect.anchorMax = Vector2.one;
            lineupRect.pivot = Vector2.one;
            lineupRect.anchoredPosition = Vector2.zero;
            lineupRect.sizeDelta = new Vector2(92f, 92f);
            lineupRect.localScale = Vector3.one;

            var fitter = lineup.GetComponent<ContentSizeFitter>()
                ?? lineup.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (Transform entry in lineup.transform)
            {
                UnityEngine.Object.DestroyImmediate(entry.GetComponent<Image>());
                UnityEngine.Object.DestroyImmediate(entry.GetComponent<Outline>());
                var entryRect = entry as RectTransform;
                entryRect.sizeDelta = new Vector2(92f, 92f);
                entryRect.localScale = Vector3.one;
                var layout = entry.GetComponent<LayoutElement>()
                    ?? entry.gameObject.AddComponent<LayoutElement>();
                layout.minWidth = 92f;
                layout.minHeight = 92f;
                layout.preferredWidth = 92f;
                layout.preferredHeight = 92f;

                var icon = Find(entry, "Icon") as RectTransform;
                if (icon != null)
                {
                    icon.sizeDelta = new Vector2(92f, 92f);
                    icon.localScale = Vector3.one;
                    var image = icon.GetComponent<Image>();
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
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
            panelRect.anchoredPosition = new Vector2(0f, -128f);
            panelRect.sizeDelta = new Vector2(260f, 64f);
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
                FontStyles.Normal);
            var priceText = EnsureShopText(
                panelRect,
                "Price",
                Vector2.zero,
                36f,
                FontStyles.Normal);
            var pickupText = EnsureShopText(
                panelRect,
                "Pickup Prompt",
                new Vector2(0f, -34f),
                22f,
                FontStyles.Normal);

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
            presenter.enabled = false;
            nameText.gameObject.SetActive(false);
            priceText.gameObject.SetActive(false);
            pickupText.gameObject.SetActive(false);
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
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (ContainsHangul(text.text) || UsesLocalizedHudTypography(text))
                {
                    continue;
                }

                if (RequiresFontMigration(text))
                {
                    PHSUIFontPaths.ApplyResolved(text);
                }
            }
        }

        private static void ConfigureLocalizedHudTypography(GameObject root)
        {
            var localizedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                CanonicalLocalizedFontPath)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=localized_font_missing path={CanonicalLocalizedFontPath}");
            var localizedTexts = root.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(UsesLocalizedHudTypography)
                .ToArray();
            if (localizedTexts.Length != 5)
            {
                throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=localized_text_count_invalid actual={localizedTexts.Length}");
            }

            foreach (var text in localizedTexts)
            {
                if (RequiresFontMigration(text))
                {
                    PHSUIFontPaths.ApplyResolved(text);
                }
            }

            var promptText = localizedTexts.First(text => text.name == "Prompt Text");
            if (string.IsNullOrWhiteSpace(promptText.text)
                || promptText.text == "INTERACT")
            {
                promptText.text = "상호작용";
            }

            var gravityWarning = localizedTexts.First(
                text => text.name == "Gravity Warning Text");
            if (string.IsNullOrWhiteSpace(gravityWarning.text)
                || gravityWarning.text == "GRAVITY FIELD OFFLINE")
            {
                gravityWarning.text = "중력장 비활성";
            }
        }

        private static bool UsesLocalizedHudTypography(TMP_Text text)
        {
            var path = GetPath(text.transform);
            return path.EndsWith(
                       "/Interaction Prompt/Input Badge/Input Text",
                       StringComparison.Ordinal)
                || path.EndsWith(
                       "/Interaction Prompt/Prompt Text",
                       StringComparison.Ordinal)
                || path.EndsWith(
                       "/Gravity Warning Text",
                       StringComparison.Ordinal)
                || path.EndsWith(
                       "/PHS Shop Product Panel/Pickup Prompt",
                       StringComparison.Ordinal)
                || path.EndsWith(
                       "/PHS Event Alert/PHS Event Alert Text",
                       StringComparison.Ordinal);
        }

        private static bool RequiresFontMigration(TMP_Text text)
        {
            if (text.font == null)
            {
                return true;
            }

            var path = AssetDatabase.GetAssetPath(text.font);
            return path.Contains("LiberationSans", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Maplestory", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProjectUiFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(font);
            return path == PHSUIFontPaths.SuitRegular
                || path == PHSUIFontPaths.SuitMedium
                || path == PHSUIFontPaths.SuitSemiBold
                || path == PHSUIFontPaths.SuitBold
                || path == PHSUIFontPaths.SuiteSemiBold
                || path == PHSUIFontPaths.SuiteBold;
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
