using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TempPlayerItemHolder : MonoBehaviour, IItemHolder
    {
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Vector3 droppedLocalOffset = new(0f, 0f, 1f);

        private GameObject heldItemInstance;
        private UtilityItemPrefabData currentItemPrefabData;

        public UtilityItemPrefabData CurrentItemPrefabData => currentItemPrefabData;

        public bool CanReplaceHeldItem(UtilityItemPrefabData itemPrefabData)
        {
            if (holdPoint == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED reason=holdPoint_missing player={name}");
                return false;
            }

            if (itemPrefabData == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED reason=itemData_missing player={name}");
                return false;
            }

            if (!itemPrefabData.HasHeldPrefab)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED reason=heldPrefab_missing item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        public void ReplaceHeldItem(UtilityItemPrefabData itemPrefabData, Transform interactionSource)
        {
            if (!CanReplaceHeldItem(itemPrefabData))
            {
                return;
            }

            DropCurrentItem();
            heldItemInstance = Instantiate(itemPrefabData.HeldPrefab, holdPoint);
            heldItemInstance.name = itemPrefabData.HeldPrefab.name;
            heldItemInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            currentItemPrefabData = itemPrefabData;

            Debug.Log($"PHS_TEMP_ITEM_HELD player={name} item={itemPrefabData.ItemId}");
        }

        private void DropCurrentItem()
        {
            if (currentItemPrefabData == null)
            {
                return;
            }

            if (!currentItemPrefabData.HasDroppedPrefab)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_DROP_FAILED reason=droppedPrefab_missing item={currentItemPrefabData.ItemId}");
            }
            else
            {
                var source = dropPoint == null ? transform : dropPoint;
                var position = source.TransformPoint(droppedLocalOffset);
                Instantiate(currentItemPrefabData.DroppedPrefab, position, source.rotation);
                Debug.Log($"PHS_TEMP_ITEM_DROPPED player={name} item={currentItemPrefabData.ItemId}");
            }

            if (heldItemInstance != null)
            {
                Destroy(heldItemInstance);
            }

            heldItemInstance = null;
            currentItemPrefabData = null;
        }
    }
}
