using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Combat
{
    public static class CombatHitResolver
    {
        public static GameObject ResolveTargetObject(GameObject hitObject)
        {
            if (hitObject == null)
            {
                return null;
            }

            var utilityTarget = hitObject.GetComponentInParent<IUtilityAttackTarget>();
            if (utilityTarget is Component utilityTargetComponent)
            {
                return utilityTargetComponent.gameObject;
            }

            var damageable = hitObject.GetComponentInParent<IDamageable>();
            if (damageable is Component damageableComponent)
            {
                return damageableComponent.gameObject;
            }

            var knockbackable = hitObject.GetComponentInParent<IKnockbackable>();
            if (knockbackable is Component knockbackableComponent)
            {
                return knockbackableComponent.gameObject;
            }

            return hitObject.transform.root.gameObject;
        }

        public static bool TryResolveUtilityAttack(
            GameObject target,
            GameObject attacker,
            string itemId,
            uint requestSequence)
        {
            if (target == null)
            {
                return false;
            }

            var utilityTarget = target.GetComponentInParent<IUtilityAttackTarget>();
            return utilityTarget != null
                && utilityTarget.TryResolveUtilityAttack(
                    new UtilityAttackHit(itemId, attacker, requestSequence));
        }

        public static void ResolveDamageAndKnockback(GameObject target, GameObject attacker, int damage, Vector3 knockbackDirection, float knockbackForce)
        {
            if(target == null)
            {
                return;
            }

            bool isPlayer = target.GetComponentInParent<NetworkPlayerController>() != null;

            if(!isPlayer && damage > 0)
            {
                var damageable = target.GetComponentInParent<IDamageable>();

                if(damageable != null && damageable.IsAlive)
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
        public static bool TryResolveCombatTarget(Collider hitCollider, out GameObject targetObject)
        {
            targetObject = null;

            if(hitCollider == null)
            {
                return false;
            }
            var damageable = hitCollider.GetComponentInParent<IDamageable>();

            if(damageable is Component damageableComponent)
            {
                targetObject = damageableComponent.gameObject;
                return true;
            }
            var statusReceiver = hitCollider.GetComponentInParent<IStatusEffectReceiver>();

            if(statusReceiver is Component statusComponent)
            {
                targetObject = statusComponent.gameObject;
                return true;
            }
            return false;
        }
        public static bool IsSameTarget(GameObject first, GameObject second)
        {
            if(first == null || second == null)
            {
                return false;
            }
            if(first == second)
            {
                return true;
            }

            return first.transform.IsChildOf(second.transform) || second.transform.IsChildOf(first.transform);
        }
    }
}
