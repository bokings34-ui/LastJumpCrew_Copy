using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSRangeCastGrappleEndpointAuthoring
    {
        internal const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        internal const string RangeCastPrefabPath =
            Root + "/03. Prefab/Items/Feedback/PHS_ItemRangeCast.prefab";
        internal const string HookPrefabPath =
            Root + "/03. Prefab/Grapple/PHS_SciFiRoboticClawHook.prefab";
        internal const string PlayerPrefabPath =
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        internal const string ClawTipPointName = "ClawTipPoint";
        internal const string RopeAttachPointName = "RopeAttachPoint";
        internal const string HookVisualContainerName = "HookVisual";
        internal const float RequiredMaximumDistance = 24f;
        internal const float RequiredHookVisualScale = 0.18f;
        internal const float RequiredArmThickness = 0.05f;
        internal const float RequiredBaseJointScale = 1f;
        internal const float RequiredEndJointScale = 1f;

        [MenuItem(
            "Tools/ParkHanSol/BEAVER/Author Range Cast And Grapple Endpoints")]
        public static void Author()
        {
            Preflight();
            AuthorRangeCast();
            AuthorHookEndpoints();
            AssetDatabase.ImportAsset(
                HookPrefabPath,
                ImportAssetOptions.ForceSynchronousImport);
            AuthorPlayerReferences();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PHSRangeCastGrappleEndpointValidator.Validate();
            Debug.Log(
                "PHS_RANGE_GRAPPLE_ENDPOINT_AUTHORING_OK "
                + "range=presentation_removed hook=wrapper_restored "
                + "arm=thin distance=24");
        }

        private static void Preflight()
        {
            RequirePrefab(RangeCastPrefabPath);
            RequirePrefab(HookPrefabPath);
            RequirePrefab(PlayerPrefabPath);

            var playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var controller = RequireSingleGrappleController(playerRoot);
                var serialized = new SerializedObject(controller);
                var maximumDistance = serialized.FindProperty("maximumDistance");
                if (maximumDistance == null
                    || !Mathf.Approximately(
                        maximumDistance.floatValue,
                        RequiredMaximumDistance))
                {
                    throw new InvalidOperationException(
                        "grapple_maximum_distance_must_remain_24");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void AuthorRangeCast()
        {
            var root = PrefabUtility.LoadPrefabContents(RangeCastPrefabPath);
            try
            {
                RemoveTemporaryRangePresentation(root);
                PrefabUtility.SaveAsPrefabAsset(root, RangeCastPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuthorHookEndpoints()
        {
            var root = PrefabUtility.LoadPrefabContents(HookPrefabPath);
            try
            {
                if (!TryCalculateMeshBounds(root, out var bounds))
                {
                    throw new InvalidOperationException(
                        $"hook_mesh_bounds_missing:{HookPrefabPath}");
                }

                var center = bounds.center;
                var ropeAttachPoint = EnsureDirectChild(
                    root.transform,
                    RopeAttachPointName);
                ropeAttachPoint.localPosition = new Vector3(
                    center.x,
                    center.y,
                    bounds.min.z);
                ResetMarkerPose(ropeAttachPoint);

                var clawTipPoint = EnsureDirectChild(
                    root.transform,
                    ClawTipPointName);
                clawTipPoint.localPosition = new Vector3(
                    center.x,
                    center.y,
                    bounds.max.z);
                ResetMarkerPose(clawTipPoint);

                PrefabUtility.SaveAsPrefabAsset(root, HookPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuthorPlayerReferences()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var controller = RequireSingleGrappleController(root);
                var clawVisual = RequireHookClawVisual(root);
                var hookVisual = RequireHookVisualContainer(
                    clawVisual.transform);
                hookVisual.localScale = Vector3.one
                    * RequiredHookVisualScale;
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("hookVisual").objectReferenceValue =
                    hookVisual;
                serialized.FindProperty("clawVisual").objectReferenceValue =
                    clawVisual;
                serialized.FindProperty("ropeAttachPoint").objectReferenceValue =
                    RequireDirectMarker(
                        clawVisual.transform,
                        RopeAttachPointName);
                serialized.FindProperty("clawTipPoint").objectReferenceValue =
                    RequireDirectMarker(
                        clawVisual.transform,
                        ClawTipPointName);
                serialized.FindProperty("armThickness").floatValue =
                    RequiredArmThickness;

                var armVisual = RequireTransformReference(
                    serialized,
                    "armVisual");
                var armSegment = RequireTransformReference(
                    serialized,
                    "armSegment");
                var armEndJoint = RequireTransformReference(
                    serialized,
                    "armEndJoint");
                var telescopicArmVisual = armSegment
                    .GetComponent<GrappleTelescopicArmVisual>();
                if (telescopicArmVisual == null
                    || !telescopicArmVisual.IsConfigured)
                {
                    throw new InvalidOperationException(
                        "Player telescopic grapple arm is not configured.");
                }

                serialized.FindProperty("telescopicArmVisual")
                    .objectReferenceValue = telescopicArmVisual;
                var baseJoint = RequireDirectMarker(
                    armVisual,
                    "BaseJoint");
                baseJoint.localScale = Vector3.one
                    * RequiredBaseJointScale;
                armSegment.localPosition = Vector3.zero;
                armSegment.localScale = Vector3.one;
                armEndJoint.localScale = Vector3.one
                    * RequiredEndJointScale;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static bool TryCalculateMeshBounds(
            GameObject root,
            out Bounds bounds)
        {
            bounds = default;
            var hasPoint = false;
            var meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var rendererIndex = 0;
                 rendererIndex < meshRenderers.Length;
                 rendererIndex++)
            {
                var meshRenderer = meshRenderers[rendererIndex];
                var meshBounds = meshRenderer.localBounds;
                for (var x = -1; x <= 1; x += 2)
                {
                    for (var y = -1; y <= 1; y += 2)
                    {
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var corner = meshBounds.center + Vector3.Scale(
                                meshBounds.extents,
                                new Vector3(x, y, z));
                            var worldPoint = meshRenderer.transform.TransformPoint(
                                corner);
                            var localPoint = root.transform.InverseTransformPoint(
                                worldPoint);
                            if (!hasPoint)
                            {
                                bounds = new Bounds(localPoint, Vector3.zero);
                                hasPoint = true;
                            }
                            else
                            {
                                bounds.Encapsulate(localPoint);
                            }
                        }
                    }
                }
            }

            return hasPoint;
        }

        internal static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void RequirePrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException($"prefab_missing:{path}");
            }
        }

        private static NetworkPlayerGrappleController
            RequireSingleGrappleController(GameObject root)
        {
            var controllers = root.GetComponentsInChildren<
                NetworkPlayerGrappleController>(true);
            if (controllers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"grapple_controller_count:{controllers.Length}");
            }

            return controllers[0];
        }

        private static GrappleClawVisual RequireHookClawVisual(GameObject root)
        {
            var clawVisuals = root.GetComponentsInChildren<GrappleClawVisual>(true);
            GrappleClawVisual match = null;
            for (var index = 0; index < clawVisuals.Length; index++)
            {
                var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    clawVisuals[index].gameObject);
                if (path != HookPrefabPath)
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "hook_claw_visual_multiple");
                }

                match = clawVisuals[index];
            }

            return match != null
                ? match
                : throw new InvalidOperationException("hook_claw_visual_missing");
        }

        private static Transform RequireDirectMarker(
            Transform hookRoot,
            string markerName)
        {
            return FindDirectChild(hookRoot, markerName)
                ?? throw new InvalidOperationException(
                    $"hook_marker_missing:{markerName}");
        }

        private static Transform RequireHookVisualContainer(
            Transform clawVisual)
        {
            var current = clawVisual.parent;
            while (current != null)
            {
                if (current.name == HookVisualContainerName)
                {
                    return current;
                }

                current = current.parent;
            }

            throw new InvalidOperationException(
                $"hook_visual_container_missing:{HookVisualContainerName}");
        }

        private static Transform RequireTransformReference(
            SerializedObject serialized,
            string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property?.objectReferenceValue is Transform transform)
            {
                return transform;
            }

            throw new InvalidOperationException(
                $"grapple_transform_reference_missing:{propertyName}");
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string childName)
        {
            var child = FindDirectChild(parent, childName);
            if (child != null)
            {
                return child;
            }

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        private static void RemoveTemporaryRangePresentation(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(renderers[index], true);
            }

            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var index = 0; index < meshFilters.Length; index++)
            {
                UnityEngine.Object.DestroyImmediate(meshFilters[index], true);
            }

            for (var index = root.transform.childCount - 1; index >= 0; index--)
            {
                var child = root.transform.GetChild(index);
                if (child.name.StartsWith(
                        "RangeOutline_",
                        StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(
                        child.gameObject,
                        true);
                }
            }
        }

        private static void ResetMarkerPose(Transform marker)
        {
            marker.localRotation = Quaternion.identity;
            marker.localScale = Vector3.one;
        }
    }
}
