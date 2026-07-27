using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public sealed class NetworkTutorialToolUseObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private PHSNetworkItemUseActionController actionController;
        [SerializeField] private PHSItemUseActionKind requiredActionKind;

        private void OnEnable()
        {
            if (actionController != null)
            {
                actionController.LocalActionStarted += HandleActionStarted;
            }
        }

        private void OnDisable()
        {
            if (actionController != null)
            {
                actionController.LocalActionStarted -= HandleActionStarted;
            }
        }

        private void HandleActionStarted(PHSItemUseActionKind actionKind)
        {
            if (CanComplete && actionKind == requiredActionKind)
            {
                CompleteObjective();
            }
        }
    }
}
