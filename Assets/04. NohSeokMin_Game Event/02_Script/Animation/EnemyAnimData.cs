using UnityEngine;

namespace SM
{
    public static class EnemyAnimData
    {
        // Animator.Play requires the concrete state path used by the controller.
        // Short names silently fail when a controller cannot resolve the state.
        public static readonly int Spawn = Animator.StringToHash("Base Layer.Spawn");
        public static readonly int Chase = Animator.StringToHash("Base Layer.Fly Forward In Place");
        public static readonly int Attack = Animator.StringToHash("Base Layer.Dash Attack In Place");
        public static readonly int TakeDamage = Animator.StringToHash("Base Layer.Take Damage");
        public static readonly int Die = Animator.StringToHash("Base Layer.Die");
    }
}
