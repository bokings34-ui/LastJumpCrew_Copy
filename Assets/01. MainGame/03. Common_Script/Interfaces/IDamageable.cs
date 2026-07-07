using UnityEngine;

namespace LastJumpCrew.Common
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(int amount, GameObject attacker);
    }
}
