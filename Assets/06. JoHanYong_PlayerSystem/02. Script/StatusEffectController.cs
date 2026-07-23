using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.Common
{
    [DisallowMultipleComponent] //플레이어 몬스터가 공통으로 사용하는 상태이상 관리자 
    //상태이상 적용+종료+재적용 담당
    public sealed class StatusEffectController : NetworkBehaviour, IStatusEffectReceiver
    {
        [Header("Electric Shock Effect")]
        [SerializeField]
        private GameObject electricShockEffectRoot;
        private ParticleSystem[] electricShockParticles;
        private AudioSource[] electricShockAudioSources;
        private Light[] electricShockLights;
        public event Action<StatusEffectType> OnStatusEffectStarted; //상태이상이 처음 시작 때 호출

        public event Action<StatusEffectType> OnStatusEffectEnded; //상태이상 종료 때 호출

        private readonly Dictionary<StatusEffectType, Coroutine> activeRoutines = new();

        private readonly HashSet<StatusEffectType> activeEffects = new();

        public bool IsShocked => IsStatusEffectActive(StatusEffectType.ElectricShok);

        private void Awake()
        {
            CacheElectricShockEffects();
        }
        public bool CanReceiveStatusEffect(StatusEffectType effectType)
        {
            if (!isActiveAndEnabled)
            {
                return false;  
            }
            return effectType != StatusEffectType.None;
        }
        //상태이상 적용 중인 확인
        public bool IsStatusEffectActive(StatusEffectType effectType)
        {
            return activeEffects.Contains(effectType);
        }
        //상태이상 적용 -> 이미 적용중이면 지속시간 새로 갱신
        public void ApplyStatusEffect(StatusEffectType effectType, float duration, GameObject source)
        {
            if (!CanReceiveStatusEffect(effectType))
            {
                return;
            }
            if(duration <= 0f)
            {
                return ;
            }
            bool wasAlreadyActive = activeEffects.Contains(effectType);

            //같은 상태싱이 이미 적용 중이면 기존 종료 코루틴 멈춤
            if(activeRoutines.TryGetValue(effectType, out Coroutine currentRoutine))
            {
                if(currentRoutine != null)
                {
                    StopCoroutine(currentRoutine);
                }
                activeRoutines.Remove(effectType); 
            }
            activeEffects.Add(effectType);

            if (!wasAlreadyActive)
            {
                OnStatusEffectStarted?.Invoke(effectType);

                if(IsSpawned && IsServer)
                {
                    PlayStatusEffectClientRpc(effectType);
                }
                else if (effectType == StatusEffectType.ElectricShok)
                {
                    PlayElectricShockEffectLocal();
                }
            }
            Coroutine newRoutine = StartCoroutine(StatusEffectRoutine(effectType, duration));

            activeRoutines[effectType] = newRoutine;

            Debug.Log($"PHS_STATUS_EFFECT_APPLIED " + $"target={name} " + $"effect={effectType} " + $"duration={duration:F2} " + $"source={(source != null ? source.name : "null")} " + $"refreshed={wasAlreadyActive}");
        }
        //지정한 상태이상을 즉시 제거합니다.
        public void RemoveStatusEffect(StatusEffectType effectType) 
        {
            if (!activeEffects.Contains(effectType))
            {
                return; 
            }
            if(activeRoutines.TryGetValue(effectType, out Coroutine routine))
            {
                if(routine != null)
                {
                    StopCoroutine(routine);
                }
                activeRoutines.Remove(effectType);
            }
            FinishStatusEffect(effectType);
            
        }
        //지정된 시간 후 상태이상을 종료합니다.
        private IEnumerator StatusEffectRoutine(StatusEffectType effectType, float duration)
        {
            yield return new WaitForSeconds(duration);

            activeRoutines.Remove(effectType) ;

            FinishStatusEffect(effectType);
        }
        //상태이상 데이터 제거하고 종료 이벤트 호출 
        private void FinishStatusEffect(StatusEffectType effectType)
        {
            if (!activeEffects.Remove(effectType))
            {
                return;
            }
            OnStatusEffectEnded?.Invoke(effectType);
            if(IsSpawned && IsServer)
            {
                StopStatusEffectClientRpc(effectType);
            }
            else if(effectType == StatusEffectType.ElectricShok)
            {
                StopElectricShockEffectLocal();
            }
            Debug.Log($"PHS_STATUS_EFFECT_ENDED " + $"target={name} " + $"effect={effectType}");

                Debug.Log($"PHS_STATUS_EFFECT_ENDED " + $"target={name} " + $"effect={effectType}");
        }
        //오브젝트 비활성화 시 상태이상 초기화 + 풀링된 몬스터 감전 유지 X 
        private void CacheElectricShockEffects()
        {
            if(electricShockEffectRoot == null)
            {
                electricShockParticles = System.Array.Empty<ParticleSystem>();

                electricShockAudioSources = System.Array.Empty<AudioSource>();

                electricShockLights = System.Array.Empty<Light>();

                return;
            }
            electricShockParticles = electricShockEffectRoot.GetComponentsInChildren <ParticleSystem>(true);
            electricShockAudioSources = electricShockEffectRoot.GetComponentsInChildren<AudioSource>(true);
            electricShockLights = electricShockEffectRoot.GetComponentsInChildren <Light>(true);

            electricShockEffectRoot.SetActive(false);

            Debug.Log($"PHS_ELECTRIC_EFFECT_CACHED " + $"target={name} " + $"particles={electricShockParticles.Length} " + $"audios={electricShockAudioSources.Length} " + $"lights={electricShockLights.Length}");


        }
        private void PlayElectricShockEffectLocal()
        {
            if(electricShockEffectRoot == null)
            {
                return ;
            }
            electricShockEffectRoot.SetActive (true);

            foreach(ParticleSystem particle in electricShockParticles)
            {
                if(particle == null)
                {
                    continue;
                }
                particle.Play(true);
            }
            foreach (AudioSource audioSource in electricShockAudioSources)
            {
                if(audioSource == null)
                {
                    continue;
                }
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
            foreach (Light effectLight in electricShockLights)
            {
                if(effectLight != null)
                {
                    effectLight.enabled = true;
                }
            }
        }
        private void StopElectricShockEffectLocal()
        {
            foreach (ParticleSystem particle in electricShockParticles)
            {
                if(particle == null)
                {
                    continue;
                }
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            foreach (AudioSource audioSource in electricShockAudioSources)
            {
                if (audioSource != null)

using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

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
            if(electricShockEffectRoot != null)
            {
                electricShockEffectRoot.SetActive(false);
            }
        }
        [ClientRpc]
        private void PlayStatusEffectClientRpc(StatusEffectType effectType)
        {
            if (effectType == StatusEffectType.ElectricShok)
            {
                PlayElectricShockEffectLocal();
            }
        }
        [ClientRpc]
        private void StopStatusEffectClientRpc(StatusEffectType effectType)
        {
            if(effectType == StatusEffectType.ElectricShok)
            {
                StopElectricShockEffectLocal();
            }
        }
        private void OnDisable()
        {
            foreach(Coroutine routine in activeRoutines.Values)
            {
                if(routine != null)
                {
                    StopCoroutine(routine);
                }
            }
            activeEffects.Clear();
            activeRoutines.Clear();

            StopElectricShockEffectLocal();


            foreach (var effectLight in electricShockLights)
            {
                if (effectLight != null)
                {
                    effectLight.enabled = active;
                }
            }

        }
    }
}
