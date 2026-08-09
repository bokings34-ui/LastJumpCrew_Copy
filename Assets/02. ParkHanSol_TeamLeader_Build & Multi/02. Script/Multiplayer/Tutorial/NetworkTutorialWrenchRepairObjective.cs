using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialWrenchRepairObjective :
        NetworkTutorialObjectiveSourceBase,
        IOfflineUtilityActionTarget
    {
        [SerializeField] private GameObject brokenDeviceRoot;
        [SerializeField] private Light statusLight;
        [SerializeField] private string interactionPrompt = "렌치 수리";

        public string InteractionPrompt => interactionPrompt;
        public UtilityItemActionKind ActionKind =>
            UtilityItemActionKind.DeviceRepair;
        public bool IsResolved => IsComplete;

        public override void SetObjectiveActive(bool active)
        {
            base.SetObjectiveActive(active);
            if (brokenDeviceRoot != null)
            {
                brokenDeviceRoot.SetActive(active || IsComplete);
            }

            if (statusLight != null)
            {
                statusLight.color = IsComplete ? Color.green : Color.red;
            }
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return CanComplete && brokenDeviceRoot != null;
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            if (!CanComplete
                || brokenDeviceRoot == null)
            {
                return false;
            }

            if (statusLight != null)
            {
                statusLight.color = Color.green;
            }

            CompleteObjective();
            return true;
        }
    }
}
