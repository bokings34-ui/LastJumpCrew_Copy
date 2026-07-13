using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class GravityGeneratorInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ShipGravityZoneController gravityController;
        [SerializeField] private string interactionPrompt = "Disable Ship Gravity";

        private bool isDisabled;

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return gravityController != null && !isDisabled;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            gravityController.TurnGravityOff();
            isDisabled = true;
            Debug.Log($"PHS_GRAVITY_GENERATOR_DISABLED generator={name}");
        }
    }
}
