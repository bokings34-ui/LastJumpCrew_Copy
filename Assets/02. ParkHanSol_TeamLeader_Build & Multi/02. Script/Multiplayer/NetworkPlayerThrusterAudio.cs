using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class NetworkPlayerThrusterAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip loopClip;
        [SerializeField, Range(0f, 1f)] private float maximumVolume = 0.65f;
        [SerializeField, Min(0.1f)] private float attackSpeed = 12f;
        [SerializeField, Min(0.1f)] private float releaseSpeed = 2.5f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.45f;
        [SerializeField, Min(0.01f)] private float minimumDistance = 0.5f;
        [SerializeField, Min(0.01f)] private float maximumDistance = 14f;

        private bool isReady;

        private void Awake()
        {
            if (audioSource == null || loopClip == null)
            {
                Debug.LogError(
                    $"PHS_THRUSTER_AUDIO_SETUP_FAILED reason=audio_reference_missing player={transform.root.name}",
                    this);
                return;
            }

            if (maximumDistance <= minimumDistance)
            {
                Debug.LogError(
                    $"PHS_THRUSTER_AUDIO_SETUP_FAILED reason=invalid_distance player={transform.root.name} min={minimumDistance} max={maximumDistance}",
                    this);
                return;
            }

            audioSource.clip = loopClip;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = spatialBlend;
            audioSource.dopplerLevel = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = minimumDistance;
            audioSource.maxDistance = maximumDistance;
            audioSource.volume = 0f;
            isReady = true;
        }

        private void OnDisable()
        {
            StopImmediate();
        }

        public void SetThrusterActive(bool isActive, float deltaTime)
        {
            if (!isReady)
            {
                return;
            }

            if (isActive && !audioSource.isPlaying)
            {
                audioSource.volume = 0f;
                audioSource.Play();
            }

            var targetVolume = isActive ? maximumVolume : 0f;
            var fadeSpeed = isActive ? attackSpeed : releaseSpeed;
            audioSource.volume = Mathf.MoveTowards(
                audioSource.volume,
                targetVolume,
                fadeSpeed * Mathf.Max(0f, deltaTime));

            if (!isActive
                && audioSource.isPlaying
                && audioSource.volume <= 0.001f)
            {
                audioSource.Stop();
            }
        }

        public void StopImmediate()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.Stop();
            audioSource.volume = 0f;
        }
    }
}
