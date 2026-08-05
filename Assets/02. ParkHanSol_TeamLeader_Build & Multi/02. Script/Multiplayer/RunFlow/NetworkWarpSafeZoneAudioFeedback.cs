using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class NetworkWarpSafeZoneAudioFeedback : NetworkBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip enterClip;
        [SerializeField] private AudioClip exitClip;
        [SerializeField, Range(0f, 1f)] private float enterVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float exitVolume = 0.72f;
        [SerializeField, Min(0.05f)] private float exitFadeSeconds = 0.18f;

        private Coroutine fadeRoutine;
        private bool setupValid;

        public bool HasRequiredReferences =>
            audioSource != null && enterClip != null && exitClip != null;

        private void Awake()
        {
            setupValid = HasRequiredReferences;
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_SETUP_FAILED player={transform.root.name}",
                    this);
            }
        }

        private void OnDisable()
        {
            StopFadeRoutine();
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.volume = 0f;
            }
        }

        public void PlayForOwner(bool isEntering)
        {
            if (!IsServer || !IsSpawned)
            {
                Debug.LogError(
                    $"PHS_WARP_SAFE_ZONE_AUDIO_FAILED reason=server_spawn_required player={name}",
                    this);
                return;
            }

            PlayForOwnerClientRpc(
                isEntering,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { OwnerClientId }
                    }
                });
        }

        [ClientRpc]
        private void PlayForOwnerClientRpc(
            bool isEntering,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner || !setupValid)
            {
                return;
            }

            StopFadeRoutine();
            audioSource.Stop();
            audioSource.clip = isEntering ? enterClip : exitClip;
            audioSource.loop = false;
            audioSource.volume = isEntering ? enterVolume : exitVolume;
            audioSource.Play();

            if (!isEntering)
            {
                fadeRoutine = StartCoroutine(FadeExitAndStop());
            }
        }

        private IEnumerator FadeExitAndStop()
        {
            var elapsed = 0f;
            while (elapsed < exitFadeSeconds && audioSource.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                var remaining = 1f - Mathf.Clamp01(elapsed / exitFadeSeconds);
                audioSource.volume = exitVolume * remaining * remaining;
                yield return null;
            }

            audioSource.Stop();
            audioSource.volume = 0f;
            fadeRoutine = null;
        }

        private void StopFadeRoutine()
        {
            if (fadeRoutine == null)
            {
                return;
            }

            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }
}
