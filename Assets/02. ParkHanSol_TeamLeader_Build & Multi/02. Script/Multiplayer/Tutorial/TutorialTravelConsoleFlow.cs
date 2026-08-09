using System;
using LastJumpCrew.ParkHanSol.Interaction;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialTravelConsoleFlow :
        MonoBehaviour,
        ITravelConsoleFlow,
        ITutorialObjectiveSource
    {
        private enum TutorialTravelPhase : byte
        {
            WarpReady,
            WarpSafe,
            DestinationSelected,
            Complete
        }

        [Header("Tutorial Objective")]
        [SerializeField] private string objectiveId = "warp_console";
        [SerializeField] private Transform warpSafeDestination;

        [Header("Presentation")]
        [SerializeField] private TMP_Text actionStatusText;
        [SerializeField] private TMP_Text leftStatusText;
        [SerializeField] private TMP_Text rightStatusText;
        [SerializeField] private Light readyStatusLight;

        private TutorialTravelPhase phase = TutorialTravelPhase.WarpReady;
        private TravelConsoleSide selectedSide;
        private bool objectiveActive;

        public string ObjectiveId => objectiveId;
        public bool IsComplete => phase == TutorialTravelPhase.Complete;
        public string ActionPrompt => phase switch
        {
            TutorialTravelPhase.WarpReady => "안전 구역 진입",
            TutorialTravelPhase.WarpSafe => "목적지를 먼저 선택하세요",
            TutorialTravelPhase.DestinationSelected => "선택 목적지로 이동",
            _ => "워프 완료"
        };

        public event Action<ITutorialObjectiveSource> Completed;

        private void Awake()
        {
            RefreshPresentation();
        }

        public void SetObjectiveActive(bool active)
        {
            objectiveActive = active && !IsComplete;
            RefreshPresentation();
        }

        public bool CanSelectSide(TravelConsoleSide side)
        {
            return objectiveActive
                && phase == TutorialTravelPhase.WarpSafe
                && side != TravelConsoleSide.None;
        }

        public void RequestSelectSide(
            IItemHolder itemHolder,
            TravelConsoleSide side)
        {
            if (!CanSelectSide(side) || !HasPlayer(itemHolder))
            {
                Debug.LogWarning(
                    $"PHS_TUTORIAL_TRAVEL_SELECT_REJECTED phase={phase} side={side}",
                    this);
                return;
            }

            selectedSide = side;
            phase = TutorialTravelPhase.DestinationSelected;
            RefreshPresentation();
        }

        public bool CanExecute(IItemHolder itemHolder)
        {
            return objectiveActive
                && HasPlayer(itemHolder)
                && phase is TutorialTravelPhase.WarpReady
                    or TutorialTravelPhase.DestinationSelected;
        }

        public void Execute(IItemHolder itemHolder)
        {
            if (!CanExecute(itemHolder))
            {
                Debug.LogWarning(
                    $"PHS_TUTORIAL_TRAVEL_EXECUTE_REJECTED phase={phase}",
                    this);
                return;
            }

            if (phase == TutorialTravelPhase.WarpReady)
            {
                phase = TutorialTravelPhase.WarpSafe;
                WarpPlayerToSafeZone(itemHolder);
                RefreshPresentation();
                return;
            }

            phase = TutorialTravelPhase.Complete;
            objectiveActive = false;
            RefreshPresentation();
            Completed?.Invoke(this);
        }

        private static bool HasPlayer(IItemHolder itemHolder)
        {
            return itemHolder is Component holder
                && holder.GetComponent<NetworkPlayerController>() != null;
        }

        private void WarpPlayerToSafeZone(IItemHolder itemHolder)
        {
            if (warpSafeDestination == null
                || itemHolder is not Component holder
                || holder.GetComponent<NetworkPlayerController>()
                    is not { } player)
            {
                Debug.LogError(
                    "PHS_TUTORIAL_TRAVEL_WARP_FAILED reason=setup_missing",
                    this);
                return;
            }

            player.transform.SetPositionAndRotation(
                warpSafeDestination.position,
                warpSafeDestination.rotation);
        }

        private void RefreshPresentation()
        {
            if (actionStatusText != null)
            {
                actionStatusText.text = ActionPrompt;
            }

            if (leftStatusText != null)
            {
                leftStatusText.text = selectedSide == TravelConsoleSide.Left
                    ? "선택됨"
                    : "왼쪽 구역";
            }

            if (rightStatusText != null)
            {
                rightStatusText.text = selectedSide == TravelConsoleSide.Right
                    ? "선택됨"
                    : "오른쪽 구역";
            }

            if (readyStatusLight != null)
            {
                readyStatusLight.enabled = objectiveActive && !IsComplete;
                readyStatusLight.color = phase == TutorialTravelPhase.WarpSafe
                    ? Color.yellow
                    : phase == TutorialTravelPhase.DestinationSelected
                        ? Color.cyan
                        : Color.green;
            }
        }
    }
}
