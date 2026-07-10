using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public class DeviceAttackEnemy : EnemyBase
    {
        protected override Transform SetTarget()
        {
            var target = DeviceRegistry.Peek()?.GetNearestDeviceTransform(transform.position);

            if (target == null)
            {
                Debug.LogWarning($"[<color=lime>[{name}]</color> DeviceRegistry에 등록된 장치가 없습니다.");
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