using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
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
        [SerializeField] private MonoBehaviour audioCuePlayerSource;
        [SerializeField] private string lobbySceneName = "ParkHanSol_LobbyScene";
        [SerializeField, Min(1f)] private float movementDistance = 3f;

        private readonly INetworkSessionExitService sessionExitService =
            new NetworkSessionExitService();
        private TutorialStep currentStep;
        private Vector3 movementStartPosition;
        private bool swapArmed;
        private string swapBaselineItemId;
        private bool isExiting;
        private bool tutorialCompleteCuePlayed;
        private bool completionPresented;
        private INetworkAudioCuePlayer audioCuePlayer;

        public event Action ProgressChanged;

        public bool IsWaitingForInteraction =>
            currentStep == TutorialStep.Interaction;

        public bool IsRoomComplete(NetworkTutorialRoom room)
        {
            return room switch
            {
                NetworkTutorialRoom.Movement =>
                    HasAdvancedPast(TutorialStep.Movement),
                NetworkTutorialRoom.ZeroGravity =>
                    HasAdvancedPast(TutorialStep.Thruster),
                NetworkTutorialRoom.Grapple =>
                    HasAdvancedPast(TutorialStep.Grapple),
                NetworkTutorialRoom.ItemPickup =>
                    HasAdvancedPast(TutorialStep.ItemPickup),
                NetworkTutorialRoom.ItemDrop =>
                    HasAdvancedPast(TutorialStep.ItemDrop),
                NetworkTutorialRoom.ItemSwap =>
                    HasAdvancedPast(TutorialStep.ItemSwap),
                NetworkTutorialRoom.Interaction =>
                    currentStep == TutorialStep.Complete,
                NetworkTutorialRoom.Complete =>
                    completionPresented,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(room),
                    room,
                    "Unsupported tutorial room.")
            };
        }

        public void GetRoomDisplay(
            NetworkTutorialRoom room,
            out string title,
            out string description,
            out string status)
        {
            title = room switch
            {
                NetworkTutorialRoom.Movement => "01 · MOVEMENT",
                NetworkTutorialRoom.ZeroGravity => "02 · ZERO GRAVITY",
                NetworkTutorialRoom.Grapple => "03 · GRAPPLE",
                NetworkTutorialRoom.ItemPickup => "04 · ITEM PICKUP",
                NetworkTutorialRoom.ItemDrop => "05 · ITEM DROP",
                NetworkTutorialRoom.ItemSwap => "06 · ITEM SWAP",
                NetworkTutorialRoom.Interaction => "07 · INTERACTION",
                NetworkTutorialRoom.Complete => "08 · COMPLETE",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(room),
                    room,
                    "Unsupported tutorial room.")
            };

            description = GetRoomDescription(room);
            if (room == NetworkTutorialRoom.Complete
                && completionPresented)
            {
                status = "STATUS: TRAINING COMPLETE";
                return;
            }

            if (room == NetworkTutorialRoom.Complete
                && currentStep == TutorialStep.Complete)
            {
                status = "STATUS: READY · ENTER THIS ROOM";
                return;
            }

            if (IsRoomComplete(room))
            {
                status = "STATUS: CLEARED · NEXT ROOM READY";
                return;
            }

            status = IsRoomActive(room)
                ? $"STATUS: IN PROGRESS · {GetInstruction(currentStep)}"
                : "STATUS: LOCKED · CLEAR PREVIOUS ROOM";
        }

        private void Awake()
        {
            audioCuePlayer = audioCuePlayerSource as INetworkAudioCuePlayer;
            if (audioCuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_AUDIO_SETUP_FAILED reason=cue_player_missing director={name}",
                    this);
            }

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

        public void ReportCompleteRoomEntered()
        {
            if (currentStep != TutorialStep.Complete)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_COMPLETE_ROOM_FAILED " +
                    $"reason=step_not_ready director={name}",
                    this);
                return;
            }

            if (completionPresented)
            {
                return;
            }

            completionPresented = true;
            instructionText.text = "TRAINING COMPLETE";
            completionPanel.SetActive(true);
            if (!tutorialCompleteCuePlayed)
            {
                tutorialCompleteCuePlayed = true;
                PlayAudioCue(NetworkAudioCue.TutorialComplete);
            }

            playerController.SetResultInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("PHS_NETWORK_TUTORIAL_COMPLETE");
            ProgressChanged?.Invoke();
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
            instructionText.text = GetInstruction(step);

            if (step != TutorialStep.Complete)
            {
                ProgressChanged?.Invoke();
                return;
            }
            ProgressChanged?.Invoke();
        }

        private string GetRoomDescription(NetworkTutorialRoom room)
        {
            return room switch
            {
                NetworkTutorialRoom.Movement =>
                    "Move through the room with WASD.",
                NetworkTutorialRoom.ZeroGravity =>
                    "Enter zero gravity and move with SPACE / SHIFT.",
                NetworkTutorialRoom.Grapple =>
                    "Hold Q and connect to the orange target.",
                NetworkTutorialRoom.ItemPickup =>
                    "Pick up the training item with F.",
                NetworkTutorialRoom.ItemDrop =>
                    "Drop the held item with RIGHT MOUSE.",
                NetworkTutorialRoom.ItemSwap =>
                    "Pick up one item, then pick up the other without dropping.",
                NetworkTutorialRoom.Interaction =>
                    "Use the completion console with F.",
                NetworkTutorialRoom.Complete =>
                    "Training results and return controls are available here.",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(room),
                    room,
                    "Unsupported tutorial room.")
            };
        }

        private bool IsRoomActive(NetworkTutorialRoom room)
        {
            return room switch
            {
                NetworkTutorialRoom.Movement =>
                    currentStep == TutorialStep.Movement,
                NetworkTutorialRoom.ZeroGravity =>
                    currentStep == TutorialStep.Thruster,
                NetworkTutorialRoom.Grapple =>
                    currentStep == TutorialStep.Grapple,
                NetworkTutorialRoom.ItemPickup =>
                    currentStep == TutorialStep.ItemPickup,
                NetworkTutorialRoom.ItemDrop =>
                    currentStep == TutorialStep.ItemDrop,
                NetworkTutorialRoom.ItemSwap =>
                    currentStep == TutorialStep.ItemSwap,
                NetworkTutorialRoom.Interaction =>
                    currentStep == TutorialStep.Interaction,
                NetworkTutorialRoom.Complete =>
                    currentStep == TutorialStep.Complete,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(room),
                    room,
                    "Unsupported tutorial room.")
            };
        }

        private bool HasAdvancedPast(TutorialStep step)
        {
            return (byte)currentStep > (byte)step;
        }

        private static string GetInstruction(TutorialStep step)
        {
            return step switch
            {
                TutorialStep.Movement => "MOVE  ·  WASD",
                TutorialStep.Thruster =>
                    "ENTER ZERO-G  ·  MOVE + SPACE / SHIFT",
                TutorialStep.Grapple =>
                    "GRAPPLE THE ORANGE TARGET  ·  HOLD Q",
                TutorialStep.ItemPickup => "PICK UP AN ITEM  ·  F",
                TutorialStep.ItemDrop =>
                    "DROP THE HELD ITEM  ·  RIGHT MOUSE",
                TutorialStep.ItemSwap =>
                    "PICK UP ONE ITEM, THEN PICK UP THE OTHER WITHOUT DROPPING",
                TutorialStep.Interaction => "USE THE EXIT CONSOLE  ·  F",
                TutorialStep.Complete => "ENTER THE COMPLETE ROOM",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(step),
                    step,
                    "Unsupported tutorial step.")
            };
        }

        private void PlayAudioCue(NetworkAudioCue cue)
        {
            if (audioCuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_AUDIO_PLAY_FAILED reason=cue_player_missing director={name} cue={cue}",
                    this);
                return;
            }

            if (!audioCuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_AUDIO_PLAY_FAILED reason={reason} director={name} cue={cue}",
                    this);
            }
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
