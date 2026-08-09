using LastJumpCrew.ParkHanSol.Shop;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialCreditGainObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private MonoBehaviour shopWalletSource;

        private IShopWallet shopWallet;
        private int baselineCredits;

        private void OnEnable()
        {
            BindWallet();
        }

        private void OnDisable()
        {
            UnbindWallet();
        }

        public override void SetObjectiveActive(bool active)
        {
            BindWallet();
            baselineCredits = shopWallet?.Credits ?? 0;
            base.SetObjectiveActive(active);
        }

        private void BindWallet()
        {
            var nextWallet = shopWalletSource as IShopWallet;
            if (ReferenceEquals(shopWallet, nextWallet))
            {
                return;
            }

            UnbindWallet();
            shopWallet = nextWallet;
            if (shopWallet != null)
            {
                shopWallet.CreditsChanged += HandleCreditsChanged;
            }
        }

        private void UnbindWallet()
        {
            if (shopWallet != null)
            {
                shopWallet.CreditsChanged -= HandleCreditsChanged;
                shopWallet = null;
            }
        }

        private void HandleCreditsChanged(int credits)
        {
            if (CanComplete && credits > baselineCredits)
            {
                CompleteObjective();
            }
        }
    }
}
