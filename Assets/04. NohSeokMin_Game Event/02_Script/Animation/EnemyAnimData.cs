using UnityEngine;

namespace SM
{
    public static class EnemyAnimData
    {
        public static readonly int Spawn = Animator.StringToHash("Spawn");
        public static readonly int Chase = Animator.StringToHash("Fly Forward In Place");
        public static readonly int Attack = Animator.StringToHash("Dash Attack In Place");
        public static readonly int TakeDamage = Animator.StringToHash("Take Damage");
        public static readonly int Die = Animator.StringToHash("Die");
    }
}