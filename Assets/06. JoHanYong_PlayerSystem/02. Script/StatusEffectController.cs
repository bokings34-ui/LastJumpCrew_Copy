using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class StatusEffectController :
        NetworkBehaviour,
        IStatusEffectReceiver
    {
        [Header("Electric Shock Presentation")]
        [SerializeField] private GameObject electricShockEffectRoot;

        private readonly NetworkVariable<bool> electricShockActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ParticleSystem[] electricShockParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] electricShockAudioSources =
            Array.Empty<AudioSource>();
        private Light[] electricShockLights = Array.Empty<Light>();
        private Coroutine electricShockRoutine;

        public bool IsShocked => electricShockActive.Value;

        public event Action<StatusEffectType> StatusEffectStarted;
        public event Action<StatusEffectType> StatusEffectEnded;

        private void Awake()
        {
            if (electricShockEffectRoot == null)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_SETUP_FAILED " +
                    $"reason=electric_effect_root_missing player={name}",
                    this);
                return;
            }

            electricShockParticles = electricShockEffectRoot
                .GetComponentsInChildren<ParticleSystem>(true);
            electricShockAudioSources = electricShockEffectRoot
                .GetComponentsInChildren<AudioSource>(true);
            electricShockLights = electricShockEffectRoot
                .GetComponentsInChildren<Light>(true);
            ApplyElectricShockPresentation(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            electricShockActive.OnValueChanged +=
                HandleElectricShockStateChanged;
            ApplyElectricShockPresentation(electricShockActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            electricShockActive.OnValueChanged -=
                HandleElectricShockStateChanged;
            StopElectricShockRoutine();
            ApplyElectricShockPresentation(false);
            base.OnNetworkDespawn();
        }

        public bool CanReceiveStatusEffect(StatusEffectType effectType)
        {
            return isActiveAndEnabled
                && electricShockEffectRoot != null
                && effectType == StatusEffectType.ElectricShok
                && (!IsSpawned || IsServer);
        }

        public void ApplyStatusEffect(
            StatusEffectType effectType,
            float duration,
            GameObject source)
        {
            if (!CanReceiveStatusEffect(effectType) || duration <= 0f)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_APPLY_FAILED effect={effectType} " +
                    $"duration={duration:F2} player={name} server={IsServer}",
                    this);
                return;
            }

            var refreshed = electricShockActive.Value;
            StopElectricShockRoutine();
            if (IsSpawned)
            {
                electricShockActive.Value = true;
            }
            else
            {
                ApplyElectricShockPresentation(true);
            }

            if (!refreshed)
            {
                StatusEffectStarted?.Invoke(effectType);
            }

            electricShockRoutine = StartCoroutine(
                RemoveElectricShockAfter(duration));
            Debug.Log(
                $"PHS_STATUS_EFFECT_APPLIED target={name} " +
                $"effect={effectType} duration={duration:F2} " +
                $"source={(source != null ? source.name : "null")} " +
                $"refreshed={refreshed}",
                this);
        }

        public void RemoveStatusEffect(StatusEffectType effectType)
        {
            if (effectType != StatusEffectType.ElectricShok)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_REMOVE_FAILED " +
                    $"reason=unsupported effect={effectType}",
                    this);
                return;
            }

            if (IsSpawned && !IsServer)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_REMOVE_FAILED " +
                    $"reason=server_required player={name}",
                    this);
                return;
            }

            FinishElectricShock();
        }

        private IEnumerator RemoveElectricShockAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            electricShockRoutine = null;
            FinishElectricShock();
        }

        private void FinishElectricShock()
        {
            StopElectricShockRoutine();
            var wasActive = IsSpawned
                ? electricShockActive.Value
                : electricShockEffectRoot != null
                    && electricShockEffectRoot.activeSelf;
            if (!wasActive)
            {
                return;
            }

            if (IsSpawned)
            {
                electricShockActive.Value = false;
            }
            else
            {
                ApplyElectricShockPresentation(false);
            }

            StatusEffectEnded?.Invoke(StatusEffectType.ElectricShok);
            Debug.Log(
                $"PHS_STATUS_EFFECT_ENDED target={name} " +
                $"effect={StatusEffectType.ElectricShok}",
                this);
        }

        private void StopElectricShockRoutine()
        {
            if (electricShockRoutine == null)
            {
                return;
            }

            StopCoroutine(electricShockRoutine);
            electricShockRoutine = null;
        }

        private void HandleElectricShockStateChanged(
            bool previous,
            bool current)
        {
            ApplyElectricShockPresentation(current);
        }

        private void ApplyElectricShockPresentation(bool active)
        {
            if (electricShockEffectRoot == null)
            {
                return;
            }

            electricShockEffectRoot.SetActive(active);
            foreach (var particle in electricShockParticles)
            {
                if (particle == null)
                {
                    continue;
                }

                if (active)
                {
                    particle.Play(true);
                }
                else
                {
                    particle.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            foreach (var audioSource in electricShockAudioSources)
            {
                if (audioSource == null)
                {
                    continue;
                }

                if (active && !audioSource.isPlaying)
                {
                    audioSource.Play();
                }
                else if (!active)

                {
                    audioSource.Stop();
                }
            }

            foreach(Light effectLight in electricShockLights)
            {
                if(effectLight != null)
                {
                    effectLight.enabled = false;
                }
            }
            
        }
        private void OnDisable()
        {
            StopElectricShockRoutine();
            ApplyElectricShockPresentation(false);
        }
    }
}
