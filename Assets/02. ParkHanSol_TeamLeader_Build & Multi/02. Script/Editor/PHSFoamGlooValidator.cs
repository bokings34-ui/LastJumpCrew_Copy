using System;
using System.Collections.Generic;
using System.IO;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSFoamGlooValidator
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
        private const string FoamBlobPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Items/Foam/PHS_NetworkFoamBlob.prefab";
        private const string CoordinatorSourcePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Items/PHSNetworkFoamCoordinator.cs";

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Foam GLOO P0")]
        public static void Validate()
        {
            var errors = new List<string>();
            ValidateBlob(errors);
            ValidateRunRoot(errors);
            ValidatePlayer(PlayerPrefabPath, errors);
            ValidatePlayer(TutorialPlayerPrefabPath, errors);
            ValidateItemData(errors);
            ValidateFoamItemPrefabs(errors);
            ValidateNetworkPrefabRegistration(errors);
            ValidateDependencies(errors);
            ValidateSourceTransitions(errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Debug.LogError($"PHS_FOAM_GLOO_VALIDATION_FAILED {error}");
                }

                throw new InvalidOperationException(
                    $"PHS_FOAM_GLOO_VALIDATION_FAILED count={errors.Count}");
            }

            Debug.Log(
                "PHS_FOAM_GLOO_VALIDATION_PASS players=2 coordinator=1 network_prefab=1 dropped_durability=1 held_durability=0 thresholds=4/6/3 transitions=3 markers=3 hold=2.00 dissolve=0.45 assets06=0");
        }

        private static void ValidateBlob(List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamBlobPrefabPath);
            if (prefab == null)
            {
                errors.Add($"reason=blob_prefab_missing path={FoamBlobPrefabPath}");
                return;
            }

            RequireCount<NetworkObject>(prefab, FoamBlobPrefabPath, 1, errors);
            var blobs = prefab.GetComponents<PHSNetworkFoamBlob>();
            if (blobs.Length != 1)
            {
                errors.Add(
                    $"reason=blob_component_count path={FoamBlobPrefabPath} count={blobs.Length}");
            }
            else if (!blobs[0].HasRequiredReferences
                || blobs[0].VisualRoot == null
                || blobs[0].FlightTrail == null
                || (blobs[0].AttachedScale
                    - new Vector3(0.22f, 0.12f, 0.22f)).sqrMagnitude
                    > 0.000001f
                || !Approximately(blobs[0].HardenSeconds, 0.18f))
            {
                errors.Add("reason=blob_reference_or_config_invalid");
            }

            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 0)
            {
                errors.Add($"reason=blob_collider_count count={colliders.Length}");
            }
        }

        private static void ValidateRunRoot(List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                RunRootPrefabPath);
            if (prefab == null)
            {
                errors.Add($"reason=run_root_missing path={RunRootPrefabPath}");
                return;
            }

            var coordinators = prefab.GetComponents<PHSNetworkFoamCoordinator>();
            if (coordinators.Length != 1)
            {
                errors.Add(
                    $"reason=coordinator_count path={RunRootPrefabPath} count={coordinators.Length}");
                return;
            }

            var coordinator = coordinators[0];
            var foamBlob = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamBlobPrefabPath);
            if (!coordinator.HasRequiredReferences
                || coordinator.FoamBlobPrefab != foamBlob
                || coordinator.HitLayers.value != Physics.DefaultRaycastLayers
                || !Approximately(coordinator.ProjectileSpeed, 18f)
                || !Approximately(coordinator.MaximumRange, 8f)
                || !Approximately(coordinator.CollisionRadius, 0.08f)
                || coordinator.MaximumBlobsPerOwner != 20
                || coordinator.MaximumBlobsGlobal != 96
                || !Approximately(coordinator.PendingTargetLifetime, 8f)
                || !Approximately(coordinator.SurfaceLifetime, 20f)
                || !Approximately(coordinator.CompletionHoldSeconds, 2f)
                || !Approximately(coordinator.DissolveSeconds, 0.45f)
                || !Approximately(coordinator.HullCaptureRadius, 0.9f)
                || !Approximately(coordinator.SurfaceClusterRadius, 0.65f)
                || coordinator.ImpactBufferCapacity != 24
                || !coordinator.RejectsSaturatedImpactCasts)
            {
                errors.Add("reason=coordinator_reference_or_config_invalid");
            }

            if (PHSNetworkFoamCoordinator.FireBlobThreshold != 4
                || PHSNetworkFoamCoordinator.HullBreachBlobThreshold != 6
                || PHSNetworkFoamCoordinator.SurfaceBlobThreshold != 3)
            {
                errors.Add("reason=threshold_contract_invalid");
            }
        }

        private static void ValidatePlayer(string path, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                errors.Add($"reason=player_prefab_missing path={path}");
                return;
            }

            var controllers = prefab.GetComponents<PHSNetworkFoamGunController>();
            if (controllers.Length != 1)
            {
                errors.Add(
                    $"reason=gun_controller_count path={path} count={controllers.Length}");
                return;
            }

            var controller = controllers[0];
            var itemRecords = prefab.GetComponents<NetworkPlayerItemRecord>();
            var lifeStates = prefab.GetComponents<NetworkPlayerLifeState>();
            var actions = prefab.GetComponents<PHSNetworkItemUseActionController>();
            var feedbacks = prefab.GetComponents<
                PHSNetworkItemUseFeedbackController>();
            var cameras = prefab.GetComponentsInChildren<Camera>(true);
            if (!controller.HasRequiredReferences
                || itemRecords.Length != 1
                || lifeStates.Length != 1
                || actions.Length != 1
                || feedbacks.Length != 1
                || cameras.Length != 1
                || controller.OwnerAimCamera == null
                || controller.OwnerAimCamera != cameras[0]
                || controller.ItemRecord != itemRecords[0]
                || controller.LifeState != lifeStates[0]
                || controller.ActionController != actions[0]
                || controller.FeedbackController != feedbacks[0]
                || controller.ServerOrigin == null
                || controller.ServerOrigin.parent != prefab.transform
                || controller.ServerOrigin.name != "PHS_FoamServerOrigin"
                || !Approximately(controller.FireIntervalSeconds, 0.125f)
                || !Approximately(controller.MaximumOriginError, 1.25f)
                || !Approximately(controller.MaximumYawError, 35f)
                || !Approximately(controller.MaximumPitch, 80f)
                || !Approximately(controller.TelegraphIntervalSeconds, 0.5f)
                || !Approximately(controller.TelegraphRadius, 0.12f)
                || !Approximately(controller.TelegraphDistance, 8f))
            {
                errors.Add($"reason=gun_reference_or_config_invalid path={path}");
            }
        }

        private static void ValidateItemData(List<string> errors)
        {
            var data = AssetDatabase.LoadAssetAtPath<UtilityItemPrefabData>(
                FoamItemDataPath);
            if (data == null)
            {
                errors.Add($"reason=item_data_missing path={FoamItemDataPath}");
                return;
            }

            if (data.ItemId != PHSNetworkFoamCoordinator.FoamItemId
                || !data.HasDurability
                || data.MaxDurability != 100
                || data.UpgradeEffect != UtilityItemUpgradeEffect.None
                || data.ActionProfiles.Count != 2
                || !HasExactProfile(
                    data,
                    UtilityItemActionKind.FireSuppression,
                    200,
                    1)
                || !HasExactProfile(
                    data,
                    UtilityItemActionKind.HullBreachRepair,
                    100,
                    1))
            {
                errors.Add("reason=item_data_contract_invalid");
            }
        }

        private static void ValidateFoamItemPrefabs(List<string> errors)
        {
            var itemData = AssetDatabase.LoadAssetAtPath<UtilityItemPrefabData>(
                FoamItemDataPath);
            var dropped = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamDroppedPrefabPath);
            if (dropped == null)
            {
                errors.Add(
                    $"reason=dropped_prefab_missing path={FoamDroppedPrefabPath}");
            }
            else
            {
                RequireCount<UtilityItemObject>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<NetworkObject>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<Rigidbody>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<NetworkTransform>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<NetworkItemPhysicsAuthority>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<ThrownItemImpact>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);
                RequireCount<NetworkUtilityItemDurabilityState>(
                    dropped,
                    FoamDroppedPrefabPath,
                    1,
                    errors);

                var itemObjects = dropped.GetComponents<UtilityItemObject>();
                var durabilityStates = dropped.GetComponents<
                    NetworkUtilityItemDurabilityState>();
                if (itemObjects.Length == 1
                    && itemObjects[0].ItemPrefabData != itemData)
                {
                    errors.Add(
                        "reason=dropped_item_data_reference_invalid");
                }

                if (itemObjects.Length == 1 && durabilityStates.Length == 1)
                {
                    var serialized = new SerializedObject(durabilityStates[0]);
                    var itemObjectProperty = serialized.FindProperty(
                        "itemObject");
                    if (itemObjectProperty == null
                        || itemObjectProperty.objectReferenceValue
                            != itemObjects[0])
                    {
                        errors.Add(
                            "reason=dropped_durability_item_reference_invalid");
                    }
                }
            }

            var held = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamHeldPrefabPath);
            if (held == null)
            {
                errors.Add(
                    $"reason=held_prefab_missing path={FoamHeldPrefabPath}");
                return;
            }

            RequireCount<NetworkUtilityItemDurabilityState>(
                held,
                FoamHeldPrefabPath,
                0,
                errors);
        }

        private static void ValidateNetworkPrefabRegistration(
            List<string> errors)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
                ActiveNetworkPrefabsPath);
            var foamBlob = AssetDatabase.LoadAssetAtPath<GameObject>(
                FoamBlobPrefabPath);
            if (list == null || foamBlob == null)
            {
                errors.Add("reason=network_prefab_assets_missing");
                return;
            }

            var count = 0;
            foreach (var entry in list.PrefabList)
            {
                if (entry != null && entry.Prefab == foamBlob)
                {
                    count++;
                    if (entry.Override != NetworkPrefabOverride.None)
                    {
                        errors.Add("reason=network_prefab_override_invalid");
                    }
                }
            }

            if (count != 1)
            {
                errors.Add($"reason=network_prefab_registration_count count={count}");
            }

        }

        private static void ValidateDependencies(List<string> errors)
        {
            foreach (var dependency in AssetDatabase.GetDependencies(
                FoamBlobPrefabPath,
                true))
            {
                if (dependency.StartsWith(
                    "Assets/06",
                    StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"reason=forbidden_dependency dependency={dependency}");
                }
            }
        }

        private static void ValidateSourceTransitions(List<string> errors)
        {
            if (!File.Exists(CoordinatorSourcePath))
            {
                errors.Add(
                    $"reason=coordinator_source_missing path={CoordinatorSourcePath}");
                return;
            }

            var source = File.ReadAllText(CoordinatorSourcePath);
            RequireSourceSequence(
                source,
                "surface_hardened",
                errors,
                "NetworkFoamTargetKind.Surface,",
                "SurfaceBlobThreshold,");
            RequireSourceSequence(
                source,
                "surface_hardened",
                errors,
                "var wasHardened = accumulator.State",
                "accumulator.State = NetworkFoamTargetState.Hardened;",
                "HardenAccumulator(accumulator, now, now + surfaceLifetime);",
                "PublishAccumulator(accumulator);",
                "if (!wasHardened)",
                "PHS_FOAM_TARGET_HARDENED kind=Surface target={accumulator.Key} blobs={accumulator.Current}/{accumulator.Required}");
            ValidateCompletionSourceTransition(
                source,
                "fire_completed",
                "NetworkFoamTargetKind.Fire,",
                "FireBlobThreshold,",
                errors);
            ValidateCompletionSourceTransition(
                source,
                "hull_breach_completed",
                "NetworkFoamTargetKind.HullBreach,",
                "HullBreachBlobThreshold,",
                errors);
        }

        private static void ValidateCompletionSourceTransition(
            string source,
            string contract,
            string kindToken,
            string thresholdToken,
            List<string> errors)
        {
            RequireSourceSequence(
                source,
                contract,
                errors,
                kindToken,
                thresholdToken);
            RequireSourceSequence(
                source,
                contract,
                errors,
                "accumulator.State = NetworkFoamTargetState.Completed;",
                "HardenAccumulator(accumulator, now, accumulator.RemoveAt);",
                "PublishAccumulator(accumulator);",
                "PublishCompletionFeedback(shooter, accumulator);",
                "PHS_FOAM_TARGET_COMPLETED kind={accumulator.Kind} target={accumulator.Key} blobs={accumulator.Current}/{accumulator.Required}");
        }

        private static void RequireSourceSequence(
            string source,
            string contract,
            List<string> errors,
            params string[] tokens)
        {
            var searchIndex = 0;
            foreach (var token in tokens)
            {
                var tokenIndex = source.IndexOf(
                    token,
                    searchIndex,
                    StringComparison.Ordinal);
                if (tokenIndex < 0)
                {
                    errors.Add(
                        $"reason=source_transition_invalid contract={contract} token={token}");
                    return;
                }

                searchIndex = tokenIndex + token.Length;
            }
        }

        private static bool HasExactProfile(
            UtilityItemPrefabData data,
            UtilityItemActionKind kind,
            int amount,
            int durabilityCost)
        {
            return data.TryGetActionProfile(kind, out var profile)
                && profile.Amount == amount
                && profile.DurabilityCost == durabilityCost;
        }

        private static void RequireCount<T>(
            GameObject root,
            string path,
            int expected,
            List<string> errors)
            where T : Component
        {
            var count = root.GetComponents<T>().Length;
            if (count != expected)
            {
                errors.Add(
                    $"reason=component_count path={path} component={typeof(T).Name} expected={expected} actual={count}");
            }
        }

        private static bool Approximately(float first, float second)
        {
            return Mathf.Abs(first - second) <= 0.0001f;
        }
    }
}
