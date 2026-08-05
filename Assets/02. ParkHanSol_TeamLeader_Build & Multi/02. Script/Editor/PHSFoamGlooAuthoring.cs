using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSFoamGlooAuthoring
    {
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string TutorialPlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab";
        private const string RunRootPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab";
        private const string FoamItemDataPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/ParkHanSol_FoamSealantGunItemPrefabData.asset";
        private const string FoamDroppedPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ParkHanSol_FoamSealantGun.prefab";
        private const string FoamHeldPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Held/ParkHanSol_FoamSealantGun_Held.prefab";
        private const string ActiveNetworkPrefabsPath =
            "Assets/DefaultNetworkPrefabs.asset";
        private const string FoamAssetFolder =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Items/Foam";
        private const string FoamBlobPrefabPath =
            FoamAssetFolder + "/PHS_NetworkFoamBlob.prefab";
        private const string FoamMaterialPath =
            FoamAssetFolder + "/PHS_FoamBlob.mat";
        private const string OriginName = "PHS_FoamServerOrigin";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Foam GLOO P0")]
        public static void Author()
        {
            RequireAsset<GameObject>(PlayerPrefabPath);
            RequireAsset<GameObject>(TutorialPlayerPrefabPath);
            RequireAsset<GameObject>(RunRootPrefabPath);
            RequireAsset<UtilityItemPrefabData>(FoamItemDataPath);
            RequireAsset<NetworkPrefabsList>(ActiveNetworkPrefabsPath);

            EnsureFolder(FoamAssetFolder);
            var material = ConfigureMaterial();
            var foamBlobPrefab = ConfigureBlobPrefab(material);
            ConfigureRunRoot(foamBlobPrefab);
            ConfigurePlayer(PlayerPrefabPath);
            ConfigurePlayer(TutorialPlayerPrefabPath);
            ConfigureFoamItemData();
            ConfigureFoamItemPrefabs();
            RegisterNetworkPrefab(foamBlobPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log(
                "PHS_FOAM_GLOO_AUTHORING_COMPLETE thresholds=fire:4,hull:6,surface:3 hold=2.00 dissolve=0.45 network_prefab=active_list");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Foam Dropped Item Contract")]
        public static void AuthorDroppedItemContract()
        {
            RequireAsset<GameObject>(FoamDroppedPrefabPath);
            RequireAsset<GameObject>(FoamHeldPrefabPath);
            ConfigureFoamItemPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log(
                "PHS_FOAM_DROPPED_ITEM_AUTHORING_COMPLETE dropped_durability=1 held_durability=0");
        }

        private static Material ConfigureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                FoamMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "PHS_FOAM_GLOO_AUTHORING_FAILED reason=urp_lit_shader_missing");
                }

                material = new Material(shader)
                {
                    name = "PHS_FoamBlob"
                };
                AssetDatabase.CreateAsset(material, FoamMaterialPath);
            }

            var baseColor = new Color(0.68f, 0.91f, 1f, 1f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.72f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.05f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject ConfigureBlobPrefab(Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamBlobPrefabPath);
            if (existing == null)
            {
                var temporaryRoot = new GameObject("PHS_NetworkFoamBlob");
                try
                {
                    ConfigureBlobRoot(temporaryRoot, material);
                    existing = PrefabUtility.SaveAsPrefabAsset(
                        temporaryRoot,
                        FoamBlobPrefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(temporaryRoot);
                }

                if (existing == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=blob_prefab_create_failed path={FoamBlobPrefabPath}");
                }

                return existing;
            }

            EditPrefab(
                FoamBlobPrefabPath,
                root => ConfigureBlobRoot(root, material));
            return RequireAsset<GameObject>(FoamBlobPrefabPath);
        }

        private static void ConfigureBlobRoot(GameObject root, Material material)
        {
            root.name = "PHS_NetworkFoamBlob";
            RequireSingleOrAdd<NetworkObject>(root, FoamBlobPrefabPath);
            var blob = RequireSingleOrAdd<PHSNetworkFoamBlob>(
                root,
                FoamBlobPrefabPath);

            var visual = root.transform.Find("Visual");
            if (visual == null)
            {
                var visualObject = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                visualObject.name = "Visual";
                visualObject.transform.SetParent(root.transform, false);
                visual = visualObject.transform;
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider, true);
            }

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=blob_renderer_count count={renderers.Length}");
            }

            renderers[0].sharedMaterial = material;
            renderers[0].shadowCastingMode = ShadowCastingMode.Off;
            renderers[0].receiveShadows = false;

            var trail = RequireSingleOrAdd<TrailRenderer>(
                root,
                FoamBlobPrefabPath);
            trail.sharedMaterial = material;
            trail.time = 0.18f;
            trail.startWidth = 0.055f;
            trail.endWidth = 0.006f;
            trail.minVertexDistance = 0.03f;
            trail.emitting = false;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var serialized = new SerializedObject(blob);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual;
            serialized.FindProperty("flightTrail").objectReferenceValue = trail;
            serialized.FindProperty("attachedScale").vector3Value =
                new Vector3(0.22f, 0.12f, 0.22f);
            serialized.FindProperty("hardenSeconds").floatValue = 0.18f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRunRoot(GameObject foamBlobPrefab)
        {
            EditPrefab(RunRootPrefabPath, root =>
            {
                RequireSingle<NetworkObject>(root, RunRootPrefabPath);
                var coordinator = RequireSingleOrAdd<PHSNetworkFoamCoordinator>(
                    root,
                    RunRootPrefabPath);
                var serialized = new SerializedObject(coordinator);
                serialized.FindProperty("foamBlobPrefab").objectReferenceValue =
                    foamBlobPrefab;
                serialized.FindProperty("hitLayers").intValue =
                    Physics.DefaultRaycastLayers;
                serialized.FindProperty("projectileSpeed").floatValue = 18f;
                serialized.FindProperty("maximumRange").floatValue = 8f;
                serialized.FindProperty("collisionRadius").floatValue = 0.08f;
                serialized.FindProperty("maximumBlobsPerOwner").intValue = 20;
                serialized.FindProperty("maximumBlobsGlobal").intValue = 96;
                serialized.FindProperty("pendingTargetLifetime").floatValue = 8f;
                serialized.FindProperty("surfaceLifetime").floatValue = 20f;
                serialized.FindProperty("completionHoldSeconds").floatValue = 2f;
                serialized.FindProperty("dissolveSeconds").floatValue = 0.45f;
                serialized.FindProperty("hullCaptureRadius").floatValue = 0.9f;
                serialized.FindProperty("surfaceClusterRadius").floatValue = 0.65f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void ConfigurePlayer(string path)
        {
            EditPrefab(path, root =>
            {
                RequireSingle<NetworkObject>(root, path);
                var itemRecord = RequireSingle<NetworkPlayerItemRecord>(
                    root,
                    path);
                var lifeState = RequireSingle<NetworkPlayerLifeState>(
                    root,
                    path);
                var action = RequireSingle<PHSNetworkItemUseActionController>(
                    root,
                    path);
                var feedback = RequireSingle<
                    PHSNetworkItemUseFeedbackController>(root, path);
                var cameras = root.GetComponentsInChildren<Camera>(true);
                if (cameras.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=player_camera_count path={path} count={cameras.Length}");
                }

                var origin = root.transform.Find(OriginName);
                if (origin == null)
                {
                    var originObject = new GameObject(OriginName);
                    originObject.transform.SetParent(root.transform, false);
                    origin = originObject.transform;
                }

                origin.localPosition = new Vector3(0f, 1.35f, 0.45f);
                origin.localRotation = Quaternion.identity;
                origin.localScale = Vector3.one;

                var gun = RequireSingleOrAdd<PHSNetworkFoamGunController>(
                    root,
                    path);
                var serialized = new SerializedObject(gun);
                serialized.FindProperty("ownerAimCamera").objectReferenceValue =
                    cameras[0];
                serialized.FindProperty("serverOrigin").objectReferenceValue =
                    origin;
                serialized.FindProperty("itemRecord").objectReferenceValue =
                    itemRecord;
                serialized.FindProperty("lifeState").objectReferenceValue =
                    lifeState;
                serialized.FindProperty("actionController").objectReferenceValue =
                    action;
                serialized.FindProperty("feedbackController").objectReferenceValue =
                    feedback;
                serialized.FindProperty("fireIntervalSeconds").floatValue = 0.125f;
                serialized.FindProperty("maximumOriginError").floatValue = 1.25f;
                serialized.FindProperty("maximumYawError").floatValue = 35f;
                serialized.FindProperty("maximumPitch").floatValue = 80f;
                serialized.FindProperty("telegraphIntervalSeconds").floatValue = 0.5f;
                serialized.FindProperty("telegraphRadius").floatValue = 0.12f;
                serialized.FindProperty("telegraphDistance").floatValue = 8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static void ConfigureFoamItemData()
        {
            var itemData = RequireAsset<UtilityItemPrefabData>(FoamItemDataPath);
            var serialized = new SerializedObject(itemData);
            serialized.FindProperty("hasDurability").boolValue = true;
            serialized.FindProperty("maxDurability").intValue = 100;
            serialized.FindProperty("upgradeEffect").intValue =
                (int)UtilityItemUpgradeEffect.None;
            serialized.FindProperty("upgradeAmount").floatValue = 0f;
            var profiles = serialized.FindProperty("actionProfiles");
            profiles.arraySize = 2;
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(0),
                UtilityItemActionKind.FireSuppression,
                200,
                1);
            ConfigureProfile(
                profiles.GetArrayElementAtIndex(1),
                UtilityItemActionKind.HullBreachRepair,
                100,
                1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemData);
        }

        private static void ConfigureFoamItemPrefabs()
        {
            EditPrefab(FoamDroppedPrefabPath, root =>
            {
                var itemObject = RequireSingle<UtilityItemObject>(
                    root,
                    FoamDroppedPrefabPath);
                var durabilityState = RequireSingleOrAdd<
                    NetworkUtilityItemDurabilityState>(
                    root,
                    FoamDroppedPrefabPath);
                var serialized = new SerializedObject(durabilityState);
                serialized.FindProperty("itemObject").objectReferenceValue =
                    itemObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            });

            EditPrefab(FoamHeldPrefabPath, root =>
            {
                foreach (var durabilityState in root.GetComponentsInChildren<
                    NetworkUtilityItemDurabilityState>(true))
                {
                    UnityEngine.Object.DestroyImmediate(
                        durabilityState,
                        true);
                }

                var remaining = root.GetComponentsInChildren<
                    NetworkUtilityItemDurabilityState>(true).Length;
                if (remaining != 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=held_durability_state_count path={FoamHeldPrefabPath} count={remaining}");
                }
            });
        }

        private static void RegisterNetworkPrefab(GameObject foamBlobPrefab)
        {
            var list = RequireAsset<NetworkPrefabsList>(
                ActiveNetworkPrefabsPath);
            var matches = new List<NetworkPrefab>();
            foreach (var entry in list.PrefabList)
            {
                if (entry != null && entry.Prefab == foamBlobPrefab)
                {
                    matches.Add(entry);
                }
            }

            for (var index = matches.Count - 1; index >= 1; index--)
            {
                list.Remove(matches[index]);
            }

            if (matches.Count == 0)
            {
                list.Add(new NetworkPrefab
                {
                    Override = NetworkPrefabOverride.None,
                    Prefab = foamBlobPrefab
                });
            }
            else
            {
                matches[0].Override = NetworkPrefabOverride.None;
                matches[0].SourcePrefabToOverride = null;
                matches[0].SourceHashToOverride = 0U;
                matches[0].OverridingTargetPrefab = null;
            }

            EditorUtility.SetDirty(list);
        }

        private static void ConfigureProfile(
            SerializedProperty property,
            UtilityItemActionKind kind,
            int amount,
            int durabilityCost)
        {
            property.FindPropertyRelative("actionKind").intValue = (int)kind;
            property.FindPropertyRelative("amount").intValue = amount;
            property.FindPropertyRelative("durabilityCost").intValue =
                durabilityCost;
        }

        private static T RequireSingle<T>(GameObject root, string path)
            where T : Component
        {
            var components = root.GetComponents<T>();
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=component_count path={path} component={typeof(T).Name} count={components.Length}");
            }

            return components[0];
        }

        private static T RequireSingleOrAdd<T>(GameObject root, string path)
            where T : Component
        {
            var components = root.GetComponents<T>();
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=component_duplicate path={path} component={typeof(T).Name} count={components.Length}");
            }

            return components.Length == 1
                ? components[0]
                : root.AddComponent<T>();
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=asset_missing path={path}");
            }

            return asset;
        }

        private static void EditPrefab(
            string path,
            Action<GameObject> configure)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=prefab_load_failed path={path}");
            }

            try
            {
                configure(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=prefab_save_failed path={path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=folder_path_invalid path={path}");
            }

            var parent = path.Substring(0, slashIndex);
            var folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(
                    AssetDatabase.CreateFolder(parent, folderName)))
            {
                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_AUTHORING_FAILED reason=folder_create_failed path={path}");
            }
        }
    }
}
