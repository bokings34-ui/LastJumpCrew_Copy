using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Physical checkout button that delegates payment and delivery to its configured checkout zone.</summary>
    public sealed class ShopCheckoutButtonInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private ShopCheckoutZone checkoutZone;
        [SerializeField] private ShopCheckoutButtonPressVisual pressVisual;
        [SerializeField] private string interactionPrompt = "Calculate & Deliver";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return checkoutZone != null && checkoutZone.CanCheckout();
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (checkoutZone == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_BUTTON_FAILED reason=checkout_zone_missing button={name}");
                return;
            }

            var accepted = checkoutZone.TryCheckout();
            pressVisual?.Play(accepted);
        }
    }
}
