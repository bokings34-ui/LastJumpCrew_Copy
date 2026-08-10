using UnityEngine;

namespace SM
{
    public class EnemyAttackState : IEnemyState
    {
        private float _cooldownTimer;
        private int _originalPriority;

        public void Enter(EnemyBase owner)
        {
            owner.Agent.isStopped = true;
            owner.Agent.velocity = Vector3.zero;

            _originalPriority = owner.Agent.avoidancePriority;
            owner.Agent.avoidancePriority = 10;

            _cooldownTimer = owner.AttackCooldown;

            if (owner.Anim != null) owner.Anim.CrossFade(EnemyAnimData.Attack, 0.05f);
        }

        public void Tick(EnemyBase owner, float deltaTime)
        {
            var target = owner.GetTarget();

            if (target == null)
            {
                owner.StateMachine.ChangeState(owner, EnemyStateType.Chase);
                return;
            }

            owner.RotateTowards(target.position, deltaTime);

            if (!owner.IsTargetWithinAttackRange(target))
            {
                owner.StateMachine.ChangeState(owner, EnemyStateType.Chase);
                return;
            }

            _cooldownTimer += deltaTime;

            if (_cooldownTimer >= owner.AttackCooldown)
            {
                _cooldownTimer = 0f;

                if (owner.Anim != null) owner.Anim.Play(EnemyAnimData.Attack, -1, 0f);
            }
        }

        public void Exit(EnemyBase owner)
        {
            owner.Agent.avoidancePriority = _originalPriority;
        }
    }
}
