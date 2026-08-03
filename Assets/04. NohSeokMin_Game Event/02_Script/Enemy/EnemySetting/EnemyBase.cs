using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace SM
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable
    {
        public event Action<EnemyBase> OnDeath;

        [Header("스탯 설정")]
        [SerializeField] private float maxHealth = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackDamage = 1f;
        [SerializeField] private float attackCooldown = 1f;

        [Header("사운드")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip attackSound;

        protected float _currentHealth;
        private GameObject _enemyPrefab;
        private Collider[] _colliders;
        private Transform _cachedTarget;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => _currentHealth;

        private Coroutine _knockbackRoutine;
        private StatusEffectController _statusEffectController;

        public bool IsAlive { get { return StateMachine.CurrentType != EnemyStateType.Dead; } }
        public bool IsShocked
        {
            get { return _statusEffectController != null && _statusEffectController.IsShocked; }
        }

        public NavMeshAgent Agent { get; private set; }
        public EnemyStateMachine StateMachine { get; private set; }
        public Animator Anim { get; private set; }

        public float AttackRange { get { return attackRange; } }
        public float AttackDamage { get { return attackDamage; } }
        public float AttackCooldown { get { return attackCooldown; } }

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            _colliders = GetComponentsInChildren<Collider>();

            Anim = GetComponentInChildren<Animator>();
            _statusEffectController = GetComponent<StatusEffectController>();

            StateMachine = new EnemyStateMachine();
            StateMachine.Register(EnemyStateType.Chase, new EnemyChaseState());
            StateMachine.Register(EnemyStateType.Attack, new EnemyAttackState());
            StateMachine.Register(EnemyStateType.Dead, new EnemyDeadState());
        }

        public void Activate(GameObject sourcePrefab)
        {
            _enemyPrefab = sourcePrefab;
            _currentHealth = maxHealth;
            _cachedTarget = null;

            Agent.enabled = true;
            gameObject.SetActive(true);

            if (Anim != null) Anim.Play(EnemyAnimData.Spawn, -1, 0f);

            GetTarget();
            StateMachine.ChangeState(this, EnemyStateType.Chase);
        }

        public void Deactivate()
        {
            _cachedTarget = null;

            if (_statusEffectController != null
                && _statusEffectController.CanReceiveStatusEffect(
                    StatusEffectType.ElectricShok))
            {
                _statusEffectController.RemoveStatusEffect(
                    StatusEffectType.ElectricShok);
            }

            if (_knockbackRoutine != null) 
            { 
                StopCoroutine(_knockbackRoutine); 
                _knockbackRoutine = null;
            }

            gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (IsShocked)
            {
                if (Agent.enabled) Agent.isStopped = true;
                return;
            }

            if (Agent.enabled) Agent.isStopped = false;
            StateMachine.Tick(this, deltaTime);
        }

        public void RotateTowards(Vector3 targetPosition, float deltaTime, float turnSpeed = 12f)
        {
            Vector3 direction = (targetPosition - transform.position);
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * deltaTime);
            }
        }

        // ________ IDamageable __________

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (!IsAlive) return;

            _currentHealth -= amount;

            if (_currentHealth <= 0f)
            {
                StateMachine.ChangeState(this, EnemyStateType.Dead);
            }
            else
            {
                if (Anim != null) Anim.Play(EnemyAnimData.TakeDamage, -1, 0f);
            }
        }

        // ________ IKnockbackable _________

        public bool CanReceiveKnockback { get { return IsAlive; } }

        public void ApplyKnockback(Vector3 direction, float force, GameObject attacker)
        {
            if (!CanReceiveKnockback) return;

            if (_knockbackRoutine != null) StopCoroutine(_knockbackRoutine);
            _knockbackRoutine = StartCoroutine(KnockbackRoutine(direction.normalized, force));
        }

        private IEnumerator KnockbackRoutine(Vector3 direction, float force)
        {
            const float knockbackDuration = 0.25f;
            float elapsed = 0f;

            bool agentWasEnabled = Agent.enabled;
            if (agentWasEnabled) Agent.enabled = false;

            while (elapsed < knockbackDuration)
            {
                float t = elapsed / knockbackDuration;
                float currentForce = force * (1f - t);
                transform.position += direction * currentForce * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (agentWasEnabled)
            {
                Agent.Warp(transform.position);
                Agent.enabled = true;
            }

            _knockbackRoutine = null;
        }

        public void SetColliderEnabled(bool enabled)
        {
            foreach (var col in _colliders)
            {
                col.enabled = enabled;
            }
        }

        public void CompleteDeath()
        {
            OnDeath?.Invoke(this);
            EnemyPool.Instance.Return(_enemyPrefab, this);
        }

        public float GetDistanceToTarget(Transform target)
        {
            var col = target.GetComponentInParent<Collider>();

            if (col != null)
            {
                Vector3 closest = Physics.ClosestPoint(transform.position, col, target.position, target.rotation);
                return Vector3.Distance(transform.position, closest);
            }

            return Vector3.Distance(transform.position, target.position);
        }

        public Transform GetTarget()
        {
            if (!IsTargetValid(_cachedTarget))
            {
                _cachedTarget = SetTarget();
            }
            return _cachedTarget;
        }

        private bool IsTargetValid(Transform target)
        {
            if (target == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable != null && !damageable.IsAlive) return false;

            return true;
        }

        protected abstract Transform SetTarget();
        public abstract void PerformAttack(Transform target);

        public void ForceReturnToPool()
        {
            EnemyPool.Instance.Return(_enemyPrefab, this);
        }

        public void PlayAttackSound()
        {
            if (audioSource != null && attackSound != null)
            {
                audioSource.PlayOneShot(attackSound);
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
