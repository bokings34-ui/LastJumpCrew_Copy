using System.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    public sealed class PHSAmbientMusicLoop : MonoBehaviour
    {
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioSource primarySource;
        [SerializeField] private AudioSource secondarySource;
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.08f;
        [SerializeField, Min(0.05f)] private float crossfadeSeconds = 1.5f;

        private Coroutine playbackRoutine;

        public bool HasRequiredReferences =>
            musicClip != null
            && primarySource != null
            && secondarySource != null
            && primarySource != secondarySource;

        private void OnEnable()
        {
            if (!HasRequiredReferences || musicClip.length <= crossfadeSeconds)
            {
                return;
            }

            playbackRoutine = StartCoroutine(PlayLoop());
        }

        private void OnDisable()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            StopSource(primarySource);
            StopSource(secondarySource);
        }

        private IEnumerator PlayLoop()
        {
            var current = primarySource;
            var next = secondarySource;

            PrepareSource(current, maximumVolume);
            PrepareSource(next, 0f);
            current.Play();

            while (enabled && gameObject.activeInHierarchy)
            {
                var crossfadeStart = Mathf.Max(0f, musicClip.length - crossfadeSeconds);
                while (current.isPlaying && current.time < crossfadeStart)
                {
                    yield return null;
                }

                PrepareSource(next, 0f);
                next.Play();

                var elapsed = 0f;
                while (elapsed < crossfadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var progress = Mathf.Clamp01(elapsed / crossfadeSeconds);
                    current.volume = maximumVolume * (1f - progress);
                    next.volume = maximumVolume * progress;
                    yield return null;
                }

                current.Stop();
                current.volume = 0f;
                next.volume = maximumVolume;
                (current, next) = (next, current);
            }
        }

        private void PrepareSource(AudioSource source, float volume)
        {
            source.clip = musicClip;
            source.loop = false;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.time = 0f;
        }

        private static void StopSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.volume = 0f;
        }
    }
}
