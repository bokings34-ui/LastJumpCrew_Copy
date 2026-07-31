using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Shop;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class NetworkScenePortalInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string destinationSceneName;
        [SerializeField] private ShopSceneTransitionMode shopTransitionMode;
        [SerializeField] private string interactionPrompt = "외부 상점 입장";
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 4f;
        [SerializeField] private bool requiresPartyVote = true;

        [Header("Optional Entry Zone")]
        [SerializeField] private bool requestOnZoneEnter;
        [SerializeField] private BoxCollider[] requestZones;
        [SerializeField] private bool allowManualInteraction = true;
        [SerializeField, Min(0.1f)] private float requestCooldownSeconds = 1f;

        private float nextLocalRequestTime;

        public string InteractionPrompt => interactionPrompt;
        public string DestinationSceneName => destinationSceneName;
        public bool RequiresPartyVote => requiresPartyVote;
        public bool RequestsOnZoneEnter => requestOnZoneEnter;
        public BoxCollider[] RequestZones => requestZones;
        public bool AllowsManualInteraction => allowManualInteraction;

        private void Awake()
        {
            if (!requestOnZoneEnter)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(destinationSceneName)
                || !HasValidRequestZones())
            {
                Debug.LogError(
                    $"PHS_NETWORK_PORTAL_ZONE_SETUP_FAILED reason=trigger_invalid portal={name}",
                    this);
            }
        }

        public bool MatchesServerRequest(
            Transform playerTransform,
            string requestedSceneName,
            ShopSceneTransitionMode requestedTransitionMode)
        {
            return playerTransform != null
                && destinationSceneName == requestedSceneName
                && ResolveTransitionMode() == requestedTransitionMode
                && IsPlayerInRequestRange(playerTransform.position);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (!allowManualInteraction
                || string.IsNullOrWhiteSpace(destinationSceneName)
                || itemHolder is not Component holderComponent)
            {
                return false;
            }

            return holderComponent.GetComponent<NetworkPlayerController>() != null;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=setup_missing portal={name}");
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            player.RequestGameplaySceneTransition(destinationSceneName, ResolveTransitionMode());
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!requestOnZoneEnter
                || !HasValidRequestZones()
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
                    $"PHS_NETWORK_PORTAL_ZONE_FAILED reason=life_state_missing player={player.name} portal={name}",
                    player);
                return;
            }

            if (!lifeState.IsAlive)
            {
                return;
            }

            nextLocalRequestTime = Time.unscaledTime + requestCooldownSeconds;
            player.RequestGameplaySceneTransition(destinationSceneName, ResolveTransitionMode());
        }

        private bool IsPlayerInRequestRange(Vector3 playerPosition)
        {
            if (!requestOnZoneEnter)
            {
                return Vector3.Distance(playerPosition, transform.position) <= serverInteractionDistance;
            }

            foreach (var zone in requestZones)
            {
                if (zone != null
                    && Vector3.Distance(playerPosition, zone.ClosestPoint(playerPosition))
                    <= serverInteractionDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasValidRequestZones()
        {
            if (requestZones == null || requestZones.Length == 0)
            {
                return false;
            }

            foreach (var zone in requestZones)
            {
                if (zone == null || zone.gameObject != gameObject || !zone.isTrigger)
                {
                    return false;
                }
            }

            return true;
        }

        private ShopSceneTransitionMode ResolveTransitionMode()
        {
            if (requestOnZoneEnter)
            {
                return shopTransitionMode;
            }

            var mapRuntime = FindAnyObjectByType<PHSMapRuntimeContext>(FindObjectsInactive.Include);
            return mapRuntime != null && mapRuntime.KeepShopPortalAlwaysActive
                ? ShopSceneTransitionMode.None
                : shopTransitionMode;
        }
    }
}
