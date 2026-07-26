using System;
using System.Collections.Generic;
using System.IO;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    internal static class PHSItemHeldPresentationValidator
    {
        private const float Tolerance = 0.0001f;

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Item Held Presentation")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(
                PHSItemHeldPresentationSpec.CatalogPath);
            Require(catalog != null, "catalog_missing");
            Require(
                catalog.Items.Count == PHSItemHeldPresentationSpec.Items.Length,
                $"catalog_count expected={PHSItemHeldPresentationSpec.Items.Length} actual={catalog.Items.Count}");

            var catalogById = BuildCatalogMap(catalog);
            foreach (var spec in PHSItemHeldPresentationSpec.Items)
            {
                Require(
                    catalogById.TryGetValue(spec.ItemId, out var item),
                    $"catalog_item_missing item={spec.ItemId}");
                ValidateItem(item, spec);
                if (spec.IsDebris)
                {
                    ValidateDebrisPrefabs(item);
                }
            }

            ValidateHolderReferences();
            Debug.Log(
                "PHS_ITEM_HELD_PRESENTATION_VALIDATE_OK " +
                $"catalog={catalog.Items.Count} poses={catalog.Items.Count * 2} " +
                "debrisHeld=presentation_safe_physics debrisDropped=physics_network " +
                "holderRefs=preserved");
        }

        private static Dictionary<string, UtilityItemPrefabData> BuildCatalogMap(
            UtilityItemCatalogSO catalog)
        {
            var result = new Dictionary<string, UtilityItemPrefabData>(
                StringComparer.Ordinal);
            foreach (var item in catalog.Items)
            {
                Require(item != null, "catalog_item_null");
                Require(
                    result.TryAdd(item.ItemId, item),
                    $"catalog_item_duplicate item={item.ItemId}");
            }

            return result;
        }

        private static void ValidateItem(
            UtilityItemPrefabData item,
            PHSItemHeldPresentationSpec.ItemPoseSpec spec)
        {
            Require(item.HeldPrefab != null, $"held_prefab_missing item={spec.ItemId}");
            Require(item.DroppedPrefab != null, $"dropped_prefab_missing item={spec.ItemId}");
            Require(
                item.HeldPrefab != item.DroppedPrefab,
                $"held_dropped_prefab_shared item={spec.ItemId}");

            var serializedItem = new SerializedObject(item);
            ValidatePose(serializedItem, "firstPersonHeldPose", spec.FirstPerson, spec.ItemId);
            ValidatePose(serializedItem, "worldHeldPose", spec.World, spec.ItemId);

            var itemPath = AssetDatabase.GetAssetPath(item);
            var yaml = File.ReadAllText(Path.GetFullPath(itemPath));
            Require(
                yaml.Contains("firstPersonHeldPose:", StringComparison.Ordinal)
                && yaml.Contains("worldHeldPose:", StringComparison.Ordinal),
                $"pose_not_explicitly_serialized item={spec.ItemId}");
        }

        private static void ValidateDebrisPrefabs(UtilityItemPrefabData item)
        {
            var held = item.HeldPrefab;
            var dropped = item.DroppedPrefab;
            Require(
                AssetDatabase.GetAssetPath(held).Contains(
                    "/Debris/Held/",
                    StringComparison.Ordinal),
                $"debris_held_path_invalid item={item.ItemId}");

            Require(Count<Rigidbody>(held) == 1, $"held_rigidbody_count item={item.ItemId}");
            Require(Count<Collider>(held) == 1, $"held_collider_count item={item.ItemId}");
            Require(Count<NetworkBehaviour>(held) == 0, $"held_network_behaviour_present item={item.ItemId}");
            Require(Count<NetworkObject>(held) == 0, $"held_network_object_present item={item.ItemId}");
            Require(Count<DebrisItem>(held) == 1, $"held_debris_item_count item={item.ItemId}");
            Require(Count<ItemGravityReceiver>(held) == 0, $"held_gravity_receiver_present item={item.ItemId}");
            Require(Count<RigidbodyGrappleTarget>(held) == 0, $"held_grapple_target_present item={item.ItemId}");
            Require(Count<GrappleCollectibleItem>(held) == 0, $"held_grapple_collectible_present item={item.ItemId}");
            Require(Count<UtilityItemObject>(held) == 1, $"held_utility_item_count item={item.ItemId}");
            Require(Count<Renderer>(held) > 0, $"held_visual_missing item={item.ItemId}");
            Require(
                held.GetComponent<UtilityItemObject>().ItemPrefabData == item,
                $"held_item_reference_invalid item={item.ItemId}");
            var heldRigidbody = held.GetComponentInChildren<Rigidbody>(true);
            var heldCollider = held.GetComponentInChildren<Collider>(true);
            Require(
                heldRigidbody.isKinematic && !heldRigidbody.useGravity,
                $"held_rigidbody_not_presentation_safe item={item.ItemId}");
            Require(
                !heldCollider.enabled,
                $"held_collider_enabled item={item.ItemId}");

            Require(Count<Rigidbody>(dropped) == 1, $"dropped_rigidbody_count item={item.ItemId}");
            Require(Count<Collider>(dropped) == 1, $"dropped_collider_count item={item.ItemId}");
            Require(Count<NetworkObject>(dropped) == 1, $"dropped_network_object_count item={item.ItemId}");
            Require(Count<NetworkTransform>(dropped) == 1, $"dropped_network_transform_count item={item.ItemId}");
            Require(
                Count<NetworkItemPhysicsAuthority>(dropped) == 1,
                $"dropped_physics_authority_count item={item.ItemId}");
            Require(Count<UtilityItemObject>(dropped) == 1, $"dropped_utility_item_count item={item.ItemId}");
            Require(Count<DebrisItem>(dropped) == 1, $"dropped_debris_item_count item={item.ItemId}");
            Require(
                dropped.GetComponent<UtilityItemObject>().ItemPrefabData == item,
                $"dropped_item_reference_invalid item={item.ItemId}");
        }

        private static void ValidateHolderReferences()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(
                PHSItemHeldPresentationSpec.PlayerPrefabPath);
            Require(player != null, "player_prefab_missing");
            Require(
                Count<TempPlayerItemHolder>(player) == 1,
                "holder_component_count");

            var holder = player.GetComponentInChildren<TempPlayerItemHolder>(true);
            var serializedHolder = new SerializedObject(holder);
            var firstPerson = RequireProperty(serializedHolder, "holdPoint")
                .objectReferenceValue as Transform;
            var world = RequireProperty(serializedHolder, "visibleHandHoldPoint")
                .objectReferenceValue as Transform;
            var drop = RequireProperty(serializedHolder, "dropPoint")
                .objectReferenceValue as Transform;
            Require(firstPerson != null, "first_person_hold_point_missing");
            Require(world != null, "world_hold_point_missing");
            Require(drop != null, "drop_point_missing");
            Require(firstPerson != world, "hold_points_shared");
            Require(
                RequireProperty(serializedHolder, "firstPersonHeldItemScale").floatValue > 0f,
                "first_person_holder_scale_invalid");
            Require(
                RequireProperty(serializedHolder, "worldHeldItemScale").floatValue > 0f,
                "world_holder_scale_invalid");
        }

        private static void ValidatePose(
            SerializedObject serializedItem,
            string propertyName,
            PHSItemHeldPresentationSpec.HeldPoseSpec expected,
            string itemId)
        {
            var pose = RequireProperty(serializedItem, propertyName);
            var position = RequireProperty(pose, "localPosition").vector3Value;
            var rotation = RequireProperty(pose, "localEulerAngles").vector3Value;
            var scale = RequireProperty(pose, "scaleMultiplier").floatValue;
            Require(IsFinite(position), $"pose_position_nonfinite item={itemId} pose={propertyName}");
            Require(IsFinite(rotation), $"pose_rotation_nonfinite item={itemId} pose={propertyName}");
            Require(float.IsFinite(scale) && scale > 0f, $"pose_scale_invalid item={itemId} pose={propertyName}");
            Require(
                Approximately(position, expected.LocalPosition)
                && Approximately(rotation, expected.LocalEulerAngles)
                && Mathf.Abs(scale - expected.ScaleMultiplier) <= Tolerance,
                $"pose_mismatch item={itemId} pose={propertyName}");
        }

        private static int Count<T>(GameObject root)
            where T : Component
        {
            return root.GetComponentsInChildren<T>(true).Length;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= Tolerance * Tolerance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x)
                && float.IsFinite(value.y)
                && float.IsFinite(value.z);
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            Require(property != null, $"serialized_property_missing property={propertyName}");
            return property;
        }

        private static SerializedProperty RequireProperty(
            SerializedProperty parent,
            string propertyName)
        {
            var property = parent.FindPropertyRelative(propertyName);
            Require(
                property != null,
                $"serialized_property_missing property={parent.propertyPath}.{propertyName}");
            return property;
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    $"PHS_ITEM_HELD_PRESENTATION_VALIDATE_FAILED reason={reason}");
            }
        }
    }
}
