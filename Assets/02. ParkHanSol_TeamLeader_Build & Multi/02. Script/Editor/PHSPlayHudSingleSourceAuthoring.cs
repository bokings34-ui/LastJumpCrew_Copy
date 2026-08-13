using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Shop;
using LastJumpCrew.ParkHanSol.UI;
using SM;
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
        private const string CanonicalEnglishFontPath =
            "Assets/99. DownloadAssets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string KoreanFallbackFontPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/_ThirdParty/Fonts/SUIT/TMP/SUIT Korean Dynamic Fallback SDF.asset";
        private const string ShipHealthIconPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/VitalsIcons/PHS_Hud_ShipHealth.png";
        private const string WarpGaugeIconPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/VitalsIcons/PHS_Hud_WarpGauge.png";
        private const float HudGraphicCurvature = 0.0425f;
        // Keep the authored alert position; only the readable icon grid dimensions change.
        private const float LifecycleEventIconSize = 44f;
        private const float LifecycleEventIconSpacing = 6f;

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
                var gauges = ResolveExistingVitalsGauges(root);

                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "economyRoot", economy.gameObject);
                SetReference(controllerData, "shipHpMotion", null);
                SetReference(controllerData, "warpGaugeMotion", gauges.warp);
                SetReference(controllerData, "shipHpGaugeMotion", gauges.ship);
                controllerData.ApplyModifiedPropertiesWithoutUndo();

                RemoveUnavailableModular3DText(root);
                EnsureEventAlertIcon(root);
                ConfigureLifecycleEventIcons(root);
                ConfigureAlertIconLineup(root);
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
                "PHS_HUD_GAUGE_AUTHOR_PASS bars=2 height=30 value_text=2 " +
                "ticks=0 change_trails=2 network_variant=true");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author HUD Graphic Curvature")]
        public static void AuthorHudGraphicCurvature()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                ConfigureHudGraphicCurvature(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPath, ImportAssetOptions.ForceSynchronousImport);
            MigrateNetworkHudToCanonicalVariant();
            ValidateHudGraphicCurvatureOrThrow();
            Debug.Log(
                "PHS_HUD_GRAPHIC_CURVATURE_AUTHOR_OK scope=hud_root " +
                "graphic_types=image_rawimage reference=hud_root");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Event Icon Readability")]
        public static void AuthorEventIconReadability()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                ConfigureAlertIconReadability(root);
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
            Debug.Log("PHS_EVENT_ICON_READABILITY_AUTHOR_OK size=44 position=preserved");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Recover Runtime HUD Contract")]
        public static void RecoverRuntimeHudContract()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                root.transform.localScale = Vector3.one;
                var controller = root.GetComponentInChildren<PHSHudFeedbackController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_RUNTIME_RECOVERY_FAILED reason=feedback_controller_missing");
                var economy = Find(root.transform, "Economy Cluster") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_RUNTIME_RECOVERY_FAILED reason=economy_cluster_missing");

                var gauges = ResolveExistingVitalsGauges(root);
                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "economyRoot", economy.gameObject);
                SetReference(controllerData, "warpGaugeMotion", gauges.warp);
                SetReference(controllerData, "shipHpGaugeMotion", gauges.ship);
                controllerData.ApplyModifiedPropertiesWithoutUndo();

                EnsureEventAlertIcon(root);
                ConfigureLifecycleEventIcons(root);
                ConfigureAlertIconLineup(root);
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
                "PHS_HUD_RUNTIME_RECOVERY_OK economy=top_right " +
                    "alert_label=dedicated entry_background=removed font=canonical");
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
                Require(Approximately(bar.sizeDelta, new Vector2(300f, 30f)),
                    $"bar_size_invalid name={name} actual={bar.sizeDelta}", errors);
                Require(Find(bar, "Change Trail")?.GetComponent<Image>() != null,
                    $"change_trail_missing name={name}", errors);
                Require(Find(bar, "Gauge Value Text")?.GetComponent<TMP_Text>() != null,
                    $"value_text_missing name={name}", errors);
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
            if (!string.IsNullOrWhiteSpace(existingGuid)
                && !string.Equals(existingGuid, savedGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=network_hud_guid_changed " +
                    $"before={existingGuid} after={savedGuid}");
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
                var change = data.FindProperty("changeImage")?.objectReferenceValue as Image;
                var valueText = data.FindProperty("valueText")?.objectReferenceValue as TMP_Text;
                Require(fill != null, $"gauge_fill_missing name={gauge.name}", errors);
                Require(change != null, $"gauge_change_trail_missing name={gauge.name}", errors);
                Require(valueText != null, $"gauge_value_text_missing name={gauge.name}", errors);
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
                Require(Approximately(warpGauge.sizeDelta, new Vector2(300f, 30f)),
                    $"warp_gauge_size_invalid actual={warpGauge.sizeDelta}", errors);
                var fill = Find(warpGauge, "Fill")?.GetComponent<Image>();
                Require(fill != null && Approximately(
                        fill.color,
                        new Color(1f, 0.36f, 0.06f, 1f)),
                    "warp_gauge_fill_not_orange", errors);
            }

            if (shipHpGauge != null)
            {
                Require(Approximately(shipHpGauge.sizeDelta, new Vector2(300f, 30f)),
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
                var hudRoot = Find(prefab.transform, "Hud Root") as RectTransform;
                Require(hudRoot != null, "hud_root_missing", errors);
                var playerEconomy = Find(prefab.transform, "Player Economy Cluster") as RectTransform;
                Require(playerEconomy != null,
                    "player_economy_cluster_missing", errors);
                Require(economy.parent == playerEconomy,
                    "economy_not_grouped_with_player_cluster", errors);
                var boost = Find(prefab.transform, "Boost Row") as RectTransform;
                Require(boost != null && boost.parent == playerEconomy,
                    "boost_not_grouped_with_player_cluster", errors);
                Require(Approximately(economy.anchoredPosition, new Vector2(12f, -10f)),
                    $"economy_player_alignment_invalid actual={economy.anchoredPosition}", errors);
                Require(boost != null && Approximately(boost.anchoredPosition, new Vector2(12f, -54f)),
                    $"boost_alignment_invalid actual={boost?.anchoredPosition}", errors);
                Require(economy.gameObject.activeSelf,
                    "economy_cluster_inactive", errors);
                var bankText = Find(economy, "Bank Text");
                Require(bankText != null && bankText.gameObject.activeSelf,
                    "bank_text_inactive_or_missing", errors);
            }

            var mission = Find(prefab.transform, "Mission Status Cluster") as RectTransform;
            var timeRoot = Find(prefab.transform, "Time Root") as RectTransform;
            Require(mission != null && Approximately(mission.anchoredPosition, new Vector2(-32f, -24f))
                    && Approximately(mission.sizeDelta, new Vector2(440f, 200f)),
                $"mission_status_layout_invalid pos={mission?.anchoredPosition} size={mission?.sizeDelta}", errors);
            Require(timeRoot != null && timeRoot.parent == mission,
                "time_root_not_grouped_with_mission_cluster", errors);
            Require(timeRoot != null && Approximately(timeRoot.localScale, Vector3.one),
                $"time_root_scale_invalid actual={timeRoot?.localScale}", errors);
            var heldSlot = Find(prefab.transform, "Held Item Slot") as RectTransform;
            var heldFrame = heldSlot == null
                ? null
                : Find(heldSlot, "Held Item Frame") as RectTransform;
            var heldName = heldFrame == null
                ? null
                : Find(heldFrame, "Held Item Name Text") as RectTransform;
            var heldDurability = heldFrame == null
                ? null
                : Find(heldFrame, "Held Item Durability Text") as RectTransform;
            var heldSegments = heldFrame == null
                ? null
                : Find(heldFrame, "Held Item Durability Segments") as RectTransform;
            Require(heldSlot != null && Mathf.Abs(heldSlot.sizeDelta.y - 180f) <= 0.01f,
                $"held_slot_height_invalid actual={heldSlot?.sizeDelta.y}", errors);
            Require(heldFrame != null && heldFrame.parent == heldSlot,
                "held_item_frame_missing_or_reparented", errors);
            Require(heldName != null && heldName.GetSiblingIndex() == 0,
                "held_item_name_not_first", errors);
            Require(heldDurability != null && Approximately(heldDurability.anchoredPosition, new Vector2(0f, 44f)),
                $"held_item_durability_position_invalid actual={heldDurability?.anchoredPosition}", errors);
            Require(heldSegments != null && Approximately(heldSegments.anchoredPosition, new Vector2(0f, 18f)),
                $"held_item_segments_position_invalid actual={heldSegments?.anchoredPosition}", errors);
            var eventView = prefab.GetComponentInChildren<PHSNetworkEventHudView>(true);
            Require(eventView != null, "event_hud_view_missing", errors);
            if (eventView != null)
            {
                var eventData = new SerializedObject(eventView);
                Require(eventData.FindProperty("eventAlertIcon")?.objectReferenceValue != null,
                    "event_alert_icon_reference_missing", errors);
                Require(eventData.FindProperty("eventAlertLabelText")?.objectReferenceValue != null,
                    "event_alert_label_reference_missing", errors);
                Require(eventData.FindProperty("iconLineupRoot")?.objectReferenceValue != null,
                    "event_icon_lineup_reference_missing", errors);
                Require(eventData.FindProperty("lifecycleIconEntries")?.arraySize == 13,
                    "lifecycle_icon_entry_count_invalid", errors);
                Require(Find(prefab.transform, "PHS Event Alert Label") != null,
                    "event_alert_label_missing", errors);
                Require(Find(prefab.transform, "Icon Mark") == null,
                    "event_alert_mark_still_present", errors);
                Require(Find(prefab.transform, "PHS Event Alert Icon") != null,
                    "event_alert_icon_missing", errors);
                var lineup = eventData.FindProperty("iconLineupRoot")
                    ?.objectReferenceValue as GameObject;
                Require(lineup != null
                        && lineup.GetComponent<GridLayoutGroup>() != null
                        && lineup.GetComponent<Image>() == null
                        && lineup.GetComponent<Outline>() == null,
                    "event_icon_lineup_should_be_grid_and_background_free",
                    errors);
                if (lineup != null)
                {
                    foreach (Transform entry in lineup.transform)
                    {
                        var icon = entry.GetComponent<Image>();
                        Require(icon != null
                                && Approximately((entry as RectTransform).sizeDelta,
                                    new Vector2(LifecycleEventIconSize, LifecycleEventIconSize))
                                && icon.sprite != null
                                && entry.GetComponent<Outline>() == null,
                            $"event_icon_entry_style_invalid name={entry.name}",
                            errors);
                    }
                }
            }

            var canonicalFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CanonicalEnglishFontPath);
            Require(canonicalFont != null, "canonical_english_font_missing", errors);
            if (canonicalFont != null)
            {
                foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true)
                             .Where(text => text.name != "PHS Event Alert Label" && !ContainsHangul(text.text)))
                {
                    Require(text.font == canonicalFont,
                        $"english_font_mismatch text={GetPath(text.transform)}", errors);
                }
            }

            var koreanAlertFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallbackFontPath);
            var alertLabel = Find(prefab.transform, "PHS Event Alert Label")
                ?.GetComponent<TextMeshProUGUI>();
            Require(koreanAlertFont != null && alertLabel != null && alertLabel.font == koreanAlertFont,
                "event_alert_korean_font_missing", errors);

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
                "PHS_HUD_SSO_VALIDATION_OK gauges=2 userLayoutPreserved=true " +
                "englishFontUnified=true networkVariant=true");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Restore User Refined HUD Layout")]
        public static void RestoreUserRefinedHudLayout()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var hudRoot = Find(root.transform, "Hud Root") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=hud_root_missing");
                var economy = Find(root.transform, "Economy Cluster") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=economy_cluster_missing");
                var boost = Find(root.transform, "Boost Row") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=boost_row_missing");

                economy.SetParent(hudRoot, false);
                economy.anchorMin = economy.anchorMax = new Vector2(0f, 1f);
                economy.pivot = new Vector2(0f, 1f);
                economy.anchoredPosition = new Vector2(210f, -24f);
                economy.localScale = Vector3.one;
                boost.SetParent(hudRoot, false);
                boost.anchorMin = boost.anchorMax = new Vector2(0f, 1f);
                boost.pivot = new Vector2(0f, 1f);
                boost.anchoredPosition = new Vector2(-27f, -69f);
                boost.localScale = Vector3.one;

                var playerEconomy = Find(root.transform, "Player Economy Cluster");
                if (playerEconomy != null)
                {
                    UnityEngine.Object.DestroyImmediate(playerEconomy.gameObject);
                }

                var mission = Find(root.transform, "Mission Status Cluster") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=mission_status_cluster_missing");
                var missionBackground = Find(mission, "Mission Status Background");
                if (missionBackground != null)
                {
                    UnityEngine.Object.DestroyImmediate(missionBackground.gameObject);
                }
                mission.anchorMin = mission.anchorMax = Vector2.one;
                mission.pivot = Vector2.one;
                mission.anchoredPosition = new Vector2(-32f, -28f);
                mission.sizeDelta = new Vector2(440f, 180f);
                mission.localScale = Vector3.one;

                var timeRoot = Find(mission, "Time Root") as RectTransform
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=time_root_missing");
                timeRoot.anchorMin = timeRoot.anchorMax = Vector2.one;
                timeRoot.pivot = Vector2.one;
                timeRoot.anchoredPosition = new Vector2(44.1f, 30.3f);
                timeRoot.sizeDelta = new Vector2(420f, 76f);
                timeRoot.localScale = Vector3.one * 1.5279f;
                var timeText = Find(timeRoot, "Time Limit Text")?.GetComponent<TMP_Text>()
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=time_limit_text_missing");
                timeText.rectTransform.anchoredPosition = new Vector2(55.299988f, -1.0999985f);
                timeText.rectTransform.sizeDelta = new Vector2(-8f, -4f);
                timeText.fontSize = 60f;

                RestoreGaugeRow(root, "Ship HP Root", -58f);
                RestoreGaugeRow(root, "Warp Gauge Root", -104f);
                RestoreHeldItemLayout(root);

                var controller = root.GetComponentInChildren<PHSHudFeedbackController>(true)
                    ?? throw new InvalidOperationException(
                        "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=feedback_controller_missing");
                var gauges = ResolveExistingVitalsGauges(root);
                var controllerData = new SerializedObject(controller);
                SetReference(controllerData, "economyRoot", economy.gameObject);
                SetReference(controllerData, "warpGaugeMotion", gauges.warp);
                SetReference(controllerData, "shipHpGaugeMotion", gauges.ship);
                controllerData.ApplyModifiedPropertiesWithoutUndo();

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
                "PHS_HUD_USER_LAYOUT_RESTORE_OK economy=210,-24 boost=-27,-69 " +
                "mission=-32,-28 timeScale=1.5279 heldSlotHeight=170");
        }

        private static void RestoreGaugeRow(GameObject root, string name, float y)
        {
            var row = Find(root.transform, name) as RectTransform
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=gauge_row_missing name={name}");
            row.anchorMin = row.anchorMax = Vector2.one;
            row.pivot = Vector2.one;
            row.anchoredPosition = new Vector2(0f, y);
            row.sizeDelta = new Vector2(380f, 64f);
            row.localScale = Vector3.one;
        }

        private static void RestoreHeldItemLayout(GameObject root)
        {
            var slot = Find(root.transform, "Held Item Slot") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_slot_missing");
            var frame = Find(slot, "Held Item Frame") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_frame_missing");
            var name = Find(frame, "Held Item Name Text") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_name_missing");
            var icon = Find(frame, "Held Item Icon") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_icon_missing");
            var durability = Find(frame, "Held Item Durability Text") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_durability_missing");
            var segments = Find(frame, "Held Item Durability Segments") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_LAYOUT_ROLLBACK_FAILED reason=held_item_segments_missing");

            slot.anchoredPosition = new Vector2(0f, 95f);
            slot.sizeDelta = new Vector2(slot.sizeDelta.x, 170f);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = Vector2.zero;
            name.SetAsFirstSibling();
            name.anchorMin = name.anchorMax = new Vector2(0.5f, 1f);
            name.pivot = new Vector2(0.5f, 1f);
            name.anchoredPosition = new Vector2(0f, -6f);
            icon.SetSiblingIndex(1);
            durability.SetSiblingIndex(2);
            durability.anchorMin = durability.anchorMax = new Vector2(0.5f, 0f);
            durability.pivot = new Vector2(0.5f, 0.5f);
            durability.anchoredPosition = new Vector2(0f, -8f);
            segments.SetSiblingIndex(3);
            segments.anchorMin = segments.anchorMax = new Vector2(0.5f, 0f);
            segments.pivot = new Vector2(0.5f, 0.5f);
            segments.anchoredPosition = new Vector2(0f, -27f);
        }

        private static (
            ParkHanSolHudGaugeMotion warp,
            ParkHanSolHudGaugeMotion ship) ResolveExistingVitalsGauges(GameObject root)
        {
            var warp = Find(root.transform, "Warp Gauge Bar")
                ?.GetComponent<ParkHanSolHudGaugeMotion>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=warp_gauge_motion_missing");
            var ship = Find(root.transform, "Ship HP Bar")
                ?.GetComponent<ParkHanSolHudGaugeMotion>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=ship_hp_gauge_motion_missing");
            return (warp, ship);
        }

        public static void ValidateHudGraphicCurvatureOrThrow()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var hudRoot = Find(root.transform, "Hud Root") as RectTransform;
                if (hudRoot == null)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_GRAPHIC_CURVATURE_VALIDATION_FAILED reason=hud_root_missing");
                }

                var errors = new List<string>();
                var graphicCount = 0;
                foreach (var graphic in hudRoot.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic is not Image && graphic is not RawImage)
                    {
                        continue;
                    }

                    graphicCount++;
                    var curve = graphic.GetComponent<PHSCurvedHudMeshEffect>();
                    Require(curve != null,
                        $"curve_missing path={GetPath(graphic.transform)}", errors);
                    if (curve == null)
                    {
                        continue;
                    }

                    var curveData = new SerializedObject(curve);
                    Require(curveData.FindProperty("referenceRect")?.objectReferenceValue == hudRoot,
                        $"curve_reference_invalid path={GetPath(graphic.transform)}", errors);
                }

                Require(graphicCount > 0, "hud_graphic_missing", errors);
                if (errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "PHS_HUD_GRAPHIC_CURVATURE_VALIDATION_FAILED\n- " +
                        string.Join("\n- ", errors));
                }

                Debug.Log(
                    $"PHS_HUD_GRAPHIC_CURVATURE_VALIDATION_OK graphics={graphicCount} " +
                    "reference=hud_root");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

            var iconTransform = Find(alertRoot.transform, "PHS Event Alert Icon");
            var createdIcon = iconTransform == null;
            var iconObject = iconTransform == null
                ? new GameObject(
                    "PHS Event Alert Icon",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Outline))
                : iconTransform.gameObject;
            var iconRect = iconObject.GetComponent<RectTransform>();
            if (createdIcon)
            {
                iconRect.SetParent(alertRoot.transform, false);
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.sizeDelta = new Vector2(46f, 46f);
                iconRect.localScale = Vector3.one;
                iconRect.localEulerAngles = new Vector3(0f, 0f, 45f);
            }

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
            SetReference(viewData, "eventAlertIcon", iconObject);
            var labelTransform = Find(alertRoot.transform, "PHS Event Alert Label");
            var createdLabel = labelTransform == null;
            var labelObject = labelTransform == null
                ? new GameObject(
                    "PHS Event Alert Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI))
                : labelTransform.gameObject;
            var labelRect = labelObject.GetComponent<RectTransform>();
            if (createdLabel)
            {
                labelRect.SetParent(alertRoot.transform, false);
            }

            // The alert root position is user-authored.  Only normalize its content
            // area: concurrent event names need room below the compact icon grid.
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -68f);
            labelRect.sizeDelta = new Vector2(260f, 128f);
            labelRect.localScale = Vector3.one;
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFallbackFontPath)
                ?? throw new InvalidOperationException(
                    $"PHS_HUD_SSO_AUTHOR_FAILED reason=korean_alert_font_missing path={KoreanFallbackFontPath}");
            label.fontSharedMaterial = label.font.material;
            label.fontSize = 18f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = new Color(1f, 0.31f, 0.08f, 1f);
            label.raycastTarget = false;
            SetReference(viewData, "eventAlertLabelText", label);
            viewData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(eventHudView);
        }

        private static RectTransform EnsureContainer(Transform parent, string name)
        {
            var existing = Find(parent, name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var container = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(parent, false);
            return container;
        }

        private static void EnsureSharedBackground(RectTransform parent, string name)
        {
            var background = Find(parent, name) as RectTransform;
            if (background == null)
            {
                background = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)).GetComponent<RectTransform>();
                background.SetParent(parent, false);
            }

            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = Vector2.zero;
            background.sizeDelta = Vector2.zero;
            background.localScale = Vector3.one;
            background.SetAsFirstSibling();
            var image = background.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = new Color(0.01f, 0.025f, 0.045f, 0.82f);
            image.raycastTarget = false;
        }

        private static void ConfigureEconomyCluster(GameObject root, RectTransform economy)
        {
            var playerEconomy = EnsureContainer(root.transform, "Player Economy Cluster");
            playerEconomy.anchorMin = new Vector2(0f, 1f);
            playerEconomy.anchorMax = new Vector2(0f, 1f);
            playerEconomy.pivot = new Vector2(0f, 1f);
            playerEconomy.anchoredPosition = new Vector2(24f, -24f);
            playerEconomy.sizeDelta = new Vector2(340f, 94f);
            playerEconomy.localScale = Vector3.one;
            EnsureSharedBackground(playerEconomy, "Player Economy Background");

            economy.SetParent(playerEconomy, false);
            economy.anchorMin = new Vector2(0f, 1f);
            economy.anchorMax = new Vector2(0f, 1f);
            economy.pivot = new Vector2(0f, 1f);
            economy.anchoredPosition = new Vector2(12f, -10f);
            economy.sizeDelta = new Vector2(316f, 40f);
            economy.localScale = Vector3.one;
            economy.gameObject.SetActive(true);

            var boost = Find(root.transform, "Boost Row") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=boost_row_missing");
            boost.SetParent(playerEconomy, false);
            boost.anchorMin = new Vector2(0f, 1f);
            boost.anchorMax = new Vector2(0f, 1f);
            boost.pivot = new Vector2(0f, 1f);
            boost.anchoredPosition = new Vector2(12f, -54f);
            boost.sizeDelta = new Vector2(316f, 28f);
            boost.localScale = Vector3.one;

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

        private static void ConfigureHeldItemLayout(GameObject root)
        {
            var heldRoot = Find(root.transform, "Held Item Slot") as RectTransform
                ?? throw new InvalidOperationException("PHS_HUD_SSO_AUTHOR_FAILED reason=held_item_slot_missing");
            var frame = Find(heldRoot, "Held Item Frame") as RectTransform;
            if (frame == null)
            {
                var frameObject = new GameObject("Held Item Frame", typeof(RectTransform));
                frame = frameObject.GetComponent<RectTransform>();
                frame.SetParent(heldRoot, false);

            }
            var name = Find(frame, "Held Item Name Text") as RectTransform
                ?? throw new InvalidOperationException("PHS_HUD_SSO_AUTHOR_FAILED reason=held_item_name_missing");
            var durability = Find(frame, "Held Item Durability Text") as RectTransform
                ?? throw new InvalidOperationException("PHS_HUD_SSO_AUTHOR_FAILED reason=held_item_durability_missing");
            var segments = Find(frame, "Held Item Durability Segments") as RectTransform
                ?? throw new InvalidOperationException("PHS_HUD_SSO_AUTHOR_FAILED reason=held_item_segments_missing");
            var icon = Find(heldRoot, "Held Item Icon") as RectTransform
                ?? throw new InvalidOperationException("PHS_HUD_SSO_AUTHOR_FAILED reason=held_item_icon_missing");

            // Single vertical reading order: name → icon → durability text/segments.
            // Reparent each visual every pass so recovered prefabs cannot leave the icon
            // outside the frame with a conflicting anchored coordinate.
            name.SetParent(frame, false);
            icon.SetParent(frame, false);
            durability.SetParent(frame, false);
            segments.SetParent(frame, false);

            heldRoot.sizeDelta = new Vector2(240f, 180f);
            frame.anchorMin = Vector2.zero;
            frame.anchorMax = Vector2.one;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = Vector2.zero;
            name.anchorMin = name.anchorMax = new Vector2(0.5f, 1f);
            name.pivot = new Vector2(0.5f, 1f);
            name.anchoredPosition = new Vector2(0f, -6f);
            name.sizeDelta = new Vector2(220f, 28f);

            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(0f, 12f);
            icon.sizeDelta = new Vector2(72f, 72f);

            durability.anchorMin = durability.anchorMax = new Vector2(0.5f, 0f);
            durability.pivot = new Vector2(0.5f, 0.5f);
            durability.anchoredPosition = new Vector2(0f, 44f);
            durability.sizeDelta = new Vector2(180f, 22f);
            segments.anchorMin = segments.anchorMax = new Vector2(0.5f, 0f);
            segments.pivot = new Vector2(0.5f, 0.5f);
            segments.anchoredPosition = new Vector2(0f, 18f);
            segments.sizeDelta = new Vector2(180f, 18f);
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
            mission.anchoredPosition = new Vector2(-32f, -24f);
            mission.sizeDelta = new Vector2(440f, 200f);
            mission.localScale = Vector3.one;
            EnsureSharedBackground(mission, "Mission Status Background");

            var timeRoot = Find(root.transform, "Time Root") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=time_root_missing");
            timeRoot.SetParent(mission, false);
            timeRoot.anchorMin = Vector2.one;
            timeRoot.anchorMax = Vector2.one;
            timeRoot.pivot = Vector2.one;
            timeRoot.anchoredPosition = Vector2.zero;
            timeRoot.sizeDelta = new Vector2(420f, 58f);
            timeRoot.localScale = Vector3.one;
            var timeText = Find(timeRoot, "Time Limit Text")?.GetComponent<TMP_Text>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=time_limit_text_missing");
            timeText.rectTransform.anchoredPosition = Vector2.zero;
            timeText.rectTransform.sizeDelta = Vector2.zero;
            timeText.fontSize = 78f;

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
            ConfigureGaugeRow(warpRoot, new Vector2(0f, -132f));

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
            row.sizeDelta = new Vector2(380f, 64f);
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
            bar.anchoredPosition = new Vector2(80f, 0f);
            bar.sizeDelta = new Vector2(300f, 30f);
            bar.localScale = Vector3.one;

            var background = bar.GetComponent<Image>() ?? bar.gameObject.AddComponent<Image>();
            // Null source image uses the uGUI white sprite: strict flat 2D rectangle,
            // not the inherited rounded HUD skin.
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = new Color(0.015f, 0.025f, 0.04f, 0.96f);
            background.raycastTarget = false;
            var outline = bar.GetComponent<Outline>();
            if (outline != null)
            {
                UnityEngine.Object.DestroyImmediate(outline);
            }

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

            foreach (var tick in bar.Cast<Transform>()
                         .Where(child => child.name.StartsWith("Gauge Tick ", StringComparison.Ordinal))
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(tick.gameObject);
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
            valueText.fontStyle = FontStyles.Bold;
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.color = Color.white;
            valueText.raycastTarget = false;
            valueText.rectTransform.SetAsLastSibling();

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
            image.sprite = null;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
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
            UnityEngine.Object.DestroyImmediate(lineup.GetComponent<VerticalLayoutGroup>());
            var grid = lineup.GetComponent<GridLayoutGroup>()
                ?? lineup.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset();
            grid.cellSize = new Vector2(LifecycleEventIconSize, LifecycleEventIconSize);
            grid.spacing = new Vector2(LifecycleEventIconSpacing, LifecycleEventIconSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            var lineupRect = lineup.GetComponent<RectTransform>();
            lineupRect.anchorMin = new Vector2(0.5f, 1f);
            lineupRect.anchorMax = new Vector2(0.5f, 1f);
            lineupRect.pivot = new Vector2(0.5f, 1f);
            // Do not overwrite the user-authored alert location.
            lineupRect.sizeDelta = new Vector2(244f, 144f);
            lineupRect.localScale = Vector3.one;

            UnityEngine.Object.DestroyImmediate(lineup.GetComponent<ContentSizeFitter>());

            foreach (Transform entry in lineup.transform)
            {
                // Entry roots are layout containers, not graphics.  Curvature requires a
                // Graphic. Lifecycle entries own their FlatSF Image, so retain it.
                UnityEngine.Object.DestroyImmediate(
                    entry.GetComponent<PHSCurvedHudMeshEffect>());
                UnityEngine.Object.DestroyImmediate(entry.GetComponent<Outline>());
                var entryRect = entry as RectTransform;
                entryRect.sizeDelta = new Vector2(LifecycleEventIconSize, LifecycleEventIconSize);
                entryRect.localScale = Vector3.one;
                var layout = entry.GetComponent<LayoutElement>()
                    ?? entry.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = LifecycleEventIconSize;
            layout.minHeight = LifecycleEventIconSize;
            layout.preferredWidth = LifecycleEventIconSize;
            layout.preferredHeight = LifecycleEventIconSize;

                var image = entry.GetComponent<Image>();
                if (image != null)
                {
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
                FontStyles.Bold);
            var priceText = EnsureShopText(
                panelRect,
                "Price",
                Vector2.zero,
                36f,
                FontStyles.Bold);
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
            nameText.gameObject.SetActive(false);
            priceText.gameObject.SetActive(true);
            pickupText.gameObject.SetActive(false);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureAlertIconReadability(GameObject root)
        {
            var view = root.GetComponentInChildren<PHSNetworkEventHudView>(true)
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_hud_view_missing");
            var lineup = new SerializedObject(view).FindProperty("iconLineupRoot")
                ?.objectReferenceValue as GameObject
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_icon_lineup_missing");
            var grid = lineup.GetComponent<GridLayoutGroup>()
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_icon_grid_missing");

            grid.cellSize = new Vector2(LifecycleEventIconSize, LifecycleEventIconSize);
            grid.spacing = new Vector2(LifecycleEventIconSpacing, LifecycleEventIconSpacing);
            var lineupRect = lineup.GetComponent<RectTransform>();
            lineupRect.sizeDelta = new Vector2(244f, 144f);

            foreach (Transform entry in lineup.transform)
            {
                var entryRect = entry as RectTransform;
                entryRect.sizeDelta = new Vector2(LifecycleEventIconSize, LifecycleEventIconSize);
                var layout = entry.GetComponent<LayoutElement>();
                if (layout == null) continue;
                layout.minWidth = layout.minHeight = LifecycleEventIconSize;
                layout.preferredWidth = layout.preferredHeight = LifecycleEventIconSize;
            }
        }

        private static void ConfigureHudGraphicCurvature(GameObject root)
        {
            var hudRoot = Find(root.transform, "Hud Root") as RectTransform
                ?? throw new InvalidOperationException(
                    "PHS_HUD_GRAPHIC_CURVATURE_AUTHOR_FAILED reason=hud_root_missing");

            foreach (var graphic in hudRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is not Image && graphic is not RawImage)
                {
                    continue;
                }

                var curve = graphic.GetComponent<PHSCurvedHudMeshEffect>()
                    ?? graphic.gameObject.AddComponent<PHSCurvedHudMeshEffect>();
                curve.Configure(hudRoot, HudGraphicCurvature);
                EditorUtility.SetDirty(curve);
            }
        }

        private static void ConfigureLifecycleEventIcons(GameObject root)
        {
            const string iconDirectory =
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/IncidentIcons/FlatSF_v2/";
            var definitions = new[]
            {
                (EventId.Fire, "Fire", "PHS_Incident_Fire_FlatSF.png"),
                (EventId.EnemySpawn, "Enemy Spawn", "PHS_Incident_EnemySpawn_FlatSF.png"),
                (EventId.PowerOff, "Power Off", "PHS_Incident_PowerFailure_FlatSF.png"),
                (EventId.OxygenLeak, "Oxygen Leak", "PHS_Incident_OxygenFailure_FlatSF.png"),
                (EventId.EngineBreak, "Engine Break", "PHS_Incident_DeviceFailure_FlatSF.png"),
                (EventId.MicDestroy, "Mic Destroy", "PHS_Incident_DeviceFailure_FlatSF.png"),
                (EventId.HullBreach, "Hull Breach", "PHS_Incident_HullBreach_FlatSF.png"),
                (EventId.SteamLeak, "Steam Leak", "PHS_Incident_SteamLeak_FlatSF.png"),
                (EventId.OxygenGeneratorFailure, "Oxygen Generator Failure", "PHS_Incident_OxygenFailure_FlatSF.png"),
                (EventId.GravityGeneratorFailure, "Gravity Generator Failure", "PHS_Incident_GravityFailure_FlatSF.png"),
                (EventId.EnemyScout, "Enemy Scout", "PHS_Incident_EnemyScout_FlatSF.png"),
                (EventId.MeteorAttack, "Meteor Attack", "PHS_Incident_MeteorAttack_FlatSF.png"),
                (EventId.EmpAttack, "EMP Attack", "PHS_Incident_EmpAttack_FlatSF.png")
            };
            var view = root.GetComponentInChildren<PHSNetworkEventHudView>(true)
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_hud_view_missing");
            var data = new SerializedObject(view);
            var lineup = data.FindProperty("iconLineupRoot")?.objectReferenceValue as GameObject
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=event_icon_lineup_missing");

            foreach (var child in lineup.transform.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var entries = data.FindProperty("lifecycleIconEntries")
                ?? throw new InvalidOperationException(
                    "PHS_HUD_SSO_AUTHOR_FAILED reason=lifecycle_icon_entries_missing");
            entries.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconDirectory + definition.Item3)
                    ?? throw new InvalidOperationException(
                        $"PHS_HUD_SSO_AUTHOR_FAILED reason=lifecycle_icon_sprite_missing path={iconDirectory}{definition.Item3}");
                var iconObject = new GameObject(
                    $"PHS Lifecycle Icon {definition.Item2}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(LayoutElement));
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.SetParent(lineup.transform, false);
                iconRect.localScale = Vector3.one;
                var image = iconObject.GetComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;

                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("eventId").intValue = (int)definition.Item1;
                entry.FindPropertyRelative("root").objectReferenceValue = iconObject;
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
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
                if (text.name == "PHS Event Alert Label" || ContainsHangul(text.text))
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

        private static bool Approximately(Vector3 actual, Vector3 expected)
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
