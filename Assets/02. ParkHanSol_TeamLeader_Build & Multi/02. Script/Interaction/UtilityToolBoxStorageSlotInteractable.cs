using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class UtilityToolBoxStorageSlotInteractable : MonoBehaviour, IInteractable
    {
        [System.Serializable]
        private sealed class ItemVisualProfile
        {
            [SerializeField] private UtilityItemPrefabData itemPrefabData;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private Vector3 localEuler = new(0f, 90f, 0f);
            [SerializeField, Min(0.001f)] private float localScale = 0.12f;

            public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
            public Vector3 LocalPosition => localPosition;
            public Vector3 LocalEuler => localEuler;
            public float LocalScale => localScale;
        }

        [SerializeField] private UtilityItemPrefabData storedItemPrefabData;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject glowOutlineRoot;
        [SerializeField] private BoxCollider slotCollider;
        [SerializeField] private ItemVisualProfile[] visualProfiles;
        [SerializeField] private string interactionPrompt = "F";
        [SerializeField] private Vector3 visibleItemLocalEuler = new(0f, 90f, 0f);
        [SerializeField, Min(0.001f)] private float visibleItemScale = 0.12f;

        private GameObject visibleItemInstance;
        private IItemHolder focusedItemHolder;
        private bool hasInteractionFocus;

        public string InteractionPrompt => interactionPrompt;

        private void Awake()
        {
            RefreshVisual();
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_SLOT_INTERACT_FAILED reason=itemHolder_missing slot={name}");
                return false;
            }

            if (!ValidateSetup())
            {
                return false;
            }

            var heldItem = itemHolder.CurrentItemPrefabData;
            if (heldItem == null)
            {
                if (storedItemPrefabData == null)
                {
                    Debug.LogWarning($"PHS_TOOL_BOX_SLOT_INTERACT_FAILED reason=empty_slot_and_empty_hand slot={name}");
                    return false;
                }

                return itemHolder.CanReplaceHeldItem(storedItemPrefabData);
            }

            if (!CanDisplayItem(heldItem))
            {
                return false;
            }

            if (storedItemPrefabData == null)
            {
                return true;
            }

            return itemHolder.CanReplaceHeldItem(storedItemPrefabData);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            var heldItem = itemHolder.CurrentItemPrefabData;
            if (heldItem == null)
            {
                TakeStoredItem(itemHolder);
                return;
            }

            if (storedItemPrefabData == null)
            {
                StoreHeldItem(itemHolder, heldItem);
                return;
            }

            SwapHeldItem(itemHolder, heldItem);
        }

        public void SetInteractionFocus(IItemHolder itemHolder, bool isFocused)
        {
            focusedItemHolder = isFocused ? itemHolder : null;
            hasInteractionFocus = isFocused;
            RefreshGlowOutline();
        }

        public void ClearInteractionFocus(IItemHolder itemHolder)
        {
            if (focusedItemHolder != itemHolder)
            {
                return;
            }

            focusedItemHolder = null;
            hasInteractionFocus = false;
            RefreshGlowOutline();
        }

        private bool CanHighlightInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null || visualRoot == null || slotCollider == null)
            {
                return false;
            }

            var heldItem = itemHolder.CurrentItemPrefabData;
            if (heldItem == null)
            {
                return storedItemPrefabData != null && itemHolder.CanReplaceHeldItem(storedItemPrefabData);
            }

            if (!CanDisplayItem(heldItem, false))
            {
                return false;
            }

            return storedItemPrefabData == null || itemHolder.CanReplaceHeldItem(storedItemPrefabData);
        }

        private void TakeStoredItem(IItemHolder itemHolder)
        {
            var itemToHold = storedItemPrefabData;
            if (itemToHold == null)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_TAKE_FAILED reason=storedItem_missing slot={name}");
                return;
            }

            storedItemPrefabData = null;
            RefreshVisual();
            itemHolder.ReplaceHeldItem(itemToHold, transform);
            Debug.Log($"PHS_TOOL_BOX_SLOT_TAKEN slot={name} item={itemToHold.ItemId}");
        }

        private void StoreHeldItem(IItemHolder itemHolder, UtilityItemPrefabData heldItem)
        {
            if (!itemHolder.TryConsumeHeldItem(heldItem.ItemId))
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_STORE_FAILED reason=consume_failed slot={name} item={heldItem.ItemId}");
                return;
            }

            storedItemPrefabData = heldItem;
            RefreshVisual();
            Debug.Log($"PHS_TOOL_BOX_SLOT_STORED slot={name} item={heldItem.ItemId}");
        }

        private void SwapHeldItem(IItemHolder itemHolder, UtilityItemPrefabData heldItem)
        {
            var itemToHold = storedItemPrefabData;
            if (itemToHold == null)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_SWAP_FAILED reason=storedItem_missing slot={name}");
                return;
            }

            if (!itemHolder.TryConsumeHeldItem(heldItem.ItemId))
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_SWAP_FAILED reason=consume_failed slot={name} item={heldItem.ItemId}");
                return;
            }

            storedItemPrefabData = heldItem;
            RefreshVisual();
            itemHolder.ReplaceHeldItem(itemToHold, transform);
            Debug.Log($"PHS_TOOL_BOX_SLOT_SWAPPED slot={name} stored={heldItem.ItemId} held={itemToHold.ItemId}");
        }

        private bool ValidateSetup()
        {
            if (visualRoot == null)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_SETUP_FAILED reason=visualRoot_missing slot={name}");
                return false;
            }

            if (slotCollider == null)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_SETUP_FAILED reason=slotCollider_missing slot={name}");
                return false;
            }

            return true;
        }

        private bool CanDisplayItem(UtilityItemPrefabData itemPrefabData, bool shouldLog = true)
        {
            if (itemPrefabData == null)
            {
                if (shouldLog)
                {
                    Debug.LogError($"PHS_TOOL_BOX_SLOT_ITEM_FAILED reason=itemData_missing slot={name}");
                }

                return false;
            }

            if (!itemPrefabData.HasHeldPrefab)
            {
                if (shouldLog)
                {
                    Debug.LogError($"PHS_TOOL_BOX_SLOT_ITEM_FAILED reason=heldPrefab_missing slot={name} item={itemPrefabData.ItemId}");
                }

                return false;
            }

            if (!itemPrefabData.HeldPrefab.TryGetComponent<UtilityItemObject>(out _))
            {
                if (shouldLog)
                {
                    Debug.LogError($"PHS_TOOL_BOX_SLOT_ITEM_FAILED reason=utilityItemObject_missing slot={name} item={itemPrefabData.ItemId}");
                }

                return false;
            }

            return true;
        }

        private void RefreshVisual()
        {
            if (!ValidateSetup())
            {
                return;
            }

            if (visibleItemInstance != null)
            {
                Destroy(visibleItemInstance);
                visibleItemInstance = null;
            }

            if (storedItemPrefabData == null)
            {
                RefreshGlowOutline();
                return;
            }

            if (!CanDisplayItem(storedItemPrefabData))
            {
                return;
            }

            visibleItemInstance = Instantiate(storedItemPrefabData.HeldPrefab, visualRoot);
            visibleItemInstance.name = $"{name}_VisibleItem";
            var visualPosition = Vector3.zero;
            var visualEuler = visibleItemLocalEuler;
            var visualScale = visibleItemScale;
            if (TryGetVisualProfile(storedItemPrefabData, out var profile))
            {
                visualPosition = profile.LocalPosition;
                visualEuler = profile.LocalEuler;
                visualScale = profile.LocalScale;
            }

            visibleItemInstance.transform.SetLocalPositionAndRotation(visualPosition, Quaternion.Euler(visualEuler));
            visibleItemInstance.transform.localScale = Vector3.one * visualScale;

            foreach (var itemCollider in visibleItemInstance.GetComponentsInChildren<Collider>(true))
            {
                itemCollider.enabled = false;
            }

            if (visibleItemInstance.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }

            RefreshGlowOutline();
        }

        private void RefreshGlowOutline()
        {
            SetGlowOutline(hasInteractionFocus && CanHighlightInteract(focusedItemHolder));
        }

        private void SetGlowOutline(bool isActive)
        {
            if (glowOutlineRoot == null)
            {
                return;
            }

            glowOutlineRoot.SetActive(isActive);
        }

        private bool TryGetVisualProfile(UtilityItemPrefabData itemPrefabData, out ItemVisualProfile profile)
        {
            profile = null;
            if (itemPrefabData == null || visualProfiles == null)
            {
                return false;
            }

            foreach (var candidate in visualProfiles)
            {
                if (candidate != null && candidate.ItemPrefabData == itemPrefabData)
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
