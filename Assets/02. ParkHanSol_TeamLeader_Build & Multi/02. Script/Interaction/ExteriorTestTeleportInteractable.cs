using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class ExteriorTestTeleportInteractable : MonoBehaviour, IInteractable
    {
        public const string DebrisEntryPortalName = "PHS_ExteriorDoorAutoPortal";
        public const string DebrisReturnPortalName = "PHS_ExteriorDebrisReturnPortal";

        [SerializeField] private string destinationId;
        [SerializeField] private NetworkPlayerSector destinationSector = NetworkPlayerSector.Transition;
        [SerializeField] private string interactionPrompt = "Move To Exterior Test Zone";
        [SerializeField, Min(0.5f)] private float serverInteractionDistance = 4f;

        public string InteractionPrompt => interactionPrompt;
        public Transform Destination => FindDestination()?.transform;
        public NetworkPlayerSector DestinationSector => destinationSector;
        public float ServerInteractionDistance => serverInteractionDistance;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !string.IsNullOrWhiteSpace(destinationId) && itemHolder is Component;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=setup_missing portal={name}");
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            if (player == null)
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=player_missing portal={name}");
                return;
            }

            player.RequestLocalPortalTeleport(name);
        }

        internal bool TryResolveServerDestination(
            NetworkPlayerController player,
            out Vector3 position,
            out Quaternion rotation,
            out string reason)
        {
            position = default;
            rotation = default;
            reason = null;
            if (player == null)
            {
                reason = "player_missing";
                return false;
            }

            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(destinationId))
            {
                reason = "portal_inactive_or_destination_id_missing";
                return false;
            }

            if (destinationSector == NetworkPlayerSector.Transition)
            {
                reason = "destination_sector_invalid";
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > serverInteractionDistance)
            {
                reason = "player_out_of_range";
                return false;
            }

            var destination = FindDestination();
            if (destination == null)
            {
                reason = "destination_not_loaded";
                return false;
            }

            position = destination.transform.position;
            rotation = destination.transform.rotation;
            return true;
        }

        private NetworkExteriorPortalDestination FindDestination()
        {
            foreach (var candidate in FindObjectsByType<NetworkExteriorPortalDestination>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (candidate.DestinationId == destinationId)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
