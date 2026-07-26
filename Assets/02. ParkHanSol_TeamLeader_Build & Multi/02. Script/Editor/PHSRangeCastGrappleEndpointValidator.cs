using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSRangeCastGrappleEndpointValidator
    {
        private const float PositionTolerance = 0.0001f;
        [MenuItem(
            "Tools/ParkHanSol/BEAVER/Validate Range Cast And Grapple Endpoints")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateRangeCast(errors);
            ValidateHookEndpoints(errors);
            ValidatePlayerReferences(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_RANGE_GRAPPLE_ENDPOINT_VALIDATE_FAILED "
                    + string.Join(" | ", errors));
            }

            Debug.Log(
                "PHS_RANGE_GRAPPLE_ENDPOINT_VALIDATE_OK "
                + "range=presentation_removed markers=bounds "
                + "hook=wrapper_visible arm=thin refs=explicit distance=24");
        }

        private static void ValidateRangeCast(ICollection<string> errors)
        {
            var path = PHSRangeCastGrappleEndpointAuthoring.RangeCastPrefabPath;
            var root = LoadPrefab(path, errors);
            if (root == null)
            {
                return;
            }

            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length != 0)
                {
                    errors.Add($"range_renderer_present:{renderers.Length}");
                }

                var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                if (meshFilters.Length != 0)
                {
                    errors.Add($"range_mesh_filter_present:{meshFilters.Length}");
                }

                for (var index = 0;
                     index < root.transform.childCount;
                     index++)
                {
                    var child = root.transform.GetChild(index);
                    if (child.name.StartsWith(
                            "RangeOutline_",
                            StringComparison.Ordinal))
                    {
                        errors.Add($"range_outline_object_present:{child.name}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateHookEndpoints(
            ICollection<string> errors)
        {
            var path = PHSRangeCastGrappleEndpointAuthoring.HookPrefabPath;
            var root = LoadPrefab(path, errors);
            if (root == null)
            {
                return;
            }

            try
            {
                if (!PHSRangeCastGrappleEndpointAuthoring.TryCalculateMeshBounds(
                        root,
                        out var bounds))
                {
                    errors.Add("hook_mesh_bounds_missing");
                    return;
                }

                var meshRenderers = root.GetComponentsInChildren<
                    MeshRenderer>(true);
                if (meshRenderers.Length == 0)
                {
                    errors.Add("hook_actual_model_renderer_missing");
                }

                for (var index = 0; index < meshRenderers.Length; index++)
                {
                    if (!meshRenderers[index].enabled
                        || !meshRenderers[index].gameObject.activeSelf)
                    {
                        errors.Add(
                            $"hook_actual_model_renderer_disabled:"
                            + meshRenderers[index].name);
                    }
                }

                var rope = PHSRangeCastGrappleEndpointAuthoring.FindDirectChild(
                    root.transform,
                    PHSRangeCastGrappleEndpointAuthoring.RopeAttachPointName);
                var tip = PHSRangeCastGrappleEndpointAuthoring.FindDirectChild(
                    root.transform,
                    PHSRangeCastGrappleEndpointAuthoring.ClawTipPointName);
                if (rope == null)
                {
                    errors.Add("rope_attach_point_missing");
                }

                if (tip == null)
                {
                    errors.Add("claw_tip_point_missing");
                }

                if (rope == null || tip == null)
                {
                    return;
                }

                var expectedRope = new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    bounds.min.z);
                var expectedTip = new Vector3(
                    bounds.center.x,
                    bounds.center.y,
                    bounds.max.z);
                RequirePosition(
                    rope.localPosition,
                    expectedRope,
                    "rope_attach_point_bounds_mismatch",
                    errors);
                RequirePosition(
                    tip.localPosition,
                    expectedTip,
                    "claw_tip_point_bounds_mismatch",
                    errors);
                if (tip.localPosition.z <= rope.localPosition.z)
                {
                    errors.Add("hook_endpoint_order_invalid");
                }

                RequireMarkerPose(rope, "rope_attach_point_pose", errors);
                RequireMarkerPose(tip, "claw_tip_point_pose", errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePlayerReferences(
            ICollection<string> errors)
        {
            var path = PHSRangeCastGrappleEndpointAuthoring.PlayerPrefabPath;
            var root = LoadPrefab(path, errors);
            if (root == null)
            {
                return;
            }

            try
            {
                var controllers = root.GetComponentsInChildren<
                    NetworkPlayerGrappleController>(true);
                if (controllers.Length != 1)
                {
                    errors.Add(
                        $"player_grapple_controller_count:{controllers.Length}");
                    return;
                }

                var serialized = new SerializedObject(controllers[0]);
                var maximumDistance = serialized.FindProperty("maximumDistance");
                if (maximumDistance == null
                    || !Mathf.Approximately(
                        maximumDistance.floatValue,
                        PHSRangeCastGrappleEndpointAuthoring
                            .RequiredMaximumDistance))
                {
                    errors.Add("player_grapple_maximum_distance_not_24");
                }

                var hookVisualProperty = serialized.FindProperty("hookVisual");
                var clawVisualProperty = serialized.FindProperty("clawVisual");
                var ropeAttachProperty = serialized.FindProperty("ropeAttachPoint");
                var clawTipProperty = serialized.FindProperty("clawTipPoint");
                var armThicknessProperty = serialized.FindProperty("armThickness");
                var armVisualProperty = serialized.FindProperty("armVisual");
                var armSegmentProperty = serialized.FindProperty("armSegment");
                var armEndJointProperty = serialized.FindProperty("armEndJoint");
                var hookVisual = hookVisualProperty?.objectReferenceValue
                    as Transform;
                var clawVisual = clawVisualProperty?.objectReferenceValue
                    as GrappleClawVisual;
                var ropeAttachPoint = ropeAttachProperty?.objectReferenceValue
                    as Transform;
                var clawTipPoint = clawTipProperty?.objectReferenceValue
                    as Transform;
                var armVisual = armVisualProperty?.objectReferenceValue
                    as Transform;
                var armSegment = armSegmentProperty?.objectReferenceValue
                    as Transform;
                var armEndJoint = armEndJointProperty?.objectReferenceValue
                    as Transform;
                if (hookVisual == null)
                {
                    errors.Add("player_hook_visual_reference_missing");
                }

                if (clawVisual == null)
                {
                    errors.Add("player_claw_visual_reference_missing");
                }

                if (ropeAttachPoint == null)
                {
                    errors.Add("player_rope_attach_reference_missing");
                }

                if (clawTipPoint == null)
                {
                    errors.Add("player_claw_tip_reference_missing");
                }

                if (hookVisual == null
                    || clawVisual == null
                    || ropeAttachPoint == null
                    || clawTipPoint == null)
                {
                    return;
                }

                if (hookVisual.name
                    != PHSRangeCastGrappleEndpointAuthoring
                        .HookVisualContainerName)
                {
                    errors.Add("player_hook_visual_not_wrapper");
                }

                if (!clawVisual.transform.IsChildOf(hookVisual))
                {
                    errors.Add("player_claw_not_under_hook_wrapper");
                }

                RequirePosition(
                    hookVisual.localScale,
                    Vector3.one
                        * PHSRangeCastGrappleEndpointAuthoring
                            .RequiredHookVisualScale,
                    "player_hook_wrapper_scale_not_restored",
                    errors);

                RequireMarkerUnderHook(
                    clawVisual.transform,
                    PHSRangeCastGrappleEndpointAuthoring.RopeAttachPointName,
                    errors);
                RequireMarkerUnderHook(
                    clawVisual.transform,
                    PHSRangeCastGrappleEndpointAuthoring.ClawTipPointName,
                    errors);
                if (ropeAttachPoint.parent != clawVisual.transform)
                {
                    errors.Add("player_rope_attach_not_direct_child");
                }

                if (clawTipPoint.parent != clawVisual.transform)
                {
                    errors.Add("player_claw_tip_not_direct_child");
                }

                if (armThicknessProperty == null
                    || !Mathf.Approximately(
                        armThicknessProperty.floatValue,
                        PHSRangeCastGrappleEndpointAuthoring
                            .RequiredArmThickness))
                {
                    errors.Add("player_arm_thickness_invalid");
                }

                if (armVisual == null
                    || armSegment == null
                    || armEndJoint == null)
                {
                    errors.Add("player_arm_reference_missing");
                    return;
                }

                var baseJoint = PHSRangeCastGrappleEndpointAuthoring
                    .FindDirectChild(armVisual, "BaseJoint");
                if (baseJoint == null)
                {
                    errors.Add("player_arm_base_joint_missing");
                }
                else
                {
                    RequirePosition(
                        baseJoint.localScale,
                        Vector3.one
                            * PHSRangeCastGrappleEndpointAuthoring
                                .RequiredBaseJointScale,
                        "player_arm_base_joint_too_thick",
                        errors);
                }

                RequirePosition(
                    new Vector3(
                        armSegment.localScale.x,
                        0f,
                        armSegment.localScale.z),
                    new Vector3(
                        PHSRangeCastGrappleEndpointAuthoring
                            .RequiredArmThickness,
                        0f,
                        PHSRangeCastGrappleEndpointAuthoring
                            .RequiredArmThickness),
                    "player_arm_segment_too_thick",
                    errors);
                RequirePosition(
                    armEndJoint.localScale,
                    Vector3.one
                        * PHSRangeCastGrappleEndpointAuthoring
                            .RequiredEndJointScale,
                    "player_arm_end_joint_too_thick",
                    errors);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject LoadPrefab(
            string path,
            ICollection<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                errors.Add($"prefab_missing:{path}");
                return null;
            }

            return PrefabUtility.LoadPrefabContents(path);
        }

        private static void RequirePosition(
            Vector3 actual,
            Vector3 expected,
            string reason,
            ICollection<string> errors)
        {
            if ((actual - expected).sqrMagnitude
                > PositionTolerance * PositionTolerance)
            {
                errors.Add(reason);
            }
        }

        private static void RequireMarkerPose(
            Transform marker,
            string reason,
            ICollection<string> errors)
        {
            if (Quaternion.Angle(marker.localRotation, Quaternion.identity)
                    > PositionTolerance
                || (marker.localScale - Vector3.one).sqrMagnitude
                    > PositionTolerance * PositionTolerance)
            {
                errors.Add(reason);
            }
        }

        private static void RequireMarkerUnderHook(
            Transform hookVisual,
            string markerName,
            ICollection<string> errors)
        {
            var marker = PHSRangeCastGrappleEndpointAuthoring.FindDirectChild(
                hookVisual,
                markerName);
            if (marker == null)
            {
                errors.Add($"player_hook_marker_missing:{markerName}");
            }
        }
    }
}
