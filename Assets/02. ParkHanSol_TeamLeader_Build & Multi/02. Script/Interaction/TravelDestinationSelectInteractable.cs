using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TravelDestinationSelectInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NetworkTravelConsoleController console;
        [SerializeField] private TravelConsoleDestination destination;
        [SerializeField] private string interactionPrompt = "목적지 선택";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return console != null
                && itemHolder is Component
                && console.CanSelectDestination(destination);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=interaction_invalid destination={destination}", this);
                return;
            }

            console.RequestSelectDestination(itemHolder, destination);
        }
    }
}
