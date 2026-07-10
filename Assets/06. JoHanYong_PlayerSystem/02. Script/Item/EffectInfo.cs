using UnityEngine;

public readonly struct EffectInfo //감전, 스턴 상태이상 정보를 전달한다
{
    public readonly EffectType EffectType;
    public readonly float Duration;
    public readonly GameObject Source;

    public EffectInfo(EffectType effectType, float duration, GameObject source)
    {
        EffectType = effectType;
        Duration = duration;
        Source = source;
    }
}
