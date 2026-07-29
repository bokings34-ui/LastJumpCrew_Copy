using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialDirector : MonoBehaviour
    {
        private const int DefaultRequiredSuccessCount = 2;
        private const int ConfiguredRoomCount = 8;

        private static readonly TutorialActionKind[] LegacyActionOrder =
        {
            TutorialActionKind.Move,
            TutorialActionKind.Jump,
            TutorialActionKind.Thruster,
            TutorialActionKind.Grapple,
            TutorialActionKind.Pickup,
            TutorialActionKind.Drop,
            TutorialActionKind.Swap,
            TutorialActionKind.Use,
            TutorialActionKind.Interaction
        };

        [Header("Observed Player")]
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private NetworkPlayerGrappleController grappleController;
        [SerializeField] private TempPlayerItemHolder itemHolder;
        [SerializeField] private MonoBehaviour actionSourceBehaviour;

        [Header("Room Sequence")]
        [SerializeField] private NetworkTutorialRoomController[] rooms =
            Array.Empty<NetworkTutorialRoomController>();

        [Header("Legacy Single-Corridor UI Fallback")]
        [SerializeField] private Image instructionImage;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private Slider instructionProgressSlider;

        [Header("Completion")]
        [SerializeField] private GameObject completionPanel;
        [SerializeField] private Button returnToLobbyButton;
        [SerializeField] private MonoBehaviour audioCuePlayerSource;
        [SerializeField] private string lobbySceneName =
            "ParkHanSol_LobbyScene";

        [Header("Success Filtering")]
        [SerializeField, Min(0.1f)] private float movementDistance = 1.5f;

        private readonly INetworkSessionExitService sessionExitService =
            new NetworkSessionExitService();
        private ITutorialActionSource actionSource;
        private INetworkAudioCuePlayer audioCuePlayer;
        private int currentRoomIndex;
        private int currentSuccessCount;
        private bool isComplete;
        private bool isExiting;
        private bool tutorialCompleteCuePlayed;

        public bool IsWaitingForInteraction =>
            !isComplete
            && CurrentAction == TutorialActionKind.Interaction;

        private TutorialActionKind CurrentAction => HasConfiguredRooms
            ? rooms[currentRoomIndex].GetExpectedAction(currentSuccessCount)
            : LegacyActionOrder[currentRoomIndex];

        private int CurrentRequiredSuccessCount => HasConfiguredRooms
            ? rooms[currentRoomIndex].RequiredSuccessCount
            : DefaultRequiredSuccessCount;

        private bool HasConfiguredRooms => rooms != null && rooms.Length > 0;

        private void Awake()
        {
            audioCuePlayer = audioCuePlayerSource as INetworkAudioCuePlayer;
            if (audioCuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_AUDIO_SETUP_FAILED reason=cue_player_missing director={name}",
                    this);
            }

            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            actionSource = ResolveActionSource();
            if (actionSource == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=action_source_missing director={name}",
                    this);
                enabled = false;
                return;
            }

            actionSource.ActionSucceeded += HandleActionSucceeded;
            foreach (var room in rooms)
            {
                room.ObjectiveProgressChanged += HandleObjectiveProgressChanged;
            }
            currentRoomIndex = 0;
            currentSuccessCount = 0;
            completionPanel.SetActive(false);
            returnToLobbyButton.onClick.AddListener(ReturnToLobby);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetRoomSequenceState();
            RefreshCurrentInstruction();
        }

        private void OnDestroy()
        {
            if (actionSource != null)
            {
                actionSource.ActionSucceeded -= HandleActionSucceeded;
            }

            if (rooms != null)
            {
                foreach (var room in rooms)
                {
                    if (room != null)
                    {
                        room.ObjectiveProgressChanged -=
                            HandleObjectiveProgressChanged;
                    }
                }
            }

            if (returnToLobbyButton != null)
            {
                returnToLobbyButton.onClick.RemoveListener(ReturnToLobby);
            }
        }

        public void ReportInteraction()
        {
            if (!IsWaitingForInteraction)
            {
                return;
            }

            actionSource.ReportInteractionSuccess();
        }

        private bool ValidateSetup()
        {
            if (playerController == null
                || grappleController == null
                || itemHolder == null
                || completionPanel == null
                || returnToLobbyButton == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=inspector_reference_missing director={name}",
                    this);
                return false;
            }

            if (!HasConfiguredRooms && instructionText == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=legacy_instruction_text_missing director={name}",
                    this);
                return false;
            }

            if (!HasConfiguredRooms)
            {
                return true;
            }

            if (rooms.Length != ConfiguredRoomCount)
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=room_count_invalid expected={ConfiguredRoomCount} actual={rooms.Length} director={name}",
                    this);
                return false;
            }

            for (var index = 0; index < rooms.Length; index++)
            {
                if (rooms[index] == null)
                {
                    Debug.LogError(
                        $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=room_reference_missing index={index} director={name}",
                        this);
                    return false;
                }

                var objectiveReason = string.Empty;
                if (rooms[index].RequiredStepCount < 1
                    || !rooms[index].TryValidateProgressContract(
                        out objectiveReason))
                {
                    Debug.LogError(
                        $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=room_contract_invalid index={index} steps={rooms[index].RequiredStepCount} objectives={objectiveReason}",
                        this);
                    return false;
                }
            }

            return true;
        }

        private ITutorialActionSource ResolveActionSource()
        {
            if (actionSourceBehaviour != null)
            {
                if (actionSourceBehaviour is ITutorialActionSource configured)
                {
                    ConfigureActionSourceIfSupported(configured);
                    return configured;
                }

                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_SETUP_FAILED reason=action_source_interface_missing source={actionSourceBehaviour.name}",
                    this);
                return null;
            }

            var existing = playerController.GetComponent<
                NetworkTutorialActionSource>();
            if (existing == null)
            {
                existing = playerController.gameObject.AddComponent<
                    NetworkTutorialActionSource>();
            }

            existing.Configure(
                playerController,
                grappleController,
                itemHolder,
                movementDistance);
            actionSourceBehaviour = existing;
            return existing;
        }

        private void ConfigureActionSourceIfSupported(
            ITutorialActionSource configured)
        {
            if (configured is NetworkTutorialActionSource stateSource)
            {
                stateSource.Configure(
                    playerController,
                    grappleController,
                    itemHolder,
                    movementDistance);
            }
        }

        private void HandleActionSucceeded(TutorialActionKind actionKind)
        {
            if (isComplete)
            {
                return;
            }

            if (HasConfiguredRooms)
            {
                if (!rooms[currentRoomIndex].TryRegisterAction(
                        actionKind,
                        currentSuccessCount,
                        out var nextSuccessCount))
                {
                    return;
                }

                currentSuccessCount = nextSuccessCount;
            }
            else
            {
                if (actionKind != CurrentAction)
                {
                    return;
                }

                currentSuccessCount = Mathf.Min(
                    currentSuccessCount + 1,
                    CurrentRequiredSuccessCount);
            }

            RefreshCurrentInstruction();
            Debug.Log(
                $"PHS_NETWORK_TUTORIAL_ACTION_OK room={currentRoomIndex} "
                + $"action={actionKind} count={currentSuccessCount}/{CurrentRequiredSuccessCount}",
                this);

            if (currentSuccessCount < CurrentRequiredSuccessCount)
            {
                return;
            }

            CompleteCurrentRoom();
        }

        private void HandleObjectiveProgressChanged(
            NetworkTutorialRoomController room,
            int completedCount)
        {
            if (isComplete
                || !HasConfiguredRooms
                || room != rooms[currentRoomIndex])
            {
                return;
            }

            currentSuccessCount = Mathf.Clamp(
                completedCount,
                0,
                CurrentRequiredSuccessCount);
            RefreshCurrentInstruction();
            Debug.Log(
                $"PHS_NETWORK_TUTORIAL_OBJECTIVE_OK room={currentRoomIndex} "
                + $"count={currentSuccessCount}/{CurrentRequiredSuccessCount}",
                this);
            if (currentSuccessCount >= CurrentRequiredSuccessCount)
            {
                CompleteCurrentRoom();
            }
        }

        private void CompleteCurrentRoom()
        {
            if (HasConfiguredRooms)
            {
                rooms[currentRoomIndex].CompleteRoom();
                rooms[currentRoomIndex].SetCurrent(false, currentSuccessCount);
                if (currentRoomIndex < rooms.Length - 1)
                {
                    PlayAudioCue(NetworkAudioCue.MissionSuccess);
                }
            }

            currentRoomIndex++;
            currentSuccessCount = 0;
            var roomCount = HasConfiguredRooms
                ? rooms.Length
                : LegacyActionOrder.Length;
            if (currentRoomIndex >= roomCount)
            {
                CompleteTutorial();
                return;
            }

            SetRoomSequenceState();
            RefreshCurrentInstruction();
        }

        private void SetRoomSequenceState()
        {
            if (!HasConfiguredRooms)
            {
                return;
            }

            for (var index = 0; index < rooms.Length; index++)
            {
                rooms[index].SetCurrent(
                    index == currentRoomIndex,
                    index == currentRoomIndex ? currentSuccessCount : 0);
            }
        }

        private void RefreshCurrentInstruction()
        {
            if (isComplete)
            {
                return;
            }

            if (HasConfiguredRooms)
            {
                rooms[currentRoomIndex].RefreshProgress(currentSuccessCount);
                return;
            }

            if (instructionImage != null)
            {
                instructionImage.gameObject.SetActive(false);
            }

            instructionText.text =
                $"{GetLegacyInstruction(CurrentAction)}  "
                + $"{currentSuccessCount}/{CurrentRequiredSuccessCount}";
            if (instructionProgressSlider != null)
            {
                instructionProgressSlider.minValue = 0f;
                instructionProgressSlider.maxValue =
                    CurrentRequiredSuccessCount;
                instructionProgressSlider.wholeNumbers = true;
                instructionProgressSlider.value = currentSuccessCount;
            }
        }

        private static string GetLegacyInstruction(
            TutorialActionKind actionKind)
        {
            return actionKind switch
            {
                TutorialActionKind.Move =>
                    "MOVE TWICE  -  RELEASE WASD BETWEEN MOVES",
                TutorialActionKind.Jump =>
                    "JUMP TWICE  -  SPACE",
                TutorialActionKind.Thruster =>
                    "THRUST TWICE IN ZERO-G  -  RELEASE BETWEEN USES",
                TutorialActionKind.Grapple =>
                    "LATCH THE ORANGE TARGET TWICE  -  HOLD Q",
                TutorialActionKind.Pickup =>
                    "PICK UP AN ITEM TWICE  -  F",
                TutorialActionKind.Drop =>
                    "PLACE THE HELD ITEM TWICE  -  TAP RIGHT MOUSE",
                TutorialActionKind.Swap =>
                    "SWAP HELD ITEMS TWICE WITHOUT DROPPING",
                TutorialActionKind.Use =>
                    "USE THE HELD TOOL TWICE  -  LEFT MOUSE",
                TutorialActionKind.Interaction =>
                    "USE THE EXIT CONSOLE TWICE  -  F",
                _ => string.Empty
            };
        }

        private void CompleteTutorial()
        {
            isComplete = true;
            if (instructionText != null)
            {
                instructionText.text = "TRAINING COMPLETE";
            }

            if (instructionProgressSlider != null)
            {
                instructionProgressSlider.value =
                    instructionProgressSlider.maxValue;
            }

            completionPanel.SetActive(true);
            if (!tutorialCompleteCuePlayed)
            {
                tutorialCompleteCuePlayed = true;
                PlayAudioCue(NetworkAudioCue.TutorialComplete);
            }

            playerController.SetResultInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("PHS_NETWORK_TUTORIAL_COMPLETE", this);
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
