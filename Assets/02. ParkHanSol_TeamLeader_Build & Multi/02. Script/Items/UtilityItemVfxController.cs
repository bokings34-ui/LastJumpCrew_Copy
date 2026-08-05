using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    public sealed class UtilityItemVfxController : MonoBehaviour, IUtilityItemVfx
    {
        [Serializable]
        private sealed class EffectChannel
        {
            [SerializeField] private ParticleSystem[] particleSystems =
                Array.Empty<ParticleSystem>();
            [SerializeField] private AudioSource[] audioSources =
                Array.Empty<AudioSource>();

            public bool HasReference => HasParticleReference()
                || HasAudioReference();

            public void Restart()
            {
                Stop(true);

                for (var index = 0; index < particleSystems.Length; index++)
                {
                    var particleSystem = particleSystems[index];
                    if (particleSystem != null)
                    {
                        particleSystem.Play(true);
                    }
                }

                for (var index = 0; index < audioSources.Length; index++)
                {
                    var audioSource = audioSources[index];
                    if (audioSource != null)
                    {
                        audioSource.Play();
                    }
                }
            }

            public void Stop(bool clearParticles)
            {
                var stopBehavior = clearParticles
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting;

                for (var index = 0; index < particleSystems.Length; index++)
                {
                    var particleSystem = particleSystems[index];
                    if (particleSystem != null)
                    {
                        particleSystem.Stop(true, stopBehavior);
                    }
                }

                for (var index = 0; index < audioSources.Length; index++)
                {
                    var audioSource = audioSources[index];
                    if (audioSource != null)
                    {
                        audioSource.Stop();
                    }
                }
            }

            private bool HasParticleReference()
            {
                for (var index = 0; index < particleSystems.Length; index++)
                {
                    if (particleSystems[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool HasAudioReference()
            {
                for (var index = 0; index < audioSources.Length; index++)
                {
                    if (audioSources[index] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Header("Inspector References")]
        [SerializeField] private EffectChannel use = new();
        [SerializeField] private EffectChannel loop = new();
        [SerializeField] private EffectChannel impact = new();

        public bool IsLoopPlaying { get; private set; }
        public bool HasAnyEffect => use.HasReference
            || loop.HasReference
            || impact.HasReference;

        private void OnDisable()
        {
            StopAll();
        }

        public void PlayUse()
        {
            use.Restart();
        }

        public void BeginLoop()
        {
            if (IsLoopPlaying)
            {
                return;
            }

            loop.Restart();
            IsLoopPlaying = true;
        }

        public void EndLoop()
        {
            loop.Stop(false);
            IsLoopPlaying = false;
        }

        public void PlayImpact()
        {
            impact.Restart();
        }

        public void StopAll()
        {
            use.Stop(true);
            loop.Stop(true);
            impact.Stop(true);
            IsLoopPlaying = false;
        }
    }
}
