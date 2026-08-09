using Unity.Netcode;
using UnityEngine;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace LastJumpCrew.ParkHanSol.Items
{
    public class PHSNetworkTutorialOfflineItemUsePolicy : MonoBehaviour
    {
        [SerializeField] private bool allowWrench = true;
        [SerializeField] private bool allowFireExtinguisher = true;

        private uint offlineRequestSequence;

        public bool CanUseOfflineItem(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemDataSO itemData,
            IItemHolder itemHolder)
        {
            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening)
            {
                return false;
            }

            var familyAllowed = familyKind switch
            {
                PHSUtilityFamilyActionKind.Wrench => allowWrench,
                PHSUtilityFamilyActionKind.FireExtinguisher =>
                    allowFireExtinguisher,
                _ => false
            };

            return familyAllowed
                && itemData != null
                && itemHolder != null
                && itemHolder is Component holderComponent
                && holderComponent.GetComponent<
                    PHSNetworkUtilityFamilyActionController>()
                    is { } controller
                && controller.TryResolveOfflineTarget(
                    familyKind,
                    itemData,
                    itemHolder,
                    out _);
        }

        public bool TryResolveOfflineItem(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemDataSO itemData,
            IItemHolder itemHolder)
        {
            if (!CanUseOfflineItem(
                    familyKind,
                    itemData,
                    itemHolder)
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<
                    PHSNetworkUtilityFamilyActionController>()
                    is not { } controller
                || !controller.TryResolveOfflineTarget(
                    familyKind,
                    itemData,
                    itemHolder,
                    out var offlineTarget))
            {
                return false;
            }

            offlineRequestSequence++;
            if (offlineRequestSequence == 0U)
            {
                offlineRequestSequence = 1U;
            }

            return offlineTarget.TryResolveUtilityAttack(
                new UtilityAttackHit(
                    itemData.ItemId,
                    holderComponent.gameObject,
                    offlineRequestSequence));
        }
    }
}
