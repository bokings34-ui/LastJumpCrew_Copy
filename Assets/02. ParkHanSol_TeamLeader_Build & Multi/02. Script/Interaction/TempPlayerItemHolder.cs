using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TempPlayerItemHolder : MonoBehaviour, IItemHolder, LastJumpCrew.Common.IItemHolder
    {
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Transform visibleHandHoldPoint;
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Vector3 droppedLocalOffset = new(0f, 0f, 1f);
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;

        private GameObject heldItemInstance;
        private UtilityItemObject currentItemObject;
        private UtilityItemPrefabData currentItemPrefabData;

        public UtilityItemPrefabData CurrentItemPrefabData => currentItemPrefabData;
        LastJumpCrew.Common.IHoldableItem LastJumpCrew.Common.IItemHolder.CurrentItem => currentItemObject;
        public bool HasItem => currentItemPrefabData != null;

        private Transform ActiveHoldPoint => visibleHandHoldPoint != null ? visibleHandHoldPoint : holdPoint;

        private void Awake()
        {
            if (visibleHandHoldPoint != null)
            {
                return;
            }

            visibleHandHoldPoint = FindChildByName(transform, "R_Hand");
            if (visibleHandHoldPoint == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_VISUAL_HAND_MISSING player={name}");
            }
        }

        public bool CanReplaceHeldItem(UtilityItemPrefabData itemPrefabData)
        {
            if (ActiveHoldPoint == null)
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
            heldItemInstance = Instantiate(itemPrefabData.HeldPrefab, ActiveHoldPoint);
            heldItemInstance.name = itemPrefabData.HeldPrefab.name;
            heldItemInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            heldItemInstance.transform.localScale = GetCompensatedHeldItemScale(itemPrefabData.HeldPrefab.transform.localScale);
            currentItemObject = heldItemInstance.GetComponent<UtilityItemObject>();
            currentItemPrefabData = itemPrefabData;

            if (currentItemObject == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED reason=utilityItemObject_missing item={itemPrefabData.ItemId}");
                Destroy(heldItemInstance);
                heldItemInstance = null;
                currentItemPrefabData = null;
                return;
            }

            currentItemObject.OnPickedUp(this);
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_HELD player={name} item={itemPrefabData.ItemId}");
        }

        public bool CanHold(LastJumpCrew.Common.IHoldableItem item)
        {
            if (item is not UtilityItemObject utilityItemObject)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED reason=unsupported_item_type player={name}");
                return false;
            }

            return CanReplaceHeldItem(utilityItemObject.ItemPrefabData);
        }

        public void Hold(LastJumpCrew.Common.IHoldableItem item)
        {
            if (item is not UtilityItemObject utilityItemObject)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED reason=unsupported_item_type player={name}");
                return;
            }

            ReplaceHeldItem(utilityItemObject.ItemPrefabData, utilityItemObject.transform);
        }

        public void Drop()
        {
            DropCurrentItem();
        }

        public bool TryConsumeHeldItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"PHS_TEMP_ITEM_CONSUME_FAILED reason=itemId_missing player={name}");
                return false;
            }

            if (currentItemPrefabData == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_CONSUME_FAILED reason=heldItem_missing player={name} item={itemId}");
                return false;
            }

            if (currentItemPrefabData.ItemId != itemId)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_CONSUME_FAILED reason=wrong_item player={name} expected={itemId} actual={currentItemPrefabData.ItemId}");
                return false;
            }

            if (heldItemInstance == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_CONSUME_FAILED reason=heldItemInstance_missing player={name} item={itemId}");
                return false;
            }

            Destroy(heldItemInstance);
            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_CONSUMED player={name} item={itemId}");
            return true;
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
                var droppedItemInstance = Instantiate(currentItemPrefabData.DroppedPrefab, position, source.rotation);
                var droppedItemObject = droppedItemInstance.GetComponent<UtilityItemObject>();
                if (droppedItemObject == null)
                {
                    Debug.LogError($"PHS_TEMP_ITEM_DROP_FAILED reason=utilityItemObject_missing item={currentItemPrefabData.ItemId}");
                }
                else
                {
                    droppedItemObject.OnDropped(position);
                }

                Debug.Log($"PHS_TEMP_ITEM_DROPPED player={name} item={currentItemPrefabData.ItemId}");
            }

            if (heldItemInstance != null)
            {
                Destroy(heldItemInstance);
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;
            RefreshHeldItemHud();
        }

        private void RefreshHeldItemHud()
        {
            if (playHudPresenter == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_UI_FAILED reason=playHudPresenter_missing player={name}");
                return;
            }

            if (currentItemPrefabData == null)
            {
                playHudPresenter.ClearHeldItem();
                return;
            }

            playHudPresenter.SetHeldItem(currentItemPrefabData);
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                {
                    return child;
                }

                var nested = FindChildByName(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private Vector3 GetCompensatedHeldItemScale(Vector3 prefabLocalScale)
        {
            var holdPointScale = ActiveHoldPoint.lossyScale;
            if (Mathf.Approximately(holdPointScale.x, 0f)
                || Mathf.Approximately(holdPointScale.y, 0f)
                || Mathf.Approximately(holdPointScale.z, 0f))
            {
                Debug.LogError($"PHS_TEMP_ITEM_SCALE_FAILED reason=holdPoint_scale_zero player={name} holdPoint={ActiveHoldPoint.name}");
                return prefabLocalScale;
            }

            return new Vector3(
                prefabLocalScale.x / holdPointScale.x,
                prefabLocalScale.y / holdPointScale.y,
                prefabLocalScale.z / holdPointScale.z);
        }
    }
}
