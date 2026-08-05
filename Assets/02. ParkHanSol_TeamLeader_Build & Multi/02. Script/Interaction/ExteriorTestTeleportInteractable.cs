using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class ExteriorTestTeleportInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform destination;
        [SerializeField] private NetworkPlayerSector destinationSector = NetworkPlayerSector.Transition;
        [SerializeField] private string interactionPrompt = "Move To Exterior Test Zone";
        [SerializeField, Min(0.5f)] private float serverInteractionDistance = 4f;

        public string InteractionPrompt => interactionPrompt;
        public Transform Destination => destination;
        public NetworkPlayerSector DestinationSector => destinationSector;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return destination != null && itemHolder is Component;
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

            if (!isActiveAndEnabled || destination == null)
            {
                reason = "portal_inactive_or_destination_missing";
                return false;
            }

            if (destinationSector == NetworkPlayerSector.Transition)
            {
                reason = "destination_sector_invalid";
                return false;
            }

            if (gameObject.scene != player.gameObject.scene)
            {
                reason = "scene_mismatch";
                return false;
            }

            if (Vector3.Distance(player.transform.position, transform.position) > serverInteractionDistance)
            {
                reason = "player_out_of_range";
                return false;
            }

            position = destination.position;
            rotation = destination.rotation;
            return true;
        }
    }
}
