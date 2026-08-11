using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;
namespace LastJumpCrew.ParkHanSol.Items
{
    public static class ItemEffectResolver
    {
        public static bool ApplyEffects(UtilityItemDataSO itemData, GameObject target, Vector3 effectDirection, GameObject attacker)
        {
            if(itemData == null)
            {
                Debug.LogError("PHS_ITEM_EFFECT_FAILED " + "reason=item_data_missing");
                return false;   
            }
            if(target == null)
            {
                Debug.LogError("PHS_ITEM_EFFECT_FAILED " + "reason=target_missing");
                return false;
            }
            if(itemData.HitEffects == null || itemData.HitEffects.Count == 0)//효과목록 누락 방어
            {
                Debug.LogWarning($"PHS_ITEM_EFFECT_FAILED " + $"reason=hit_effects_missing " + $"item={itemData.ItemId} " + $"target={target.name}");
                return false;
            }
            bool isPlayerTarget = target.GetComponentInParent<NetworkPlayerController>() != null; //Resolver 내부에서 플레이어 판별

            bool isEnemyTarget =!isPlayerTarget && IsCombatTarget(target);  //플레이어가 아닌 모든 오브젝트를 적으로 취급하지 않도록 검사

            bool anyEffectApplied = false;

            foreach(ItemEffectData effect in itemData.HitEffects)
            {
                if (!CanApplyToTarget(effect.TargetType, isPlayerTarget, isEnemyTarget))
                {
                    continue;
                }
                bool applied = ApplySingleEffect(effect, target, effectDirection, attacker);

                if (applied)
                {
                    anyEffectApplied = true;

                    Debug.Log($"PHS_ITEM_EFFECT_APPLIED " + $"item={itemData.ItemId} " + $"target={target.name} "
                        + $"effect={effect.EffectType} " + $"targetType={effect.TargetType} " + $"amount={effect.Amount:F2}");
                }
            }
            return anyEffectApplied;

        }
        private static bool IsCombatTarget(GameObject target)// EnemyOnly 판별을 위한 전투 대상 검사
        {
            if(target == null)
            {
                return false;
            }
            return target.GetComponentInParent<IDamageable>() != null
            || target.GetComponentInParent<IKnockbackable>() != null || target.GetComponentInParent<IStatusEffectReceiver>() != null;
        }
        private static bool CanApplyToTarget(EffectTargetType targetType, bool isPlayerTarget, bool isEnemyTarget)
        {
            switch (targetType)
            {
                case EffectTargetType.All: return isPlayerTarget || isEnemyTarget;

                case EffectTargetType.PlayerOnly: return isPlayerTarget;

                case EffectTargetType.EnemyOnly: return isEnemyTarget;

                default: return false;
            }
        }
        private static bool ApplySingleEffect(ItemEffectData effect, GameObject target, Vector3 effectDirction, GameObject attacker)
        {
            switch (effect.EffectType)
            {
                case ItemEffectType.Damage:
                    return ApplyDamage(target, effect.Amount, attacker);
                case ItemEffectType.Knockback:
                    return ApplyKnockback(target, effectDirction, effect.Amount, attacker);
                case ItemEffectType.StatusEffect:
                    return ApplyStatusEffect(target, effect, attacker);
                default: Debug.LogError($"PHS_ITEM_EFFECT_FAILED " + $"reason=unsupported_effect_type " + $"effect={effect.EffectType}");

                    return false;

            }
        }
        private static bool ApplyDamage(GameObject target, float amount, GameObject attacker)
        {
            //IDamageable을 찾아서 데미지 적용
            if(amount <= 0f)
            {
                return false;
            }
            IDamageable damageable = target.GetComponentInParent<IDamageable>();

            if(damageable == null || !damageable.IsAlive)
            {
                return false;
            } 
            int damageAmount = Mathf.RoundToInt(amount); //IDamageable.ApplyDamage가 int을 받아서 float Amount를 int로 변환하기

            if(damageAmount <= 0f) //반올림 데미지가 0이면 데미지 적용X
            {
                return false;
            }
            damageable.ApplyDamage(damageAmount, attacker);

            return true;
        }
        private static bool ApplyKnockback(GameObject target, Vector3 direction, float force, GameObject attacker)
        {
            if(force <= 0f)
            {
                return false;   
            }
            IKnockbackable knockbackable = target.GetComponentInParent<IKnockbackable>();

            if(knockbackable == null || !knockbackable.CanReceiveKnockback)
            {
                return false;
            }
            if(direction.sqrMagnitude <= 0.0001f) // 방향 벡터의 크기가 0이면 정상적인 넉백 바향을 계산 할 수 없음
            {
                return false;   
            }
            knockbackable.ApplyKnockback(direction.normalized, force, attacker);

            return true;
        }
        private static bool ApplyStatusEffect(GameObject target, ItemEffectData effect, GameObject attacker)
        {
            if(effect.StatusEffectType == StatusEffectType.None)
            {
                return false;   
            }
            if (effect.Duration <= 0f)
            {
                return false;
            }
            IStatusEffectReceiver receiver = target.GetComponentInParent<IStatusEffectReceiver>();

            if(receiver == null)
            {
                return false;
            }
            if (!receiver.CanReceiveStatusEffect(effect.StatusEffectType))
            {
                return false;
            }
            var request = new StatusEffectRequest(effect.StatusEffectType, effect.Duration, effect.Amount, effect.StatusEffectApplyMode, effect.MaxStacks, attacker);

            receiver.ApplyStatusEffect(request);

            return true;
        }
      
    }
}