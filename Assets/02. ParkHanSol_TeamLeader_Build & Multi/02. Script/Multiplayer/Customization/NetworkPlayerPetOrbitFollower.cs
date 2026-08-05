using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerPetOrbitFollower : MonoBehaviour
    {
        [SerializeField] private Transform orbitCenter;
        [FormerlySerializedAs("orbitRadius")]
        [SerializeField, Min(0f)] private float trailingDistance = 0.58f;
        [FormerlySerializedAs("orbitHeight")]
        [SerializeField] private float trailingHeight = 0.18f;
        [SerializeField] private float trailingSideOffset = 0.35f;

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

            transform.position = orbitCenter.TransformPoint(
                new Vector3(trailingSideOffset, trailingHeight, -trailingDistance));
        }

        private void OnValidate()
        {
            trailingDistance = Mathf.Max(0f, trailingDistance);
        }
    }
}
