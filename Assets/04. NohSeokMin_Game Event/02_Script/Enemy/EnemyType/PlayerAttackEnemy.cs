using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public class PlayerAttackEnemy : EnemyBase
    {
        protected override Transform SetTarget()
        {
            var target = PlayerRegistry.Peek()?.GetNearestPlayer(transform.position);

            if (target == null)
            {
                Debug.LogWarning($"[<color=lime>[{name}]</color> PlayerRegistry에 등록된 Player가 없습니다.");
            }

            return target;
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