using System;
using UnityEngine;

namespace LastJumpCrew.Common
{
    public enum ItemEffectType
    {
        Damage,
        Knockback,
        StatusEffect
    }
    public enum EffectTargetType
    {
        All,
        PlayerOnly,
        EnemyOnly
    }
    public enum StatusEffectApplyMode
    {
        Refresh, //다시 맞으면 지속시간만 갱신
        Stack, //중첩
        Fixed //고정값
    }
    [Serializable]
    public struct ItemEffectData
    {
        [Header("Effect Type")]
        [Tooltip("데미지, 넉백, 상태이상 중 적용할 효과")]
        [SerializeField]
        private ItemEffectType effectType;

        [Header("Target")]
        [Tooltip("이 효과를 적용할 대상 종류")]
        [SerializeField]
        private EffectTargetType targetType;

        [Header("Effect Value")]
        [Tooltip("데미지 수치 or 넉백 세기")]
        [SerializeField, Min(0f)]
        private float amount;

        [Header("Status Effect")]
        [Tooltip("상태이상 효과일 때 제공할 상태이상 종류")]
        [SerializeField]
        private StatusEffectType statusEffectType;

        [Tooltip("상태이상 지속 시간")]
        [SerializeField, Min(0f)]
        private float duration;

        [Tooltip("상태이상 모드")]
        [SerializeField]
        private StatusEffectApplyMode statusEffectApplyMode;

        [SerializeField, Min(1)]
        private int maxStacks;

        public ItemEffectType EffectType => effectType;

        public EffectTargetType TargetType => targetType;

        public float Amount => amount;

        public StatusEffectType StatusEffectType => statusEffectType;

        public float Duration => duration;

        public StatusEffectApplyMode StatusEffectApplyMode => statusEffectApplyMode;

        public int MaxStacks => maxStacks;
    }
}