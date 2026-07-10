using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype
{
    public sealed class SquishyRagdollJellyBounceBody : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private Rigidbody[] sharedBodies;
        [SerializeField, Min(0.1f)] private float minImpactSpeed = 1.2f;
        [SerializeField, Min(0f)] private float reboundMultiplier = 1.35f;
        [SerializeField, Min(0.1f)] private float maxVelocityChange = 5.5f;
        [SerializeField, Range(0f, 1f)] private float sharedBodyRatio = 0.28f;
        [SerializeField, Min(0f)] private float angularKick = 2.5f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.07f;
        [SerializeField, Range(0f, 1f)] private float floorNormalYThreshold = 0.55f;
        [SerializeField, Min(0f)] private float minFloorInwardSpeed = 0.45f;
        [SerializeField, Min(0)] private int maxFloorBounces = 3;
        [SerializeField, Min(0f)] private float floorReboundMultiplier = 2.2f;
        [SerializeField, Range(0f, 1f)] private float floorReboundDecay = 0.55f;
        [SerializeField, Min(0f)] private float settledLinearDamping = 3.5f;
        [SerializeField, Min(0f)] private float settledAngularDamping = 4.5f;

        private bool jellyActive;
        private bool setupErrorReported;
        private float lastBounceTime = -999f;
        private int floorBounceCount;

        public void Configure(
            Rigidbody ownerBody,
            Rigidbody[] allBodies,
            float impactSpeed,
            float rebound,
            float maxChange,
            float sharedRatio,
            float torqueKick,
            float cooldown,
            float floorNormalThreshold,
            int floorBounces,
            float floorRebound,
            float floorDecay,
            float settleLinear,
            float settleAngular)
        {
            body = ownerBody;
            sharedBodies = allBodies;
            minImpactSpeed = impactSpeed;
            reboundMultiplier = rebound;
            maxVelocityChange = maxChange;
            sharedBodyRatio = sharedRatio;
            angularKick = torqueKick;
            cooldownSeconds = cooldown;
            floorNormalYThreshold = floorNormalThreshold;
            maxFloorBounces = floorBounces;
            floorReboundMultiplier = floorRebound;
            floorReboundDecay = floorDecay;
            settledLinearDamping = settleLinear;
            settledAngularDamping = settleAngular;
        }

        public void SetJellyActive(bool active)
        {
            jellyActive = active;
            if (!active)
            {
                lastBounceTime = -999f;
            }

            floorBounceCount = 0;
        }

        private void Awake()
        {
            ValidateSetup();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryApplyJellyBounce(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TryApplyJellyBounce(collision);
        }

        private void TryApplyJellyBounce(Collision collision)
        {
            if (!jellyActive || !ValidateSetup())
            {
                return;
            }

            if (Time.time - lastBounceTime < cooldownSeconds)
            {
                return;
            }

            var impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minImpactSpeed)
            {
                return;
            }

            var normal = GetAverageNormal(collision);
            var isFloorHit = normal.y >= floorNormalYThreshold && IsFloorCollision(collision);
            if (isFloorHit && floorBounceCount >= maxFloorBounces)
            {
                SettleBody();
                return;
            }

            var inwardSpeed = -Vector3.Dot(body.linearVelocity, normal);
            if (isFloorHit)
            {
                inwardSpeed = Mathf.Max(inwardSpeed, Vector3.Dot(collision.relativeVelocity, -normal));
            }

            if (inwardSpeed <= 0f)
            {
                return;
            }

            if (isFloorHit && inwardSpeed < minFloorInwardSpeed)
            {
                return;
            }

            var activeRebound = GetActiveReboundMultiplier(isFloorHit);
            var velocityChange = normal * Mathf.Min(inwardSpeed * activeRebound, maxVelocityChange);
            body.AddForce(velocityChange, ForceMode.VelocityChange);

            for (var i = 0; i < sharedBodies.Length; i++)
            {
                var sharedBody = sharedBodies[i];
                if (sharedBody == null || sharedBody == body)
                {
                    continue;
                }

                sharedBody.AddForce(velocityChange * sharedBodyRatio, ForceMode.VelocityChange);
            }

            if (angularKick > 0f)
            {
                body.AddTorque(Vector3.Cross(normal, Vector3.up + Vector3.forward).normalized * angularKick, ForceMode.VelocityChange);
            }

            if (isFloorHit)
            {
                floorBounceCount++;
                if (floorBounceCount >= maxFloorBounces)
                {
                    SettleBody();
                }
            }

            lastBounceTime = Time.time;
        }

        private float GetActiveReboundMultiplier(bool isFloorHit)
        {
            if (!isFloorHit)
            {
                return reboundMultiplier;
            }

            return floorReboundMultiplier * Mathf.Pow(floorReboundDecay, floorBounceCount);
        }

        private void SettleBody()
        {
            body.linearDamping = Mathf.Max(body.linearDamping, settledLinearDamping);
            body.angularDamping = Mathf.Max(body.angularDamping, settledAngularDamping);
        }

        private static Vector3 GetAverageNormal(Collision collision)
        {
            var normal = Vector3.zero;
            var contactCount = collision.contactCount;
            for (var i = 0; i < contactCount; i++)
            {
                normal += collision.GetContact(i).normal;
            }

            return contactCount > 0 ? normal.normalized : Vector3.up;
        }

        private static bool IsFloorCollision(Collision collision)
        {
            var otherName = collision.gameObject.name;
            return otherName.Contains("Ground") || otherName.Contains("Floor");
        }

        private bool ValidateSetup()
        {
            if (body == null)
            {
                LogSetupError("body_missing");
                return false;
            }

            if (sharedBodies == null || sharedBodies.Length == 0)
            {
                LogSetupError("sharedBodies_missing");
                return false;
            }

            return true;
        }

        private void LogSetupError(string reason)
        {
            if (setupErrorReported)
            {
                return;
            }

            setupErrorReported = true;
            Debug.LogError($"PHS_JELLY_BOUNCE_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
