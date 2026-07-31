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

        [Header("Optional Door Trigger")]
        [SerializeField] private bool teleportOnTriggerEnter;
        [SerializeField] private BoxCollider doorTrigger;
        [SerializeField] private bool allowManualInteraction = true;
        [SerializeField, Min(0.1f)] private float requestCooldownSeconds = 1f;

        private float nextLocalRequestTime;

        public string InteractionPrompt => interactionPrompt;
        public Transform Destination => destination;
        public NetworkPlayerSector DestinationSector => destinationSector;
        public bool TeleportsOnTriggerEnter => teleportOnTriggerEnter;
        public BoxCollider DoorTrigger => doorTrigger;
        public bool AllowsManualInteraction => allowManualInteraction;

        private void Awake()
        {
            if (!teleportOnTriggerEnter)
            {
                return;
            }

            if (destination == null
                || destinationSector == NetworkPlayerSector.Transition
                || doorTrigger == null
                || doorTrigger.gameObject != gameObject
                || !doorTrigger.isTrigger)
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_TRIGGER_SETUP_FAILED reason=trigger_invalid portal={name}",
                    this);
            }
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return allowManualInteraction && destination != null && itemHolder is Component;
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

        private void OnTriggerEnter(Collider other)
        {
            if (!teleportOnTriggerEnter
                || doorTrigger == null
                || !doorTrigger.isTrigger
                || Time.unscaledTime < nextLocalRequestTime)
            {
                return;
            }

            var player = other.GetComponent<NetworkPlayerController>();
            if (player == null || !player.IsSpawned || !player.IsOwner)
            {
                return;
            }

            var lifeState = other.GetComponent<NetworkPlayerLifeState>();
            if (lifeState == null)
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_TRIGGER_FAILED reason=life_state_missing player={player.name} portal={name}",
                    player);
                return;
            }

            if (!lifeState.IsAlive)
            {
                return;
            }

            nextLocalRequestTime = Time.unscaledTime + requestCooldownSeconds;
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
