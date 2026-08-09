using UnityEngine;
using UnityEngine.Serialization;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TravelConsoleExecuteInteractable : MonoBehaviour, IInteractable
    {
        [FormerlySerializedAs("console")]
        [SerializeField] private MonoBehaviour flowSource;

        private ITravelConsoleFlow Flow => flowSource as ITravelConsoleFlow;

        public string InteractionPrompt => Flow != null
            ? Flow.ActionPrompt
            : "이동 실행";

        public bool CanInteract(IItemHolder itemHolder)
        {
            return Flow != null && Flow.CanExecute(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning("PHS_TRAVEL_EXECUTE_FAILED reason=interaction_invalid", this);
                return;
            }

            Flow.Execute(itemHolder);
        }
    }
}
