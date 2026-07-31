using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class ShopTransitionVotePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private ParkHanSolLobbyPanelTransition panelTransition;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;

        private bool panelVisible;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            panelTransition.SetVisible(false, true);
        }

        private void Update()
        {
            var vote = NetworkShopTransitionVoteCoordinator.Instance;
            var shouldShow = vote != null && vote.IsSpawned && vote.IsVoteActive;
            if (shouldShow != panelVisible)
            {
                panelVisible = shouldShow;
                panelTransition.SetVisible(shouldShow);
            }

            if (!shouldShow)
            {
                return;
            }

            titleText.text = vote.IsShopExitVote
                ? "상점 퇴장 희망"
                : "상점 입장 희망";
            var action = vote.IsShopExitVote ? "퇴장" : "입장";
            statusText.text = $"플레이어가 상점 {(vote.IsShopExitVote ? "퇴장" : "입장")}을 희망합니다\n"
                + $"{action} 구역에 모이기  {vote.AgreeCount}/{vote.RequiredAgreeCount}";
        }

        private bool ValidateReferences()
        {
            if (panel != null
                && panelTransition != null
                && panelTransition.gameObject == panel
                && titleText != null
                && statusText != null)
            {
                return true;
            }

            Debug.LogError("PHS_SHOP_VOTE_UI_SETUP_FAILED reason=inspector_reference_missing", this);
            return false;
        }

    }
}
