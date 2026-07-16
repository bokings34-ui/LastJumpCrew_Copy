using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PartyCreditsHudBinder : MonoBehaviour
    {
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;

        private SessionPartyCreditsWallet boundWallet;

        private void OnEnable()
        {
            BindActiveWallet();
        }

        private void OnDisable()
        {
            UnbindWallet();
        }

        private void Update()
        {
            if (boundWallet != SessionPartyCreditsWallet.Instance)
            {
                BindActiveWallet();
            }
        }

        private void BindActiveWallet()
        {
            UnbindWallet();
            boundWallet = SessionPartyCreditsWallet.Instance;
            if (boundWallet == null || playHudPresenter == null)
            {
                return;
            }

            boundWallet.CreditsChanged += HandleCreditsChanged;
            HandleCreditsChanged(boundWallet.Credits);
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
