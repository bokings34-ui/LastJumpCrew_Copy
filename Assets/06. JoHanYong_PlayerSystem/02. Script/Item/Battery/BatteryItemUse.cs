using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class BatteryItemUse : MonoBehaviour, IUsableItem
    {
        public bool CanUse(IItemHolder holder, IInteractable target)
        {
            return holder != null && holder.HasItem;
        }

        public void Use(IItemHolder holder, IInteractable target)
        {
            if (holder == null)
            {
                Debug.LogError("PHS_BATTERY_USE_FAILED reason=holder_missing");
                return;
            }

            holder.Drop();
            Debug.Log("PHS_BATTERY_PLACED_FROM_USE");
        }
    }
}
