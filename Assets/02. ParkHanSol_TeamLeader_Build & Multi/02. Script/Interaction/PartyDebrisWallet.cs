using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class PartyDebrisWallet : MonoBehaviour, IPartyCreditsWallet
    {
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [SerializeField, Min(0)] private int credits;

        public int Credits => credits;

        private void Awake()
        {
            RefreshHud();
        }

        public void AddCredits(int value)
        {
            if (value <= 0)
            {
                Debug.LogError($"PHS_DEBRIS_WALLET_ADD_FAILED reason=invalid_value wallet={name} value={value}");
                return;
            }

            credits += value;
            RefreshHud();
            Debug.Log($"PHS_DEBRIS_WALLET_CREDITS_ADDED wallet={name} value={value} total={credits}");
        }

        private void RefreshHud()
        {
            if (playHudPresenter == null)
            {
                Debug.LogError($"PHS_DEBRIS_WALLET_SETUP_FAILED reason=play_hud_presenter_missing wallet={name}");
                return;
            }

            playHudPresenter.SetEconomy(0, credits);
        }
    }
}
