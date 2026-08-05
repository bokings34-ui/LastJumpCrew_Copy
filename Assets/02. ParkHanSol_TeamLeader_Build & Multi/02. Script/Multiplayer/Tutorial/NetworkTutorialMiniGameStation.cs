using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using UnityEngine;
using MiniGameTarget = LastJumpCrew.Common.IMiniGameTarget;
using TutorialInteractable = LastJumpCrew.ParkHanSol.Interaction.IInteractable;
using TutorialItemHolder = LastJumpCrew.ParkHanSol.Interaction.IItemHolder;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialMiniGameStation :
        MonoBehaviour,
        TutorialInteractable,
        ITutorialObjectiveSource,
        MiniGameTarget,
        IPHSFinalMiniGameSessionOwner
    {
        [Header("Mini Game")]
        [SerializeField] private PHSMiniGameManager miniGameManager;
        [SerializeField] private PHSMiniGameType miniGameType;

        [Header("Tutorial Objective")]
        [SerializeField] private string interactionPrompt = "미니게임 시작";
        [SerializeField] private string objectiveId = "training_minigame";

        private NetworkPlayerController activePlayer;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool objectiveActive;
        private bool sessionOpen;
        private bool completionPending;
        private bool completed;

        public string InteractionPrompt => interactionPrompt;
        public string ObjectiveId => objectiveId;
        public string MiniGameTargetId => objectiveId;
        public bool IsComplete => completed;

        public event Action<ITutorialObjectiveSource> Completed;

        private void OnDisable()
        {
            RestorePlayerInput();
        }

        public void SetObjectiveActive(bool active)
        {
            objectiveActive = active && !completed;
        }

        public bool CanInteract(TutorialItemHolder itemHolder)
        {
            return objectiveActive
                && !completed
                && !sessionOpen
                && miniGameManager != null;
        }

        public void Interact(TutorialItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning(
                    $"PHS_TUTORIAL_MINIGAME_OPEN_REJECTED station={name} " +
                    "reason=objective_inactive_or_session_busy",
                    this);
                return;
            }

            activePlayer = (itemHolder as Component)
                ?.GetComponent<NetworkPlayerController>();
            if (activePlayer == null)
            {
                Debug.LogError(
                    $"PHS_TUTORIAL_MINIGAME_OPEN_REJECTED station={name} " +
                    "reason=network_player_missing",
                    this);
                return;
            }

            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            activePlayer.SetPauseInputBlocked(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            sessionOpen = true;

            if (miniGameManager.OpenMiniGame(miniGameType, this))
            {
                return;
            }

            Debug.LogWarning(
                $"PHS_TUTORIAL_MINIGAME_OPEN_REJECTED station={name} " +
                $"reason=manager_busy type={miniGameType}",
                this);
            RestorePlayerInput();
        }

        public void OnMiniGameSucceeded()
        {
            if (!sessionOpen || completed)
            {
                return;
            }

            completionPending = true;
        }

        public void OnMiniGameFailed()
        {
            // 실패는 완료로 세지 않는다. 세션이 닫힌 뒤 같은 단말에서 재시도한다.
            completionPending = false;
        }

        public void OnMiniGameSessionClosed()
        {
            var shouldComplete = completionPending && !completed;
            RestorePlayerInput();
            completionPending = false;
            if (!shouldComplete)
            {
                return;
            }

            completed = true;
            objectiveActive = false;
            Completed?.Invoke(this);
        }

        private void RestorePlayerInput()
        {
            if (activePlayer != null)
            {
                activePlayer.SetPauseInputBlocked(false);
                activePlayer = null;
            }

            if (!sessionOpen)
            {
                return;
            }

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            sessionOpen = false;
        }
    }
}
