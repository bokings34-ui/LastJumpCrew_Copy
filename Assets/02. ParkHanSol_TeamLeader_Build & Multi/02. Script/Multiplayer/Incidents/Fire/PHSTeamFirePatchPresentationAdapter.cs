using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSTeamFirePatchPresentationAdapter :
        MonoBehaviour
    {
        private static readonly int VerticalCutId =
            Shader.PropertyToID("_Verticalcut");
        private static readonly int TurbulenceSpeedId =
            Shader.PropertyToID("_TurbulenceSpeed");

        [SerializeField] private MeshRenderer fireRenderer;
        [SerializeField] private Material fireMaterial;
        [SerializeField] private Shader fireShader;
        [SerializeField]
        private ParticleSystem[] fireParticles =
            Array.Empty<ParticleSystem>();
        [SerializeField] private Light presentationLight;
        [SerializeField] private AudioSource fireAudio;

        private MaterialPropertyBlock propertyBlock;

        private MaterialPropertyBlock PropertyBlock =>
            propertyBlock ??= new MaterialPropertyBlock();

        private void OnEnable()
        {
            ResetPresentation();
        }

        private void OnDisable()
        {
            ResetPresentation();
        }

        public void ApplyState(
            PHSFireIntensity intensity,
            bool allowAudio)
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_TEAM_FIRE_PATCH_PRESENTATION_FAILED " +
                    $"reason={reason}",
                    this);
                return;
            }

            if (intensity == PHSFireIntensity.None)
            {
                ResetPresentation();
                return;
            }

            fireRenderer.enabled = true;
            fireRenderer.GetPropertyBlock(PropertyBlock);
            PropertyBlock.SetFloat(VerticalCutId, 0f);
            PropertyBlock.SetFloat(
                TurbulenceSpeedId,
                GetTurbulenceSpeed(intensity));
            fireRenderer.SetPropertyBlock(PropertyBlock);
            SetParticlesPlaying(true);

            // PHSFirePatchRuntimeTarget owns the one gameplay light per patch.
            // The team prefab light remains a required controller reference,
            // but must not multiply once per visual socket and spread bridge.
            presentationLight.enabled = false;
            presentationLight.intensity = 0f;
            if (allowAudio)
            {
                fireAudio.volume = GetAudioVolume(intensity);
                if (!fireAudio.isPlaying)
                {
                    fireAudio.Play();
                }
            }
            else
            {
                fireAudio.Stop();
                fireAudio.volume = 0f;
            }

        }

        public bool TryValidate(out string reason)
        {
            if (fireRenderer == null)
            {
                reason = "renderer_missing";
                return false;
            }

            if (fireMaterial == null)
            {
                reason = "material_missing";
                return false;
            }

            if (fireShader == null)
            {
                reason = "shader_missing";
                return false;
            }

            if (fireRenderer.sharedMaterial != fireMaterial)
            {
                reason = "renderer_material_mismatch";
                return false;
            }

            if (fireMaterial.shader != fireShader)
            {
                reason = "material_shader_mismatch";
                return false;
            }

            if (!fireMaterial.HasProperty(VerticalCutId)
                || !fireMaterial.HasProperty(TurbulenceSpeedId))
            {
                reason = "shader_contract_missing";
                return false;
            }

            if (fireAudio == null)
            {
                reason = "audio_missing";
                return false;
            }

            if (presentationLight == null)
            {
                reason = "light_missing";
                return false;
            }

            if (fireAudio.clip == null)
            {
                reason = "audio_clip_missing";
                return false;
            }

            foreach (var fireParticle in fireParticles)
            {
                if (fireParticle == null)
                {
                    reason = "particle_reference_missing";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private void ResetPresentation()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_TEAM_FIRE_PATCH_PRESENTATION_FAILED " +
                    $"reason={reason}",
                    this);
                return;
            }

            fireRenderer.enabled = false;
            fireRenderer.GetPropertyBlock(PropertyBlock);
            PropertyBlock.SetFloat(VerticalCutId, 1f);
            PropertyBlock.SetFloat(TurbulenceSpeedId, 1f);
            fireRenderer.SetPropertyBlock(PropertyBlock);
            SetParticlesPlaying(false);
            presentationLight.enabled = false;
            presentationLight.intensity = 0f;
            fireAudio.Stop();
            fireAudio.volume = 0f;
        }

        private static float GetTurbulenceSpeed(
            PHSFireIntensity intensity)
        {
            return intensity switch
            {
                PHSFireIntensity.Small => 0.5f,
                PHSFireIntensity.Medium => 1f,
                PHSFireIntensity.Large => 1.8f,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(intensity),
                    intensity,
                    "Unsupported fire intensity.")
            };
        }

        private static float GetAudioVolume(
            PHSFireIntensity intensity)
        {
            return intensity switch
            {
                PHSFireIntensity.Small => 0.12f,
                PHSFireIntensity.Medium => 0.2f,
                PHSFireIntensity.Large => 0.3f,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(intensity),
                    intensity,
                    "Unsupported fire intensity.")
            };
        }

        private void SetParticlesPlaying(bool shouldPlay)
        {
            foreach (var fireParticle in fireParticles)
            {
                if (shouldPlay)
                {
                    if (!fireParticle.isPlaying)
                    {
                        fireParticle.Play(true);
                    }
                }
                else
                {
                    fireParticle.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
