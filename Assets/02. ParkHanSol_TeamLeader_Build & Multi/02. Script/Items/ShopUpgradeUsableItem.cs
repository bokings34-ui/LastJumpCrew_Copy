using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class ShopUpgradeUsableItem : UtilityItemUseBehaviour
    {
        protected override bool CanUseItem(IItemHolder user, IInteractable target)
        {
            if (user is not Component userComponent)
            {
                return false;
            }

            var itemData = userComponent
                .GetComponent<LastJumpCrew.ParkHanSol.Interaction.TempPlayerItemHolder>()
                ?.CurrentItemPrefabData;
            return itemData != null
                && itemData.IsUpgradeItem
                && userComponent.GetComponent<NetworkPlayerUpgradeState>() != null;
        }

        protected override void OnUseFinished(IItemHolder user, IInteractable target)
        {
            if (user is not Component userComponent)
            {
                Debug.LogError($"PHS_UPGRADE_ITEM_USE_FAILED reason=user_component_missing item={name}", this);
                return;
            }

            var upgradeState = userComponent.GetComponent<NetworkPlayerUpgradeState>();
            if (upgradeState == null || !upgradeState.RequestUseHeldUpgrade())
            {
                Debug.LogWarning($"PHS_UPGRADE_ITEM_USE_FAILED reason=request_rejected item={name}", this);
            }
        }
    }
}
