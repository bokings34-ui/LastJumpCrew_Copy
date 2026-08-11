using LastJumpCrew.Common;
using System;
using System.Collections;
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
        [Header("Stun Presentation")]
        [SerializeField]
        private GameObject stunEffectRoot;
        [Header("Frozen Presentation")]
        [SerializeField]
        private GameObject frozenEffectRoot;
        [Header("Slow Presentation")]
        [SerializeField]
        private GameObject slowEffectRoot;

        private readonly NetworkVariable<bool> electricShockActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        //기절
        private readonly NetworkVariable<bool> stunActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        //빙결
        private readonly NetworkVariable<bool> frozenActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        //슬로우
        private readonly NetworkVariable<bool> slowActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        //감전 캐시
        private ParticleSystem[] electricShockParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] electricShockAudioSources =
            Array.Empty<AudioSource>();
        private Light[] electricShockLights = Array.Empty<Light>();
        //기절 캐시
        private ParticleSystem[] stunParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] stunAudioSources =
            Array.Empty<AudioSource>();
        private Light[] stunLights =
            Array.Empty<Light>();
        //빙결 캐시
        private ParticleSystem[] frozenParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] frozenAudioSources =
            Array.Empty<AudioSource>();
        private Light[] frozenLights =
            Array.Empty<Light>();
        //슬로우 캐시
        private ParticleSystem[] slowParticles =
            Array.Empty<ParticleSystem>();
        private AudioSource[] slowAudioSources =
            Array.Empty<AudioSource>();
        private Light[] slowLights =
            Array.Empty<Light>();

        private Coroutine electricShockRoutine;
        private Coroutine stunRoutine;
        private Coroutine frozenRoutine;
        private Coroutine slowRoutine;

        public bool IsShocked => IsSpawned
            ? electricShockActive.Value
            : electricShockEffectRoot != null
                && electricShockEffectRoot.activeSelf;
        public bool IsStunned => IsSpawned
            ? stunActive.Value
            : false;
        public bool IsFrozen => IsSpawned
            ? frozenActive.Value
            : false;
        public bool IsSlowed => IsSpawned
            ? slowActive.Value
            : false;
        public bool IsMovementBlocked => IsShocked || IsStunned || IsFrozen; //빙결 기절 감전 시 움직임 봉쇄
        public bool IsActionBlocked => IsShocked || IsStunned || IsFrozen; //빙결 기절 감전 시 공격 봉쇄
        public float MovementSpeedMultiplier
        {
            get
            {
                if (!IsSlowed)
                {
                    return 1f;
                }
                //currentSlowAmount는 0.3 = 30% 감속 방식 사용
                return Mathf.Clamp01(1f - currentSlowAmount);
            }
        }

        public event Action<StatusEffectType> StatusEffectStarted;
        public event Action<StatusEffectType> StatusEffectEnded;

        private int currentSlowStacks; //현재 슬로우 중첩 수
        private float currentSlowAmount; //현재 적용 중인 슬로우 퍼센트

        private void Awake()
        {
            if (electricShockEffectRoot == null)
            {
                Debug.LogError(
                    $"PHS_STATUS_EFFECT_SETUP_FAILED " +
                    $"reason=electric_effect_root_missing player={name}",
                    this);
            }

            CachePresentation(electricShockEffectRoot, out electricShockParticles, out electricShockAudioSources, out electricShockLights);
            CachePresentation(stunEffectRoot, out stunParticles, out stunAudioSources, out stunLights);
            CachePresentation(frozenEffectRoot, out frozenParticles, out frozenAudioSources, out frozenLights);
            CachePresentation(slowEffectRoot, out slowParticles, out slowAudioSources, out slowLights);

            ApplyElectricShockPresentation(false);
            ApplyStunPresentation(false);
            ApplyFrozenPresentation(false);
            ApplySlowPresentation(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // 감전 상태 변경 감지
            electricShockActive.OnValueChanged +=
                HandleElectricShockStateChanged;
            // 기절 상태 변경 감지
            stunActive.OnValueChanged +=
                HandleStunStateChanged;
            // 빙결 상태 변겨 감지
            frozenActive.OnValueChanged +=
                HandleFrozenStateChanged;
            //슬로우 상태 변경 감지 
            slowActive.OnValueChanged +=
                HandleSlowStateChanged;
            ApplyElectricShockPresentation(electricShockActive.Value);

            ApplyStunPresentation(stunActive.Value);

            ApplyFrozenPresentation(frozenActive.Value);

            ApplySlowPresentation(slowActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            electricShockActive.OnValueChanged -=
                HandleElectricShockStateChanged;
            stunActive.OnValueChanged -=
                HandleStunStateChanged;
            frozenActive.OnValueChanged -=
                HandleFrozenStateChanged;
            slowActive.OnValueChanged -=
                HandleSlowStateChanged;
            // 실행 중인 상태이상 종료 Coroutine 정리
            StopElectricShockRoutine();
            StopStunRoutine();
            StopFrozenRoutine();
            StopSlowRoutine();

            // 화면에 남아있는 상태이상 연출 정리
            ApplyElectricShockPresentation(false);
            ApplyStunPresentation(false);
            ApplyFrozenPresentation(false);
            ApplySlowPresentation(false);

            base.OnNetworkDespawn();
        }
        public bool CanReceiveStatusEffect(StatusEffectType effectType)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }
            if (IsSpawned && !IsServer)
            {
                return false;
            }
            switch (effectType)
            {
                case StatusEffectType.ElectricShok:
                case StatusEffectType.Stun:
                case StatusEffectType.Freeze:
                case StatusEffectType.Slow:
                    return true;

            }
            return false;
        }

        public void ApplyStatusEffect(StatusEffectRequest request)
        {
            if (!CanReceiveStatusEffect(request.EffectType))
            {
                return; //현재 이 상태이상을 받을 수 없는 상태라면 적용하지 않는다.
            }
            if (request.Duration <= 0f)
            {
                return;
            }
            switch (request.EffectType)
            {
                case StatusEffectType.ElectricShok:
                    ApplyElectricShock(request);
                    break;

                case StatusEffectType.Stun:
                    ApplyStun(request);
                    break;

                case StatusEffectType.Freeze:
                    ApplyFreeze(request);
                    break;

                case StatusEffectType.Slow:
                    ApplySlow(request);
                    break;

                default:
                    Debug.LogError($"PHS_STATUS_EFFECT_APPLY_FAILED " + $"reason=unsupported_effect " + $"effect={request.EffectType}", this);
                    break;
            }
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
                case StatusEffectType.ElectricShok:
                    FinishElectricShock();
                    break;
                case StatusEffectType.Stun:
                    FinishStun();
                    break;
                case StatusEffectType.Freeze:
                    FinishFrozen();
                    break;
                case StatusEffectType.Slow:
                    FinishSlow();
                    break;
                default:
                    Debug.LogError($"PHS_STATUS_REMOVE_FAILED" + $"reason = unsupported effect = {effectType}", this);
                    break;
            }
        }

        private IEnumerator RemoveElectricShockAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            electricShockRoutine = null;
            FinishElectricShock();
        }
        private IEnumerator RemoveStunAfter(float duration) //기절 지속시간
        {
            yield return new WaitForSeconds(duration);

            // 현재 실행 중인 코루틴은 종료됐으므로 참조를 비운다.
            stunRoutine = null;

            FinishStun();
        }
        // 빙결 지속시간이 끝나면 빙결 상태를 종료한다.
        private IEnumerator RemoveFrozenAfter(float duration)
        {
            yield return new WaitForSeconds(duration);

            // 현재 실행 중인 코루틴은 종료됐으므로 참조를 비운다.
            frozenRoutine = null;

            FinishFrozen();
        }
        // 슬로우 지속시간이 끝나면 슬로우 상태를 종료한다.
        private IEnumerator RemoveSlowAfter(float duration)
        {
            yield return new WaitForSeconds(duration);

            slowRoutine = null;

            FinishSlow();
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
        private void FinishStun()
        {
            StopStunRoutine();

            // 현재 기절 상태가 아니라면 중복 종료하지 않는다.
            if (!IsStunned)
            {
                return;
            }

            if (IsSpawned)
            {
                // 멀티에서는 서버가 NetworkVariable을 변경한다.
                stunActive.Value = false;
            }
            else
            {
                // 로컬 테스트에서는 Presentation을 직접 끈다.
                ApplyStunPresentation(false);
            }

            // 다른 시스템이 기절 종료를 알 수 있도록 이벤트 전달
            StatusEffectEnded?.Invoke(StatusEffectType.Stun);
         
            Debug.Log($"PHS_STATUS_EFFECT_ENDED " + $"target={name} " + $"effect={StatusEffectType.Stun}", this);
        }
        private void FinishFrozen()
        {
            // 기존 빙결 종료 코루틴 정리
            StopFrozenRoutine();

            // 이미 빙결 상태가 아니라면 아무것도 하지 않는다.
            if (!IsFrozen)
            {
                return;
            }

            if (IsSpawned)
            {
                frozenActive.Value = false;
            }
            else
            {
                ApplyFrozenPresentation(false);
            }

            StatusEffectEnded?.Invoke(StatusEffectType.Freeze);
           
            Debug.Log($"PHS_STATUS_EFFECT_ENDED " + $"target={name} " + $"effect={StatusEffectType.Freeze}", this);
        }
        private void FinishSlow()
        {
            StopSlowRoutine();

            if (!IsSlowed)
            {
                return;
            }
            if (IsSpawned)
            {
                slowActive.Value = false;
            }
            else
            {
                ApplySlowPresentation(false);
            }
            currentSlowStacks = 0;
            currentSlowAmount = 0f;
            StatusEffectEnded?.Invoke(StatusEffectType.Slow);
            Debug.Log($"PHS_STATUS_EFFECT_ENDED " + $"target={name} " + $"effect={StatusEffectType.Slow}", this);
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
        private void StopStunRoutine()
        {
            if (stunRoutine == null)
            {
                return;
            }
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }
        private void StopFrozenRoutine()
        {
            if (frozenRoutine == null)
            {
                return;
            }

            StopCoroutine(frozenRoutine);
            frozenRoutine = null;
        }
        private void StopSlowRoutine()
        {
            if (slowRoutine == null)
            {
                return;
            }

            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }
        private void HandleElectricShockStateChanged(
            bool previous,
            bool current)
        {
            ApplyElectricShockPresentation(current);
        }
        private void HandleStunStateChanged(bool previous, bool current)
        {
            ApplyStunPresentation(current);
        }
        private void HandleFrozenStateChanged(bool previous, bool current)
        {
            ApplyFrozenPresentation(current);
        }
        private void HandleSlowStateChanged(bool previous, bool current)
        {
            ApplySlowPresentation(current);
        }
        private void CachePresentation(GameObject effectRoot, out ParticleSystem[] particles, out AudioSource[] audioSources, out Light[] lights)
        {
            if(effectRoot == null)//이펙트 Root가 없는 상태도 안전하게 처리한다.
            {
                particles = Array.Empty<ParticleSystem>();
                audioSources = Array.Empty<AudioSource>();
                lights = Array.Empty<Light>();
                return;
            }
            particles = effectRoot.GetComponentsInChildren<ParticleSystem>(true);
            audioSources = effectRoot.GetComponentsInChildren<AudioSource>(true);
            lights = effectRoot.GetComponentsInChildren<Light>(true);
        }
        private void ApplyPresentation(GameObject effectRoot, ParticleSystem[] particles, AudioSource[] audioSources, Light[] lights, bool active)
        {
            if(effectRoot == null)
            {
                return;
            }
            //전체 이펙트 오브젝트 활성/비활성화
            effectRoot.SetActive(active);

            //파티클 재생/ 정지
            if(particles != null)
            {
                foreach(var particle in particles)
                {
                    if(particle == null)
                    {
                        continue;
                    }
                    if (active)
                    {
                        particle.Play(true);
                    }
                    else
                    {
                        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
            if(audioSources != null)
            {
                foreach(var audioSource in audioSources)
                {
                    if (audioSource == null)
                    {
                        continue;
                    }
                    if (active)
                    {
                        //Clip이 있고 아직 재생 중이 아닐 때만 재생
                        if(audioSource.clip != null && !audioSource.isPlaying)
                        {
                            audioSource.Play();
                        }
                    }
                    else
                    {
                        audioSource.Stop();
                    }
                }
            }
            if(lights != null) //Light 활성/ 비활성
            {
                foreach(var effectLight in lights)
                {
                    if(effectLight != null)
                    {
                        effectLight.enabled = active;
                    }
                }
            }
        }
        private void ApplyElectricShockPresentation(bool active)
        {
            ApplyPresentation(electricShockEffectRoot, electricShockParticles, electricShockAudioSources, electricShockLights, active);
        }
        private void ApplyFrozenPresentation(bool active)
        {
            ApplyPresentation(frozenEffectRoot, frozenParticles, frozenAudioSources, frozenLights, active);
        }
        private void ApplyStunPresentation(bool active)
        {
            ApplyPresentation(stunEffectRoot, stunParticles, stunAudioSources, stunLights, active);
        }
        private void ApplySlowPresentation(bool active)
        {
            ApplyPresentation(slowEffectRoot, slowParticles, slowAudioSources, slowLights, active);
        }
        private void OnDisable()
        {
            // 실행 중인 상태이상 종료 타이머 정리
            StopElectricShockRoutine();
            StopStunRoutine();
            StopFrozenRoutine();
            StopSlowRoutine();

            // 서버라면 NetworkVariable도 초기화
            if (IsSpawned && IsServer)
            {
                electricShockActive.Value = false;
                stunActive.Value = false;
                frozenActive.Value = false;
                slowActive.Value = false;
            }

            // Slow 계산값 초기화
            currentSlowStacks = 0;
            currentSlowAmount = 0f;

            // 남아 있는 연출 제거
            ApplyElectricShockPresentation(false);
            ApplyStunPresentation(false);
            ApplyFrozenPresentation(false);
            ApplySlowPresentation(false);
        }

        public bool HasStatusEffect(StatusEffectType effectType)
        {
            switch (effectType)
            {
                case StatusEffectType.ElectricShok:
                    return IsShocked;

                case StatusEffectType.Stun:
                    return IsStunned;

                case StatusEffectType.Freeze:
                    return IsFrozen;

                case StatusEffectType.Slow:
                    return IsSlowed;

                default:
                    return false;
            }
        }
        private void ApplyElectricShock(StatusEffectRequest request)
        {
            if (!CanReceiveStatusEffect(request.EffectType))
            {
                return;
            }
            bool refreshed = IsShocked; //기존 감전 상태였다면 지속시간 갱신으로 본다

            StopElectricShockRoutine();

            if (IsSpawned)
            {
                // 멀티에서는 서버가 NetworkVariable을 변경한다.
                electricShockActive.Value = true;
            }
            else
            {
                // 네트워크 테스트가 아니면 로컬 이펙트를 직접 켠다.
                ApplyElectricShockPresentation(true);
            }
            // 처음 감전에 걸렸을 때만 시작 이벤트 호출
            if (!refreshed)
            {
                StatusEffectStarted?.Invoke(
                    StatusEffectType.ElectricShok);
            }
            // SO에서 전달받은 Duration만큼 감전 유지
            electricShockRoutine = StartCoroutine(
                RemoveElectricShockAfter(
                    request.Duration));
            Debug.Log($"PHS_STATUS_EFFECT_APPLIED " + $"target={name} " + $"effect={StatusEffectType.ElectricShok} " + $"duration={request.Duration:F2} "
                + $"source={(request.Source != null ? request.Source.name : "null")} " + $"refreshed={refreshed}", this);
        }
        private void ApplyStun(StatusEffectRequest request)
        {
            if (!CanReceiveStatusEffect(request.EffectType))
            {
                return;
            }
            bool refreshed = IsStunned;

            //기존 기절 종료 타이머가 있으면 새 시족시간으로 갱신하기 위해 제거
            StopStunRoutine();
            if (IsSpawned)
            {
                stunActive.Value = true;
            }
            else
            {
                ApplyStunPresentation(true);
            }

            if (!refreshed)
            {
                StatusEffectStarted?.Invoke(
                    StatusEffectType.Stun);
            }
            stunRoutine = StartCoroutine(RemoveStunAfter(request.Duration));

            Debug.Log($"PHS_STATUS_EFFECT_APPLIED " + $"target={name} " + $"effect={StatusEffectType.Stun} " + $"duration={request.Duration:F2} " +
                $"source={(request.Source != null ? request.Source.name : "null")} " + $"refreshed={refreshed}", this);
        }
        private void ApplyFreeze(StatusEffectRequest request)
        {
            if (!CanReceiveStatusEffect(request.EffectType))
            {
                return;
            }
            bool refreshed = IsFrozen;

            StopFrozenRoutine();

            if (IsSpawned)
            {
                frozenActive.Value = true;
            }
            else
            {
                ApplyFrozenPresentation(true);
            }
            if (!refreshed)
            {
                StatusEffectStarted?.Invoke(StatusEffectType.Freeze);
            }

            frozenRoutine = StartCoroutine(RemoveFrozenAfter(request.Duration));
            Debug.Log($"PHS_STATUS_EFFECT_APPLIED " + $"target={name} " + $"effect={StatusEffectType.Freeze} " + $"duration={request.Duration:F2} " +
                $"source={(request.Source != null ? request.Source.name : "null")} " + $"refreshed={refreshed}", this);

        }
        private void ApplySlow(StatusEffectRequest request)
        {
            if (!CanReceiveStatusEffect(request.EffectType))
            {
                return;
            }

            StopSlowRoutine();

            switch (request.ApplyMode)
            {
                case StatusEffectApplyMode.Refresh:
                    currentSlowAmount = request.Amount; //지속시간만 갱신
                    break;
                case StatusEffectApplyMode.Stack:
                    currentSlowStacks = Mathf.Min(currentSlowStacks + 1, request.MaxStacks);

                    currentSlowAmount = request.Amount * currentSlowStacks;

                    break;
                case StatusEffectApplyMode.Fixed:
                    //항상 고정 수치
                    currentSlowStacks = 1;
                    currentSlowAmount = request.Amount;

                    break;
            }
            if (IsSpawned)
            {
                slowActive.Value = true;
            }
            else
            {
                ApplySlowPresentation(true);
            }
            StatusEffectStarted?.Invoke(request.EffectType);

            slowRoutine = StartCoroutine(RemoveSlowAfter(request.Duration));

            Debug.Log($"PHS_SLOW_APPLIED " + $"amount={currentSlowAmount:F2} " + $"stack={currentSlowStacks}", this);
        }
    }
}
