#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHS0723PlayerReactionAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private static readonly string[] PlayerPrefabPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };
        private const string ElectricShockPrefabPath =
            "Assets/MasterMagicFX/ParticlesVer3/Lightnings/" +
            "LightningRing/Prefabs/Par_LightningRing.prefab";
        private const string ElectricShockAudioPath =
            LastJumpCrew.ParkHanSol.Editor.PHSCuratedAssetSfxAuthoring.BatteryShockPath;
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

            var electricShockAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(
                ElectricShockAudioPath);
            if (electricShockAudio == null)
            {
                throw new InvalidOperationException(
                    "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                    "reason=electric_shock_audio_missing");
            }

            foreach (var playerPrefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayer(
                    playerPrefabPath,
                    electricShockPrefab,
                    electricShockAudio);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"PHS_PLAYER_REACTION_AUTHORING_OK prefabs={PlayerPrefabPaths.Length} " +
                "statusReceiver=true knockbackReceiver=true shockAudio3D=true");
        }

        private static void ConfigurePlayer(
            string playerPrefabPath,
            GameObject electricShockPrefab,
            AudioClip electricShockAudio)
        {
            var root = PrefabUtility.LoadPrefabContents(playerPrefabPath);
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

                var audioSources = effectRoot
                    .GetComponentsInChildren<AudioSource>(true);
                if (audioSources.Length > 1)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                        $"reason=electric_shock_audio_duplicate path={playerPrefabPath}");
                }

                var audioSource = audioSources.Length == 1
                    ? audioSources[0]
                    : effectRoot.gameObject.AddComponent<AudioSource>();
                if (audioSource.gameObject != effectRoot.gameObject)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                        $"reason=electric_shock_audio_owner_invalid path={playerPrefabPath}");
                }
                audioSource.enabled = true;
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.clip = electricShockAudio;
                audioSource.volume = 0.65f;
                audioSource.spatialBlend = 1f;
                audioSource.dopplerLevel = 0f;
                audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 15f;

                var serializedStatus = new SerializedObject(statusController);
                serializedStatus.FindProperty("electricShockEffectRoot")
                    .objectReferenceValue = effectRoot.gameObject;
                serializedStatus.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(statusController);
                EditorUtility.SetDirty(effectRoot.gameObject);
                EditorUtility.SetDirty(audioSource);
                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    playerPrefabPath,
                    out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "PHS_PLAYER_REACTION_AUTHORING_FAILED " +
                        "reason=player_prefab_save_failed");
                }

            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
#endif
