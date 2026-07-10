using System;
using UnityEngine;
using UnityEngine.AI;
using LastJumpCrew.Common;

namespace SM
{
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable
    {
        public event Action<EnemyBase> OnDeath;

        [Header("스탯 설정")]
        [SerializeField] private float maxHealth = 10f;
        [SerializeField] private float attackRange = 2f;
        [SerializeField] private float attackDamage = 1f;
        [SerializeField] private float attackCooldown = 1f;

        protected float _currentHealth;
        private GameObject _enemyPrefab;
        private Collider[] _colliders;
        private Transform _cachedTarget;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsAlive { get { return StateMachine.CurrentType != EnemyStateType.Dead; } }

        public NavMeshAgent Agent { get; private set; }
        public EnemyStateMachine StateMachine { get; private set; }
        public float AttackRange { get { return attackRange; } }
        public float AttackDamage { get { return attackDamage; } }
        public float AttackCooldown { get { return attackCooldown; } }

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            _colliders = GetComponentsInChildren<Collider>();

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
            GetTarget();
            StateMachine.ChangeState(this, EnemyStateType.Chase);
        }

        public void Deactivate()
        {
            _cachedTarget = null;
            gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            StateMachine.Tick(this, deltaTime);
        }

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (!IsAlive) return;

            _currentHealth -= amount;

            if (_currentHealth <= 0f)
            {
                StateMachine.ChangeState(this, EnemyStateType.Dead);
            }
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
            if (_cachedTarget == null)
            {
                _cachedTarget = SetTarget();
            }
            return _cachedTarget;
        }

        protected abstract Transform SetTarget();
        public abstract void PerformAttack(Transform target);

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}