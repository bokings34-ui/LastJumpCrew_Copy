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
            && failAudioSource != null;

        private void Awake()
        {
            Cleanup();
        }

        private void OnDisable()
        {
            SetSocketState(null);
            StopAllAudio();
            CurrentPhase = PHSExternalEventPresentationPhase.Cleanup;
        }

        public void ShowTelegraph()
        {
            ApplyPhase(
                PHSExternalEventPresentationPhase.Telegraph,
                telegraphAudioSource);
        }

        public void ShowActive()
        {
            ApplyPhase(
                PHSExternalEventPresentationPhase.Active,
                activeAudioSource);
        }

        public void ShowResolved()
        {
            ApplyPhase(
                PHSExternalEventPresentationPhase.Resolve,
                resolveAudioSource);
        }

        public void ShowFailed()
        {
            ApplyPhase(
                PHSExternalEventPresentationPhase.Fail,
                failAudioSource);
        }

        public void Cleanup()
        {
            SetSocketState(null);
            StopAllAudio();
            ClearRuntimeChildren();
            CurrentPhase = PHSExternalEventPresentationPhase.Cleanup;
        }

        private void ApplyPhase(
            PHSExternalEventPresentationPhase phase,
            AudioSource phaseAudioSource)
        {
            SetSocketState(phase);
            StopAllAudio();
            PlayIfAssigned(phaseAudioSource);
            CurrentPhase = phase;
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

        private static void PlayIfAssigned(AudioSource source)
        {
            if (source != null && source.clip != null)
            {
                source.Play();
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
