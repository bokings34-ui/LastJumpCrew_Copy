using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHS0723PartyCreditsHudAuthoring
    {
        private const string PlayHudPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab";
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string BinderObjectName = "PHS_PartyCreditsHudBinder";
        private static readonly Dictionary<string, string> WalletPathsByScene = new(StringComparer.Ordinal);

        [MenuItem("Tools/ParkHanSol/0723/Repair Party Credits HUD Wiring")]
        public static void Repair()
        {
            PreflightScene(MapScenePath);
            PreflightScene(ShopScenePath);
            RemovePrefabOwnedBinders();
            AuthorSceneBinder(MapScenePath);
            AuthorSceneBinder(ShopScenePath);
            Validate();
            Debug.Log("PHS_PARTY_CREDITS_HUD_AUTHORING_OK prefabBinders=0 mapBinders=1 shopBinders=1");
        }

        [MenuItem("Tools/ParkHanSol/0723/Validate Party Credits HUD Wiring")]
        public static void Validate()
        {
            ValidatePrefab();
            ValidateScene(MapScenePath);
            ValidateScene(ShopScenePath);
            Debug.Log("PHS_PARTY_CREDITS_HUD_VALIDATION_OK prefabBinders=0 mapBinders=1 shopBinders=1");
        }

        private static void PreflightScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RequireSingleSceneComponent<ParkHanSolPlayHudMockPresenter>(scene, scenePath);
            var referencedWallets = FindSceneComponents<PartyCreditsHudBinder>(scene)
                .Select(binder => new SerializedObject(binder).FindProperty("shopWalletSource")?.objectReferenceValue)
                .OfType<MonoBehaviour>()
                .Where(component => component is IShopWallet)
                .Distinct()
                .ToArray();
            if (referencedWallets.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=referenced_wallet_count path={scenePath} actual={referencedWallets.Length}");
            }

            WalletPathsByScene[scenePath] = GetHierarchyPath(referencedWallets[0].transform);
        }

        private static void RemovePrefabOwnedBinders()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayHudPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=prefab_missing path={PlayHudPrefabPath}");
            }

            try
            {
                var binders = root.GetComponentsInChildren<PartyCreditsHudBinder>(true);
                if (binders.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=unexpected_prefab_binder_count actual={binders.Length}");
                }

                foreach (var binder in binders)
                {
                    UnityEngine.Object.DestroyImmediate(binder);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayHudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AuthorSceneBinder(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var presenter = RequireSingleSceneComponent<ParkHanSolPlayHudMockPresenter>(scene, scenePath);
            var wallet = RequireWalletAtCapturedPath(scene, scenePath);

            foreach (var binder in FindSceneComponents<PartyCreditsHudBinder>(scene))
            {
                UnityEngine.Object.DestroyImmediate(binder);
            }

            var binderObject = new GameObject(BinderObjectName);
            SceneManager.MoveGameObjectToScene(binderObject, scene);
            var authoredBinder = binderObject.AddComponent<PartyCreditsHudBinder>();
            var serializedBinder = new SerializedObject(authoredBinder);
            serializedBinder.FindProperty("playHudPresenter").objectReferenceValue = presenter;
            serializedBinder.FindProperty("shopWalletSource").objectReferenceValue = wallet;
            serializedBinder.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=scene_save_failed path={scenePath}");
            }
        }

        private static void ValidatePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayHudPrefabPath);
            if (root == null)
            {
                throw new InvalidOperationException($"PHS_PARTY_CREDITS_HUD_VALIDATION_FAILED reason=prefab_missing path={PlayHudPrefabPath}");
            }

            try
            {
                var count = root.GetComponentsInChildren<PartyCreditsHudBinder>(true).Length;
                if (count != 0)
                {
                    throw new InvalidOperationException(
                        $"PHS_PARTY_CREDITS_HUD_VALIDATION_FAILED reason=prefab_binder_count actual={count}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var presenter = RequireSingleSceneComponent<ParkHanSolPlayHudMockPresenter>(scene, scenePath);
            var binders = FindSceneComponents<PartyCreditsHudBinder>(scene);
            if (binders.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_VALIDATION_FAILED reason=scene_binder_count path={scenePath} actual={binders.Length}");
            }

            var serializedBinder = new SerializedObject(binders[0]);
            var presenterReference = serializedBinder.FindProperty("playHudPresenter")?.objectReferenceValue;
            var walletReference = serializedBinder.FindProperty("shopWalletSource")?.objectReferenceValue;
            if (presenterReference != presenter || walletReference is not MonoBehaviour wallet || wallet is not IShopWallet)
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_VALIDATION_FAILED reason=scene_reference_mismatch path={scenePath}");
            }
        }

        private static T RequireSingleSceneComponent<T>(Scene scene, string scenePath) where T : Component
        {
            var matches = FindSceneComponents<T>(scene);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=component_count type={typeof(T).Name} path={scenePath} actual={matches.Length}");
            }

            return matches[0];
        }

        private static MonoBehaviour RequireWalletAtCapturedPath(Scene scene, string scenePath)
        {
            if (!WalletPathsByScene.TryGetValue(scenePath, out var expectedPath))
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=wallet_path_not_captured path={scenePath}");
            }

            var matches = FindSceneComponents<MonoBehaviour>(scene)
                .Where(component => component is IShopWallet)
                .Where(component => string.Equals(GetHierarchyPath(component.transform), expectedPath, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_PARTY_CREDITS_HUD_AUTHORING_FAILED reason=captured_wallet_count path={scenePath} " +
                    $"expected={expectedPath} actual={matches.Length}");
            }

            return matches[0];
        }

        private static T[] FindSceneComponents<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static string GetHierarchyPath(Transform target)
        {
            var path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }
}
