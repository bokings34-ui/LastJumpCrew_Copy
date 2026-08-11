using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSShipMapRealtimeAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/" +
            "PHS_CuteWhiteGhost_Player.prefab";
        private const string RenderTexturePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/" +
            "PHS_ShipMapRealtime.renderTexture";
        private const string MaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Maps/" +
            "PHS_ShipMapSchematic.mat";
        private const string MapRenderLayerName = "MapRender";
        private static readonly Vector2 MapViewportSize = new(120f, 360f);

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Realtime Ship Map")]
        public static void Author()
        {
            var mapRenderLayer = EnsureMapRenderLayer();
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
            ExcludeMapRenderLayerFromSceneCameras(rig.MapCamera, mapRenderLayer);
            EditorUtility.SetDirty(layout);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.SaveScene(scene);
            ConfigurePlayerPrefab(mapTexture, mapRenderLayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "PHS_SHIP_MAP_REALTIME_AUTHOR_PASS texture=240x720 " +
                "viewport=120x360 marker_style=diamond_glyph");
        }

        private static PHSShipMapRenderRig ConfigureSceneRig(
            PHSShipMapWorldLayout layout,
            RenderTexture mapTexture,
            Material material,
            int mapRenderLayer)
        {
            var sourcePositions = CollectMapSourcePositions();
            if (sourcePositions.Count == 0)
            {
                throw new InvalidOperationException(
                    "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=source_positions_missing");
            }

            var bounds = BuildBounds(sourcePositions);
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
            BuildSchematic(schematicRoot, bounds, material, mapRenderLayer);
            var cameraTransform = GetOrCreateChild(root.transform, "MapCamera");
            cameraTransform.gameObject.layer = mapRenderLayer;
            cameraTransform.localPosition = new Vector3(0f, renderY + 50f, 0f);
            cameraTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var mapCamera = cameraTransform.GetComponent<Camera>()
                ?? cameraTransform.gameObject.AddComponent<Camera>();
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

                var views = root.GetComponentsInChildren<PHSHandheldShipMapView>(true);
                if (views.Length == 0)
                {
                    throw new InvalidOperationException(
                        "PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=map_view_missing");
                }

                foreach (var view in views)
                {
                    ConfigureMapView(view, mapTexture);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureMapView(PHSHandheldShipMapView view, RenderTexture mapTexture)
        {
            var mapField = Find(view.transform, "MapField") as RectTransform;
            var markerRoot = Find(view.transform, "MarkerRoot") as RectTransform;
            var mapImage = Find(view.transform, "Neon Map Reference")?.GetComponent<RawImage>();
            var markerTemplate = Find(view.transform, "MarkerTemplate")?.GetComponent<Image>();
            var incidentRail = Find(view.transform, "Incident Rail") as RectTransform;
            if (mapField == null || markerRoot == null || mapImage == null
                || markerTemplate == null || incidentRail == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SHIP_MAP_REALTIME_AUTHOR_FAILED reason=view_hierarchy_invalid view={view.name}");
            }

            var viewport = GetOrCreateRectChild(mapField, "MapViewport");
            viewport.anchorMin = viewport.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = MapViewportSize;
            viewport.localScale = Vector3.one;
            if (viewport.GetComponent<RectMask2D>() == null)
            {
                viewport.gameObject.AddComponent<RectMask2D>();
            }

            ConfigureViewportChild(mapImage.rectTransform, viewport, Vector2.zero);
            mapImage.texture = mapTexture;
            mapImage.color = Color.white;
            ConfigureViewportChild(markerRoot, viewport, new Vector2(-8f, -8f));
            if (incidentRail.GetComponent<RectMask2D>() == null)
            {
                incidentRail.gameObject.AddComponent<RectMask2D>();
            }

            var markerGlyph = EnsureMarkerGlyph(markerTemplate);
            SetObjectReference(view, "mapImage", mapImage);
            SetObjectReference(view, "markerGlyphTemplate", markerGlyph);
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
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(10f, 10f);
            rect.localScale = Vector3.one;
            glyph.color = Color.white;
            glyph.raycastTarget = false;
            glyph.preserveAspect = true;
            glyph.gameObject.SetActive(false);
            return glyph;
        }

        private static List<Vector3> CollectMapSourcePositions()
        {
            var positions = new List<Vector3>();
            foreach (var anchor in UnityEngine.Object.FindObjectsByType<PHSShipMapObjectAnchor>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                positions.Add(anchor.transform.position);
            }

            foreach (var anchor in UnityEngine.Object.FindObjectsByType<
                         LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents.PHSShipAccidentAnchor>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                positions.Add(anchor.RepairPosition);
            }

            return positions;
        }

        private static Bounds BuildBounds(IReadOnlyList<Vector3> positions)
        {
            var bounds = new Bounds(positions[0], Vector3.zero);
            for (var index = 1; index < positions.Count; index++)
            {
                bounds.Encapsulate(positions[index]);
            }

            bounds.Expand(new Vector3(12f, 0f, 16f));
            return bounds;
        }

        private static void BuildSchematic(
            Transform schematicRoot,
            Bounds bounds,
            Material material,
            int mapRenderLayer)
        {
            for (var index = schematicRoot.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.DestroyImmediate(schematicRoot.GetChild(index).gameObject);
            }

            var width = Mathf.Max(12f, bounds.size.x);
            var length = Mathf.Max(24f, bounds.size.z);
            CreatePlate(
                schematicRoot,
                "Hull",
                Vector3.zero,
                new Vector2(width * 0.54f, length),
                new Color(0.025f, 0.16f, 0.28f, 1f),
                material,
                mapRenderLayer);
            CreatePlate(
                schematicRoot,
                "PortWing",
                new Vector3(-width * 0.28f, 0.02f, length * 0.05f),
                new Vector2(width * 0.36f, length * 0.35f),
                new Color(0.03f, 0.24f, 0.38f, 1f),
                material,
                mapRenderLayer);
            CreatePlate(
                schematicRoot,
                "StarboardWing",
                new Vector3(width * 0.28f, 0.02f, length * 0.05f),
                new Vector2(width * 0.36f, length * 0.35f),
                new Color(0.03f, 0.24f, 0.38f, 1f),
                material,
                mapRenderLayer);
            CreatePlate(
                schematicRoot,
                "CommandSection",
                new Vector3(0f, 0.04f, length * 0.31f),
                new Vector2(width * 0.72f, length * 0.22f),
                new Color(0.08f, 0.42f, 0.55f, 1f),
                material,
                mapRenderLayer);
        }

        private static void CreatePlate(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector2 size,
            Color color,
            Material material,
            int mapRenderLayer)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = name;
            plate.layer = mapRenderLayer;
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = localPosition;
            plate.transform.localScale = new Vector3(size.x, 0.04f, size.y);
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
            var renderer = plate.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
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
                texture = new RenderTexture(240, 720, 0, RenderTextureFormat.ARGB32)
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
                texture.depth = 0;
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
