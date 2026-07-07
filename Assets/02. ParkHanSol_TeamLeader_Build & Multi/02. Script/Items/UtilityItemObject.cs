using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class UtilityItemObject : MonoBehaviour, IHoldableItem
    {
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        private IItemHolder currentHolder;

        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
        public string ItemId => itemPrefabData == null ? string.Empty : itemPrefabData.ItemId;
        public string DisplayName => itemPrefabData == null ? string.Empty : itemPrefabData.DisplayName;
        public Transform HoldTransform => transform;
        public bool IsHeld => currentHolder != null;

        public void OnPickedUp(IItemHolder holder)
        {
            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=itemData_missing item={name}");
                return;
            }

            if (holder == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=holder_missing item={name}");
                return;
            }

            currentHolder = holder;

            if (TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }

        }

        public void OnDropped(Vector3 dropPosition)
        {
            currentHolder = null;
            transform.position = dropPosition;

            if (TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody.useGravity = true;
                rigidbody.isKinematic = false;
            }

        }
    }
}
