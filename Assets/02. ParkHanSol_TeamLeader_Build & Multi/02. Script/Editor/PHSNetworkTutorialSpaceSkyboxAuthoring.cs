#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkTutorialSpaceSkyboxAuthoring
    {
        private const int SourceWidth = 2048;
        private const int SourceHeight = 1024;
        private const int OutputWidth = 8192;
        private const int OutputHeight = 4096;
        private const int StarCount = 14000;
        private const string EnvironmentFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Environment/Tutorial";
        private const string TexturePath = EnvironmentFolder +
            "/PHS_NetworkTutorialSpaceSkybox.jpg";
        private const string MaterialPath = EnvironmentFolder +
            "/PHS_NetworkTutorialSpaceSkybox.mat";
        private const string LicensePath = EnvironmentFolder +
            "/PHS_NetworkTutorialSpaceSkybox_LICENSE.md";
        private const string TutorialScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Network Tutorial Space Skybox")]
        public static void Author()
        {
            EnsureFolder(EnvironmentFolder);
            Debug.Log(
                $"PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_START size={OutputWidth}x{OutputHeight} " +
                "source=self_created_procedural");

            Texture2D output = null;
            try
            {
                output = GeneratePanorama();
                File.WriteAllBytes(TexturePath, output.EncodeToJPG(92));
            }
            finally
            {
                if (output != null)
                {
                    UnityEngine.Object.DestroyImmediate(output);
                }
            }

            WriteLicenseNote();
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(LicensePath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporter();
            var material = CreateOrUpdateMaterial();
            AssignTutorialSkybox(material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                $"PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_OK texture={TexturePath} " +
                $"material={MaterialPath} scene={TutorialScenePath}");
        }

        private static Texture2D GeneratePanorama()
        {
            var source = new Texture2D(
                SourceWidth,
                SourceHeight,
                TextureFormat.RGB24,
                false,
                false)
            {
                name = "PHS_NetworkTutorialSpaceSkybox_Source",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var renderTexture = RenderTexture.GetTemporary(
                OutputWidth,
                OutputHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var previousRenderTexture = RenderTexture.active;
            try
            {
                source.SetPixels32(GenerateNebulaPixels());
                source.Apply(false, false);
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                var output = new Texture2D(
                    OutputWidth,
                    OutputHeight,
                    TextureFormat.RGB24,
                    false,
                    false)
                {
                    name = "PHS_NetworkTutorialSpaceSkybox"
                };
                output.ReadPixels(
                    new Rect(0f, 0f, OutputWidth, OutputHeight),
                    0,
                    0,
                    false);
                output.Apply(false, false);
                var pixels = output.GetPixels32();
                AddStars(pixels);
                CopySeamColumn(pixels);
                output.SetPixels32(pixels);
                output.Apply(false, false);
                return output;
            }
            finally
            {
                RenderTexture.active = previousRenderTexture;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Color32[] GenerateNebulaPixels()
        {
            var pixels = new Color32[SourceWidth * SourceHeight];
            var axis = new Vector3(0.28f, 0.92f, -0.27f).normalized;
            for (var y = 0; y < SourceHeight; y++)
            {
                var latitude = ((float)y / (SourceHeight - 1) - 0.5f) * Mathf.PI;
                var cosLatitude = Mathf.Cos(latitude);
                var sinLatitude = Mathf.Sin(latitude);
                for (var x = 0; x < SourceWidth; x++)
                {
                    var longitude = (float)x / (SourceWidth - 1) * Mathf.PI * 2f;
                    var direction = new Vector3(
                        cosLatitude * Mathf.Cos(longitude),
                        sinLatitude,
                        cosLatitude * Mathf.Sin(longitude));
                    var grainA = 0.5f + 0.5f * Mathf.Sin(
                        direction.x * 7.1f + direction.y * 10.7f + direction.z * 4.3f);
                    var grainB = 0.5f + 0.5f * Mathf.Sin(
                        direction.x * 15.3f - direction.y * 6.4f + direction.z * 12.8f + 1.7f);
                    var grainC = 0.5f + 0.5f * Mathf.Sin(
                        direction.x * 31.7f + direction.y * 19.1f - direction.z * 24.2f + 0.4f);
                    var bandDistance = Mathf.Abs(
                        Vector3.Dot(direction, axis) +
                        (grainB - 0.5f) * 0.12f);
                    var band = Mathf.Exp(-bandDistance * bandDistance / 0.055f);
                    var dust = Mathf.Clamp01(
                        grainA * 0.48f + grainB * 0.34f + grainC * 0.18f - 0.38f);
                    var nebula = band * dust * dust;
                    var violet = nebula * (0.55f + grainC * 0.45f);
                    var teal = band * Mathf.Clamp01(grainB - 0.52f) * 0.55f;
                    var red = 3f + violet * 42f + teal * 4f;
                    var green = 5f + violet * 15f + teal * 34f;
                    var blue = 12f + violet * 58f + teal * 43f;
                    pixels[y * SourceWidth + x] = new Color32(
                        ToByte(red),
                        ToByte(green),
                        ToByte(blue),
                        255);
                }
            }

            return pixels;
        }

        private static void AddStars(Color32[] pixels)
        {
            var random = new System.Random(20260723);
            var seamlessWidth = OutputWidth - 1;
            for (var index = 0; index < StarCount; index++)
            {
                var x = random.Next(0, seamlessWidth);
                var y = random.Next(2, OutputHeight - 2);
                var roll = random.NextDouble();
                var radius = roll > 0.995 ? 2 : roll > 0.93 ? 1 : 0;
                var brightness = random.Next(115, radius == 2 ? 221 : 196);
                var temperature = random.NextDouble();
                var starColor = temperature < 0.28
                    ? new Color32((byte)brightness, (byte)(brightness * 0.78f), (byte)(brightness * 0.62f), 255)
                    : temperature > 0.78
                        ? new Color32((byte)(brightness * 0.68f), (byte)(brightness * 0.82f), (byte)brightness, 255)
                        : new Color32((byte)brightness, (byte)brightness, (byte)(brightness * 0.94f), 255);
                DrawStar(pixels, x, y, radius, starColor, seamlessWidth);
            }
        }

        private static void DrawStar(
            Color32[] pixels,
            int centerX,
            int centerY,
            int radius,
            Color32 color,
            int seamlessWidth)
        {
            for (var offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                    if (distanceSquared > radius * radius + 1)
                    {
                        continue;
                    }

                    var x = (centerX + offsetX + seamlessWidth) % seamlessWidth;
                    var y = Mathf.Clamp(centerY + offsetY, 0, OutputHeight - 1);
                    var falloff = radius == 0
                        ? 1f
                        : Mathf.Clamp01(1f - Mathf.Sqrt(distanceSquared) / (radius + 0.7f));
                    var pixelIndex = y * OutputWidth + x;
                    var current = pixels[pixelIndex];
                    pixels[pixelIndex] = new Color32(
                        (byte)Mathf.Max(current.r, color.r * falloff),
                        (byte)Mathf.Max(current.g, color.g * falloff),
                        (byte)Mathf.Max(current.b, color.b * falloff),
                        255);
                }
            }
        }

        private static void CopySeamColumn(Color32[] pixels)
        {
            for (var y = 0; y < OutputHeight; y++)
            {
                pixels[y * OutputWidth + OutputWidth - 1] = pixels[y * OutputWidth];
            }
        }

        private static void ConfigureTextureImporter()
        {
            var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_FAILED reason=texture_importer_missing");
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = OutputWidth;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 1;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 75;
            importer.isReadable = false;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true;
            standalone.maxTextureSize = OutputWidth;
            standalone.format = TextureImporterFormat.DXT1;
            standalone.textureCompression = TextureImporterCompression.CompressedHQ;
            standalone.compressionQuality = 75;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial()
        {
            var shader = Shader.Find("LastJumpCrew/Skybox/Panoramic");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_FAILED reason=panoramic_shader_missing");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "PHS_NetworkTutorialSpaceSkybox"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null)
            {
                throw new InvalidOperationException(
                    "PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_FAILED reason=texture_asset_missing");
            }

            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Exposure", 1.3f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignTutorialSkybox(Material material)
        {
            var previousScene = SceneManager.GetActiveScene();
            var openedAdditively = previousScene.path != TutorialScenePath;
            var tutorialScene = openedAdditively
                ? EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Additive)
                : previousScene;
            try
            {
                SceneManager.SetActiveScene(tutorialScene);
                RenderSettings.skybox = material;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 0.65f;
                RenderSettings.reflectionIntensity = 0.55f;
                EditorSceneManager.MarkSceneDirty(tutorialScene);
                if (!EditorSceneManager.SaveScene(tutorialScene))
                {
                    throw new InvalidOperationException(
                        "PHS_NETWORK_TUTORIAL_SKYBOX_AUTHOR_FAILED reason=scene_save_failed");
                }
            }
            finally
            {
                if (openedAdditively)
                {
                    EditorSceneManager.CloseScene(tutorialScene, true);
                    if (previousScene.IsValid() && previousScene.isLoaded)
                    {
                        SceneManager.SetActiveScene(previousScene);
                    }
                }
            }
        }

        private static void WriteLicenseNote()
        {
            File.WriteAllText(
                LicensePath,
                "# PHS Network Tutorial Space Skybox\n\n" +
                "Original self-created procedural artwork generated inside the LastJumpCrew Unity project.\n" +
                "No external image, model, texture, or generative service was used.\n" +
                "Created for LastJumpCrew / BEAVER 2026 tutorial presentation.\n");
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }
    }
}
#endif
