using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    public sealed class PHS_CuteWhiteGhostJellySquashController : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float squashAmount = 0.18f;
        [SerializeField] private float reboundAmount = 0.08f;
        [SerializeField] private float impactThreshold = 2f;
        [SerializeField] private float recoverySpeed = 8f;

        private Vector3 targetScale = Vector3.one;

        private void Update()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, recoverySpeed * Time.deltaTime);
            targetScale = Vector3.Lerp(targetScale, Vector3.one, recoverySpeed * Time.deltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (visualRoot == null || collision.relativeVelocity.magnitude < impactThreshold)
            {
                return;
            }

            targetScale = new Vector3(1f + reboundAmount, 1f - squashAmount, 1f + reboundAmount);
        }
    }
}
