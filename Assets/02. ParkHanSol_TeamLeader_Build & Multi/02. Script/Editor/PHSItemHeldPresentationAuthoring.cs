using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    internal static class PHSItemHeldPresentationAuthoring
    {
        [MenuItem("Tools/ParkHanSol/BEAVER/Author Item Held Presentation")]
        public static void Author()
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
                AuthorPose(item, spec);
                if (spec.IsDebris)
                {
                    RebuildDebrisHeldPrefab(item);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            PHSItemHeldPresentationValidator.Validate();
            Debug.Log(
                "PHS_ITEM_HELD_PRESENTATION_AUTHOR_OK " +
                $"catalog={catalog.Items.Count} debrisHeld=5 poses={catalog.Items.Count * 2}");
        }

        private static Dictionary<string, UtilityItemDataSO> BuildCatalogMap(
            UtilityItemCatalogSO catalog)
        {
            var result = new Dictionary<string, UtilityItemDataSO>(
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

        private static void AuthorPose(
            UtilityItemDataSO item,
            PHSItemHeldPresentationSpec.ItemPoseSpec spec)
        {
            var heldPrefab = item.HandPrefab;
            var droppedPrefab = item.DroppedPrefab;
            Require(heldPrefab != null, $"held_prefab_missing item={spec.ItemId}");
            Require(droppedPrefab != null, $"dropped_prefab_missing item={spec.ItemId}");
            Require(
                heldPrefab != droppedPrefab,
                $"held_dropped_prefab_shared item={spec.ItemId}");

            var serializedItem = new SerializedObject(item);
            WritePose(serializedItem, "firstPersonHeldPose", spec.FirstPerson);
            WritePose(serializedItem, "worldHeldPose", spec.World);
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);

            Require(
                item.HandPrefab == heldPrefab && item.DroppedPrefab == droppedPrefab,
                $"prefab_reference_changed item={spec.ItemId}");
        }

        private static void RebuildDebrisHeldPrefab(UtilityItemDataSO item)
        {
            var heldPrefabPath = AssetDatabase.GetAssetPath(item.HandPrefab);
            var droppedPrefabPath = AssetDatabase.GetAssetPath(item.DroppedPrefab);
            Require(
                heldPrefabPath.Contains("/Debris/Held/", StringComparison.Ordinal),
                $"debris_held_path_invalid item={item.ItemId} path={heldPrefabPath}");
            Require(
                !string.Equals(
                    heldPrefabPath,
                    droppedPrefabPath,
                    StringComparison.Ordinal),
                $"debris_prefab_shared item={item.ItemId}");

            var heldName = item.HandPrefab.name;
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject heldRoot = null;
            try
            {
                heldRoot = PrefabUtility.InstantiatePrefab(
                    item.DroppedPrefab,
                    previewScene) as GameObject;
                Require(
                    heldRoot != null,
                    $"debris_dropped_instantiate_failed item={item.ItemId}");
                heldRoot.name = heldName;
                heldRoot.transform.SetLocalPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                RemoveAll<GrappleCollectibleItem>(heldRoot);
                RemoveAll<RigidbodyGrappleTarget>(heldRoot);
                RemoveAll<ItemGravityReceiver>(heldRoot);
                RemoveAll<NetworkBehaviour>(heldRoot);
                RemoveAll<NetworkObject>(heldRoot);

                var rigidbodies = heldRoot.GetComponentsInChildren<Rigidbody>(true);
                var colliders = heldRoot.GetComponentsInChildren<Collider>(true);
                Require(
                    rigidbodies.Length == 1,
                    $"held_rigidbody_count item={item.ItemId} count={rigidbodies.Length}");
                Require(
                colliders.Length == 1,
                    $"held_collider_count item={item.ItemId} count={colliders.Length}");
                rigidbodies[0].useGravity = false;
                rigidbodies[0].isKinematic = true;
                colliders[0].enabled = false;

                Require(
                    heldRoot.GetComponent<UtilityItemObject>() != null,
                    $"held_utility_item_missing item={item.ItemId}");
                Require(
                    heldRoot.GetComponentsInChildren<Renderer>(true).Length > 0,
                    $"held_visual_missing item={item.ItemId}");
                PrefabUtility.SaveAsPrefabAsset(heldRoot, heldPrefabPath);
            }
            finally
            {
                if (heldRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(heldRoot);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void WritePose(
            SerializedObject serializedItem,
            string propertyName,
            PHSItemHeldPresentationSpec.HeldPoseSpec pose)
        {
            Require(pose.ScaleMultiplier > 0f, $"pose_scale_invalid property={propertyName}");
            var property = RequireProperty(serializedItem, propertyName);
            RequireProperty(property, "localPosition").vector3Value = pose.LocalPosition;
            RequireProperty(property, "localEulerAngles").vector3Value = pose.LocalEulerAngles;
            RequireProperty(property, "scaleMultiplier").floatValue = pose.ScaleMultiplier;
        }

        private static void RemoveAll<T>(GameObject root)
            where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
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
                    $"PHS_ITEM_HELD_PRESENTATION_AUTHOR_FAILED reason={reason}");
            }
        }
    }
}
