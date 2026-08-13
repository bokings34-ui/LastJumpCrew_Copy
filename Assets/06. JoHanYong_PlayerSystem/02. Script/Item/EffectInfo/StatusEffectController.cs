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
        private readonly NetworkVariable<bool> freezeActive = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> slowActive = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private ParticleSystem[] electricShockParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] electricShockAudioSources =
            Array.Empty<AudioSource>();
        private Light[] electricShockLights = Array.Empty<Light>();
        private Coroutine electricShockRoutine;
        private Coroutine freezeRoutine;
        private Coroutine slowRoutine;

        public bool IsShocked => IsSpawned
            ? electricShockActive.Value
            : electricShockEffectRoot != null
                && electricShockEffectRoot.activeSelf;
        // Event enemies are authoritative server simulations and use a separate
        // client mirror for presentation. Their gameplay NetworkObject is not
        // spawned to clients, but the server-side status value must still drive
        // EnemyBase.Tick.
        public bool IsFrozen => freezeActive.Value;
        public bool IsSlowed => slowActive.Value;
        public bool IsMovementBlocked => IsShocked || IsFrozen;
        public float MovementSpeedMultiplier => IsSlowed ? 0.5f : 1f;

        public event Action<StatusEffectType> StatusEffectStarted;
        public event Action<StatusEffectType> StatusEffectEnded;
        public event Action<StatusEffectController, StatusEffectType, bool>
            StatusEffectStateChanged;

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
            freezeActive.OnValueChanged += HandleFreezeStateChanged;
            slowActive.OnValueChanged += HandleSlowStateChanged;
            ApplyElectricShockPresentation(electricShockActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            electricShockActive.OnValueChanged -=
                HandleElectricShockStateChanged;
            freezeActive.OnValueChanged -= HandleFreezeStateChanged;
            slowActive.OnValueChanged -= HandleSlowStateChanged;
            StopElectricShockRoutine();
            StopTimedRoutine(ref freezeRoutine);
            StopTimedRoutine(ref slowRoutine);
            ApplyElectricShockPresentation(false);
            base.OnNetworkDespawn();
        }

        public bool CanReceiveStatusEffect(StatusEffectType effectType)
        {
            return isActiveAndEnabled
                && electricShockEffectRoot != null
                && (effectType == StatusEffectType.ElectricShok
                    || effectType == StatusEffectType.Freeze
                    || effectType == StatusEffectType.Slow)
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

            if (effectType == StatusEffectType.Freeze)
            {
                ApplyTimedEffect(freezeActive, ref freezeRoutine, effectType, duration, source);
                return;
            }
            if (effectType == StatusEffectType.Slow)
            {
                ApplyTimedEffect(slowActive, ref slowRoutine, effectType, duration, source);
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
                StatusEffectStateChanged?.Invoke(this, effectType, true);
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

        public void ApplyStatusEffect(StatusEffectRequest request)
        {
            ApplyStatusEffect(request.EffectType, request.Duration, request.Source);
        }

        public void RemoveStatusEffect(StatusEffectType effectType)
        {
            if (IsSpawned && !IsServer)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_REMOVE_FAILED " +
                    $"reason=server_required player={name}",
                    this);
                return;
            }

            switch (effectType)
            {
                case StatusEffectType.ElectricShok: FinishElectricShock(); break;
                case StatusEffectType.Freeze: FinishTimedEffect(freezeActive, ref freezeRoutine, effectType); break;
                case StatusEffectType.Slow: FinishTimedEffect(slowActive, ref slowRoutine, effectType); break;
            }
        }

        /// <summary>
        /// Clears a pooled actor's transient shock state before it is reused.
        /// The server owns the replicated value; clients only clear stale local presentation
        /// until the authoritative value arrives.
        /// </summary>
        public void ResetElectricShockForReuse()
        {
            StopElectricShockRoutine();

            if (IsSpawned && IsServer)
            {
                electricShockActive.Value = false;
                freezeActive.Value = false;
                slowActive.Value = false;
            }
            else if (!IsSpawned)
            {
                freezeActive.Value = false;
                slowActive.Value = false;
            }

            ApplyElectricShockPresentation(false);
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
            StatusEffectStateChanged?.Invoke(
                this,
                StatusEffectType.ElectricShok,
                false);
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

        private void ApplyTimedEffect(NetworkVariable<bool> state, ref Coroutine routine, StatusEffectType effectType, float duration, GameObject source)
        {
            var refreshed = state.Value;
            StopTimedRoutine(ref routine);
            state.Value = true;
            if (!refreshed)
            {
                StatusEffectStarted?.Invoke(effectType);
                StatusEffectStateChanged?.Invoke(this, effectType, true);
            }
            routine = StartCoroutine(RemoveTimedEffectAfter(state, effectType, duration));
            Debug.Log($"PHS_STATUS_EFFECT_APPLIED target={name} effect={effectType} duration={duration:F2} source={(source != null ? source.name : "null")}", this);
        }

        private IEnumerator RemoveTimedEffectAfter(NetworkVariable<bool> state, StatusEffectType effectType, float duration)
        {
            yield return new WaitForSeconds(duration);
            state.Value = false;
            StatusEffectEnded?.Invoke(effectType);
            StatusEffectStateChanged?.Invoke(this, effectType, false);
        }

        private void FinishTimedEffect(NetworkVariable<bool> state, ref Coroutine routine, StatusEffectType effectType)
        {
            StopTimedRoutine(ref routine);
            if (!state.Value) return;
            state.Value = false;
            StatusEffectEnded?.Invoke(effectType);
            StatusEffectStateChanged?.Invoke(this, effectType, false);
        }

        private void StopTimedRoutine(ref Coroutine routine)
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
        }

        private void HandleFreezeStateChanged(bool previous, bool current) => ApplyElectricShockPresentation(current || IsShocked);
        private void HandleSlowStateChanged(bool previous, bool current) { }

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

            foreach (var effectLight in electricShockLights)
            {
                if (effectLight != null)
                {
                    effectLight.enabled = active;
                }
            }
        }

        private void OnDisable()
        {
            StopElectricShockRoutine();
            StopTimedRoutine(ref freezeRoutine);
            StopTimedRoutine(ref slowRoutine);
            if (IsSpawned && IsServer && electricShockActive.Value)
            {
                electricShockActive.Value = false;
                freezeActive.Value = false;
                slowActive.Value = false;
            }
            ApplyElectricShockPresentation(false);
        }
    }
}
