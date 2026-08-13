using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    public class DeviceAttackEnemy : EnemyBase
    {
        private bool targetUnavailableLogged;

        protected override Transform SetTarget()
        {
            var target = DeviceRegistry.Peek()?.GetNearestDeviceTransform(transform.position);

            if (target == null && !targetUnavailableLogged)
            {
                Debug.LogWarning(
                    $"PHS_ENEMY_DEVICE_TARGET_UNAVAILABLE enemy={name} " +
                    "reason=reachable_device_path_missing");
                targetUnavailableLogged = true;
            }
            else if (target != null)
            {
                targetUnavailableLogged = false;
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
