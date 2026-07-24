using TinyGiantStudio.Text;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopItemPricePresentation : MonoBehaviour
    {
        [SerializeField] private GameObject priceRoot;
        [SerializeField] private Modular3DText priceText;

        public bool TryShow(int credits)
        {
            if (credits <= 0)
            {
                Debug.LogError($"PHS_ITEM_PRICE_FAILED reason=price_invalid item={name} price={credits}", this);
                return false;
            }

            if (priceRoot == null || priceText == null)
            {
                Debug.LogError($"PHS_ITEM_PRICE_FAILED reason=reference_missing item={name}", this);
                return false;
            }

            priceRoot.SetActive(true);
            priceText.UpdateText($"{credits} CR");
            return true;
        }

        public void Hide()
        {
            if (priceText != null)
                priceText.UpdateText(string.Empty);

            if (priceRoot != null)
                priceRoot.SetActive(false);
        }
    }
}
