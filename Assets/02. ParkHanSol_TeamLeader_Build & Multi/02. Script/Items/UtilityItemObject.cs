using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class UtilityItemObject : MonoBehaviour, IHoldableItem
    {
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
        public string ItemId => itemPrefabData == null ? string.Empty : itemPrefabData.ItemId;
        public string DisplayName => itemPrefabData == null ? string.Empty : itemPrefabData.DisplayName;
        public Transform HoldTransform => transform;

        public void OnPickedUp(IItemHolder holder)
        {
            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=itemData_missing item={name}");
                return;
            }
        }

        public void OnDropped(Vector3 dropPosition)
        {
            transform.position = dropPosition;
        }
    }
}
