using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class ShopTransitionVotePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private ParkHanSolLobbyPanelTransition panelTransition;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button agreeButton;
        [SerializeField] private Button declineButton;

        private bool panelVisible;
        private bool localVoteSubmitted;

        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            agreeButton.onClick.AddListener(Agree);
            declineButton.onClick.AddListener(Decline);
            panelTransition.SetVisible(false, true);
        }

        private void OnDestroy()
        {
            if (agreeButton != null)
            {
                agreeButton.onClick.RemoveListener(Agree);
            }

            if (declineButton != null)
            {
                declineButton.onClick.RemoveListener(Decline);
            }

            if (panelVisible)
            {
                SetVotingInputBlocked(false);
            }
        }

        private void Update()
        {
            var vote = NetworkShopTransitionVoteCoordinator.Instance;
            var shouldShow = vote != null && vote.IsSpawned && vote.IsVoteActive;
            if (shouldShow != panelVisible)
            {
                panelVisible = shouldShow;
                localVoteSubmitted = false;
                panelTransition.SetVisible(shouldShow);
                SetVotingInputBlocked(shouldShow);
            }

            if (!shouldShow)
            {
                return;
            }

            titleText.text = vote.IsShopExitVote
                ? "LEAVE SHOP?"
                : "ENTER SHOP?";
            statusText.text = localVoteSubmitted
                ? $"VOTE LOCKED  ·  AGREE {vote.AgreeCount}/{vote.RequiredAgreeCount}"
                : $"AGREE {vote.AgreeCount}/{vote.RequiredAgreeCount}  ·  PARTY {vote.EligiblePlayerCount}";
            agreeButton.interactable = !localVoteSubmitted;
            declineButton.interactable = !localVoteSubmitted;
        }

        private void Agree()
        {
            SubmitVote(true);
        }

        private void Decline()
        {
            SubmitVote(false);
        }

        private void SubmitVote(bool agree)
        {
            var vote = NetworkShopTransitionVoteCoordinator.Instance;
            if (vote == null || !vote.IsVoteActive)
            {
                Debug.LogError("PHS_SHOP_VOTE_UI_FAILED reason=vote_inactive", this);
                return;
            }

            vote.SubmitLocalVote(agree);
            localVoteSubmitted = true;
        }

        private bool ValidateReferences()
        {
            if (panel != null
                && panelTransition != null
                && panelTransition.gameObject == panel
                && titleText != null
                && statusText != null
                && agreeButton != null
                && declineButton != null)
            {
                return true;
            }

            Debug.LogError("PHS_SHOP_VOTE_UI_SETUP_FAILED reason=inspector_reference_missing", this);
            return false;
        }

        private static void SetVotingInputBlocked(bool blocked)
        {
            var pauseMenu = FindAnyObjectByType<ParkHanSolPauseMenuController>(FindObjectsInactive.Include);
            if (!blocked && pauseMenu != null && pauseMenu.IsOpen)
            {
                return;
            }

            foreach (var player in FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None))
            {
                if (player.IsOwner)
                {
                    player.SetPauseInputBlocked(blocked);
                }
            }

            if (blocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
