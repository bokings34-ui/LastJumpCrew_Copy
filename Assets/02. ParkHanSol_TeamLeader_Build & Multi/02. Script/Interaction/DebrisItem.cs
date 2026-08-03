using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DebrisItem : MonoBehaviour
    {
        [Tooltip("판매 가격을 가진 데브리 아이템 데이터. Inspector에서 직접 연결한다.")]
        [SerializeField] private UtilityItemDataSO debrisData;
        [SerializeField] private bool recalculateMassFromVolume = true;
        [SerializeField, Min(0.01f)] private float materialDensity = 2f;

        private Rigidbody targetRigidbody;
        private float physicalVolume;
        private bool setupValid;

        public float Mass => targetRigidbody == null ? 0f : Mathf.Max(0.1f, targetRigidbody.mass);
        public float PhysicalVolume => physicalVolume;
        public int Value => setupValid && debrisData != null ? debrisData.Price : 0;

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            setupValid = TryCalculatePhysicalVolume(out physicalVolume);
            if (targetRigidbody == null)
            {
                setupValid = false;
                Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=rigidbody_missing debris={name}");
            }

            if (debrisData == null || debrisData.Price <= 0)
            {
                setupValid = false;
                Debug.LogError($"PHS_DEBRIS_SETUP_FAILED reason=debris_data_invalid debris={name}");
            }

            if (setupValid && recalculateMassFromVolume)
            {
                targetRigidbody.mass = Mathf.Max(0.1f, physicalVolume * materialDensity);
            }
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
