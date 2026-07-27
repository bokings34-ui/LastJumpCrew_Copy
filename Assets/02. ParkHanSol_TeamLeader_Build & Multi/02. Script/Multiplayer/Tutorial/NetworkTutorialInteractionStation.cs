using System;
using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialInteractionStation :
        MonoBehaviour,
        IInteractable,
        ITutorialObjectiveSource
    {
        [SerializeField] private NetworkTutorialDirector tutorialDirector;
        [SerializeField] private string interactionPrompt = "Complete Training";
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool objectiveMode;
        [SerializeField] private string objectiveId = "incident_terminal";

        private bool hasBeenUsed;
        private bool objectiveActive;

        public string InteractionPrompt => interactionPrompt;
        public string ObjectiveId => objectiveId;
        public bool IsComplete => hasBeenUsed;

        public event Action<ITutorialObjectiveSource> Completed;

        public void SetObjectiveActive(bool active)
        {
            objectiveActive = objectiveMode && active && !hasBeenUsed;
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (singleUse && hasBeenUsed)
            {
                return false;
            }

            return objectiveMode
                ? objectiveActive
                : tutorialDirector != null
                    && tutorialDirector.IsWaitingForInteraction;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_INTERACTION_FAILED reason=step_not_ready station={name}",
                    this);
                return;
            }

            hasBeenUsed = true;
            if (objectiveMode)
            {
                objectiveActive = false;
                Completed?.Invoke(this);
                return;
            }

            tutorialDirector.ReportInteraction();
        }
    }
}
