using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSShipMapRealtimeAuthoring
    {
        private readonly struct ShipNavMeshFootprint
        {
            public ShipNavMeshFootprint(Bounds bounds, Vector3[] vertices, int[] indices)
            {
                Bounds = bounds;
                Vertices = vertices;
                Indices = indices;
            }

            public Bounds Bounds { get; }
            public Vector3[] Vertices { get; }
            public int[] Indices { get; }
        }

        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/" +
            "PHS_CuteWhiteGhost_Player.prefab";
        private const string EmbeddedMapPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Maps/" +
            "PHS_HandheldShipMap.prefab";
        private const string RenderTexturePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/" +
            "PHS_ShipMapRealtime.renderTexture";
        private const string MaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/" +
            "PHS_ShipMapSchematic.mat";
        private const string IconDirectory =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/IncidentIcons/FlatSF_v2/";
        private const string PlayerIconPath = IconDirectory + "PHS_Map_Player_Diamond_FlatSF_v2.png";
        private const string WrenchIconPath = IconDirectory + "PHS_Map_Wrench_Diamond_FlatSF_v2.png";
        private const string FireExtinguisherIconPath = IconDirectory + "PHS_Map_FireExtinguisher_Diamond_FlatSF_v2.png";
        private const string BatteryIconPath = IconDirectory + "PHS_Map_Battery_Diamond_FlatSF_v2.png";
        private const string EnemySpawnIconPath = IconDirectory + "PHS_Incident_EnemySpawn_FlatSF.png";
        private static readonly string[] LegacyMapIconPaths =
        {
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/PHS_MapIcon_Vending.svg",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/PHS_MapIcon_Player.svg",
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/PHS_MapIcon_DiamondFrame.png"
        };
        private const string MapRenderLayerName = "MapRender";
        private static readonly Vector2 MapViewportSize = new(120f, 360f);
        private static readonly Vector2 PlayerMarkerSize = new(18f, 18f);
        private static readonly Color PlayerMarkerColor = new(0.9f, 0.96f, 1f, 1f);
        private static readonly Quaternion MarkerGlyphUprightRotation =
            Quaternion.Euler(0f, 0f, -90f);

        private static readonly (string PropertyName, string AssetPath)[] MapIconBindings =
        {
            ("fireIcon", IconDirectory + "PHS_Incident_Fire_FlatSF.png"),
            ("powerFailureIcon", IconDirectory + "PHS_Incident_PowerFailure_FlatSF.png"),
            ("deviceFailureIcon", IconDirectory + "PHS_Incident_DeviceFailure_FlatSF.png"),
            ("hullBreachIcon", IconDirectory + "PHS_Incident_HullBreach_FlatSF.png"),
            ("steamLeakIcon", IconDirectory + "PHS_Incident_SteamLeak_FlatSF.png"),
            ("oxygenFailureIcon", IconDirectory + "PHS_Incident_OxygenFailure_FlatSF.png"),
            ("gravityFailureIcon", IconDirectory + "PHS_Incident_GravityFailure_FlatSF.png"),
            ("enemySpawnIcon", EnemySpawnIconPath),
            ("patrolZoneIcon", IconDirectory + "PHS_Incident_PatrolZone_FlatSF.png"),
            ("meteorZoneIcon", IconDirectory + "PHS_Incident_MeteorZone_FlatSF.png"),
            ("nebulaZoneIcon", IconDirectory + "PHS_Incident_NebulaZone_FlatSF.png"),
            ("planetZoneIcon", IconDirectory + "PHS_Incident_PlanetZone_FlatSF.png"),
            ("powerSyncIcon", IconDirectory + "PHS_ExternalInteraction_EnemyScout_FlatSF.png"),
            ("cannonIcon", IconDirectory + "PHS_ExternalInteraction_MeteorAttack_FlatSF.png"),
            ("wireFixIcon", IconDirectory + "PHS_ExternalInteraction_EmpAttack_FlatSF.png"),
            ("warpIcon", IconDirectory + "PHS_Incident_PlanetZone_FlatSF.png"),
            ("batteryIcon", BatteryIconPath),
            ("wrenchIcon", WrenchIconPath),
            ("fireExtinguisherIcon", FireExtinguisherIconPath),
            ("playerIcon", PlayerIconPath)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Realtime Ship Map")]
        public static void Author()
        {
            var mapRenderLayer = EnsureMapRenderLayer();
            ConfigureFlatSpriteImportSettings();
            var mapTexture = CreateOrUpdateRenderTexture();
            var material = CreateOrUpdateSchematicMaterial();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var layout = UnityEngine.Object.FindFirstObjectByType<PHSShipMapWorldLayout>(
                FindObjectsInactive.Include);
            if (layout == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=layout_missing");
            }

            var rig = ConfigureSceneRig(layout, mapTexture, material, mapRenderLayer);
            ConfigureVendingMapIcons(layout);
            ExcludeMapRenderLayerFromSceneCameras(rig.MapCamera, mapRenderLayer);
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.SaveScene(scene);
            ConfigureEmbeddedMapPrefab(mapTexture);
            ConfigurePlayerPrefab(mapTexture, mapRenderLayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "PHS_SHIP_MAP_REALTIME_AUTHOR_PASS texture=240x720 " +
                "viewport=120x360 marker_style=diamond_glyph");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Ship Map Marker Presentation Only")]
        public static void AuthorMarkerPresentationOnly()
        {
            // Deliberately presentation-only. Do not open the map scene, rebuild
            // its schematic, or rewrite viewport/marker anchor positions.
            ConfigureMarkerPresentationOnlyPrefab(EmbeddedMapPrefabPath);
            ConfigureMarkerPresentationOnlyPrefab(PlayerPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMarkerPresentationPrefab(EmbeddedMapPrefabPath);
            ValidateMarkerPresentationPrefab(PlayerPrefabPath);
            Debug.Log(
                "PHS_SHIP_MAP_MARKER_AUTHOR_OK scope=prefabs_only root_rotation=identity " +
                "glyph_rotation=-90 layout_unchanged=true");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Ship Map Marker Presentation")]
        public static void ValidateMarkerPresentationOnly()
        {
            ValidateMarkerPresentationPrefab(EmbeddedMapPrefabPath);
            ValidateMarkerPresentationPrefab(PlayerPrefabPath);
            Debug.Log(
                "PHS_SHIP_MAP_MARKER_VALIDATE_OK root_rotation=identity " +
                "glyph_rotation=-90 refs=true");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author FlatSF v2 Map Icon Integration")]
        public static void AuthorFlatSfV2IconIntegration()
        {
            RecolorEnemySpawnIconOrange();
            RecolorWrenchIconPurple();
            ConfigureFlatSpriteImportSettings();
            ConfigurePlayerIconPresentation();
            foreach (var legacyPath in LegacyMapIconPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(legacyPath) != null
                    && !AssetDatabase.DeleteAsset(legacyPath))
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=legacy_icon_delete_failed path={legacyPath}");
                }
            }

            ValidateEnemySpawnIconOrange();
            ValidateWrenchIconPurple();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateFlatSfV2IconIntegration();
            Debug.Log("PHS_SHIP_MAP_ICON_AUTHOR_OK folder=FlatSF_v2 legacy_assets=3 player_name=generated");
        }

        private static void RecolorEnemySpawnIconOrange()
        {
            if (!File.Exists(EnemySpawnIconPath))
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=enemy_icon_missing path={EnemySpawnIconPath}");
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(EnemySpawnIconPath), false))
                {
                    throw new InvalidOperationException("PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=enemy_icon_load_failed");
                }

                var pixels = texture.GetPixels32();
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    if (pixel.a == 0 || pixel.r <= pixel.g || pixel.r <= pixel.b)
                    {
                        continue;
                    }

                    Color.RGBToHSV(pixel, out var hue, out var saturation, out var value);
                    if (hue > 0.12f && hue < 0.92f)
                    {
                        continue;
                    }

                    var orange = Color.HSVToRGB(0.075f, saturation, value);
                    pixels[index] = orange;
                    pixels[index].a = pixel.a;
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(EnemySpawnIconPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(EnemySpawnIconPath, ImportAssetOptions.ForceUpdate);
        }

        private static void ValidateEnemySpawnIconOrange()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(EnemySpawnIconPath), false))
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=enemy_icon_load_failed");
                }
                var redPixels = 0;
                foreach (var pixel in texture.GetPixels32())
                {
                    if (pixel.a > 127 && pixel.r > 120 && pixel.g < pixel.r * 0.28f && pixel.b < pixel.r * 0.4f)
                    {
                        redPixels++;
                    }
                }

                if (redPixels != 0)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=enemy_icon_red_legacy_pixels_present");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void RecolorWrenchIconPurple()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(WrenchIconPath), false))
                {
                    throw new InvalidOperationException("PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=wrench_icon_load_failed");
                }

                var pixels = texture.GetPixels32();
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    Color.RGBToHSV(pixel, out var hue, out var saturation, out var value);
                    if (pixel.a == 0 || saturation < 0.2f || hue < 0.42f || hue > 0.58f)
                    {
                        continue;
                    }

                    var purple = Color.HSVToRGB(0.75f, saturation, value);
                    pixels[index] = purple;
                    pixels[index].a = pixel.a;
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(WrenchIconPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(WrenchIconPath, ImportAssetOptions.ForceUpdate);
        }

        private static void ValidateWrenchIconPurple()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(WrenchIconPath), false))
                {
                    throw new InvalidOperationException("PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=wrench_icon_load_failed");
                }

                var cyanPixels = 0;
                foreach (var pixel in texture.GetPixels32())
                {
                    Color.RGBToHSV(pixel, out var hue, out var saturation, out _);
                    if (pixel.a > 127 && saturation > 0.35f && hue >= 0.42f && hue <= 0.58f)
                    {
                        cyanPixels++;
                    }
                }

                if (cyanPixels != 0)
                {
                    throw new InvalidOperationException("PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=wrench_icon_cyan_pixels_present");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate FlatSF v2 Map Icon Integration")]
        public static void ValidateFlatSfV2IconIntegration()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<NetworkPlayerDisplayName>() == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=player_display_name_component_missing");
                }

                var controller = root.GetComponentInChildren<PHSHandheldShipMapController>(true);
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=map_controller_missing");
                }

                var serializedController = new SerializedObject(controller);
                ValidateMapViewIconReferences(
                    serializedController.FindProperty("firstPersonView")?.objectReferenceValue as PHSHandheldShipMapView,
                    "first_person");
                ValidateMapViewIconReferences(
                    serializedController.FindProperty("worldView")?.objectReferenceValue as PHSHandheldShipMapView,
                    "world");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var legacyPath in LegacyMapIconPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(legacyPath) != null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=legacy_icon_not_removed path={legacyPath}");
                }
            }

            Debug.Log("PHS_SHIP_MAP_ICON_VALIDATE_PASS folder=FlatSF_v2 views=2 legacy_assets=0");
        }

        private static PHSShipMapRenderRig ConfigureSceneRig(
            PHSShipMapWorldLayout layout,
            RenderTexture mapTexture,
            Material material,
            int mapRenderLayer)
        {
            var shipNavMesh = CollectShipNavMeshFootprint();
            var bounds = shipNavMesh.Bounds;
            FilterOutOfMapObjectAnchors(layout, bounds);
            var center = bounds.center;
            var renderY = bounds.max.y + 12f;
            var root = GameObject.Find("PHS_ShipMapRenderRig")
                ?? new GameObject("PHS_ShipMapRenderRig");
            root.layer = mapRenderLayer;
            root.transform.position = new Vector3(center.x, 0f, center.z);
            var rig = root.GetComponent<PHSShipMapRenderRig>()
                ?? root.AddComponent<PHSShipMapRenderRig>();
            var schematicRoot = GetOrCreateChild(root.transform, "SchematicRoot");
            schematicRoot.gameObject.layer = mapRenderLayer;
            schematicRoot.localPosition = new Vector3(0f, renderY, 0f);
            schematicRoot.localRotation = Quaternion.identity;
            BuildSchematic(
                schematicRoot,
                shipNavMesh,
                root.transform.position,
                material,
                mapRenderLayer);
            var cameraTransform = GetOrCreateChild(root.transform, "MapCamera");
            cameraTransform.gameObject.layer = mapRenderLayer;
            cameraTransform.localPosition = new Vector3(0f, renderY + 50f, 0f);
            cameraTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mapCamera = cameraTransform.GetComponent<Camera>();
            if (mapCamera == null)
            {
                mapCamera = cameraTransform.gameObject.AddComponent<Camera>();
            }
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = CalculateOrthographicSize(bounds, mapTexture);
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.005f, 0.012f, 0.028f, 1f);
            mapCamera.cullingMask = 1 << mapRenderLayer;
            mapCamera.targetTexture = mapTexture;
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane = 160f;
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = false;
            mapCamera.enabled = false;
            SetObjectReference(rig, "mapCamera", mapCamera);
            SetObjectReference(rig, "mapTexture", mapTexture);
            SetObjectReference(rig, "schematicRoot", schematicRoot);
            SetObjectReference(layout, "mapRenderRig", rig);
            return rig;
        }

        private static void ConfigurePlayerPrefab(RenderTexture mapTexture, int mapRenderLayer)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.cullingMask &= ~(1 << mapRenderLayer);
                }

                var controller = root.GetComponentInChildren<PHSHandheldShipMapController>(true);
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=map_controller_missing");
                }

                EnsurePlayerDisplayName(root);

                var serializedController = new SerializedObject(controller);
                var firstPersonView = serializedController
                    .FindProperty("firstPersonView")
                    ?.objectReferenceValue as PHSHandheldShipMapView;
                var worldView = serializedController
                    .FindProperty("worldView")
                    ?.objectReferenceValue as PHSHandheldShipMapView;
                if (firstPersonView == null || worldView == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=map_view_reference_missing " +
                        $"first_person={firstPersonView != null} world={worldView != null}");
                }

                ConfigureMapView(firstPersonView, mapTexture);
                if (worldView != firstPersonView)
                {
                    ConfigureMapView(worldView, mapTexture);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureEmbeddedMapPrefab(RenderTexture mapTexture)
        {
            var root = PrefabUtility.LoadPrefabContents(EmbeddedMapPrefabPath);
            try
            {
                var mapField = Find(root.transform, "MapField") as RectTransform;
                var mapImage = FindMapImage(root.transform);
                if (mapField == null || mapImage == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=embedded_map_hierarchy_invalid");
                }

                ResolveMapViewport(mapField, mapImage);
                mapImage.texture = mapTexture;
                mapImage.color = Color.white;
                var mapView = root.GetComponentInChildren<PHSHandheldShipMapView>(true);
                if (mapView == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=embedded_map_view_missing");
                }

                ConfigureMapView(mapView, mapTexture);
                PrefabUtility.SaveAsPrefabAsset(root, EmbeddedMapPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureMapView(PHSHandheldShipMapView view, RenderTexture mapTexture)
        {
            var mapField = Find(view.transform, "MapField") as RectTransform;
            var mapImage = FindMapImage(view.transform);
            if (mapField != null && mapImage != null)
            {
                EnsureMarkerHierarchy(view, mapField, mapImage);
            }

            var markerRoot = Find(view.transform, "MarkerRoot") as RectTransform;
            var markerTemplate = Find(view.transform, "MarkerTemplate")?.GetComponent<Image>();
            var incidentRail = Find(view.transform, "Incident Rail") as RectTransform;
            if (mapField == null || markerRoot == null || mapImage == null
                || markerTemplate == null || incidentRail == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=view_hierarchy_invalid view={view.name}");
            }

            var viewport = ResolveMapViewport(mapField, mapImage);
            mapImage.texture = mapTexture;
            mapImage.color = Color.white;
            ConfigureViewportChild(markerRoot, viewport, new Vector2(-8f, -8f));
            RemoveEmptyDuplicateViewports(mapField, viewport);
            if (incidentRail.GetComponent<RectMask2D>() == null)
            {
                incidentRail.gameObject.AddComponent<RectMask2D>();
            }

            ConfigureMarkerPresentation(view, markerTemplate);
            SetObjectReference(view, "mapImage", mapImage);
            SetObjectReference(view, "markerRoot", markerRoot);
            SetObjectReference(view, "markerTemplate", markerTemplate);
            SetObjectReference(
                view,
                "markerLabelTemplate",
                markerTemplate.transform.Find("MarkerLabel")?.GetComponent<TMP_Text>());
        }

        // The standalone handheld-map source is intentionally a display shell.  Its
        // runtime marker hierarchy lives on the player-held prefab, so copy only
        // that overlay hierarchy when this source prefab has none.  Do not touch
        // the authored schematic, map viewport size, or RenderRig.
        private static void EnsureMarkerHierarchy(
            PHSHandheldShipMapView view,
            RectTransform mapField,
            RawImage mapImage)
        {
            RectTransform existingRoot = null;
            existingRoot = Find(view.transform, "MarkerRoot") as RectTransform;
            if (existingRoot != null
                && existingRoot.Find("MarkerTemplate")?.GetComponent<Image>() != null)
            {
                return;
            }

            var mapViewport = ResolveMapViewport(mapField, mapImage);
            var playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var sourceView = playerRoot.GetComponentInChildren<PHSHandheldShipMapView>(true);
                var sourceRoot = sourceView == null
                    ? null
                    : Find(sourceView.transform, "MarkerRoot") as RectTransform;
                if (sourceRoot == null
                    || sourceRoot.Find("MarkerTemplate")?.GetComponent<Image>() == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=player_marker_source_missing");
                }

                if (existingRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingRoot.gameObject);
                }

                var markerRootObject = UnityEngine.Object.Instantiate(sourceRoot.gameObject, mapViewport, false);
                markerRootObject.name = "MarkerRoot";
                var markerRoot = markerRootObject.GetComponent<RectTransform>();
                ConfigureViewportChild(markerRoot, mapViewport, new Vector2(-8f, -8f));
                markerRoot.localRotation = Quaternion.identity;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigurePlayerIconPresentation()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                EnsurePlayerDisplayName(root);
                var controller = root.GetComponentInChildren<PHSHandheldShipMapController>(true);
                if (controller == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=map_controller_missing");
                }

                var serializedController = new SerializedObject(controller);
                var firstPersonView = serializedController.FindProperty("firstPersonView")
                    ?.objectReferenceValue as PHSHandheldShipMapView;
                var worldView = serializedController.FindProperty("worldView")
                    ?.objectReferenceValue as PHSHandheldShipMapView;
                if (firstPersonView == null || worldView == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=map_view_reference_missing");
                }

                ConfigureMarkerPresentation(
                    firstPersonView,
                    Find(firstPersonView.transform, "MarkerTemplate")?.GetComponent<Image>());
                if (worldView != firstPersonView)
                {
                    ConfigureMarkerPresentation(
                        worldView,
                        Find(worldView.transform, "MarkerTemplate")?.GetComponent<Image>());
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsurePlayerDisplayName(GameObject playerRoot)
        {
            if (playerRoot.GetComponent<NetworkPlayerDisplayName>() == null)
            {
                playerRoot.AddComponent<NetworkPlayerDisplayName>();
            }
        }

        private static void ConfigureMarkerPresentation(
            PHSHandheldShipMapView view,
            Image markerTemplate)
        {
            if (view == null || markerTemplate == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=view_or_marker_template_missing");
            }

            var markerGlyph = EnsureMarkerGlyph(markerTemplate);
            ConfigureMarkerLabel(markerTemplate);
            var markerRoot = markerTemplate.transform.parent as RectTransform;
            if (markerRoot == null || markerRoot.name != "MarkerRoot")
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=marker_root_missing");
            }

            // Marker positions are authored in MarkerRoot space. Rotating that
            // container also rotates every coordinate around the map centre, so
            // keep the coordinate space stable and counter-rotate visuals only.
            markerRoot.localRotation = Quaternion.identity;
            markerTemplate.rectTransform.localRotation = Quaternion.identity;
            markerTemplate.rectTransform.localScale = Vector3.one;
            markerGlyph.rectTransform.localRotation = MarkerGlyphUprightRotation;
            markerTemplate.sprite = null;
            markerTemplate.color = Color.clear;
            markerTemplate.enabled = false;
            SetObjectReference(view, "markerGlyphTemplate", markerGlyph);
            ConfigurePlayerMarkerPriority(view);
            ConfigureMapIconReferences(view);
        }

        private static void ConfigureMarkerPresentationOnlyPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var views = root.GetComponentsInChildren<PHSHandheldShipMapView>(true);
                if (views.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=view_missing prefab={prefabPath}");
                }

                foreach (var view in views)
                {
                    var serialized = new SerializedObject(view);
                    var marker = serialized.FindProperty("markerTemplate")?.objectReferenceValue as Image;
                    var markerRoot = serialized.FindProperty("markerRoot")?.objectReferenceValue as RectTransform;
                    if (marker == null || markerRoot == null)
                    {
                        throw new InvalidOperationException(
                            $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=marker_reference_missing " +
                            $"prefab={prefabPath} view={view.name}");
                    }

                    var rootPosition = markerRoot.anchoredPosition;
                    var rootSize = markerRoot.sizeDelta;
                    var markerPosition = marker.rectTransform.anchoredPosition;
                    var markerSize = marker.rectTransform.sizeDelta;
                    ConfigureMarkerPresentation(view, marker);
                    if (markerRoot.anchoredPosition != rootPosition
                        || markerRoot.sizeDelta != rootSize
                        || marker.rectTransform.anchoredPosition != markerPosition
                        || marker.rectTransform.sizeDelta != markerSize)
                    {
                        throw new InvalidOperationException(
                            $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=layout_changed " +
                            $"prefab={prefabPath} view={view.name}");
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigurePlayerMarkerPriority(PHSHandheldShipMapView view)
        {
            var serialized = new SerializedObject(view);
            var size = serialized.FindProperty("playerMarkerSize");
            var color = serialized.FindProperty("playerMarkerColor");
            if (size == null || color == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_ICON_AUTHOR_FAILED reason=player_marker_priority_property_missing");
            }

            size.vector2Value = PlayerMarkerSize;
            color.colorValue = PlayerMarkerColor;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RawImage FindMapImage(Transform viewRoot)
        {
            var firstPersonImage = Find(viewRoot, "Neon Map Reference")?.GetComponent<RawImage>();
            var worldImage = Find(viewRoot, "Actual Ship Map")?.GetComponent<RawImage>();
            if (firstPersonImage != null && worldImage != null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=map_image_ambiguous view={viewRoot.name}");
            }

            if (firstPersonImage != null)
            {
                return firstPersonImage;
            }

            if (worldImage != null)
            {
                return worldImage;
            }

            return null;
        }

        private static void ConfigureViewportChild(
            RectTransform child,
            RectTransform viewport,
            Vector2 sizeDelta)
        {
            child.SetParent(viewport, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.pivot = new Vector2(0.5f, 0.5f);
            child.anchoredPosition = Vector2.zero;
            child.sizeDelta = sizeDelta;
            child.localScale = Vector3.one;
        }

        private static RectTransform ResolveMapViewport(
            RectTransform mapField,
            RawImage mapImage)
        {
            var viewport = mapImage.rectTransform.parent as RectTransform;
            if (viewport == null || viewport.name != "MapViewport")
            {
                viewport = GetOrCreateRectChild(mapField, "MapViewport");
            }

            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = MapViewportSize;
            viewport.localScale = Vector3.one;
            var viewportMask = viewport.GetComponent<RectMask2D>();
            if (viewportMask != null)
            {
                UnityEngine.Object.DestroyImmediate(viewportMask);
            }

            ConfigureViewportChild(mapImage.rectTransform, viewport, Vector2.zero);
            return viewport;
        }

        private static void RemoveEmptyDuplicateViewports(
            RectTransform mapField,
            RectTransform activeViewport)
        {
            for (var index = mapField.childCount - 1; index >= 0; index--)
            {
                var child = mapField.GetChild(index) as RectTransform;
                if (child != null
                    && child != activeViewport
                    && child.name == "MapViewport"
                    && child.childCount == 0)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Image EnsureMarkerGlyph(Image markerTemplate)
        {
            var glyph = markerTemplate.transform.Find("MarkerGlyph")?.GetComponent<Image>();
            if (glyph == null)
            {
                var glyphObject = new GameObject(
                    "MarkerGlyph",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                glyph = glyphObject.GetComponent<Image>();
                glyph.transform.SetParent(markerTemplate.transform, false);
            }

            var rect = glyph.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = MarkerGlyphUprightRotation;
            glyph.color = Color.white;
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            glyph.gameObject.SetActive(false);
            return glyph;
        }

        private static void ConfigureMarkerLabel(Image markerTemplate)
        {
            var label = markerTemplate.transform.Find("MarkerLabel")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=marker_label_missing marker={markerTemplate.name}");
            }

            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 12f;
            label.color = Color.white;
            label.raycastTarget = false;
            label.gameObject.SetActive(false);
        }

        private static void ValidateMarkerPresentationPrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var views = root.GetComponentsInChildren<PHSHandheldShipMapView>(true);
                if (views.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=view_missing prefab={prefabPath}");
                }

                foreach (var view in views)
                {
                    var serialized = new SerializedObject(view);
                    var glyph = serialized.FindProperty("markerGlyphTemplate")?.objectReferenceValue as Image;
                    var marker = serialized.FindProperty("markerTemplate")?.objectReferenceValue as Image;
                    var rootRect = serialized.FindProperty("markerRoot")?.objectReferenceValue as RectTransform;
                    if (glyph == null || marker == null || rootRect == null
                        || Quaternion.Angle(
                            glyph.rectTransform.localRotation,
                            MarkerGlyphUprightRotation) > 0.01f
                        || Quaternion.Angle(
                            marker.rectTransform.localRotation,
                            Quaternion.identity) > 0.01f
                        || Quaternion.Angle(
                            rootRect.localRotation,
                            Quaternion.identity) > 0.01f)
                    {
                        throw new InvalidOperationException(
                            $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=marker_presentation_invalid " +
                            $"prefab={prefabPath} view={view.name}");
                    }

                    foreach (var binding in MapIconBindings)
                    {
                        if (serialized.FindProperty(binding.PropertyName)?.objectReferenceValue == null)
                        {
                            throw new InvalidOperationException(
                                $"PHS_SHIP_MAP_MARKER_AUTHOR_FAILED reason=icon_missing " +
                                $"prefab={prefabPath} view={view.name} icon={binding.PropertyName}");
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static ShipNavMeshFootprint CollectShipNavMeshFootprint()
        {
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length < 3
                || triangulation.indices == null || triangulation.indices.Length < 3
                || triangulation.indices.Length % 3 != 0)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=navmesh_data_missing");
            }

            var bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
            for (var index = 1; index < triangulation.vertices.Length; index++)
            {
                bounds.Encapsulate(triangulation.vertices[index]);
            }

            bounds.Expand(new Vector3(12f, 0f, 16f));
            return new ShipNavMeshFootprint(bounds, triangulation.vertices, triangulation.indices);
        }

        private static void FilterOutOfMapObjectAnchors(
            PHSShipMapWorldLayout layout,
            Bounds mapBounds)
        {
            var serialized = new SerializedObject(layout);
            var anchors = serialized.FindProperty("objectAnchors");
            if (anchors == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=object_anchor_property_missing");
            }

            EnsureAllPhysicalVendingAnchorsBoundToLayout(serialized, anchors);
            serialized.Update();
            anchors = serialized.FindProperty("objectAnchors")
                ?? throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=object_anchor_property_missing_after_bind");

            var allowedBounds = mapBounds;
            allowedBounds.Expand(new Vector3(4f, 0f, 4f));
            var retained = new List<PHSShipMapObjectAnchor>();
            for (var index = 0; index < anchors.arraySize; index++)
            {
                var anchor = anchors.GetArrayElementAtIndex(index)
                    .objectReferenceValue as PHSShipMapObjectAnchor;
                if (anchor != null && allowedBounds.Contains(anchor.transform.position))
                {
                    retained.Add(anchor);
                }
            }

            if (retained.Count == 0)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=all_object_anchors_outside_ship_floor");
            }

            if (retained.Count == anchors.arraySize)
            {
                return;
            }

            var removed = anchors.arraySize - retained.Count;
            anchors.arraySize = retained.Count;
            for (var index = 0; index < retained.Count; index++)
            {
                anchors.GetArrayElementAtIndex(index).objectReferenceValue = retained[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(
                $"PHS_SHIP_MAP_REALTIME_ANCHOR_FILTER removed={removed} retained={retained.Count}");
        }

        private static void ConfigureVendingMapIcons(PHSShipMapWorldLayout layout)
        {
            var serializedLayout = new SerializedObject(layout);
            var anchors = serializedLayout.FindProperty("objectAnchors");
            if (anchors == null)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=object_anchor_property_missing");
            }

            EnsureAllPhysicalVendingAnchorsBoundToLayout(serializedLayout, anchors);
            serializedLayout.Update();
            anchors = serializedLayout.FindProperty("objectAnchors")
                ?? throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=object_anchor_property_missing_after_bind");

            const int expectedVendingCountPerItem = 2;
            const int expectedVendingCount = 6;
            var wrenchCount = 0;
            var extinguisherCount = 0;
            var batteryCount = 0;
            var vendingCount = 0;
            var resolvedVendingObjects = new HashSet<UtilityVendingMachineInteractable>();
            for (var index = 0; index < anchors.arraySize; index++)
            {
                var anchor = anchors.GetArrayElementAtIndex(index)
                    .objectReferenceValue as PHSShipMapObjectAnchor;
                if (anchor == null || anchor.Kind != ShipMapObjectKind.Vending)
                {
                    continue;
                }

                var serializedAnchor = new SerializedObject(anchor);
                var iconId = serializedAnchor.FindProperty("iconId");
                if (iconId == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=vending_icon_property_missing " +
                        $"anchor={anchor.name}");
                }

                var vending = anchor.GetComponentInParent<UtilityVendingMachineInteractable>()
                    ?? anchor.GetComponentInChildren<UtilityVendingMachineInteractable>(true);
                if (vending == null || !resolvedVendingObjects.Add(vending))
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=vending_anchor_binding_invalid " +
                        $"anchor={anchor.name} vending={vending != null}");
                }

                var networkObject = vending.GetComponent<Unity.Netcode.NetworkObject>()
                    ?? vending.gameObject.AddComponent<Unity.Netcode.NetworkObject>();
                EditorUtility.SetDirty(networkObject);
                if (vending.GetComponentInChildren<Collider>(true) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=vending_interaction_contract_invalid " +
                        $"anchor={anchor.name} networkObject=" +
                        $"{networkObject != null} collider=" +
                        $"{vending.GetComponentInChildren<Collider>(true) != null}");
                }

                var itemId = vending.VendingMachineData?.ItemPrefabData?.ItemId;
                iconId.enumValueIndex = itemId switch
                {
                    "wrench" => RegisterVendingIcon(
                        ref wrenchCount,
                        ShipMapIconId.Wrench),
                    "fire_extinguisher" => RegisterVendingIcon(
                        ref extinguisherCount,
                        ShipMapIconId.FireExtinguisher),
                    "battery_pack" => RegisterVendingIcon(
                        ref batteryCount,
                        ShipMapIconId.Battery),
                    _ => throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=vending_item_icon_unknown " +
                        $"anchor={anchor.name} item={itemId ?? "missing"}")
                };
                anchor.transform.SetPositionAndRotation(
                    vending.transform.position,
                    vending.transform.rotation);
                serializedAnchor.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(anchor);
                vendingCount++;
            }

            if (vendingCount != expectedVendingCount
                || wrenchCount != expectedVendingCountPerItem
                || extinguisherCount != expectedVendingCountPerItem
                || batteryCount != expectedVendingCountPerItem)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=vending_cardinality_invalid " +
                    $"total={vendingCount} wrench={wrenchCount} extinguisher={extinguisherCount} battery={batteryCount}");
            }
        }

        private static void EnsureAllPhysicalVendingAnchorsBoundToLayout(
            SerializedObject serializedLayout,
            SerializedProperty anchors)
        {
            const int expectedVendingCount = 6;
            var physicalVendingAnchors = UnityEngine.Object
                .FindObjectsByType<PHSShipMapObjectAnchor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.InstanceID)
                .Where(anchor => anchor != null && anchor.Kind == ShipMapObjectKind.Vending)
                .ToArray();
            if (physicalVendingAnchors.Length != expectedVendingCount)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=physical_vending_cardinality_invalid " +
                    $"total={physicalVendingAnchors.Length} expected={expectedVendingCount}");
            }

            var boundAnchors = new HashSet<PHSShipMapObjectAnchor>();
            for (var index = 0; index < anchors.arraySize; index++)
            {
                var existing = anchors.GetArrayElementAtIndex(index)
                    .objectReferenceValue as PHSShipMapObjectAnchor;
                if (existing != null)
                {
                    boundAnchors.Add(existing);
                }
            }

            var appended = 0;
            foreach (var physicalAnchor in physicalVendingAnchors)
            {
                if (boundAnchors.Contains(physicalAnchor))
                {
                    continue;
                }

                anchors.InsertArrayElementAtIndex(anchors.arraySize);
                anchors.GetArrayElementAtIndex(anchors.arraySize - 1).objectReferenceValue = physicalAnchor;
                boundAnchors.Add(physicalAnchor);
                appended++;
            }

            if (appended == 0)
            {
                return;
            }

            serializedLayout.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(serializedLayout.targetObject);
            Debug.Log(
                $"PHS_SHIP_MAP_VENDING_ANCHORS_BOUND appended={appended} " +
                $"total={physicalVendingAnchors.Length}");
        }

        private static int RegisterVendingIcon(
            ref int count,
            ShipMapIconId iconId)
        {
            count++;
            return (int)iconId;
        }


        private static void BuildSchematic(
            Transform schematicRoot,
            ShipNavMeshFootprint shipNavMesh,
            Vector3 rigWorldPosition,
            Material material,
            int mapRenderLayer)
        {
            for (var index = schematicRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(schematicRoot.GetChild(index).gameObject);
            }

            CreateNavMeshFootprint(
                schematicRoot,
                shipNavMesh,
                rigWorldPosition,
                material,
                mapRenderLayer);
        }

        private static Color ResolveFloorColor()
        {
            return new Color(0.025f, 0.24f, 0.42f, 1f);
        }

        private static void CreateNavMeshFootprint(
            Transform parent,
            ShipNavMeshFootprint footprint,
            Vector3 rigWorldPosition,
            Material material,
            int mapRenderLayer)
        {
            var plate = new GameObject("NavigableShipFootprint", typeof(MeshFilter), typeof(MeshRenderer));
            plate.layer = mapRenderLayer;
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = Vector3.zero;
            var vertices = new Vector3[footprint.Vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
            {
                var worldVertex = footprint.Vertices[index];
                vertices[index] = new Vector3(
                    worldVertex.x - rigWorldPosition.x,
                    0f,
                    worldVertex.z - rigWorldPosition.z);
            }

            var mesh = new Mesh { name = "NavigableShipFootprint_Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = footprint.Indices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            plate.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = plate.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateOrUpdatePlateMaterial(
                "Floor",
                material,
                ResolveFloorColor());
        }

        private static float CalculateOrthographicSize(Bounds bounds, RenderTexture texture)
        {
            var aspect = (float)texture.width / texture.height;
            var verticalSize = bounds.size.z * 0.5f;
            var horizontalSize = bounds.size.x * 0.5f / aspect;
            return Mathf.Max(verticalSize, horizontalSize) + 6f;
        }

        private static RenderTexture CreateOrUpdateRenderTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (texture == null)
            {
                texture = new RenderTexture(240, 720, 24, RenderTextureFormat.ARGB32)
                {
                    name = "PHS_ShipMapRealtime"
                };
                AssetDatabase.CreateAsset(texture, RenderTexturePath);
            }
            else
            {
                texture.Release();
                texture.width = 240;
                texture.height = 720;
                texture.depth = 24;
            }

            texture.useMipMap = false;
            texture.autoGenerateMips = false;
            texture.antiAliasing = 1;
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Material CreateOrUpdateSchematicMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=urp_unlit_shader_missing");
                }

                material = new Material(shader)
                {
                    name = "PHS_ShipMapSchematic"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            return material;
        }

        private static void ConfigureFlatSpriteImportSettings()
        {
            foreach (var binding in MapIconBindings)
            {
                var importer = AssetImporter.GetAtPath(binding.AssetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=flat_sprite_importer_missing path={binding.AssetPath}");
                }

                if (importer.textureType == TextureImporterType.Sprite
                    && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.maxTextureSize == 512
                    && !importer.mipmapEnabled)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void ConfigureMapIconReferences(PHSHandheldShipMapView view)
        {
            foreach (var binding in MapIconBindings)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(binding.AssetPath);
                if (icon == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=flat_sprite_missing " +
                        $"property={binding.PropertyName} path={binding.AssetPath}");
                }

                SetObjectReference(view, binding.PropertyName, icon);
            }
        }

        private static Material CreateOrUpdatePlateMaterial(
            string plateName,
            Material source,
            Color color)
        {
            var path =
                "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/" +
                $"PHS_ShipMap_{plateName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source)
                {
                    name = $"PHS_ShipMap_{plateName}"
                };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static int EnsureMapRenderLayer()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var index = 8; index < layers.arraySize; index++)
            {
                if (layers.GetArrayElementAtIndex(index).stringValue == MapRenderLayerName)
                {
                    return index;
                }
            }

            for (var index = 8; index < layers.arraySize; index++)
            {
                var layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrWhiteSpace(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = MapRenderLayerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return index;
            }

            throw new InvalidOperationException(
                "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=map_render_layer_slot_missing");
        }

        private static void ExcludeMapRenderLayerFromSceneCameras(Camera mapCamera, int mapRenderLayer)
        {
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (camera != mapCamera)
                {
                    camera.cullingMask &= ~(1 << mapRenderLayer);
                }
            }
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(name);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static RectTransform GetOrCreateRectChild(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
            {
                return existing;
            }

            var childObject = new GameObject(name, typeof(RectTransform));
            var rect = childObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var result = Find(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static void ValidateMapViewIconReferences(
            PHSHandheldShipMapView view,
            string viewName)
        {
            if (view == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=view_missing view={viewName}");
            }

            var markerTemplate = Find(view.transform, "MarkerTemplate")?.GetComponent<Image>();
            var markerLabel = Find(view.transform, "MarkerLabel")?.GetComponent<TMP_Text>();
            if (markerTemplate == null
                || markerTemplate.enabled
                || markerTemplate.sprite != null
                || markerLabel == null
                || markerLabel.alignment != TextAlignmentOptions.Center)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=marker_template_invalid view={viewName}");
            }

            var serializedView = new SerializedObject(view);
            var playerMarkerSize = serializedView.FindProperty("playerMarkerSize");
            var playerMarkerColor = serializedView.FindProperty("playerMarkerColor");
            if (playerMarkerSize == null
                || playerMarkerSize.vector2Value != PlayerMarkerSize
                || playerMarkerColor == null
                || playerMarkerColor.colorValue != PlayerMarkerColor)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=player_marker_priority_invalid view={viewName}");
            }

            foreach (var binding in MapIconBindings)
            {
                var sprite = serializedView.FindProperty(binding.PropertyName)
                    ?.objectReferenceValue as Sprite;
                var actualPath = sprite == null ? string.Empty : AssetDatabase.GetAssetPath(sprite);
                if (!string.Equals(actualPath, binding.AssetPath, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"PHS_SHIP_MAP_ICON_VALIDATE_FAILED reason=external_or_wrong_sprite " +
                        $"view={viewName} property={binding.PropertyName} path={actualPath}");
                }
            }
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=property_missing property={propertyName}");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
