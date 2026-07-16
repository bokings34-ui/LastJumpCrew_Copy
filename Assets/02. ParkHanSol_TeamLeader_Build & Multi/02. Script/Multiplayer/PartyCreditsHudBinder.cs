using LastJumpCrew.ParkHanSol.Shop;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PartyCreditsHudBinder : MonoBehaviour
    {
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [SerializeField] private MonoBehaviour shopWalletSource;

        private IShopWallet boundWallet;

        private void OnEnable()
        {
            boundWallet = shopWalletSource as IShopWallet;
            if (boundWallet == null)
            {
                Debug.LogError($"PHS_PARTY_CREDITS_HUD_BIND_FAILED reason=shop_wallet_missing binder={name}", this);
                return;
            }

            if (playHudPresenter == null)
            {
                Debug.LogError($"PHS_PARTY_CREDITS_HUD_BIND_FAILED reason=presenter_missing binder={name}", this);
                return;
            }

            boundWallet.CreditsChanged += HandleCreditsChanged;
            HandleCreditsChanged(boundWallet.Credits);
        }

        private void OnDisable()
        {
            UnbindWallet();
        }

        private void UnbindWallet()
        {
            if (boundWallet != null)
            {
                boundWallet.CreditsChanged -= HandleCreditsChanged;
            }

            boundWallet = null;
        }

        private void HandleCreditsChanged(int credits)
        {
            playHudPresenter.SetEconomy(0, credits);
        }
    }
}
