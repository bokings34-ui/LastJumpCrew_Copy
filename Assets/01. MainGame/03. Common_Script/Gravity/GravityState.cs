using UnityEngine;

namespace LastJumpCrew.Common
{
    [System.Serializable]
    public struct GravityState
    {
        [SerializeField] private GravityMode mode;
        [SerializeField] private int priority;
        [SerializeField] private Vector3 gravityDirection;
        [SerializeField] private float gravityStrength;

        public GravityMode Mode => mode;
        public int Priority => priority;
        public Vector3 GravityDirection => gravityDirection.sqrMagnitude <= 0.0001f ? Vector3.down : gravityDirection.normalized;
        public float GravityStrength => Mathf.Max(0f, gravityStrength);

        public GravityState(GravityMode mode, int priority, Vector3 gravityDirection, float gravityStrength)
        {
            this.mode = mode;
            this.priority = priority;
            this.gravityDirection = gravityDirection.sqrMagnitude <= 0.0001f ? Vector3.down : gravityDirection.normalized;
            this.gravityStrength = Mathf.Max(0f, gravityStrength);
        }

        public static GravityState ZeroGravity(int priority = int.MinValue)
        {
            return new GravityState(GravityMode.ZeroGravity, priority, Vector3.down, 0f);
        }

        public static GravityState Spacewalk(int priority = int.MinValue)
        {
            return new GravityState(GravityMode.Spacewalk, priority, Vector3.down, 0f);
        }
    }
}
