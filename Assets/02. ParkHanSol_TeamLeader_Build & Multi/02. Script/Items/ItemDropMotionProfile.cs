using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_ItemDropMotionProfile",
        menuName = "LastJumpCrew/ParkHanSol/Item Drop Motion Profile")]
    public sealed class ItemDropMotionProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float forwardSpeed = 0.65f;
        [SerializeField, Min(0f)] private float downwardSpeed = 0.25f;
        [SerializeField] private Vector3 localAngularVelocity = new(1.1f, 0.35f, 0.75f);

        public bool TryApply(Rigidbody targetRigidbody, Quaternion sourceRotation)
        {
            if (targetRigidbody == null)
            {
                return false;
            }

            targetRigidbody.isKinematic = false;
            targetRigidbody.detectCollisions = true;
            targetRigidbody.linearVelocity =
                sourceRotation * Vector3.forward * forwardSpeed
                + Vector3.down * downwardSpeed;
            targetRigidbody.angularVelocity =
                sourceRotation * localAngularVelocity;
            targetRigidbody.WakeUp();
            return true;
        }
    }
}
