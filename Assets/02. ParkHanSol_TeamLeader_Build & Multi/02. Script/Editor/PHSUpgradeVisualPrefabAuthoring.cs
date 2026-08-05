using System;
using System.Collections.Generic;
using System.IO;
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
    public static class PHSUpgradeVisualPrefabAuthoring
    {
        private const string VisualFolder = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Items/Visual";
        private const string UpgradeFolder = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/ShopUpgrades";

        private sealed class VisualSpec
        {
            public VisualSpec(string modelPath, string modelGuid, string prefabName, float scale)
            {
                ModelPath = modelPath;
                ModelGuid = modelGuid;
                PrefabPath = $"{VisualFolder}/{prefabName}.prefab";
                Scale = scale;
            }

            public string ModelPath { get; }
            public string ModelGuid { get; }
            public string PrefabPath { get; }
            public float Scale { get; }
        }

        private static readonly VisualSpec[] Specs =
        {
            new("Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/grapple charger 3d model/GrappleCharger.fbx", "8e9edf937eb319048bd904f66356987c", "PHS_HookPowerUpgrade_Visual", 0.6f),
            new("Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/RepairBundle/RepairBundle.fbx", "00ec8b50c5167f74b83d7be21b3f87d9", "PHS_ShipHpRestore_Visual", 0.8f),
            new("Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/UpgradelGadget/UpgradelGadget_3D_model.fbx", "3e1eb4d8cc1637142a7b84028e699406", "PHS_ShipMaxHpUpgrade_Visual", 0.6f),
            new("Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/futuristic booster 3d model/futuristic_booster_3d_model.fbx", "73ee8c21d8e3cad458daf08caed810c9", "PHS_ThrusterDurationUpgrade_Visual", 0.6f),
            new("Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/EnergyDrink/EnergyDrink.fbx", "1786de0d5a1a8a942a43644b2b1e0a58", "PHS_PlayerMaxHpUpgrade_Visual", 1.4f),
        };

        private sealed class WrapperSpec
        {
            public WrapperSpec(
                string itemName,
                string droppedGuid,
                string heldGuid,
                Vector3 droppedPosition,
                Vector3 droppedEuler,
                Vector3 droppedScale,
                Vector3 heldPosition,
                Vector3 heldEuler,
                Vector3 heldScale)
            {
                VisualPath = $"{VisualFolder}/{itemName}_Visual.prefab";
                DroppedPath = $"{UpgradeFolder}/{itemName}.prefab";
                HeldPath = $"{UpgradeFolder}/Held/{itemName}_Held.prefab";
                DroppedGuid = droppedGuid;
                HeldGuid = heldGuid;
                DroppedPosition = droppedPosition;
                DroppedEuler = droppedEuler;
                DroppedScale = droppedScale;
                HeldPosition = heldPosition;
                HeldEuler = heldEuler;
                HeldScale = heldScale;
            }

            public string VisualPath { get; }
            public string DroppedPath { get; }
            public string HeldPath { get; }
            public string DroppedGuid { get; }
            public string HeldGuid { get; }
            public Vector3 DroppedPosition { get; }
            public Vector3 DroppedEuler { get; }
            public Vector3 DroppedScale { get; }
            public Vector3 HeldPosition { get; }
            public Vector3 HeldEuler { get; }
            public Vector3 HeldScale { get; }
        }

        private static readonly WrapperSpec[] WrapperSpecs =
        {
            new("PHS_HookPowerUpgrade", "b3a78886dd6848b4299d5f70c70efe40", "42afa171533da204993c56890045c46b", Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, Vector3.one),
            new("PHS_ShipHpRestore", "031073f8939dac94294c2ca20d1836b3", "358bef38b1c9cab479074e27bd138631", Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, Vector3.one),
            new("PHS_ShipMaxHpUpgrade", "aefdd7d1af0afb1459b2b15aa8414361", "f7aa98435fea7574f8b334fe624dbd5e", Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, Vector3.one),
            new("PHS_ThrusterDurationUpgrade", "7c5ae1d676fad2d4b8af19d10a9f3e6a", "e4ef2e2ad2fedea45bb1ce467185d29b", Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, Vector3.one),
            new("PHS_PlayerMaxHpUpgrade", "85b4ba61384736742873bbb1a581a4de", "7a5f21e26f9974840917576290d4c498", Vector3.zero, Vector3.zero, Vector3.one, Vector3.zero, Vector3.zero, Vector3.one),
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Build Upgrade Item Visual Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before authoring upgrade visual prefabs.");
            }

            ValidateModelSources();
            EnsureFolder(VisualFolder);

            foreach (VisualSpec spec in Specs)
            {
                BuildVisualPrefab(spec);
            }

            BuildHeldAndDroppedPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOrThrow();
            Debug.Log($"PHS_UPGRADE_VISUAL_AUTHORING_OK items={Specs.Length}");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Upgrade Item Visual Prefabs")]
        public static void Validate()
        {
            ValidateOrThrow();
            Debug.Log($"PHS_UPGRADE_VISUAL_VALIDATION_OK items={Specs.Length}");
        }

        public static void ValidateOrThrow()
        {
            var failures = new List<string>();

            foreach (VisualSpec spec in Specs)
            {
                ValidateSpec(spec, failures);
            }

            ValidateHeldAndDropped(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("PHS_UPGRADE_VISUAL_VALIDATION_FAILED\n- " + string.Join("\n- ", failures));
            }
        }

        private static void BuildVisualPrefab(VisualSpec spec)
        {
            string existingGuid = AssetDatabase.AssetPathToGUID(spec.PrefabPath);
            Scene previewScene = EditorSceneManager.NewPreviewScene();

            try
            {
                var root = new GameObject("Visual");
                SceneManager.MoveGameObjectToScene(root, previewScene);

                var controller = root.AddComponent<UtilityItemVfxController>();
                Transform modelRoot = CreateChild(root.transform, "ModelRoot");
                Transform vfxRoot = CreateChild(root.transform, "VFXRoot");
                CreateChild(vfxRoot, "Use");
                CreateChild(vfxRoot, "Loop");
                CreateChild(vfxRoot, "Impact");

                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
                var model = PrefabUtility.InstantiatePrefab(modelAsset, previewScene) as GameObject;
                if (model == null)
                {
                    throw new InvalidOperationException($"Could not instantiate model: {spec.ModelPath}");
                }

                model.name = "Model";
                model.transform.SetParent(modelRoot, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * spec.Scale;

                SetEmptyChannels(controller);

                if (PrefabUtility.SaveAsPrefabAsset(root, spec.PrefabPath) == null)
                {
                    throw new InvalidOperationException($"Could not save prefab: {spec.PrefabPath}");
                }

                string savedGuid = AssetDatabase.AssetPathToGUID(spec.PrefabPath);
                if (!string.IsNullOrEmpty(existingGuid) && !string.Equals(existingGuid, savedGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Prefab GUID changed: {spec.PrefabPath}");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void SetEmptyChannels(UtilityItemVfxController controller)
        {
            var serializedController = new SerializedObject(controller);
            foreach (string channelName in new[] { "use", "loop", "impact" })
            {
                SerializedProperty channel = serializedController.FindProperty(channelName);
                SerializedProperty particles = channel?.FindPropertyRelative("particleSystems");
                SerializedProperty audio = channel?.FindPropertyRelative("audioSources");
                if (particles == null || audio == null || !particles.isArray || !audio.isArray)
                {
                    throw new InvalidOperationException($"UtilityItemVfxController channel layout mismatch: {channelName}");
                }

                particles.arraySize = 0;
                audio.arraySize = 0;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateModelSources()
        {
            foreach (VisualSpec spec in Specs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath) == null ||
                    !string.Equals(AssetDatabase.AssetPathToGUID(spec.ModelPath), spec.ModelGuid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Model source missing or GUID mismatch: {spec.ModelPath}");
                }
            }
        }

        private static void ValidateSpec(VisualSpec spec, ICollection<string> failures)
        {
            if (!string.Equals(AssetDatabase.AssetPathToGUID(spec.ModelPath), spec.ModelGuid, StringComparison.Ordinal))
            {
                failures.Add($"Model GUID mismatch: {spec.ModelPath}");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
            if (prefab == null)
            {
                failures.Add($"Missing prefab: {spec.PrefabPath}");
                return;
            }

            string expectedRootName = Path.GetFileNameWithoutExtension(spec.PrefabPath);
            if (prefab.name != expectedRootName || prefab.transform.childCount != 2)
            {
                failures.Add($"Root hierarchy mismatch: {spec.PrefabPath}");
            }

            Transform modelRoot = prefab.transform.Find("ModelRoot");
            Transform vfxRoot = prefab.transform.Find("VFXRoot");
            if (modelRoot == null || vfxRoot == null || modelRoot.parent != prefab.transform || vfxRoot.parent != prefab.transform)
            {
                failures.Add($"ModelRoot/VFXRoot mismatch: {spec.PrefabPath}");
                return;
            }

            if (modelRoot.childCount != 1 || modelRoot.GetComponentsInChildren<Renderer>(true).Length == 0)
            {
                failures.Add($"ModelRoot content mismatch: {spec.PrefabPath}");
            }
            else
            {
                Transform model = modelRoot.GetChild(0);
                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject);
                if (AssetDatabase.GetAssetPath(source) != spec.ModelPath ||
                    (model.localScale - Vector3.one * spec.Scale).sqrMagnitude > 0.0001f)
                {
                    failures.Add($"Model source/scale mismatch: {spec.PrefabPath}");
                }
            }

            if (vfxRoot.childCount != 3 || !HasDirectChild(vfxRoot, "Use") || !HasDirectChild(vfxRoot, "Loop") || !HasDirectChild(vfxRoot, "Impact"))
            {
                failures.Add($"VFXRoot channels mismatch: {spec.PrefabPath}");
            }

            UtilityItemVfxController[] controllers = prefab.GetComponents<UtilityItemVfxController>();
            if (controllers.Length != 1)
            {
                failures.Add($"Controller mismatch: {spec.PrefabPath}");
                return;
            }

            var serializedController = new SerializedObject(controllers[0]);
            foreach (string channelName in new[] { "use", "loop", "impact" })
            {
                SerializedProperty channel = serializedController.FindProperty(channelName);
                SerializedProperty particles = channel?.FindPropertyRelative("particleSystems");
                SerializedProperty audio = channel?.FindPropertyRelative("audioSources");
                if (particles == null || audio == null || particles.arraySize != 0 || audio.arraySize != 0)
                {
                    failures.Add($"Channel array mismatch ({channelName}): {spec.PrefabPath}");
                }
            }
        }

        private static void BuildHeldAndDroppedPrefabs()
        {
            foreach (WrapperSpec spec in WrapperSpecs)
            {
                BuildWrapper(spec.DroppedPath, spec.DroppedGuid, spec.VisualPath,
                    spec.DroppedPosition, spec.DroppedEuler, spec.DroppedScale, false);
                BuildWrapper(spec.HeldPath, spec.HeldGuid, spec.VisualPath,
                    spec.HeldPosition, spec.HeldEuler, spec.HeldScale, true);
            }
        }

        private static void BuildWrapper(
            string wrapperPath,
            string expectedGuid,
            string visualPath,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale,
            bool isHeld)
        {
            string originalGuid = AssetDatabase.AssetPathToGUID(wrapperPath);
            if (!string.Equals(originalGuid, expectedGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Wrapper missing or GUID mismatch: {wrapperPath}");
            }

            GameObject visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
            if (visualAsset == null)
            {
                throw new InvalidOperationException($"Canonical visual missing: {visualPath}");
            }

            GameObject root = PrefabUtility.LoadPrefabContents(wrapperPath);
            try
            {
                RemoveCanonicalVisualChildren(root.transform, visualPath);

                foreach (Renderer legacyRenderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    legacyRenderer.enabled = false;
                }

                if (isHeld)
                {
                    RemoveHeldOnlyComponents(root);
                }

                var visual = PrefabUtility.InstantiatePrefab(visualAsset, root.scene) as GameObject;
                if (visual == null)
                {
                    throw new InvalidOperationException($"Could not instantiate canonical visual: {visualPath}");
                }

                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = localPosition;
                visual.transform.localRotation = Quaternion.Euler(localEuler);
                visual.transform.localScale = localScale;

                if (PrefabUtility.SaveAsPrefabAsset(root, wrapperPath) == null)
                {
                    throw new InvalidOperationException($"Could not save wrapper: {wrapperPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (!string.Equals(AssetDatabase.AssetPathToGUID(wrapperPath), originalGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Wrapper GUID changed: {wrapperPath}");
            }
        }

        private static void RemoveCanonicalVisualChildren(Transform root, string visualPath)
        {
            for (int index = root.childCount - 1; index >= 0; index--)
            {
                Transform child = root.GetChild(index);
                UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                bool isCanonicalVisual = child.name == "Visual" &&
                    string.Equals(AssetDatabase.GetAssetPath(source), visualPath, StringComparison.Ordinal);
                if (isCanonicalVisual)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                else if (child.name == "Visual")
                {
                    throw new InvalidOperationException($"Unrecognized Visual child; refusing overwrite: {visualPath}");
                }
            }
        }

        private static void RemoveHeldOnlyComponents(GameObject root)
        {
            DestroyComponents(root.GetComponentsInChildren<BatteryThrownImpact>(true));
            DestroyComponents(root.GetComponentsInChildren<ThrownItemImpact>(true));
            DestroyComponents(root.GetComponentsInChildren<NetworkItemPhysicsAuthority>(true));
            DestroyComponents(root.GetComponentsInChildren<NetworkUtilityItemDurabilityState>(true));
            DestroyComponents(root.GetComponentsInChildren<ItemGravityReceiver>(true));
            DestroyComponents(root.GetComponentsInChildren<RigidbodyGrappleTarget>(true));
            DestroyComponents(root.GetComponentsInChildren<NetworkTransform>(true));
            DestroyComponents(root.GetComponentsInChildren<NetworkObject>(true));
            DestroyComponents(root.GetComponentsInChildren<Collider>(true));
            DestroyComponents(root.GetComponentsInChildren<Rigidbody>(true));
        }

        private static void DestroyComponents<T>(T[] components) where T : Component
        {
            foreach (T component in components)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static void ValidateHeldAndDropped(ICollection<string> failures)
        {
            foreach (WrapperSpec spec in WrapperSpecs)
            {
                ValidateWrapper(spec.DroppedPath, spec.DroppedGuid, spec.VisualPath,
                    spec.DroppedPosition, spec.DroppedEuler, spec.DroppedScale, false, failures);
                ValidateWrapper(spec.HeldPath, spec.HeldGuid, spec.VisualPath,
                    spec.HeldPosition, spec.HeldEuler, spec.HeldScale, true, failures);
            }
        }

        private static void ValidateWrapper(
            string wrapperPath,
            string expectedGuid,
            string visualPath,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale,
            bool isHeld,
            ICollection<string> failures)
        {
            if (!string.Equals(AssetDatabase.AssetPathToGUID(wrapperPath), expectedGuid, StringComparison.Ordinal))
            {
                failures.Add($"Wrapper GUID mismatch: {wrapperPath}");
                return;
            }

            GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
            if (wrapper == null)
            {
                failures.Add($"Missing wrapper: {wrapperPath}");
                return;
            }

            Transform visual = wrapper.transform.Find("Visual");
            int directVisualCount = 0;
            for (int index = 0; index < wrapper.transform.childCount; index++)
            {
                if (wrapper.transform.GetChild(index).name == "Visual")
                {
                    directVisualCount++;
                }
            }

            UnityEngine.Object visualSource = visual == null
                ? null
                : PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject);
            if (visual == null || directVisualCount != 1 || visual.parent != wrapper.transform ||
                !string.Equals(AssetDatabase.GetAssetPath(visualSource), visualPath, StringComparison.Ordinal))
            {
                failures.Add($"Canonical nested Visual mismatch: {wrapperPath}");
                return;
            }

            if ((visual.localPosition - localPosition).sqrMagnitude > 0.0001f ||
                Quaternion.Angle(visual.localRotation, Quaternion.Euler(localEuler)) > 0.01f ||
                (visual.localScale - localScale).sqrMagnitude > 0.0001f)
            {
                failures.Add($"Visual pose mismatch: {wrapperPath}");
            }

            foreach (Renderer renderer in wrapper.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.transform.IsChildOf(visual) && renderer.enabled)
                {
                    failures.Add($"Enabled legacy renderer remains: {wrapperPath}/{renderer.name}");
                }
            }

            int rigidbodies = wrapper.GetComponentsInChildren<Rigidbody>(true).Length;
            int colliders = wrapper.GetComponentsInChildren<Collider>(true).Length;
            int networkObjects = wrapper.GetComponentsInChildren<NetworkObject>(true).Length;
            int networkTransforms = wrapper.GetComponentsInChildren<NetworkTransform>(true).Length;
            int thrownImpacts = wrapper.GetComponentsInChildren<BatteryThrownImpact>(true).Length +
                wrapper.GetComponentsInChildren<ThrownItemImpact>(true).Length;

            if (wrapper.GetComponents<UtilityItemObject>().Length != 1 ||
                wrapper.GetComponents<ShopUpgradeUsableItem>().Length != 1)
            {
                failures.Add($"Root item function contract mismatch: {wrapperPath}");
            }

            if (isHeld)
            {
                if (rigidbodies != 0 || colliders != 0 || networkObjects != 0 || networkTransforms != 0 || thrownImpacts != 0)
                {
                    failures.Add($"Held component contract mismatch: {wrapperPath}");
                }
            }
            else if (rigidbodies != 1 || colliders != 1 || networkObjects != 1 || networkTransforms != 1)
            {
                failures.Add($"Dropped component contract mismatch: {wrapperPath}");
            }
        }

        private static bool HasDirectChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null && child.parent == parent;
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }
    }
}
