using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialGrappleAnchorObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private NetworkPlayerGrappleController grappleController;
        [SerializeField] private Collider anchorCollider;

        private void OnEnable()
        {
            if (grappleController != null)
            {
                grappleController.GrappleLatched += HandleGrappleLatched;
            }
        }

        private void OnDisable()
        {
            if (grappleController != null)
            {
                grappleController.GrappleLatched -= HandleGrappleLatched;
            }
        }

        private void HandleGrappleLatched(Collider latchedCollider)
        {
            if (!CanComplete
                || anchorCollider == null
                || latchedCollider == null)
            {
                return;
            }

            var latchedTransform = latchedCollider.transform;
            var anchorTransform = anchorCollider.transform;
            if (latchedCollider == anchorCollider
                || latchedTransform.IsChildOf(anchorTransform)
                || anchorTransform.IsChildOf(latchedTransform))
            {
                CompleteObjective();
            }
        }
    }
}
