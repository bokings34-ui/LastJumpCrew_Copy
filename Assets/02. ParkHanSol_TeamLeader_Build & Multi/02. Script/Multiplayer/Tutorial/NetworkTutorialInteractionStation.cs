using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialInteractionStation :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] private NetworkTutorialDirector tutorialDirector;
        [SerializeField] private string interactionPrompt = "Complete Training";

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return tutorialDirector != null
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

            tutorialDirector.ReportInteraction();
        }
    }
}
