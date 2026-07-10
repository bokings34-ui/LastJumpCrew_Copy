using UnityEngine;

namespace SM
{
    public class EnemyAttackState : IEnemyState
    {
        private float _cooldownTimer;

        public void Enter(EnemyBase owner)
        {
            owner.Agent.isStopped = true;
            _cooldownTimer = 0f;
        }

        public void Tick(EnemyBase owner, float deltaTime)
        {
            var target = owner.GetTarget();

            if (target == null)
            {
                owner.StateMachine.ChangeState(owner, EnemyStateType.Chase);
                return;
            }

            if (owner.GetDistanceToTarget(target) > owner.AttackRange)
            {
                owner.StateMachine.ChangeState(owner, EnemyStateType.Chase);
                return;
            }

            _cooldownTimer += deltaTime;

            if (_cooldownTimer >= owner.AttackCooldown)
            {
                _cooldownTimer = 0f;
                owner.PerformAttack(target);
            }
        }

        public void Exit(EnemyBase owner) { }
    }
}