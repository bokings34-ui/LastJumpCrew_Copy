using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TravelConsoleExecuteInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NetworkTravelConsoleController console;

        public string InteractionPrompt => console != null
            ? console.ActionPrompt
            : "이동 실행";

        public bool CanInteract(IItemHolder itemHolder)
        {
            return console != null && console.CanExecute(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning("PHS_TRAVEL_EXECUTE_FAILED reason=interaction_invalid", this);
                return;
            }

            console.Execute(itemHolder);
        }
    }
}
