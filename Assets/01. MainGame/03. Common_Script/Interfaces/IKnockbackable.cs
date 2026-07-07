using UnityEngine;

namespace LastJumpCrew.Common
{
    public interface IKnockbackable
    {
        bool CanReceiveKnockback { get; }
        void ApplyKnockback(Vector3 force, GameObject source);
    }
}
