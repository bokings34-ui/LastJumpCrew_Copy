using UnityEngine;

namespace LastJumpCrew.Common
{
    public interface IStatusEffectReceiver
    {
        bool CanReceiveStatusEffect(string effectId);
        void ApplyStatusEffect(string effectId, float duration, GameObject source);
        void RemoveStatusEffect(string effectId);
    }
}
