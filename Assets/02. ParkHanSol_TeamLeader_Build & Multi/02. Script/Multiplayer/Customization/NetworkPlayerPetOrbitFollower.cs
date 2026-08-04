using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerPetOrbitFollower : MonoBehaviour
    {
        [SerializeField] private Transform orbitCenter;
        [SerializeField, Min(0f)] private float orbitRadius = 0.58f;
        [SerializeField] private float orbitHeight = 0.18f;
        [SerializeField] private float orbitDegreesPerSecond = 80f;
        [SerializeField] private float phaseOffsetDegrees = 205f;

        private void Awake()
        {
            if (orbitCenter == null)
            {
                orbitCenter = transform.parent;
            }
        }

        private void LateUpdate()
        {
            if (orbitCenter == null)
            {
                return;
            }

            var angle = (phaseOffsetDegrees + Time.time * orbitDegreesPerSecond) * Mathf.Deg2Rad;
            var orbitOffset = new Vector3(
                Mathf.Cos(angle) * orbitRadius,
                orbitHeight,
                Mathf.Sin(angle) * orbitRadius);
            transform.position = orbitCenter.TransformPoint(orbitOffset);
        }

        private void OnValidate()
        {
            orbitRadius = Mathf.Max(0f, orbitRadius);
        }
    }
}
