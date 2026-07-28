using LastJumpCrew.ParkHanSol.Interaction;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public sealed class ShopLocalProductHudPresenter : MonoBehaviour
    {
        [SerializeField] private NetworkShopStockRegistry stockRegistry;
        [SerializeField] private TempPlayerInteractionScanner interactionScanner;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private CanvasGroup productPanel;
        [SerializeField] private TMP_Text productNameText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text pickupPromptText;
        [SerializeField, Min(0.1f)] private float displayRange = 2.5f;

        private NetworkObject playerNetworkObject;
        private IShopStockPresentation currentPresentation;

        public void BindStockRegistry(NetworkShopStockRegistry registry)
        {
            stockRegistry = registry;
            if (stockRegistry == null)
            {
                Hide();
            }
        }

        public void BindLocalPlayer(
            TempPlayerInteractionScanner scanner,
            Camera playerCamera)
        {
            interactionScanner = scanner;
            interactionCamera = playerCamera;
            playerNetworkObject = interactionScanner == null
                ? null
                : interactionScanner.GetComponent<NetworkObject>();
            if (interactionScanner == null || interactionCamera == null)
            {
                Hide();
            }
        }

        private void Awake()
        {
            if (interactionScanner != null)
            {
                playerNetworkObject = interactionScanner.GetComponent<NetworkObject>();
            }

            Hide();
        }

        private void Update()
        {
            if (!CanPresentForLocalPlayer())
            {
                Hide();
                return;
            }

            var nearest = GetNearestAvailablePresentation();
            if (nearest == null)
            {
                Hide();
                return;
            }

            Show(nearest);
        }

        private bool CanPresentForLocalPlayer()
        {
            if (stockRegistry == null
                || interactionScanner == null
                || interactionCamera == null
                || productPanel == null
                || priceText == null
                || !interactionCamera.isActiveAndEnabled)
            {
                return false;
            }

            return playerNetworkObject == null
                || !playerNetworkObject.IsSpawned
                || playerNetworkObject.IsOwner;
        }

        private IShopStockPresentation GetNearestAvailablePresentation()
        {
            var slots = stockRegistry.DisplaySlots;
            if (slots == null)
            {
                return null;
            }

            var origin = interactionCamera.transform.position;
            var maximumDistanceSquared = displayRange * displayRange;
            var nearestDistanceSquared = float.PositiveInfinity;
            IShopStockPresentation nearest = null;
            foreach (var slot in slots)
            {
                if (slot == null
                    || !slot.IsInStock
                    || slot.CurrentProduct == null
                    || slot.PresentationAnchor == null)
                {
                    continue;
                }

                var distanceSquared =
                    (slot.PresentationAnchor.position - origin).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared
                    || distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                nearest = slot;
            }

            return nearest;
        }

        private void Show(IShopStockPresentation presentation)
        {
            var product = presentation.CurrentProduct;
            if (product == null || !presentation.IsInStock)
            {
                Hide();
                return;
            }

            currentPresentation = presentation;
            productPanel.alpha = 1f;
            productPanel.interactable = false;
            productPanel.blocksRaycasts = false;
            ShowPriceOnly(product.PurchasePrice);
        }

        private void Hide()
        {
            currentPresentation = null;
            if (productPanel != null)
            {
                productPanel.alpha = 0f;
                productPanel.interactable = false;
                productPanel.blocksRaycasts = false;
            }

            if (productNameText != null)
            {
                productNameText.text = string.Empty;
            }

            if (priceText != null)
            {
                priceText.text = string.Empty;
            }

            if (pickupPromptText != null)
            {
                pickupPromptText.text = string.Empty;
            }
        }

        private void ShowPriceOnly(int price)
        {
            if (productNameText != null)
            {
                productNameText.text = string.Empty;
                productNameText.gameObject.SetActive(false);
            }

            if (priceText != null)
            {
                priceText.text = $"${price}";
                priceText.gameObject.SetActive(true);
            }

            if (pickupPromptText != null)
            {
                pickupPromptText.text = string.Empty;
                pickupPromptText.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
