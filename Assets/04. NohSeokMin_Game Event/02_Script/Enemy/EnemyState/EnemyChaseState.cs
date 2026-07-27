using UnityEngine;

namespace SM
{
    public class EnemyChaseState : IEnemyState
    {
        private const float DestinationUpdateInterval = 0.2f;
        private const float MinMoveDistance = 0.5f;

        private float _timer;
        private Vector3 _lastPosition;

        public void Enter(EnemyBase owner)
        {
            owner.Agent.isStopped = false;
            _timer = 0f;
            _lastPosition = owner.transform.position;

            if (owner.Anim != null) owner.Anim.CrossFade(EnemyAnimData.Chase, 0.1f);
        }

        public void Tick(EnemyBase owner, float deltaTime)
        {
            var target = owner.GetTarget();
            if (target == null) return;

            _timer += deltaTime;

            if (_timer >= DestinationUpdateInterval)
            {
                _timer = 0f;

                if (Vector3.Distance(_lastPosition, target.position) >= MinMoveDistance)
                {
                    owner.Agent.SetDestination(target.position);
                    _lastPosition = target.position;
                }
            }

            if (owner.GetDistanceToTarget(target) <= owner.AttackRange)
            {
                owner.StateMachine.ChangeState(owner, EnemyStateType.Attack);
            }
        }

        public void Exit(EnemyBase owner) { }
    }
}