using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TravelDestinationSelectInteractable : MonoBehaviour, IInteractable
    {
        [FormerlySerializedAs("console")]
        [SerializeField] private MonoBehaviour flowSource;
        [SerializeField] private TravelConsoleSide side;
        [SerializeField] private string interactionPrompt = "목적지 선택";

        private ITravelConsoleFlow Flow => flowSource as ITravelConsoleFlow;

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(IItemHolder itemHolder)
        {
            return Flow != null
                && itemHolder is Component
                && Flow.CanSelectSide(side);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=interaction_invalid side={side}", this);
                return;
            }

            Flow.RequestSelectSide(itemHolder, side);
        }
    }
}
