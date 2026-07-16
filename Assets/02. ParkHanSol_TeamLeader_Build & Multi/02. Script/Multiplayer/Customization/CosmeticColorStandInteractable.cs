using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public sealed class CosmeticColorStandInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Color bodyColor = Color.white;
        [SerializeField] private string interactionPrompt = "Apply Color";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return TryResolveCustomization(itemHolder, out _);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!TryResolveCustomization(itemHolder, out var customization))
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_STAND_FAILED reason=customization_missing stand={name}", this);
                return;
            }

            customization.RequestSetBodyColor(bodyColor);
        }

        private static bool TryResolveCustomization(
            IItemHolder itemHolder,
            out NetworkPlayerCustomization customization)
        {
            customization = null;
            return itemHolder is Component itemHolderComponent &&
                   itemHolderComponent.TryGetComponent(out customization);
        }
    }
}
