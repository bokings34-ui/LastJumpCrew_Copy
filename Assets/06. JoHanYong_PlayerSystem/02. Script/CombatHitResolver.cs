using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Combat
{
    public static class CombatHitResolver
    {
        public static void ResolveDamageAndKnockback(GameObject target, GameObject attacker, int damage, Vector3 knockbackDirection, float knockbackForce)
        {
            if(target == null)
            {
                return;
            }
            if(damage > 0)
            {
                var damageable = target.GetComponentInParent<IDamageable>();//데미지 처리

                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(damage, attacker);
                }
            }
            if(knockbackForce <= 0f)
            {
                return;
            }
            var knockbackable = target.GetComponentInParent<IKnockbackable>();

            if(knockbackable == null || !knockbackable.CanReceiveKnockback)
            {
                return ;
            }
            if(knockbackDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }
            knockbackable.ApplyKnockback(knockbackDirection.normalized, knockbackForce, attacker);
        }
        public static void ResolveStatusEffect(GameObject target, GameObject source, StatusEffectType effectType, float duration)
        {
            if(target == null)
            {
                return;
            }
            if(effectType == StatusEffectType.None)
            {
                return;
            }
            if(duration <= 0f)
            {
                return;
            }
            var statusReceiver = target.GetComponentInParent<IStatusEffectReceiver>();

            if(statusReceiver == null)
            {
                return;
            }
            if (!statusReceiver.CanReceiveStatusEffect(effectType)) //대상이 현재 해당 효과를 받을 수 없으면 적용X
            {
                return;
            }
            statusReceiver.ApplyStatusEffect(effectType, duration, source);
        }
    }
}
