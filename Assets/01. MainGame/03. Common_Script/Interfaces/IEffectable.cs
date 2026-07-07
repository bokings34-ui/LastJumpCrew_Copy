using UnityEngine;

namespace LastJumpCrew.Common
{
    public interface IEffectable
    {
        bool CanReceiveEffect(string effectId);
        void ApplyEffect(string effectId, GameObject source);
    }
}
