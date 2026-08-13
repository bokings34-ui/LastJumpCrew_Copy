using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSExteriorDebrisHullBounceAuthoring
    {
        private const string ScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string BounceMaterialPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/Debris/PHS_DebrisExteriorBounce.physicMaterial";
        private const string ExteriorHullRootName = "Spaceship_SpaceCrew_Outside_MapVer3";
        private const float Restitution = 0.95f;
        private const float MinimumOutgoingSpeed = 3.5f;
        private const float OutwardVelocityBoost = 1.25f;

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Exterior Debris Hull Bounce")]
        public static void AuthorExteriorDebrisHullBounce()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var exteriorHullRoot = RequireExteriorHullRoot(scene);
            var shipWallLayer = LayerMask.NameToLayer("ShipWall");
            Require(shipWallLayer >= 0, "ship_wall_layer_missing");

            var hullColliders = GetExteriorHullColliders(exteriorHullRoot).ToArray();
            Require(hullColliders.Length > 0, "exterior_hull_colliders_missing");
            foreach (var hullCollider in hullColliders)
            {
                hullCollider.gameObject.layer = shipWallLayer;
                if (!hullCollider.TryGetComponent<PHSExteriorHullDebrisBounce>(out var bounce))
                {
                    bounce = hullCollider.gameObject.AddComponent<PHSExteriorHullDebrisBounce>();
                }

                var serializedBounce = new SerializedObject(bounce);
                serializedBounce.FindProperty("restitution").floatValue = Restitution;
                serializedBounce.FindProperty("minimumOutgoingSpeed").floatValue = MinimumOutgoingSpeed;
                serializedBounce.FindProperty("outwardVelocityBoost").floatValue = OutwardVelocityBoost;
                serializedBounce.ApplyModifiedPropertiesWithoutUndo();
            }

            var bounceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BounceMaterialPath);
            Require(bounceMaterial != null, "debris_bounce_material_missing");
            Require(bounceMaterial.bounciness >= 0.3f, "debris_bounce_material_restitution_small");
            var debrisPrefabs = GetExteriorDebrisPrefabs(scene);
            foreach (var debrisPrefab in debrisPrefabs)
            {
                foreach (var collider in debrisPrefab.GetComponentsInChildren<Collider>(true))
                {
                    collider.sharedMaterial = bounceMaterial;
                }

                EditorUtility.SetDirty(debrisPrefab);
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "scene_save_failed");
            ValidateExteriorDebrisHullBounce();
            Debug.Log($"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_AUTHOR_OK hull_colliders={hullColliders.Length} debris_prefabs={debrisPrefabs.Count}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Exterior Debris Hull Bounce")]
        public static void ValidateExteriorDebrisHullBounce()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var errors = new List<string>();
            var shipWallLayer = LayerMask.NameToLayer("ShipWall");
            Require(shipWallLayer >= 0, "ship_wall_layer_missing", errors);
            if (shipWallLayer >= 0)
            {
                Require(
                    !Physics.GetIgnoreLayerCollision(0, shipWallLayer),
                    "debris_default_ship_wall_collision_disabled",
                    errors);
            }

            var exteriorHullRoot = FindByName(scene, ExteriorHullRootName);
            Require(exteriorHullRoot != null, "exterior_hull_root_missing", errors);
            var hullColliders = exteriorHullRoot == null
                ? Array.Empty<Collider>()
                : GetExteriorHullColliders(exteriorHullRoot).ToArray();
            Require(hullColliders.Length > 0, "exterior_hull_colliders_missing", errors);
            foreach (var hullCollider in hullColliders)
            {
                Require(hullCollider.gameObject.layer == shipWallLayer, $"hull_layer_invalid:{hullCollider.name}", errors);
                Require(
                    hullCollider.TryGetComponent<PHSExteriorHullDebrisBounce>(out _),
                    $"hull_bounce_missing:{hullCollider.name}",
                    errors);
            }

            var bounceMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BounceMaterialPath);
            Require(bounceMaterial != null, "debris_bounce_material_missing", errors);
            if (bounceMaterial != null)
            {
                Require(bounceMaterial.bounciness >= 0.3f, "debris_bounce_material_restitution_small", errors);
            }

            var debrisPrefabs = GetExteriorDebrisPrefabs(scene);
            Require(debrisPrefabs.Count > 0, "exterior_debris_prefabs_missing", errors);
            foreach (var debrisPrefab in debrisPrefabs)
            {
                Require(debrisPrefab.GetComponent<DebrisItem>() != null, $"debris_item_missing:{debrisPrefab.name}", errors);
                Require(debrisPrefab.GetComponent<Rigidbody>() != null, $"debris_rigidbody_missing:{debrisPrefab.name}", errors);
                Require(debrisPrefab.GetComponent<NetworkObject>() != null, $"debris_network_object_missing:{debrisPrefab.name}", errors);
                Require(debrisPrefab.GetComponent<NetworkTransform>() != null, $"debris_network_transform_missing:{debrisPrefab.name}", errors);
                Require(debrisPrefab.GetComponent<NetworkItemPhysicsAuthority>() != null, $"debris_server_authority_missing:{debrisPrefab.name}", errors);
                var colliders = debrisPrefab.GetComponentsInChildren<Collider>(true);
                Require(colliders.Length > 0, $"debris_collider_missing:{debrisPrefab.name}", errors);
                foreach (var collider in colliders)
                {
                    Require(collider.sharedMaterial == bounceMaterial, $"debris_bounce_material_missing:{debrisPrefab.name}:{collider.name}", errors);
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_VALIDATE_FAILED errors={string.Join(",", errors)}");
            }

            Debug.Log($"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_VALIDATE_PASS hull_colliders={hullColliders.Length} debris_prefabs={debrisPrefabs.Count} server_authority=true");
        }

        private static List<GameObject> GetExteriorDebrisPrefabs(Scene scene)
        {
            var stream = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PHSRandomDebrisStream>(true))
                .SingleOrDefault();
            if (stream == null)
            {
                throw new InvalidOperationException("PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_FAILED reason=debris_stream_missing");
            }

            var state = new SerializedObject(stream);
            var debrisRoots = state.FindProperty("debrisRoots");
            if (debrisRoots == null || !debrisRoots.isArray)
            {
                throw new InvalidOperationException("PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_FAILED reason=debris_roots_missing");
            }

            var prefabs = new HashSet<GameObject>();
            for (var index = 0; index < debrisRoots.arraySize; index++)
            {
                if (debrisRoots.GetArrayElementAtIndex(index).objectReferenceValue is not Transform debrisRoot)
                {
                    throw new InvalidOperationException($"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_FAILED reason=debris_seed_missing index={index}");
                }

                var itemObject = debrisRoot.GetComponent<UtilityItemObject>();
                var prefab = itemObject?.ItemPrefabData?.DroppedPrefab;
                if (prefab == null)
                {
                    throw new InvalidOperationException($"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_FAILED reason=debris_prefab_missing seed={debrisRoot.name}");
                }

                prefabs.Add(prefab);
            }

            return prefabs.OrderBy(prefab => prefab.name, StringComparer.Ordinal).ToList();
        }

        private static Transform RequireExteriorHullRoot(Scene scene)
        {
            var root = FindByName(scene, ExteriorHullRootName);
            Require(root != null, "exterior_hull_root_missing");
            return root;
        }

        private static Transform FindByName(Scene scene, string targetName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == targetName);
        }

        private static IEnumerable<Collider> GetExteriorHullColliders(Transform exteriorHullRoot)
        {
            return exteriorHullRoot
                .GetComponentsInChildren<Collider>(true)
                .Where(collider => collider.enabled && !collider.isTrigger);
        }

        private static void Require(bool condition, string reason)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"PHS_EXTERIOR_DEBRIS_HULL_BOUNCE_FAILED reason={reason}");
            }
        }

        private static void Require(bool condition, string reason, ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(reason);
            }
        }
    }
}
