using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    // Attached only to the exterior ship hull colliders.  Debris physics is
    // server-authoritative, so the reflection must also run only on the server.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PHSExteriorHullDebrisBounce : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float restitution = 0.95f;
        [SerializeField, Min(0f)] private float minimumOutgoingSpeed = 3.5f;
        [SerializeField, Min(0f)] private float outwardVelocityBoost = 1.25f;

        private void OnCollisionEnter(Collision collision)
        {
            var body = collision.rigidbody;
            if (body == null
                || !body.TryGetComponent<DebrisItem>(out _)
                || !body.TryGetComponent<NetworkItemPhysicsAuthority>(out var authority)
                || !authority.IsServer
                || collision.contactCount == 0)
            {
                return;
            }

            var normal = collision.GetContact(0).normal;
            var incomingSpeedAlongNormal = Vector3.Dot(body.linearVelocity, normal);
            if (incomingSpeedAlongNormal > 0f)
            {
                normal = -normal;
                incomingSpeedAlongNormal = -incomingSpeedAlongNormal;
            }

            if (incomingSpeedAlongNormal >= 0f)
            {
                return;
            }

            var reflected = Vector3.Reflect(body.linearVelocity, normal)
                * Mathf.Clamp01(restitution);
            var reflectedSpeed = reflected.magnitude;
            if (reflectedSpeed < minimumOutgoingSpeed)
            {
                reflected = normal * minimumOutgoingSpeed;
            }

            reflected += normal * outwardVelocityBoost;

            body.linearVelocity = reflected;
            body.WakeUp();
        }
    }
}
