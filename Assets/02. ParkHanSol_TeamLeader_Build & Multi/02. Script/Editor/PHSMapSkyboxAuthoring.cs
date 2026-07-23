#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    internal static class PHSMapSkyboxCatalog
    {
        internal const string Folder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Environment/MapSkyboxes";
        internal const string ShaderPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Environment/PHS_PanoramicSkybox.shader";
        internal const string ShaderName = "LastJumpCrew/Skybox/Panoramic";

        internal static readonly Definition[] Definitions =
        {
            new Definition(8001, "WasteOrbit", 1f),
            new Definition(8002, "AsteroidField", 0.78f),
            new Definition(8003, "BrokenSatellites", 1f),
            new Definition(8004, "NebulaDebris", 1f)
        };

        internal sealed class Definition
        {
            internal Definition(
                int mapId,
                string suffix,
                float exposure)
            {
                MapId = mapId;
                Suffix = suffix;
                Exposure = exposure;
            }

            internal int MapId { get; }
            internal string Suffix { get; }
            internal float Exposure { get; }
            internal string BaseName => $"PHS_Map_{MapId}_{Suffix}";
            internal string TexturePath => $"{Folder}/{BaseName}_Panorama.png";
            internal string MaterialPath => $"{Folder}/{BaseName}_Skybox.mat";
            internal string ProfilePath =>
                $"Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/Maps/{BaseName}.asset";
        }
    }

    public static class PHSMapSkyboxAuthoring
    {
        private const string MenuPath =
            "Tools/ParkHanSol/BEAVER/Author Map Skyboxes";

        [MenuItem(MenuPath)]
        public static void Author()
        {
            var shader = RequirePrerequisites();
            var changedMaterials = 0;
            var changedProfiles = 0;
            var changedImporters = 0;

            foreach (var definition in PHSMapSkyboxCatalog.Definitions)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    definition.TexturePath);
                if (ConfigureTextureImporter(definition.TexturePath))
                {
                    changedImporters++;
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        definition.TexturePath);
                }

                var material = CreateOrUpdateMaterial(
                    definition,
                    shader,
                    texture,
                    out var materialChanged);
                if (materialChanged)
                {
                    changedMaterials++;
                }

                if (ConnectProfile(definition, material))
                {
                    changedProfiles++;
                }
            }

            AssetDatabase.SaveAssets();
            PHSMapSkyboxValidator.ValidateOrThrow();
            Debug.Log(
                "PHS_MAP_SKYBOX_AUTHOR_OK " +
                $"count={PHSMapSkyboxCatalog.Definitions.Length} " +
                $"importersChanged={changedImporters} " +
                $"materialsChanged={changedMaterials} " +
                $"profilesChanged={changedProfiles} " +
                $"folder={PHSMapSkyboxCatalog.Folder}");
        }

        private static Shader RequirePrerequisites()
        {
            var errors = new List<string>();
            if (!AssetDatabase.IsValidFolder(PHSMapSkyboxCatalog.Folder))
            {
                errors.Add($"folder_missing:path={PHSMapSkyboxCatalog.Folder}");
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                PHSMapSkyboxCatalog.ShaderPath);
            if (shader == null)
            {
                errors.Add($"shader_missing:path={PHSMapSkyboxCatalog.ShaderPath}");
            }
            else if (!string.Equals(
                         shader.name,
                         PHSMapSkyboxCatalog.ShaderName,
                         StringComparison.Ordinal))
            {
                errors.Add(
                    $"shader_name_mismatch:expected={PHSMapSkyboxCatalog.ShaderName}:actual={shader.name}");
            }

            foreach (var definition in PHSMapSkyboxCatalog.Definitions)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(definition.TexturePath) == null)
                {
                    errors.Add(
                        $"texture_missing:mapId={definition.MapId}:path={definition.TexturePath}");
                }

                var profile = AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>(
                    definition.ProfilePath);
                if (profile == null)
                {
                    errors.Add(
                        $"profile_missing:mapId={definition.MapId}:path={definition.ProfilePath}");
                }
                else if (profile.MapId != definition.MapId)
                {
                    errors.Add(
                        $"profile_id_mismatch:path={definition.ProfilePath}:" +
                        $"expected={definition.MapId}:actual={profile.MapId}");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_MAP_SKYBOX_AUTHOR_FAILED errors=" +
                    string.Join("|", errors));
            }

            return shader;
        }

        private static bool ConfigureTextureImporter(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"PHS_MAP_SKYBOX_AUTHOR_FAILED errors=texture_importer_missing:path={texturePath}");
            }

            var changed = false;
            changed |= SetIfDifferent(
                importer.textureType,
                TextureImporterType.Default,
                value => importer.textureType = value);
            changed |= SetIfDifferent(
                importer.textureShape,
                TextureImporterShape.Texture2D,
                value => importer.textureShape = value);
            changed |= SetIfDifferent(
                importer.alphaSource,
                TextureImporterAlphaSource.None,
                value => importer.alphaSource = value);
            changed |= SetIfDifferent(
                importer.sRGBTexture,
                true,
                value => importer.sRGBTexture = value);
            changed |= SetIfDifferent(
                importer.mipmapEnabled,
                true,
                value => importer.mipmapEnabled = value);
            changed |= SetIfDifferent(
                importer.maxTextureSize,
                2048,
                value => importer.maxTextureSize = value);
            changed |= SetIfDifferent(
                importer.wrapMode,
                TextureWrapMode.Repeat,
                value => importer.wrapMode = value);
            changed |= SetIfDifferent(
                importer.filterMode,
                FilterMode.Trilinear,
                value => importer.filterMode = value);
            changed |= SetIfDifferent(
                importer.anisoLevel,
                1,
                value => importer.anisoLevel = value);
            changed |= SetIfDifferent(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);
            changed |= SetIfDifferent(
                importer.compressionQuality,
                75,
                value => importer.compressionQuality = value);
            changed |= SetIfDifferent(
                importer.isReadable,
                false,
                value => importer.isReadable = value);

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            var standaloneChanged = !standalone.overridden
                || standalone.maxTextureSize != 2048
                || standalone.format != TextureImporterFormat.DXT1
                || standalone.textureCompression != TextureImporterCompression.CompressedHQ
                || standalone.compressionQuality != 75;
            if (standaloneChanged)
            {
                standalone.overridden = true;
                standalone.maxTextureSize = 2048;
                standalone.format = TextureImporterFormat.DXT1;
                standalone.textureCompression = TextureImporterCompression.CompressedHQ;
                standalone.compressionQuality = 75;
                importer.SetPlatformTextureSettings(standalone);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static Material CreateOrUpdateMaterial(
            PHSMapSkyboxCatalog.Definition definition,
            Shader shader,
            Texture2D texture,
            out bool changed)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                definition.MaterialPath);
            changed = false;
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = $"{definition.BaseName}_Skybox"
                };
                AssetDatabase.CreateAsset(material, definition.MaterialPath);
                changed = true;
            }

            if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }

            if (material.GetTexture("_MainTex") != texture)
            {
                material.SetTexture("_MainTex", texture);
                changed = true;
            }

            if (!Mathf.Approximately(
                    material.GetFloat("_Exposure"),
                    definition.Exposure))
            {
                material.SetFloat("_Exposure", definition.Exposure);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static bool ConnectProfile(
            PHSMapSkyboxCatalog.Definition definition,
            Material material)
        {
            var profile = AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>(
                definition.ProfilePath);
            var serializedProfile = new SerializedObject(profile);
            var skyboxMode = RequireProperty(serializedProfile, "skyboxMode");
            var gameplaySkybox = RequireProperty(
                serializedProfile,
                "gameplaySkybox");
            var arrivalSkybox = RequireProperty(
                serializedProfile,
                "arrivalSkybox");
            var expectedMode = (int)PHSMapSkyboxMode.ProfileMaterials;
            var changed = skyboxMode.enumValueIndex != expectedMode
                || gameplaySkybox.objectReferenceValue != material
                || arrivalSkybox.objectReferenceValue != material;
            if (!changed)
            {
                return false;
            }

            Undo.RecordObject(profile, "Connect Map Skybox Profile");
            skyboxMode.enumValueIndex = expectedMode;
            gameplaySkybox.objectReferenceValue = material;
            arrivalSkybox.objectReferenceValue = material;
            serializedProfile.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            return true;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "PHS_MAP_SKYBOX_AUTHOR_FAILED errors=" +
                    $"serialized_property_missing:type={serializedObject.targetObject.GetType().FullName}:" +
                    $"property={propertyName}");
            }

            return property;
        }

        private static bool SetIfDifferent<T>(
            T current,
            T expected,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }
    }

    public static class PHSMapSkyboxValidator
    {
        private const string MenuPath =
            "Tools/ParkHanSol/BEAVER/Validate Map Skyboxes";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                PHSMapSkyboxCatalog.ShaderPath);
            if (shader == null)
            {
                errors.Add($"shader_missing:path={PHSMapSkyboxCatalog.ShaderPath}");
            }

            foreach (var definition in PHSMapSkyboxCatalog.Definitions)
            {
                ValidateDefinition(definition, shader, errors);
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_MAP_SKYBOX_VALIDATE_FAILED errors=" +
                    string.Join("|", errors));
            }

            Debug.Log(
                "PHS_MAP_SKYBOX_VALIDATE_OK " +
                $"count={PHSMapSkyboxCatalog.Definitions.Length} " +
                $"folder={PHSMapSkyboxCatalog.Folder}");
        }

        private static void ValidateDefinition(
            PHSMapSkyboxCatalog.Definition definition,
            Shader expectedShader,
            List<string> errors)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                definition.TexturePath);
            if (texture == null)
            {
                errors.Add(
                    $"texture_missing:mapId={definition.MapId}:path={definition.TexturePath}");
            }
            else if (texture.width != texture.height * 2)
            {
                errors.Add(
                    $"texture_aspect_invalid:mapId={definition.MapId}:" +
                    $"expected=2:1:actual={texture.width}x{texture.height}");
            }

            ValidateTextureImporter(definition, errors);

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                definition.MaterialPath);
            if (material == null)
            {
                errors.Add(
                    $"material_missing:mapId={definition.MapId}:path={definition.MaterialPath}");
            }
            else
            {
                if (expectedShader == null || material.shader != expectedShader)
                {
                    errors.Add($"material_shader_mismatch:mapId={definition.MapId}");
                }

                if (material.GetTexture("_MainTex") != texture)
                {
                    errors.Add($"material_texture_mismatch:mapId={definition.MapId}");
                }

                if (!Mathf.Approximately(
                        material.GetFloat("_Exposure"),
                        definition.Exposure))
                {
                    errors.Add(
                        $"material_exposure_mismatch:mapId={definition.MapId}:" +
                        $"expected={definition.Exposure}:" +
                        $"actual={material.GetFloat("_Exposure")}");
                }
            }

            var profile = AssetDatabase.LoadAssetAtPath<PHSMapProfileSO>(
                definition.ProfilePath);
            if (profile == null)
            {
                errors.Add(
                    $"profile_missing:mapId={definition.MapId}:path={definition.ProfilePath}");
                return;
            }

            if (profile.MapId != definition.MapId)
            {
                errors.Add(
                    $"profile_id_mismatch:path={definition.ProfilePath}:" +
                    $"expected={definition.MapId}:actual={profile.MapId}");
            }

            if (profile.SkyboxMode != PHSMapSkyboxMode.ProfileMaterials)
            {
                errors.Add($"profile_skybox_mode_mismatch:mapId={definition.MapId}");
            }

            if (profile.GameplaySkybox != material)
            {
                errors.Add($"profile_gameplay_skybox_mismatch:mapId={definition.MapId}");
            }

            if (profile.ArrivalSkybox != material)
            {
                errors.Add($"profile_arrival_skybox_mismatch:mapId={definition.MapId}");
            }

            if (!profile.TryValidate(out var reason))
            {
                errors.Add(
                    $"profile_contract_invalid:mapId={definition.MapId}:reason={reason}");
            }
        }

        private static void ValidateTextureImporter(
            PHSMapSkyboxCatalog.Definition definition,
            List<string> errors)
        {
            var importer = AssetImporter.GetAtPath(
                definition.TexturePath) as TextureImporter;
            if (importer == null)
            {
                errors.Add(
                    $"texture_importer_missing:mapId={definition.MapId}:path={definition.TexturePath}");
                return;
            }

            if (importer.textureType != TextureImporterType.Default
                || importer.textureShape != TextureImporterShape.Texture2D
                || importer.alphaSource != TextureImporterAlphaSource.None
                || !importer.sRGBTexture
                || !importer.mipmapEnabled
                || importer.maxTextureSize != 2048
                || importer.wrapMode != TextureWrapMode.Repeat
                || importer.filterMode != FilterMode.Trilinear
                || importer.anisoLevel != 1
                || importer.textureCompression != TextureImporterCompression.CompressedHQ
                || importer.compressionQuality != 75
                || importer.isReadable)
            {
                errors.Add($"texture_import_settings_mismatch:mapId={definition.MapId}");
            }

            var standalone = importer.GetPlatformTextureSettings("Standalone");
            if (!standalone.overridden
                || standalone.maxTextureSize != 2048
                || standalone.format != TextureImporterFormat.DXT1
                || standalone.textureCompression != TextureImporterCompression.CompressedHQ
                || standalone.compressionQuality != 75)
            {
                errors.Add($"texture_standalone_settings_mismatch:mapId={definition.MapId}");
            }
        }
    }
}
#endif
