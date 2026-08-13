#if UNITY_EDITOR
using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSPlayerDamageFeedbackAuthoring
    {
        private const string Root =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private static readonly string[] PlayerPrefabPaths =
        {
            Root + "/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab",
            Root + "/03. Prefab/Tutorial/PHS_NetworkTutorialPlayer.prefab"
        };
        private const string HitEffectPrefabPath =
            "Assets/MasterMagicFX/ParticlesVer3/Slashes/BloodSlashes/Prefabs/Par_BloodSlashes_Hit.prefab";
        private const string HitAudioPath =
            "Assets/04. NohSeokMin_Game Event/99_Resource/Sound_Enemy_Attack.mp3";
        private const string FeedbackRootName = "PHS_PlayerDamageFeedback";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Player Damage Feedback")]
        public static void Author()
        {
            var sourceEffect = AssetDatabase.LoadAssetAtPath<GameObject>(
                HitEffectPrefabPath)
                ?? throw new InvalidOperationException(
                    "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=effect_prefab_missing");
            var hitAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(HitAudioPath)
                ?? throw new InvalidOperationException(
                    "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=hit_audio_missing");

            foreach (var playerPrefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayer(playerPrefabPath, sourceEffect, hitAudio);
            }

            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_OK prefabs=2 effect=BloodSlashes audio=EnemyAttack");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Player Damage Feedback")]
        public static void Validate()
        {
            foreach (var playerPrefabPath in PlayerPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(playerPrefabPath);
                try
                {
                    var feedback = root.GetComponent<NetworkPlayerDamageFeedback>();
                    var feedbackRoot = root.transform.Find(FeedbackRootName);
                    if (feedback == null || feedbackRoot == null
                        || feedbackRoot.GetComponentInChildren<ParticleSystem>(true) == null
                        || feedbackRoot.GetComponent<AudioSource>() is not { clip: not null }
                        || root.GetComponent<NetworkPlayerLifeState>() == null
                        || root.GetComponent<NetworkPlayerSquishyVisualFeedback>() == null)
                    {
                        throw new InvalidOperationException(
                            $"PHS_PLAYER_DAMAGE_FEEDBACK_VALIDATE_FAILED path={playerPrefabPath}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log("PHS_PLAYER_DAMAGE_FEEDBACK_VALIDATED prefabs=2");
        }

        private static void ConfigurePlayer(
            string playerPrefabPath,
            GameObject sourceEffect,
            AudioClip hitAudio)
        {
            var root = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            try
            {
                var lifeState = root.GetComponent<NetworkPlayerLifeState>()
                    ?? throw new InvalidOperationException(
                        "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=life_state_missing");
                var squishy = root.GetComponent<NetworkPlayerSquishyVisualFeedback>()
                    ?? throw new InvalidOperationException(
                        "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=squishy_missing");
                var feedback = root.GetComponent<NetworkPlayerDamageFeedback>()
                    ?? root.AddComponent<NetworkPlayerDamageFeedback>();
                var feedbackRoot = root.transform.Find(FeedbackRootName);
                if (feedbackRoot == null)
                {
                    feedbackRoot = new GameObject(FeedbackRootName).transform;
                    feedbackRoot.SetParent(root.transform, false);
                }

                feedbackRoot.localPosition = new Vector3(0f, 1f, 0f);
                feedbackRoot.localRotation = Quaternion.identity;
                feedbackRoot.localScale = Vector3.one * 0.45f;
                var effect = feedbackRoot.GetComponentInChildren<ParticleSystem>(true);
                if (effect == null)
                {
                    var instance = PrefabUtility.InstantiatePrefab(
                        sourceEffect,
                        feedbackRoot) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException(
                            "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=effect_instantiate_failed");
                    }

                    instance.name = "PHS_PlayerDamageHitEffect";
                    effect = instance.GetComponentInChildren<ParticleSystem>(true)
                        ?? throw new InvalidOperationException(
                            "PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=particle_missing");
                }

                AudioSource audioSource = null;
                foreach (var candidate in feedbackRoot
                             .GetComponents<AudioSource>())
                {
                    if (candidate != null)
                    {
                        audioSource = candidate;
                        break;
                    }
                }
                if (audioSource == null)
                {
                    audioSource = feedbackRoot.gameObject.AddComponent<AudioSource>();
                }
                foreach (var particle in feedbackRoot
                             .GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particle.main;
                    main.playOnAwake = false;
                    particle.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.clip = hitAudio;
                audioSource.volume = 0.7f;
                audioSource.spatialBlend = 1f;
                audioSource.dopplerLevel = 0f;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 14f;

                var serializedFeedback = new SerializedObject(feedback);
                serializedFeedback.FindProperty("lifeState").objectReferenceValue = lifeState;
                serializedFeedback.FindProperty("squishyFeedback").objectReferenceValue = squishy;
                serializedFeedback.FindProperty("hitEffect").objectReferenceValue = effect;
                serializedFeedback.FindProperty("hitAudio").objectReferenceValue = audioSource;
                serializedFeedback.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, playerPrefabPath) == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_PLAYER_DAMAGE_FEEDBACK_AUTHOR_FAILED reason=save_failed path={playerPrefabPath}");
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
