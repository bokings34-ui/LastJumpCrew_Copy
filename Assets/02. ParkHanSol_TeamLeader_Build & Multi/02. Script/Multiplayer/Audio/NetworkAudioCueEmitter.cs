using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class NetworkAudioCueEmitter :
        MonoBehaviour,
        IPositionedNetworkAudioCuePlayer
    {
        private const int DefaultPositionedVoiceLimit = 3;

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

        private sealed class PositionedVoice
        {
            public PositionedVoice(GameObject root, AudioSource source)
            {
                Root = root;
                Source = source;
            }

            public GameObject Root { get; }
            public AudioSource Source { get; }
            public ulong PlayOrder { get; set; }
        }

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private CueBinding[] cueBindings = Array.Empty<CueBinding>();
        [SerializeField, Min(1)]
        private int positionedVoiceLimit = DefaultPositionedVoiceLimit;

        private readonly Dictionary<NetworkAudioCue, CueBinding> bindingsByCue =
            new();
        private readonly Dictionary<NetworkAudioCue, float> lastPlayedAtByCue =
            new();
        private readonly List<PositionedVoice> positionedVoices = new();
        private GameObject positionedVoiceRoot;
        private ulong positionedVoicePlayOrder;
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
            if (!TryBeginPlayback(cue, out var binding, out failureReason))
            {
                return false;
            }

            audioSource.PlayOneShot(binding.Clip, binding.VolumeScale);
            return true;
        }

        public bool TryPlayAt(
            NetworkAudioCue cue,
            Vector3 position,
            out string failureReason)
        {
            if (!IsFinite(position))
            {
                return Fail(cue, "position_invalid", out failureReason);
            }

            if (!TryBeginPlayback(cue, out var binding, out failureReason))
            {
                return false;
            }

            EnsurePositionedVoicePool();
            var voice = SelectPositionedVoice();
            voice.Source.Stop();
            CopyPlaybackSettings(audioSource, voice.Source);
            voice.Root.transform.position = position;
            voice.PlayOrder = ++positionedVoicePlayOrder;
            voice.Source.PlayOneShot(binding.Clip, binding.VolumeScale);
            return true;
        }

        private void EnsurePositionedVoicePool()
        {
            if (positionedVoices.Count > 0)
            {
                return;
            }

            positionedVoiceRoot = new GameObject(
                $"PHS_PositionedAudioPool_{name}");
            var ownerScene = gameObject.scene;
            if (ownerScene.IsValid() && ownerScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(
                    positionedVoiceRoot,
                    ownerScene);
            }

            var voiceLimit = Mathf.Max(1, positionedVoiceLimit);
            positionedVoices.Capacity = voiceLimit;
            for (var index = 0; index < voiceLimit; index++)
            {
                var voiceObject = new GameObject(
                    $"PHS_PositionedVoice_{index + 1}");
                voiceObject.transform.SetParent(
                    positionedVoiceRoot.transform,
                    false);
                var voiceSource = voiceObject.AddComponent<AudioSource>();
                CopyPlaybackSettings(audioSource, voiceSource);
                positionedVoices.Add(
                    new PositionedVoice(voiceObject, voiceSource));
            }
        }

        private PositionedVoice SelectPositionedVoice()
        {
            var oldest = positionedVoices[0];
            foreach (var voice in positionedVoices)
            {
                if (!voice.Source.isPlaying)
                {
                    return voice;
                }

                if (voice.PlayOrder < oldest.PlayOrder)
                {
                    oldest = voice;
                }
            }

            return oldest;
        }

        private void OnDestroy()
        {
            foreach (var voice in positionedVoices)
            {
                if (voice.Source != null)
                {
                    voice.Source.Stop();
                }
            }

            positionedVoices.Clear();
            if (positionedVoiceRoot != null)
            {
                Destroy(positionedVoiceRoot);
                positionedVoiceRoot = null;
            }
        }

        private bool TryBeginPlayback(
            NetworkAudioCue cue,
            out CueBinding binding,
            out string failureReason)
        {
            EnsureBindingsBuilt();
            binding = null;
            if (audioSource == null)
            {
                return Fail(cue, "audio_source_missing", out failureReason);
            }

            if (!audioSource.isActiveAndEnabled)
            {
                return Fail(cue, "audio_source_inactive", out failureReason);
            }

            if (!bindingsByCue.TryGetValue(cue, out binding)
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
            failureReason = null;
            return true;
        }

        private static void CopyPlaybackSettings(
            AudioSource source,
            AudioSource destination)
        {
            destination.playOnAwake = false;
            destination.loop = false;
            destination.outputAudioMixerGroup = source.outputAudioMixerGroup;
            destination.mute = source.mute;
            destination.bypassEffects = source.bypassEffects;
            destination.bypassListenerEffects = source.bypassListenerEffects;
            destination.bypassReverbZones = source.bypassReverbZones;
            destination.priority = source.priority;
            destination.volume = source.volume;
            destination.pitch = source.pitch;
            destination.panStereo = source.panStereo;
            destination.spatialBlend = source.spatialBlend;
            destination.reverbZoneMix = source.reverbZoneMix;
            destination.dopplerLevel = source.dopplerLevel;
            destination.spread = source.spread;
            destination.rolloffMode = source.rolloffMode;
            destination.minDistance = source.minDistance;
            destination.maxDistance = source.maxDistance;
            destination.spatialize = source.spatialize;
            destination.spatializePostEffects = source.spatializePostEffects;
            destination.ignoreListenerPause = source.ignoreListenerPause;
            destination.ignoreListenerVolume = source.ignoreListenerVolume;
        }

        private static bool IsFinite(Vector3 position)
        {
            return IsFinite(position.x)
                && IsFinite(position.y)
                && IsFinite(position.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
