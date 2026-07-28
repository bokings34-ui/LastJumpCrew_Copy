using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class NetworkAudioCueEmitter :
        MonoBehaviour,
        INetworkAudioCuePlayer
    {
        [Serializable]
        private sealed class CueBinding
        {
            [SerializeField] private NetworkAudioCue cue;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;
            [SerializeField, Min(0f)] private float cooldownSeconds = 0.08f;

            public NetworkAudioCue Cue => cue;
            public AudioClip Clip => clip;
            public float VolumeScale => volumeScale;
            public float CooldownSeconds => cooldownSeconds;
        }

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private CueBinding[] cueBindings = Array.Empty<CueBinding>();

        private readonly Dictionary<NetworkAudioCue, CueBinding> bindingsByCue =
            new();
        private readonly Dictionary<NetworkAudioCue, float> lastPlayedAtByCue =
            new();
        private bool bindingsBuilt;

        public bool HasRequiredReferences
        {
            get
            {
                EnsureBindingsBuilt();
                return audioSource != null
                    && bindingsByCue.Count > 0
                    && cueBindings != null
                    && Array.TrueForAll(
                        cueBindings,
                        binding => binding != null && binding.Clip != null);
            }
        }

        private void Awake()
        {
            EnsureBindingsBuilt();
            if (audioSource == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_AUDIO_SETUP_FAILED reason=audio_source_missing emitter={name}",
                    this);
            }
        }

        public bool TryPlay(
            NetworkAudioCue cue,
            out string failureReason)
        {
            EnsureBindingsBuilt();
            if (audioSource == null)
            {
                return Fail(cue, "audio_source_missing", out failureReason);
            }

            if (!audioSource.isActiveAndEnabled)
            {
                return Fail(cue, "audio_source_inactive", out failureReason);
            }

            if (!bindingsByCue.TryGetValue(cue, out var binding)
                || binding == null)
            {
                return Fail(cue, "cue_binding_missing", out failureReason);
            }

            if (binding.Clip == null)
            {
                return Fail(cue, "clip_missing", out failureReason);
            }

            var now = Time.unscaledTime;
            if (lastPlayedAtByCue.TryGetValue(cue, out var lastPlayedAt)
                && now - lastPlayedAt < binding.CooldownSeconds)
            {
                failureReason = "cue_cooldown";
                return false;
            }

            lastPlayedAtByCue[cue] = now;
            audioSource.PlayOneShot(binding.Clip, binding.VolumeScale);
            failureReason = null;
            return true;
        }

        private void EnsureBindingsBuilt()
        {
            if (bindingsBuilt)
            {
                return;
            }

            bindingsBuilt = true;
            bindingsByCue.Clear();
            if (cueBindings == null)
            {
                return;
            }

            foreach (var binding in cueBindings)
            {
                if (binding == null)
                {
                    continue;
                }

                if (!bindingsByCue.TryAdd(binding.Cue, binding))
                {
                    Debug.LogError(
                        $"PHS_NETWORK_AUDIO_SETUP_FAILED reason=cue_duplicate emitter={name} cue={binding.Cue}",
                        this);
                }
            }
        }

        private bool Fail(
            NetworkAudioCue cue,
            string reason,
            out string failureReason)
        {
            failureReason = reason;
            Debug.LogError(
                $"PHS_NETWORK_AUDIO_CUE_FAILED reason={reason} emitter={name} cue={cue}",
                this);
            return false;
        }
    }
}
