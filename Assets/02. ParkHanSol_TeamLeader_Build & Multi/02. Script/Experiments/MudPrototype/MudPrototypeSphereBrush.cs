using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype
{
    public sealed class MudPrototypeSphereBrush : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 0.5f;
        [SerializeField, Min(0f)] private float strength = 1f;
        [SerializeField] private Color gizmoColor = new(0.8f, 0.95f, 1f, 0.35f);

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Max(0.01f, value);
        }

        public float Strength
        {
            get => strength;
            set => strength = Mathf.Max(0f, value);
        }

        public float Evaluate(Vector3 worldPosition)
        {
            var distance = Vector3.Distance(worldPosition, transform.position);
            var normalizedDistance = Mathf.Clamp01(1f - distance / radius);
            return strength * normalizedDistance * normalizedDistance * (3f - 2f * normalizedDistance);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
