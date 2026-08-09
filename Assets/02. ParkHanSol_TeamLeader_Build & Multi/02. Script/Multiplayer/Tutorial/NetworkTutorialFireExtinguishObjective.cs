using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialFireExtinguishObjective :
        NetworkTutorialObjectiveSourceBase,
        IOfflineUtilityActionTarget
    {
        [SerializeField] private GameObject fireRoot;
        [SerializeField] private string interactionPrompt = "소화기 분사";

        public string InteractionPrompt => interactionPrompt;
        public UtilityItemActionKind ActionKind =>
            UtilityItemActionKind.FireSuppression;
        public bool IsResolved => IsComplete;

        public override void SetObjectiveActive(bool active)
        {
            base.SetObjectiveActive(active);
            if (fireRoot != null)
            {
                fireRoot.SetActive(active && !IsComplete);
            }
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return CanComplete && fireRoot != null && fireRoot.activeInHierarchy;
        }

        public void Interact(IItemHolder itemHolder)
        {
        }

        public bool TryResolveUtilityAttack(in UtilityAttackHit hit)
        {
            if (!CanComplete
                || fireRoot == null)
            {
                return false;
            }

            fireRoot.SetActive(false);
            CompleteObjective();
            return true;
        }
    }
}
