using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.External
{
    [DisallowMultipleComponent]
    public sealed class PHSExternalSolarFlarePresentationView :
        MonoBehaviour,
        IExternalEventPresentationView
    {
        [Header("Presentation Sockets")]
        [SerializeField] private GameObject telegraphSocket;
        [SerializeField] private GameObject activeSocket;
        [SerializeField] private GameObject resolveSocket;
        [SerializeField] private GameObject failSocket;
        [SerializeField] private Transform cleanupRoot;

        [Header("Phase Audio")]
        [SerializeField] private AudioSource telegraphAudioSource;
        [SerializeField] private AudioSource activeAudioSource;
        [SerializeField] private AudioSource resolveAudioSource;
        [SerializeField] private AudioSource failAudioSource;

        public PHSExternalEventPresentationPhase CurrentPhase { get; private set; }

        public bool HasCompleteWiring =>
            telegraphSocket != null
            && activeSocket != null
            && resolveSocket != null
            && failSocket != null
            && cleanupRoot != null
            && telegraphAudioSource != null
            && activeAudioSource != null
            && resolveAudioSource != null
            && failAudioSource != null
            && !telegraphAudioSource.loop
            && activeAudioSource.loop
            && !resolveAudioSource.loop
            && !failAudioSource.loop;

        private void Awake()
        {
            Cleanup();
        }

        private void OnDisable()
        {
            SetSocketState(null);
            StopAllAudio();
            ClearRuntimeChildren();
            CurrentPhase = PHSExternalEventPresentationPhase.Cleanup;
        }

        public void ShowTelegraph(
            float phaseElapsedSeconds,
            bool allowOneShotAudio)
        {
            ApplyOneShotPhase(
                PHSExternalEventPresentationPhase.Telegraph,
                telegraphAudioSource,
                phaseElapsedSeconds,
                allowOneShotAudio);
        }

        public void ShowActive(float phaseElapsedSeconds)
        {
            SetSocketState(PHSExternalEventPresentationPhase.Active);
            StopAllAudio();
            PlayLoopAligned(activeAudioSource, phaseElapsedSeconds);
            CurrentPhase = PHSExternalEventPresentationPhase.Active;
        }

        public float ShowResolved(
            float phaseElapsedSeconds,
            bool allowOneShotAudio)
        {
            return ApplyOneShotPhase(
                PHSExternalEventPresentationPhase.Resolve,
                resolveAudioSource,
                phaseElapsedSeconds,
                allowOneShotAudio);
        }

        public float ShowFailed(
            float phaseElapsedSeconds,
            bool allowOneShotAudio)
        {
            return ApplyOneShotPhase(
                PHSExternalEventPresentationPhase.Fail,
                failAudioSource,
                phaseElapsedSeconds,
                allowOneShotAudio);
        }

        private float ApplyOneShotPhase(
            PHSExternalEventPresentationPhase phase,
            AudioSource phaseAudioSource,
            float phaseElapsedSeconds,
            bool allowOneShotAudio)
        {
            SetSocketState(phase);
            StopAllAudio();
            var remainingAudioSeconds = allowOneShotAudio
                ? PlayOneShotFromElapsed(
                    phaseAudioSource,
                    phaseElapsedSeconds)
                : 0f;
            CurrentPhase = phase;
            return remainingAudioSeconds;
        }

        private static void PlayLoopAligned(
            AudioSource source,
            float phaseElapsedSeconds)
        {
            if (source == null || source.clip == null || !source.loop)
            {
                return;
            }

            var clipLength = source.clip.length;
            var playbackSpeed = Mathf.Abs(source.pitch);
            if (clipLength <= 0f || playbackSpeed <= 0.0001f)
            {
                return;
            }

            var elapsed = SanitizeElapsed(phaseElapsedSeconds);
            source.time = Mathf.Repeat(elapsed * playbackSpeed, clipLength);
            source.Play();
        }

        private static float PlayOneShotFromElapsed(
            AudioSource source,
            float phaseElapsedSeconds)
        {
            if (source == null || source.clip == null || source.loop)
            {
                return 0f;
            }

            var clipLength = source.clip.length;
            var playbackSpeed = Mathf.Abs(source.pitch);
            if (clipLength <= 0f || playbackSpeed <= 0.0001f)
            {
                return 0f;
            }

            var elapsed = SanitizeElapsed(phaseElapsedSeconds);
            var clipElapsed = elapsed * playbackSpeed;
            if (clipElapsed >= clipLength)
            {
                return 0f;
            }

            source.time = clipElapsed;
            source.Play();
            return (clipLength - clipElapsed) / playbackSpeed;
        }

        private static float SanitizeElapsed(float elapsed)
        {
            return float.IsNaN(elapsed) || float.IsInfinity(elapsed)
                ? 0f
                : Mathf.Max(0f, elapsed);
        }

        public void Cleanup()
        {
            SetSocketState(null);
            StopAllAudio();
            ClearRuntimeChildren();
            CurrentPhase = PHSExternalEventPresentationPhase.Cleanup;
        }

        private void SetSocketState(
            PHSExternalEventPresentationPhase? phase)
        {
            SetActive(
                telegraphSocket,
                phase == PHSExternalEventPresentationPhase.Telegraph);
            SetActive(
                activeSocket,
                phase == PHSExternalEventPresentationPhase.Active);
            SetActive(
                resolveSocket,
                phase == PHSExternalEventPresentationPhase.Resolve);
            SetActive(
                failSocket,
                phase == PHSExternalEventPresentationPhase.Fail);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void StopAllAudio()
        {
            StopIfPresent(telegraphAudioSource);
            StopIfPresent(activeAudioSource);
            StopIfPresent(resolveAudioSource);
            StopIfPresent(failAudioSource);
        }

        private static void StopIfPresent(AudioSource source)
        {
            if (source != null)
            {
                source.Stop();
            }
        }

        private void ClearRuntimeChildren()
        {
            if (cleanupRoot == null)
            {
                return;
            }

            for (var index = cleanupRoot.childCount - 1; index >= 0; index--)
            {
                var child = cleanupRoot.GetChild(index).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
            }
        }
    }
}
