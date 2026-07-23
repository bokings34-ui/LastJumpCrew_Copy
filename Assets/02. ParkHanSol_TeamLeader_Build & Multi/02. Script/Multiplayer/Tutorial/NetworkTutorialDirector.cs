using LastJumpCrew.ParkHanSol.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialDirector : MonoBehaviour
    {
        private enum TutorialStep : byte
        {
            Movement,
            Thruster,
            Grapple,
            ItemPickup,
            ItemDrop,
            ItemSwap,
            Interaction,
            Complete
        }

        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private NetworkPlayerGrappleController grappleController;
        [SerializeField] private TempPlayerItemHolder itemHolder;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private GameObject completionPanel;
        [SerializeField] private Button returnToLobbyButton;
        [SerializeField] private string lobbySceneName = "ParkHanSol_LobbyScene";
        [SerializeField, Min(1f)] private float movementDistance = 3f;

        private readonly INetworkSessionExitService sessionExitService =
            new NetworkSessionExitService();
        private TutorialStep currentStep;
        private Vector3 movementStartPosition;
        private bool swapArmed;
        private string swapBaselineItemId;
        private bool isExiting;

        public bool IsWaitingForInteraction =>
            currentStep == TutorialStep.Interaction;

        private void Awake()
        {
            if (playerController == null
                || grappleController == null
                || itemHolder == null
                || instructionText == null
                || completionPanel == null
                || returnToLobbyButton == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=inspector_reference_missing director={name}",
                    this);
                enabled = false;
                return;
            }

            movementStartPosition = playerController.transform.position;
            completionPanel.SetActive(false);
            returnToLobbyButton.onClick.AddListener(ReturnToLobby);
            SetStep(TutorialStep.Movement);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDestroy()
        {
            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.onClick.RemoveListener(ReturnToLobby);
            }
        }

        private void Update()
        {
            switch (currentStep)
            {
                case TutorialStep.Movement:
                    var displacement = playerController.transform.position
                        - movementStartPosition;
                    displacement.y = 0f;
                    if (displacement.magnitude >= movementDistance)
                    {
                        SetStep(TutorialStep.Thruster);
                    }
                    break;
                case TutorialStep.Thruster:
                    if (playerController.GravityMode
                            != NetworkPlayerGravityMode.ShipGravity
                        && playerController.HasMoveInput)
                    {
                        SetStep(TutorialStep.Grapple);
                    }
                    break;
                case TutorialStep.Grapple:
                    if (grappleController.IsGrappleActive)
                    {
                        SetStep(TutorialStep.ItemPickup);
                    }
                    break;
                case TutorialStep.ItemPickup:
                    if (itemHolder.HasItem)
                    {
                        SetStep(TutorialStep.ItemDrop);
                    }
                    break;
                case TutorialStep.ItemDrop:
                    if (!itemHolder.HasItem)
                    {
                        swapArmed = false;
                        swapBaselineItemId = null;
                        SetStep(TutorialStep.ItemSwap);
                    }
                    break;
                case TutorialStep.ItemSwap:
                    UpdateItemSwapStep();
                    break;
            }
        }

        public void ReportInteraction()
        {
            if (currentStep == TutorialStep.Interaction)
            {
                SetStep(TutorialStep.Complete);
            }
        }

        private void UpdateItemSwapStep()
        {
            if (!itemHolder.HasItem)
            {
                swapArmed = false;
                swapBaselineItemId = null;
                return;
            }

            var itemId = itemHolder.CurrentItemPrefabData?.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            if (!swapArmed)
            {
                swapArmed = true;
                swapBaselineItemId = itemId;
                return;
            }

            if (!string.Equals(itemId, swapBaselineItemId,
                    System.StringComparison.Ordinal))
            {
                SetStep(TutorialStep.Interaction);
            }
        }

        private void SetStep(TutorialStep step)
        {
            currentStep = step;
            instructionText.text = step switch
            {
                TutorialStep.Movement => "MOVE  ·  WASD",
                TutorialStep.Thruster => "ENTER ZERO-G  ·  MOVE + SPACE / SHIFT",
                TutorialStep.Grapple => "GRAPPLE THE ORANGE TARGET  ·  HOLD Q",
                TutorialStep.ItemPickup => "PICK UP AN ITEM  ·  F",
                TutorialStep.ItemDrop => "DROP THE HELD ITEM  ·  RIGHT MOUSE",
                TutorialStep.ItemSwap => "PICK UP ONE ITEM, THEN PICK UP THE OTHER WITHOUT DROPPING",
                TutorialStep.Interaction => "USE THE EXIT CONSOLE  ·  F",
                TutorialStep.Complete => "TRAINING COMPLETE",
                _ => string.Empty
            };

            if (step != TutorialStep.Complete)
            {
                return;
            }

            completionPanel.SetActive(true);
            playerController.SetResultInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("PHS_NETWORK_TUTORIAL_COMPLETE");
        }

        private async void ReturnToLobby()
        {
            if (isExiting)
            {
                return;
            }

            isExiting = true;
            returnToLobbyButton.interactable = false;
            if (!await sessionExitService.LeaveToLobbyAsync(lobbySceneName))
            {
                isExiting = false;
                if (returnToLobbyButton != null)
                {
                    returnToLobbyButton.interactable = true;
                }
            }
        }
    }
}
