#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHS0723PlayerReactionAuthoring
    {
        private const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/" +
            "03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";
        private const string ElectricShockPrefabPath =
            "Assets/MasterMagicFX/ParticlesVer3/Lightnings/" +
            "LightningRing/Prefabs/Par_LightningRing.prefab";
        private const string ElectricShockRootName =
            "PHS_ElectricShockEffectRoot";

        [MenuItem("Tools/ParkHanSol/Author 0723 Player Reactions")]
        public static void AuthorPlayerReactions()
        {
            var electricShockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ElectricShockPrefabPath);
            if (electricShockPrefab == null)
            {
                throw new InvalidOperationException(
                    "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                    "reason=electric_shock_prefab_missing");
            }

            if (electricShockPrefab
                    .GetComponentsInChildren<Collider>(true).Length > 0)
            {
                throw new InvalidOperationException(
                    "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                    "reason=electric_shock_collider_present");
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var knockbackReceiver =
                    root.GetComponent<NetworkPlayerKnockbackReceiver>();
                if (knockbackReceiver == null)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                        "reason=knockback_receiver_missing");
                }

                var statusController =
                    root.GetComponent<StatusEffectController>();
                if (statusController == null)
                {
                    statusController =
                        root.AddComponent<StatusEffectController>();
                }

                var effectRoot = root.transform.Find(ElectricShockRootName);
                if (effectRoot == null)
                {
                    var instance = PrefabUtility.InstantiatePrefab(
                        electricShockPrefab,
                        root.transform) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                            "reason=electric_shock_instantiate_failed");
                    }

                    instance.name = ElectricShockRootName;
                    effectRoot = instance.transform;
                }

                effectRoot.localPosition = new Vector3(0f, 1f, 0f);
                effectRoot.localRotation = Quaternion.identity;
                effectRoot.localScale = Vector3.one * 0.35f;
                effectRoot.gameObject.SetActive(false);

                var serializedStatus = new SerializedObject(statusController);
                serializedStatus.FindProperty("electricShockEffectRoot")
                    .objectReferenceValue = effectRoot.gameObject;
                serializedStatus.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(statusController);
                EditorUtility.SetDirty(effectRoot.gameObject);
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    PlayerPrefabPath,
                    out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                        "reason=player_prefab_save_failed");
                }

                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"PHS_PLAYER_REACTION_AUTHORING_OK " +
                    $"prefab={PlayerPrefabPath} " +
                    $"statusReceiver=true knockbackReceiver=true");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
