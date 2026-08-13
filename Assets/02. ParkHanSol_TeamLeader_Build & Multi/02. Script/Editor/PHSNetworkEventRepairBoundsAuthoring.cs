using System;
using SM;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSNetworkEventRepairBoundsAuthoring
    {
        private const string FirePrefabPath =
            "Assets/04. NohSeokMin_Game Event/03_Prefab/Fire/Effect_Fire.prefab";
        private const string OxygenLeakPrefabPath =
            "Assets/04. NohSeokMin_Game Event/03_Prefab/OxygenLeak/Effect_OxygenLeak.prefab";
        private const string OxygenLeakRuntimePrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventPresentation/PHS_OxygenLeakPipeRuntime.prefab";
        private const string BoundsName = "PHS_RepairBounds";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Team Event Repair Bounds")]
        public static void Author()
        {
            ConfigureFireBounds();
            ConfigureOxygenLeakBounds();
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_EVENT_REPAIR_BOUNDS_AUTHORED fire=4x2.5x4 oxygen=4x3x4");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Team Event Repair Bounds")]
        public static void Validate()
        {
            ValidateBounds<FireEffectInstance>(
                FirePrefabPath,
                new Vector3(4f, 2.5f, 4f),
                new Vector3(0f, 1.2f, 0f));
            ValidateBounds<OxygenLeakEffectInstance>(
                OxygenLeakPrefabPath,
                new Vector3(4f, 3f, 4f),
                new Vector3(0f, 1f, 0f));
            ValidateBounds<OxygenLeakEffectInstance>(
                OxygenLeakRuntimePrefabPath,
                new Vector3(4f, 3f, 4f),
                new Vector3(0f, 1f, 0f));
            Debug.Log("PHS_EVENT_REPAIR_BOUNDS_VALIDATED fire=true oxygenLegacy=true oxygenRuntime=true");
        }

        private static void ConfigureFireBounds()
        {
            ConfigureBounds<FireEffectInstance>(
                FirePrefabPath,
                new Vector3(4f, 2.5f, 4f),
                new Vector3(0f, 1.2f, 0f));
        }

        private static void ConfigureOxygenLeakBounds()
        {
            ConfigureBounds<OxygenLeakEffectInstance>(
                OxygenLeakPrefabPath,
                new Vector3(4f, 3f, 4f),
                new Vector3(0f, 1f, 0f));
            ConfigureBounds<OxygenLeakEffectInstance>(
                OxygenLeakRuntimePrefabPath,
                new Vector3(4f, 3f, 4f),
                new Vector3(0f, 1f, 0f));
        }

        private static void ConfigureBounds<T>(
            string prefabPath,
            Vector3 size,
            Vector3 center)
            where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_EVENT_REPAIR_BOUNDS_AUTHORING_FAILED reason=prefab_missing path={prefabPath}");
            }

            try
            {
                var target = root.GetComponentInChildren<T>(true);
                if (target == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_EVENT_REPAIR_BOUNDS_AUTHORING_FAILED reason=target_missing path={prefabPath} type={typeof(T).Name}");
                }

                var boundsTransform = target.transform.Find(BoundsName);
                if (boundsTransform == null)
                {
                    var boundsObject = new GameObject(BoundsName);
                    boundsObject.transform.SetParent(target.transform, false);
                    boundsTransform = boundsObject.transform;
                }

                var collider = boundsTransform.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = boundsTransform.gameObject.AddComponent<BoxCollider>();
                }

                collider.isTrigger = true;
                collider.center = center;
                collider.size = size;
                boundsTransform.gameObject.layer = target.gameObject.layer;

                var serializedTarget = new SerializedObject(target);
                var repairBounds = serializedTarget.FindProperty("repairBounds");
                if (repairBounds == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_EVENT_REPAIR_BOUNDS_AUTHORING_FAILED reason=field_missing path={prefabPath} field=repairBounds");
                }

                repairBounds.objectReferenceValue = collider;
                serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, prefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_EVENT_REPAIR_BOUNDS_AUTHORING_FAILED reason=save_failed path={prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateBounds<T>(
            string prefabPath,
            Vector3 expectedSize,
            Vector3 expectedCenter)
            where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_EVENT_REPAIR_BOUNDS_VALIDATION_FAILED reason=prefab_missing path={prefabPath}");
            }

            try
            {
                var target = root.GetComponentInChildren<T>(true);
                var boundsTransform = target == null
                    ? null
                    : target.transform.Find(BoundsName);
                var collider = boundsTransform == null
                    ? null
                    : boundsTransform.GetComponent<BoxCollider>();
                var serializedTarget = target == null
                    ? null
                    : new SerializedObject(target);
                var assignedBounds = serializedTarget?
                    .FindProperty("repairBounds")
                    ?.objectReferenceValue as Collider;
                if (target == null
                    || collider == null
                    || assignedBounds != collider
                    || !collider.isTrigger
                    || collider.size != expectedSize
                    || collider.center != expectedCenter)
                {
                    throw new InvalidOperationException(
                        $"PHS_EVENT_REPAIR_BOUNDS_VALIDATION_FAILED path={prefabPath} target={target != null} bounds={collider != null} assigned={assignedBounds == collider} trigger={collider != null && collider.isTrigger}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
