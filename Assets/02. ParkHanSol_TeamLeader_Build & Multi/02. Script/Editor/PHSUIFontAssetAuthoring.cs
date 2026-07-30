using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSUIFontAssetAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string FontRoot = Root + "/_ThirdParty/Fonts";
        private const string SuitSourceRoot = FontRoot + "/SUIT/Source";
        private const string SuiteSourceRoot = FontRoot + "/SUITE/Source";
        private const string SuitOutputRoot = FontRoot + "/SUIT/TMP";
        private const string SuiteOutputRoot = FontRoot + "/SUITE/TMP";
        private const int StaticAtlasSize = 2048;
        private const int SamplingPointSize = 54;
        private const int AtlasPadding = 7;

        public const string SuitRegularAssetPath = PHSUIFontPaths.SuitRegular;
        public const string SuitMediumAssetPath = PHSUIFontPaths.SuitMedium;
        public const string SuitSemiBoldAssetPath = PHSUIFontPaths.SuitSemiBold;
        public const string SuitBoldAssetPath = PHSUIFontPaths.SuitBold;
        public const string SuiteSemiBoldAssetPath = PHSUIFontPaths.SuiteSemiBold;
        public const string SuiteBoldAssetPath = PHSUIFontPaths.SuiteBold;
        private const string CanonicalTmpSettingsPath =
            "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static readonly HashSet<string> ReplacedFontPaths = new()
        {
            Root + "/_ThirdParty/Fonts/Maplestory Light SDF.asset",
            "Assets/99. DownloadAssets/TextMesh Pro/Fonts/Maplestory Bold SDF.asset",
            "Assets/99. DownloadAssets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset",
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset"
        };

        private static readonly string[] SerializedUiPrefabPaths =
        {
            Root + "/03. Prefab/Props/Prefabs/ShopCheckoutCounter/PHS_ShopCheckoutCounter.prefab",
            Root + "/03. Prefab/Shop/PHS_NetworkShopCheckoutCounter.prefab",
            Root + "/03. Prefab/Shop/PHS_ShopDisplayDesk.prefab",
            Root + "/03. Prefab/Shop/PHS_ShopDisplayDesk_Shared.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialDisplayDesk.prefab",
            Root + "/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab",
            Root + "/03. Prefab/UI/ParkHanSol_StartLobbyUI.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkOwnerPauseUI.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkRoomListEntry.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkRunResultPanel.prefab",
            Root + "/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab",
            Root + "/03. Prefab/UI/Customization/PHS_LobbyCustomizationPanel.prefab",
            Root + "/03. Prefab/UI/Customization/PHS_NetworkLobbyCustomizationFrontend.prefab",
            Root + "/03. Prefab/UI/Maps/PHS_HandheldShipMap.prefab"
        };

        private static readonly string[] GeneratedFontAssetPaths =
        {
            SuitRegularAssetPath,
            SuitMediumAssetPath,
            SuitSemiBoldAssetPath,
            SuitBoldAssetPath,
            SuiteSemiBoldAssetPath,
            SuiteBoldAssetPath
        };

        private static readonly IReadOnlyDictionary<string, int> BuildSceneLegacyFontCounts =
            new Dictionary<string, int>
            {
                {
                    Root + "/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity",
                    45
                },
                {
                    Root + "/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity",
                    7
                },
                {
                    Root + "/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity",
                    5
                }
            };

        private const string RequiredKoreanUiGlyphs =
            "상호작용 현재 구역 구매 불가 중력장 비활성 함선 체력 워프 진행도 " +
            "이벤트 지도 상태 경고 방 참가 나가기 준비 시작 실패 성공 호스트 연결 종료 " +
            "가격 보유 크레딧 장비 장착 해제 모자 등 장식 플레이어 근 볼 승 탑";

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Author Unified UI Font Assets")]
        public static void Author()
        {
            EnsureSourceAssets();
            EnsureGeneratedAssetsDoNotExist();
            EnsureOutputFolders();
            var glyphs = CollectProjectUiGlyphs();

            var suitRegular = CreateStaticFontAsset(
                SuitSourceRoot + "/SUIT-Regular.ttf",
                SuitRegularAssetPath,
                "SUIT Regular SDF",
                glyphs);
            var suitMedium = CreateStaticFontAsset(
                SuitSourceRoot + "/SUIT-Medium.ttf",
                SuitMediumAssetPath,
                "SUIT Medium SDF",
                glyphs);
            var suitSemiBold = CreateStaticFontAsset(
                SuitSourceRoot + "/SUIT-SemiBold.ttf",
                SuitSemiBoldAssetPath,
                "SUIT SemiBold SDF",
                glyphs);
            var suitBold = CreateStaticFontAsset(
                SuitSourceRoot + "/SUIT-Bold.ttf",
                SuitBoldAssetPath,
                "SUIT Bold SDF",
                glyphs);
            var suiteSemiBold = CreateStaticFontAsset(
                SuiteSourceRoot + "/SUITE-SemiBold.ttf",
                SuiteSemiBoldAssetPath,
                "SUITE SemiBold SDF",
                glyphs);
            var suiteBold = CreateStaticFontAsset(
                SuiteSourceRoot + "/SUITE-Bold.ttf",
                SuiteBoldAssetPath,
                "SUITE Bold SDF",
                glyphs);

            ConfigureWeightTable(suitRegular, 500, suitMedium);
            ConfigureWeightTable(suitRegular, 600, suitSemiBold);
            ConfigureWeightTable(suitRegular, 700, suitBold);
            ConfigureWeightTable(suiteSemiBold, 700, suiteBold);
            AssetDatabase.SaveAssets();

            ValidateGeneratedAssets(glyphs);
            Debug.Log(
                "PHS_UI_FONT_AUTHOR_OK sources=6 static=6 fallback=0 " +
                $"glyphs={glyphs.Length} atlas={StaticAtlasSize}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Validate Unified UI Font Assets")]
        public static void ValidateFromMenu()
        {
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Add Missing UI Glyphs")]
        public static void AddMissingUiGlyphs()
        {
            var glyphs = CollectProjectUiGlyphs();
            foreach (var path in GeneratedFontAssetPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
                    ?? throw new InvalidOperationException(
                        $"PHS_UI_FONT_GLYPH_ADD_FAILED reason=font_missing path={path}");
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.TryAddCharacters(glyphs, out var missingCharacters);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                if (missingCharacters.Length > 0)
                {
                    throw new InvalidOperationException(
                        "PHS_UI_FONT_GLYPH_ADD_FAILED " +
                        $"reason=source_glyphs_missing path={path} " +
                        $"count={missingCharacters.Length} chars={missingCharacters}");
                }

                EditorUtility.SetDirty(font);
            }

            AssetDatabase.SaveAssets();
            ValidateGeneratedAssets(glyphs);
            Debug.Log(
                $"PHS_UI_FONT_GLYPH_ADD_OK fonts={GeneratedFontAssetPaths.Length} " +
                $"requiredGlyphs={glyphs.Length}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Apply Unified UI Fonts")]
        public static void ApplyUnifiedUiFonts()
        {
            var originalGuids = CaptureGeneratedFontGuids();
            ClearStaticFontFallbacks();
            ConfigureCanonicalTmpSettings();
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
            var textCount = 0;
            foreach (var prefabPath in SerializedUiPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    textCount += ApplyTypography(root);
                    if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                    {
                        throw new InvalidOperationException(
                            $"PHS_UI_FONT_APPLY_FAILED reason=prefab_save_failed path={prefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            ValidateGeneratedFontGuids(originalGuids);
            ValidateUnifiedUiFonts();
            Debug.Log(
                $"PHS_UI_FONT_APPLY_OK prefabs={SerializedUiPrefabPaths.Length} texts={textCount}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Validate Applied UI Fonts")]
        public static void ValidateUnifiedUiFonts()
        {
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
            var textCount = 0;
            var replacedFontCount = 0;
            var styleCount = 0;
            var materialMismatchCount = 0;
            var roleMismatchCount = 0;
            var unresolvedGlyphCount = 0;
            var missingScriptCount = 0;
            foreach (var prefabPath in SerializedUiPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    ?? throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=prefab_missing path={prefabPath}");
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                        transform.gameObject);
                }

                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    textCount++;
                    if (text.font == null || text.fontSharedMaterial == null)
                    {
                        materialMismatchCount++;
                        continue;
                    }

                    if (ReplacedFontPaths.Contains(AssetDatabase.GetAssetPath(text.font)))
                    {
                        replacedFontCount++;
                    }

                    if (text.fontStyle != FontStyles.Normal ||
                        text.fontWeight != FontWeight.Regular)
                    {
                        styleCount++;
                    }

                    if (text.fontSharedMaterial.mainTexture != text.font.atlasTexture)
                    {
                        materialMismatchCount++;
                    }

                    var expectedFont = PHSUIFontPaths.Load(
                        PHSUIFontPaths.ResolveRole(text));
                    if (text.font != expectedFont)
                    {
                        roleMismatchCount++;
                    }

                    foreach (var character in text.text ?? string.Empty)
                    {
                        if (!char.IsControl(character) &&
                            !text.font.HasCharacter(character, false, false))
                        {
                            unresolvedGlyphCount++;
                        }
                    }
                }
            }

            ValidateCanonicalTmpSettings();
            if (replacedFontCount + styleCount + materialMismatchCount +
                roleMismatchCount + unresolvedGlyphCount + missingScriptCount > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UI_FONT_VALIDATION_FAILED " +
                    $"oldFonts={replacedFontCount} styles={styleCount} " +
                    $"materials={materialMismatchCount} roles={roleMismatchCount} " +
                    $"glyphs={unresolvedGlyphCount} missingScripts={missingScriptCount}");
            }

            Debug.Log(
                $"PHS_UI_FONT_APPLIED_VALIDATION_OK prefabs={SerializedUiPrefabPaths.Length} " +
                $"texts={textCount} oldFonts=0 styles=0 materials=0 roles=0 " +
                "glyphs=0 missingScripts=0 fallbackRefs=0");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Apply Unified UI Fonts To Build Scenes")]
        public static void ApplyUnifiedUiFontsToBuildScenes()
        {
            var activeScenePath = ValidateBuildScenePreflight();
            var appliedTextCount = 0;
            var incompatibleCustomMaterialCount = 0;
            foreach (var pair in BuildSceneLegacyFontCounts)
            {
                var scene = EditorSceneManager.OpenScene(
                    pair.Key,
                    OpenSceneMode.Additive);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                        {
                            if (!IsReplacedFont(text.font))
                            {
                                continue;
                            }

                            var targetFont = PHSUIFontPaths.Load(
                                PHSUIFontPaths.ResolveRole(text));
                            if (IsIncompatibleCustomMaterial(text, targetFont))
                            {
                                incompatibleCustomMaterialCount++;
                            }

                            PHSUIFontPaths.ApplyResolved(text);
                            appliedTextCount++;
                        }
                    }

                    if (!EditorSceneManager.SaveScene(scene, pair.Key, false))
                    {
                        throw new InvalidOperationException(
                            $"PHS_UI_SCENE_FONT_APPLY_FAILED reason=scene_save_failed path={pair.Key}");
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                ValidateActiveSceneGuard(activeScenePath);
            }

            ValidateAppliedBuildSceneFontsInternal(activeScenePath);
            Debug.Log(
                $"PHS_UI_SCENE_FONT_APPLY_OK scenes={BuildSceneLegacyFontCounts.Count} " +
                $"texts={appliedTextCount} incompatibleCustomMaterialsReplaced=" +
                incompatibleCustomMaterialCount);
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Fonts/Validate Applied Build Scene Fonts")]
        public static void ValidateAppliedBuildSceneFonts()
        {
            var activeScenePath = RequireCleanActiveScene();
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
            ValidateAppliedBuildSceneFontsInternal(activeScenePath);
        }

        public static int ApplyTypography(GameObject root)
        {
            var count = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                PHSUIFontPaths.ApplyResolved(text);
                count++;
            }

            return count;
        }

        private static string ValidateBuildScenePreflight()
        {
            var activeScenePath = RequireCleanActiveScene();
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
            ValidateCanonicalTmpSettings();
            var legacyFontCount = 0;
            var incompatibleCustomMaterialCount = 0;
            foreach (var pair in BuildSceneLegacyFontCounts)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(pair.Key) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_SCENE_FONT_PREFLIGHT_FAILED reason=scene_missing path={pair.Key}");
                }

                if (SceneManager.GetSceneByPath(pair.Key).isLoaded)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_SCENE_FONT_PREFLIGHT_FAILED reason=target_scene_already_loaded path={pair.Key}");
                }

                var scan = ScanBuildScene(pair.Key);
                if (scan.LegacyFontCount != 0 &&
                    scan.LegacyFontCount != pair.Value)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_SCENE_FONT_PREFLIGHT_FAILED reason=legacy_font_count_unexpected " +
                        $"path={pair.Key} expected={pair.Value} actual={scan.LegacyFontCount}");
                }

                if (scan.MissingScriptCount + scan.MissingReferenceCount > 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_SCENE_FONT_PREFLIGHT_FAILED reason=missing_references " +
                        $"path={pair.Key} missingScripts={scan.MissingScriptCount} " +
                        $"missingRefs={scan.MissingReferenceCount}");
                }

                legacyFontCount += scan.LegacyFontCount;
                incompatibleCustomMaterialCount +=
                    scan.IncompatibleCustomMaterialCount;
                ValidateActiveSceneGuard(activeScenePath);
            }

            Debug.Log(
                $"PHS_UI_SCENE_FONT_PREFLIGHT_OK scenes={BuildSceneLegacyFontCounts.Count} " +
                $"legacyFonts={legacyFontCount} incompatibleCustomMaterials=" +
                incompatibleCustomMaterialCount);
            return activeScenePath;
        }

        private static void ValidateAppliedBuildSceneFontsInternal(
            string activeScenePath)
        {
            ValidateGeneratedAssets(CollectProjectUiGlyphs());
            ValidateCanonicalTmpSettings();
            var total = new BuildSceneFontScan();
            foreach (var pair in BuildSceneLegacyFontCounts)
            {
                var scan = ScanBuildScene(pair.Key);
                total.Add(scan);
                ValidateActiveSceneGuard(activeScenePath);
            }

            if (total.LegacyFontCount + total.StyleCount +
                total.MaterialMismatchCount + total.RoleMismatchCount +
                total.MissingGlyphCount + total.MissingScriptCount +
                total.MissingReferenceCount > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UI_SCENE_FONT_VALIDATION_FAILED " +
                    $"oldFonts={total.LegacyFontCount} styles={total.StyleCount} " +
                    $"materials={total.MaterialMismatchCount} roles={total.RoleMismatchCount} " +
                    $"glyphs={total.MissingGlyphCount} missingScripts={total.MissingScriptCount} " +
                    $"missingRefs={total.MissingReferenceCount} fallbackRefs=0");
            }

            Debug.Log(
                $"PHS_UI_SCENE_FONT_VALIDATION_OK scenes={BuildSceneLegacyFontCounts.Count} " +
                $"texts={total.TextCount} oldFonts=0 styles=0 materials=0 roles=0 " +
                "glyphs=0 missingScripts=0 missingRefs=0 fallbackRefs=0");
        }

        private static BuildSceneFontScan ScanBuildScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var scan = new BuildSceneFontScan();
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        scan.MissingScriptCount +=
                            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                                transform.gameObject);
                    }

                    foreach (var component in root.GetComponentsInChildren<Component>(true))
                    {
                        if (component != null)
                        {
                            scan.MissingReferenceCount += CountMissingObjectReferences(
                                component);
                        }
                    }

                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        scan.TextCount++;
                        if (IsReplacedFont(text.font))
                        {
                            scan.LegacyFontCount++;
                            var targetFont = PHSUIFontPaths.Load(
                                PHSUIFontPaths.ResolveRole(text));
                            if (IsIncompatibleCustomMaterial(text, targetFont))
                            {
                                scan.IncompatibleCustomMaterialCount++;
                            }
                        }

                        if (text.font == null || text.fontSharedMaterial == null)
                        {
                            scan.MaterialMismatchCount++;
                            continue;
                        }

                        if (text.fontStyle != FontStyles.Normal ||
                            text.fontWeight != FontWeight.Regular)
                        {
                            scan.StyleCount++;
                        }

                        if (text.fontSharedMaterial.mainTexture != text.font.atlasTexture)
                        {
                            scan.MaterialMismatchCount++;
                        }

                        if (text.font != PHSUIFontPaths.Load(
                                PHSUIFontPaths.ResolveRole(text)))
                        {
                            scan.RoleMismatchCount++;
                        }

                        foreach (var character in text.text ?? string.Empty)
                        {
                            if (!char.IsControl(character) &&
                                !text.font.HasCharacter(character, false, false))
                            {
                                scan.MissingGlyphCount++;
                            }
                        }
                    }
                }

                return scan;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int CountMissingObjectReferences(Component component)
        {
            var count = 0;
            var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                var referenceEntityId = property.propertyType ==
                    SerializedPropertyType.ObjectReference
                    ? property.objectReferenceEntityIdValue
                    : default;
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null &&
                    referenceEntityId != default)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsReplacedFont(TMP_FontAsset font)
        {
            return font != null &&
                ReplacedFontPaths.Contains(AssetDatabase.GetAssetPath(font));
        }

        private static bool IsIncompatibleCustomMaterial(
            TMP_Text text,
            TMP_FontAsset targetFont)
        {
            var currentFont = text.font;
            var currentMaterial = text.fontSharedMaterial;
            return currentFont != null &&
                currentMaterial != null &&
                currentMaterial != currentFont.material &&
                currentMaterial.mainTexture != targetFont.atlasTexture;
        }

        private static string RequireCleanActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            var protectedPath =
                Root + "/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
            if (!activeScene.IsValid() ||
                activeScene.path != protectedPath ||
                activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_SCENE_FONT_PREFLIGHT_FAILED reason=active_scene_guard " +
                    $"expected={protectedPath} actual={activeScene.path} dirty={activeScene.isDirty}");
            }

            return activeScene.path;
        }

        private static void ValidateActiveSceneGuard(string expectedPath)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() ||
                activeScene.path != expectedPath ||
                activeScene.isDirty)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_SCENE_FONT_VALIDATION_FAILED reason=active_scene_changed " +
                    $"expected={expectedPath} actual={activeScene.path} dirty={activeScene.isDirty}");
            }
        }

        private sealed class BuildSceneFontScan
        {
            public int TextCount;
            public int LegacyFontCount;
            public int StyleCount;
            public int MaterialMismatchCount;
            public int RoleMismatchCount;
            public int MissingGlyphCount;
            public int MissingScriptCount;
            public int MissingReferenceCount;
            public int IncompatibleCustomMaterialCount;

            public void Add(BuildSceneFontScan other)
            {
                TextCount += other.TextCount;
                LegacyFontCount += other.LegacyFontCount;
                StyleCount += other.StyleCount;
                MaterialMismatchCount += other.MaterialMismatchCount;
                RoleMismatchCount += other.RoleMismatchCount;
                MissingGlyphCount += other.MissingGlyphCount;
                MissingScriptCount += other.MissingScriptCount;
                MissingReferenceCount += other.MissingReferenceCount;
                IncompatibleCustomMaterialCount +=
                    other.IncompatibleCustomMaterialCount;
            }
        }

        private static TMP_FontAsset CreateStaticFontAsset(
            string sourcePath,
            string outputPath,
            string assetName,
            string glyphs)
        {
            var fontAsset = CreateFontAsset(
                sourcePath,
                outputPath,
                assetName,
                AtlasPopulationMode.Dynamic,
                StaticAtlasSize,
                false);
            fontAsset.TryAddCharacters(glyphs, out var missingCharacters);
            if (missingCharacters.Length > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_FONT_AUTHOR_FAILED reason=direct_glyphs_missing font={assetName} " +
                    $"count={missingCharacters.Length} chars={missingCharacters}");
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static TMP_FontAsset CreateFontAsset(
            string sourcePath,
            string outputPath,
            string assetName,
            AtlasPopulationMode populationMode,
            int atlasSize,
            bool multiAtlas)
        {
            AssetDatabase.ImportAsset(sourcePath, ImportAssetOptions.ForceSynchronousImport);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (sourceFont == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_FONT_AUTHOR_FAILED reason=source_missing path={sourcePath}");
            }

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_FONT_AUTHOR_FAILED reason=existing_asset_preserved path={outputPath}");
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SDFAA,
                atlasSize,
                atlasSize,
                populationMode,
                multiAtlas);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_FONT_AUTHOR_FAILED reason=create_failed path={sourcePath}");
            }

            fontAsset.name = assetName;
            fontAsset.isMultiAtlasTexturesEnabled = multiAtlas;
            fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();

            AssetDatabase.CreateAsset(fontAsset, outputPath);
            PersistSubAsset(fontAsset.atlasTexture, fontAsset, assetName + " Atlas");
            PersistSubAsset(fontAsset.material, fontAsset, assetName + " Material");
            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static void PersistSubAsset(
            UnityEngine.Object subAsset,
            TMP_FontAsset owner,
            string name)
        {
            if (subAsset == null || AssetDatabase.Contains(subAsset))
            {
                return;
            }

            subAsset.name = name;
            AssetDatabase.AddObjectToAsset(subAsset, owner);
        }

        private static void ConfigureWeightTable(
            TMP_FontAsset baseFont,
            int weight,
            TMP_FontAsset typeface)
        {
            var index = weight / 100;
            var table = baseFont.fontWeightTable;
            var pair = table[index];
            pair.regularTypeface = typeface;
            table[index] = pair;
            EditorUtility.SetDirty(baseFont);
        }

        private static string CollectProjectUiGlyphs()
        {
            var characters = new SortedSet<char>();
            for (var code = 32; code <= 126; code++)
            {
                characters.Add((char)code);
            }

            AddCharacters(characters, RequiredKoreanUiGlyphs);
            AddCharacters(characters, "\u00A0\u200B\u2026\u25A1₩℃×→←↑↓±·");

            foreach (var prefabPath in SerializedUiPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"PHS_UI_FONT_GLYPH_SOURCE_MISSING path={prefabPath}");
                    continue;
                }

                foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    AddCharacters(characters, text.text);
                }
            }

            var scriptRoot = Path.GetFullPath(Root + "/02. Script");
            foreach (var scriptPath in Directory.EnumerateFiles(
                         scriptRoot,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                AddRuntimeKoreanCharacters(
                    characters,
                    File.ReadAllText(scriptPath, Encoding.UTF8));
            }

            return new string(characters.ToArray());
        }

        private static void AddCharacters(ISet<char> target, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var character in value)
            {
                if (!char.IsControl(character))
                {
                    target.Add(character);
                }
            }
        }

        private static void AddRuntimeKoreanCharacters(
            ISet<char> target,
            string source)
        {
            foreach (var character in source)
            {
                if (character is >= '\uAC00' and <= '\uD7A3' ||
                    character is >= '\u1100' and <= '\u11FF' ||
                    character is >= '\u3130' and <= '\u318F')
                {
                    target.Add(character);
                }
            }
        }

        private static void EnsureSourceAssets()
        {
            var requiredPaths = new[]
            {
                SuitSourceRoot + "/SUIT-Regular.ttf",
                SuitSourceRoot + "/SUIT-Medium.ttf",
                SuitSourceRoot + "/SUIT-SemiBold.ttf",
                SuitSourceRoot + "/SUIT-Bold.ttf",
                SuiteSourceRoot + "/SUITE-SemiBold.ttf",
                SuiteSourceRoot + "/SUITE-Bold.ttf",
                FontRoot + "/Licenses/SUIT-OFL.txt",
                FontRoot + "/Licenses/SUITE-OFL.txt"
            };
            var missing = requiredPaths.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UI_FONT_AUTHOR_FAILED reason=source_or_license_missing " +
                    $"paths={string.Join(",", missing)}");
            }
        }

        private static void EnsureOutputFolders()
        {
            Directory.CreateDirectory(SuitOutputRoot);
            Directory.CreateDirectory(SuiteOutputRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureGeneratedAssetsDoNotExist()
        {
            var existing = GeneratedFontAssetPaths
                .Where(path => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null)
                .ToArray();
            if (existing.Length > 0)
            {
                throw new InvalidOperationException(
                    "PHS_UI_FONT_AUTHOR_FAILED reason=existing_assets_preserved " +
                    $"count={existing.Length} paths={string.Join(",", existing)}");
            }
        }

        private static Dictionary<string, string> CaptureGeneratedFontGuids()
        {
            var result = new Dictionary<string, string>();
            foreach (var path in GeneratedFontAssetPaths)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrWhiteSpace(guid))
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_APPLY_FAILED reason=font_guid_missing path={path}");
                }

                result.Add(path, guid);
            }

            return result;
        }

        private static void ValidateGeneratedFontGuids(
            IReadOnlyDictionary<string, string> originalGuids)
        {
            foreach (var pair in originalGuids)
            {
                var currentGuid = AssetDatabase.AssetPathToGUID(pair.Key);
                if (!string.Equals(pair.Value, currentGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=font_guid_changed " +
                        $"path={pair.Key} before={pair.Value} after={currentGuid}");
                }
            }
        }

        private static void ClearStaticFontFallbacks()
        {
            foreach (var path in GeneratedFontAssetPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
                    ?? throw new InvalidOperationException(
                        $"PHS_UI_FONT_APPLY_FAILED reason=font_missing path={path}");
                font.fallbackFontAssetTable = new List<TMP_FontAsset>();
                EditorUtility.SetDirty(font);
            }
        }

        private static void ValidateGeneratedAssets(string requiredGlyphs)
        {
            foreach (var path in GeneratedFontAssetPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)
                    ?? throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=asset_missing path={path}");
                if (font.atlasPopulationMode != AtlasPopulationMode.Static)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=not_static path={path}");
                }

                if (font.material == null || font.atlasTexture == null ||
                    font.material.mainTexture != font.atlasTexture)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=material_atlas_mismatch path={path}");
                }

                var fallback = font.fallbackFontAssetTable;
                if (fallback != null && fallback.Count != 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=fallback_not_empty path={path} " +
                        $"count={fallback.Count}");
                }

                font.HasCharacters(requiredGlyphs, out var missingCharacters);
                if (missingCharacters.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_UI_FONT_VALIDATION_FAILED reason=direct_glyphs_missing path={path} " +
                        $"count={missingCharacters.Count} chars={new string(missingCharacters.ToArray())}");
                }
            }

            ValidateWeightTypeface(SuitRegularAssetPath, 500, SuitMediumAssetPath);
            ValidateWeightTypeface(SuitRegularAssetPath, 600, SuitSemiBoldAssetPath);
            ValidateWeightTypeface(SuitRegularAssetPath, 700, SuitBoldAssetPath);
            ValidateWeightTypeface(SuiteSemiBoldAssetPath, 700, SuiteBoldAssetPath);

            Debug.Log(
                "PHS_UI_FONT_VALIDATION_OK static=6 fallback=0 " +
                $"requiredGlyphs={requiredGlyphs.Length}");
        }

        private static void ValidateWeightTypeface(
            string baseFontPath,
            int weight,
            string expectedTypefacePath)
        {
            var baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(baseFontPath);
            var expectedTypeface = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                expectedTypefacePath);
            var actualTypeface = baseFont.fontWeightTable[weight / 100].regularTypeface;
            if (actualTypeface != expectedTypeface)
            {
                throw new InvalidOperationException(
                    $"PHS_UI_FONT_VALIDATION_FAILED reason=weight_table_invalid " +
                    $"base={baseFontPath} weight={weight} expected={expectedTypefacePath}");
            }
        }

        private static void ConfigureCanonicalTmpSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                CanonicalTmpSettingsPath)
                ?? throw new InvalidOperationException(
                    $"PHS_UI_FONT_APPLY_FAILED reason=tmp_settings_missing path={CanonicalTmpSettingsPath}");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue =
                PHSUIFontPaths.Load(PHSUIFontRole.Body);
            var fallbacks = serialized.FindProperty("m_fallbackFontAssets");
            fallbacks.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ValidateCanonicalTmpSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                CanonicalTmpSettingsPath)
                ?? throw new InvalidOperationException(
                    $"PHS_UI_FONT_VALIDATION_FAILED reason=tmp_settings_missing path={CanonicalTmpSettingsPath}");
            var serialized = new SerializedObject(settings);
            var defaultFont = serialized.FindProperty("m_defaultFontAsset")
                .objectReferenceValue as TMP_FontAsset;
            var fallbacks = serialized.FindProperty("m_fallbackFontAssets");
            if (AssetDatabase.GetAssetPath(defaultFont) != PHSUIFontPaths.SuitRegular ||
                fallbacks.arraySize != 0)
            {
                throw new InvalidOperationException(
                    "PHS_UI_FONT_VALIDATION_FAILED reason=tmp_settings_font_refs_invalid");
            }
        }
    }
}








