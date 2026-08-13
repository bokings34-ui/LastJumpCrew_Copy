using UnityEngine;

namespace LastJumpCrew.Common
{
    public enum StatusEffectApplyMode
    {
        Refresh,
        Stack,
        Fixed
    }

    public readonly struct StatusEffectRequest
    {
        public StatusEffectType EffectType { get; }
        public float Duration { get; }
        public float Amount { get; }
        public StatusEffectApplyMode ApplyMode { get; }
        public int MaxStacks { get; }
        public GameObject Source { get; }

        public StatusEffectRequest(StatusEffectType effectType, float duration, float amount,
            StatusEffectApplyMode applyMode, int maxStacks, GameObject source)
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
