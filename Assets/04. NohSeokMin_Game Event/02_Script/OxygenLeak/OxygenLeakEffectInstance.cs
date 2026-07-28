using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace SM
{
    public class OxygenLeakEffectInstance :
        MonoBehaviour,
        IInteractable,
        IRequireHeldItem,
        IEventRepairableEffect,
        IUtilityAttackTarget
    {
        private const string WrenchItemId = "wrench";

        [Header("벽 무시 레이어 설정")]
        [SerializeField] private LayerMask _wallLayerMask;

        [Header("흡입력 감소 시간 설정")]
        [SerializeField] private float pullActiveDuration = 5f;

        private float _outerPullRadius;
        private float _innerDamageRadius;
        private float _initialPullSpeed;
        private int _centerDamage;
        private float _damageTickInterval;
        private float _maxRepairProgress;

        private float _repairProgress;
        private float _damageTimer;
        private float _elapsedSinceSpawn;
        private IEventRepairRuntimeBridge _repairRuntimeBridge;
        private bool _hazardHandledExternally;

        public bool IsSealed { get; private set; }
        public ulong EventInstanceId { get; private set; }
        public uint EffectInstanceId { get; private set; }
        public EventEffectKind EffectKind => EventEffectKind.OxygenLeak;
        public Vector3 RepairPosition => transform.position;
        public bool IsRepairComplete => IsSealed;
        public event Action<OxygenLeakEffectInstance> OnSealed;

        private struct PullTarget
        {
            public CharacterController Controller;
            public NavMeshAgent Agent;
        }

        private readonly Dictionary<Transform, PullTarget> _targetsInRange
            = new Dictionary<Transform, PullTarget>();

        public void Activate(
            OxygenLeakEventDataSO data,
            bool hazardHandledExternally)
        {
            _outerPullRadius = data.outerPullRadius;
            _innerDamageRadius = data.innerDamageRadius;
            _initialPullSpeed = data.pullSpeed;
            _centerDamage = data.centerDamage;
            _damageTickInterval = data.damageTickInterval;
            _maxRepairProgress = data.maxRepairProgress;

            _repairProgress = 0f;
            _damageTimer = 0f;
            _elapsedSinceSpawn = 0f;
            _hazardHandledExternally = hazardHandledExternally;
            IsSealed = false;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            UnbindRepairTarget();
            _targetsInRange.Clear();
            _hazardHandledExternally = false;
            gameObject.SetActive(false);
        }

        public bool BindRepairTarget(
            ulong eventInstanceId,
            uint effectInstanceId,
            IEventRepairRuntimeBridge repairRuntimeBridge)
        {
            UnbindRepairTarget();
            if (eventInstanceId == 0UL || effectInstanceId == 0U || repairRuntimeBridge == null)
            {
                return false;
            }

            EventInstanceId = eventInstanceId;
            EffectInstanceId = effectInstanceId;
            _repairRuntimeBridge = repairRuntimeBridge;
            if (_repairRuntimeBridge.RegisterRepairTarget(this))
            {
                return true;
            }

            EventInstanceId = 0UL;
            EffectInstanceId = 0U;
            _repairRuntimeBridge = null;
            return false;
        }

        public void UnbindRepairTarget()
        {
            if (_repairRuntimeBridge != null && EventInstanceId != 0UL && EffectInstanceId != 0U)
            {
                _repairRuntimeBridge.UnregisterRepairTarget(EventInstanceId, EffectInstanceId);
            }

            EventInstanceId = 0UL;
            EffectInstanceId = 0U;
            _repairRuntimeBridge = null;
        }

        private void Update()
        {
            if (IsSealed || _hazardHandledExternally) return;

            _elapsedSinceSpawn += Time.deltaTime;

            FindTargetsInRange();
            PullTargets();
            ApplyCenterDamage();
        }

        private float GetCurrentPullSpeed()
        {
            if (_elapsedSinceSpawn >= pullActiveDuration) return 0f;

            float t = _elapsedSinceSpawn / pullActiveDuration;
            return Mathf.Lerp(_initialPullSpeed, 0f, t);
        }

        private void FindTargetsInRange()
        {
            _targetsInRange.Clear();

            var hits = Physics.OverlapSphere(transform.position, _outerPullRadius);

            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;

                var controller = hit.GetComponentInParent<CharacterController>();
                var agent = hit.GetComponentInParent<NavMeshAgent>();

                if (controller == null && agent == null) continue;

                if (Physics.Linecast(transform.position, hit.transform.position, _wallLayerMask)) continue;

                _targetsInRange[hit.transform] = new PullTarget { Controller = controller, Agent = agent };
            }
        }

        private void PullTargets()
        {
            float currentPullSpeed = GetCurrentPullSpeed();
            if (currentPullSpeed <= 0f) return;

            foreach (var kvp in _targetsInRange)
            {
                var targetTransform = kvp.Key;
                var pullTarget = kvp.Value;

                Vector3 direction = (transform.position - targetTransform.position);

                if (direction.sqrMagnitude < 0.01f) continue;

                Vector3 pullMotion = direction.normalized * currentPullSpeed * Time.deltaTime;

                if (pullTarget.Controller != null)
                {
                    pullTarget.Controller.Move(pullMotion);
                }
                else if (pullTarget.Agent != null && pullTarget.Agent.enabled)
                {
                    pullTarget.Agent.nextPosition += pullMotion;
                }
            }
        }

        private void ApplyCenterDamage()
        {
            _damageTimer += Time.deltaTime;

            if (_damageTimer < _damageTickInterval) return;

            _damageTimer = 0f;

            foreach (var kvp in _targetsInRange)
            {
                var playerTransform = kvp.Key;

                float dist = Vector3.Distance(transform.position, playerTransform.position);

                if (dist <= _innerDamageRadius)
                {
                    var damageable = playerTransform.GetComponentInParent<IDamageable>();

                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.ApplyDamage(_centerDamage, gameObject);
                    }
                }
            }
        }

        // ___________ IRequireHeldItem ___________

        public string RequiredItemId { get { return WrenchItemId; } }

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        // ___________  IInteractable ______________

        public string InteractionPrompt { get { return "렌치 필요"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !IsSealed && IsRequirementMet(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

        // __________ 플레이어가 수리할 때 호출하는 함수 ____________
        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            if (IsSealed
                || hit.ItemId != RequiredItemId
                || hit.Attacker == null
                || hit.RequestSequence == 0U
                || _repairRuntimeBridge == null)
            {
                return false;
            }

            var itemRecord =
                hit.Attacker.GetComponentInParent<NetworkPlayerItemRecord>();
            if (itemRecord == null)
            {
                itemRecord =
                    hit.Attacker.GetComponentInChildren<
                        NetworkPlayerItemRecord>(true);
            }

            if (itemRecord == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_REPAIR_REQUEST_REJECTED reason=item_record_missing target={name}",
                    this);
                return false;
            }

            return _repairRuntimeBridge.RequestEffectRepair(
                this,
                itemRecord,
                hit.RequestSequence);
        }

        public void ApplyRepair(float amount)
        {
            if (IsSealed) return;

            _repairProgress += amount;

            if (_repairProgress >= _maxRepairProgress)
            {
                IsSealed = true;
                OnSealed?.Invoke(this);
            }
        }

        public bool TryApplyRepairStep(float amount)
        {
            if (IsSealed || amount <= 0f)
            {
                return false;
            }

            ApplyRepair(amount);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, _outerPullRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _innerDamageRadius);
        }
    }
}
