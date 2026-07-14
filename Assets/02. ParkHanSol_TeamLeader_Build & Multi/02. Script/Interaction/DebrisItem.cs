using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisItem : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("value"), Min(1)] private int referenceValue = 100;
        [SerializeField, Min(0.01f)] private float referenceMass = 1f;
        [SerializeField, Min(0.001f)] private float referenceVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float massValueWeight = 0.6f;
        [SerializeField] private bool recalculateMassFromVolume = true;
        [SerializeField, Min(0.01f)] private float materialDensity = 2f;

        private Rigidbody targetRigidbody;
        private float physicalVolume;
        private bool setupValid;

        public float Mass => targetRigidbody == null ? 0f : Mathf.Max(0.1f, targetRigidbody.mass);
        public float PhysicalVolume => physicalVolume;
        public int Value => setupValid ? CalculateValue() : 0;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            setupValid = TryCalculatePhysicalVolume(out physicalVolume);
            if (targetRigidbody == null)
            {
                setupValid = false;
                Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=rigidbody_missing debris={name}");
            }

            if (setupValid && recalculateMassFromVolume)
            {
                targetRigidbody.mass = Mathf.Max(0.1f, physicalVolume * materialDensity);
            }
        }

        private int CalculateValue()
        {
            var massRatio = Mass / Mathf.Max(0.01f, referenceMass);
            var volumeRatio = physicalVolume / Mathf.Max(0.001f, referenceVolume);
            var valueRatio = massRatio * massValueWeight
                + volumeRatio * (1f - massValueWeight);
            return Mathf.Max(1, Mathf.RoundToInt(referenceValue * valueRatio));
        }

        private bool TryCalculatePhysicalVolume(out float volume)
        {
            volume = 0f;
            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=collider_missing debris={name}");
                return false;
            }

            foreach (var targetCollider in colliders)
            {
                if (!TryCalculateColliderVolume(targetCollider, out var colliderVolume))
                {
                    Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=collider_type_unsupported debris={name} collider={targetCollider.GetType().Name}");
                    return false;
                }

                volume += colliderVolume;
            }

            if (volume <= 0.0001f)
            {
                Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=volume_invalid debris={name} volume={volume}");
                return false;
            }

            return true;
        }

        private static bool TryCalculateColliderVolume(Collider targetCollider, out float volume)
        {
            volume = 0f;
            var scale = Abs(targetCollider.transform.lossyScale);
            switch (targetCollider)
            {
                case BoxCollider boxCollider:
                    var boxSize = Vector3.Scale(boxCollider.size, scale);
                    volume = boxSize.x * boxSize.y * boxSize.z;
                    return true;

                case SphereCollider sphereCollider:
                    var radii = scale * sphereCollider.radius;
                    volume = 4f / 3f * Mathf.PI * radii.x * radii.y * radii.z;
                    return true;

                case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                    var meshSize = Vector3.Scale(meshCollider.sharedMesh.bounds.size, scale);
                    volume = meshSize.x * meshSize.y * meshSize.z;
                    return true;

                default:
                    return false;
            }
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
