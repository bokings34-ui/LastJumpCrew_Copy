using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public class PlayerAttackEnemy : EnemyBase
    {
        [SerializeField] private float verticalAttackTolerance = 2f;

        protected override Transform SetTarget()
        {
            var target = PlayerRegistry.Peek()?.GetNearestPlayer(transform.position);

            if (target == null)
            {
                Debug.LogWarning($"<color=lime>[{name}]</color> PlayerRegistry에 등록된 Player가 없습니다.");
            }

            return target;
        }

        public override bool IsTargetWithinAttackRange(Transform target)
        {
            if (base.IsTargetWithinAttackRange(target)) return true;
            if (target == null) return false;

            var offset = target.position - transform.position;
            var verticalDistance = Mathf.Abs(offset.y);
            offset.y = 0f;
            return verticalDistance <= verticalAttackTolerance
                && offset.sqrMagnitude <= AttackRange * AttackRange;
        }

        public override void PerformAttack(Transform target)
        {
            var damageable = target.GetComponentInParent<IDamageable>();

            if (damageable != null && damageable.IsAlive)
            {
                damageable.ApplyDamage(Mathf.RoundToInt(AttackDamage), gameObject);
            }
        }
    }
}
