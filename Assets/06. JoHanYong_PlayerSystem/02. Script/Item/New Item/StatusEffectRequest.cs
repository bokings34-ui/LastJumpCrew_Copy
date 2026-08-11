using UnityEngine;

namespace LastJumpCrew.Common
{
    //상태이상 적용 요청 데이터
    public readonly struct StatusEffectRequest
    {
        public StatusEffectType EffectType { get; }

        //상태이상 지속시간
        public float Duration { get; }
        //효과 수치
        public float Amount { get; }
        //적용 방식
        public StatusEffectApplyMode ApplyMode { get; }
        //최대 스탯 수
        public int MaxStacks { get; }
        //상태이상을 건 공격자
        public GameObject Source { get; }

        public StatusEffectRequest(StatusEffectType effectType, float duration, float amount, StatusEffectApplyMode applyMode, int maxStacks, GameObject source)
        {
            EffectType = effectType;
            Duration = duration;
            Amount = amount;
            ApplyMode = applyMode;
            MaxStacks = Mathf.Max(1, maxStacks);
            Source = source;
        } 
    }
}


