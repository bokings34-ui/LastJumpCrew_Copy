using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 툴박스 보관 슬롯 하나를 담당하는 상호작용 컴포넌트다.
    // 빈손이면 보관된 아이템을 꺼내고, 아이템을 들고 있으면 보관 또는 교체한다.
    public sealed class UtilityToolBoxStorageSlotInteractable : MonoBehaviour, IInteractable
    {
        // 아이템마다 보관 슬롯 안에서 보이는 위치/회전/크기가 다를 때 쓰는 시각화 설정이다.
        [System.Serializable]
        private sealed class ItemVisualProfile
        {
            // 이 프로필을 적용할 아이템 데이터다.
            [SerializeField] private UtilityItemPrefabData itemPrefabData;

            // visualRoot 기준 로컬 위치다.
            [SerializeField] private Vector3 localPosition;

            // visualRoot 기준 로컬 회전이다.
            [SerializeField] private Vector3 localEuler = new(0f, 90f, 0f);

            // visualRoot 기준 표시 크기다.
            [SerializeField, Min(0.001f)] private float localScale = 0.12f;

            public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
            public Vector3 LocalPosition => localPosition;
            public Vector3 LocalEuler => localEuler;
            public float LocalScale => localScale;
        }

        // 현재 슬롯에 보관된 아이템 데이터다. null이면 빈 슬롯이다.
        [SerializeField] private UtilityItemPrefabData storedItemPrefabData;

        // 보관 아이템 프리뷰가 생성될 부모 Transform이다.
        [SerializeField] private Transform visualRoot;

        // 상호작용 가능할 때 켜는 슬롯 외곽선/강조 오브젝트다.
        [SerializeField] private GameObject glowOutlineRoot;

        // 슬롯의 상호작용/초점 기준 Collider다.
        [SerializeField] private BoxCollider slotCollider;

        // 아이템별 프리뷰 배치 보정 목록이다.
        [SerializeField] private ItemVisualProfile[] visualProfiles;

        // 상호작용 UI에 표시할 문구다.
        [SerializeField] private string interactionPrompt = "F";

        // 개별 프로필이 없을 때 쓰는 기본 프리뷰 회전이다.
        [SerializeField] private Vector3 visibleItemLocalEuler = new(0f, 90f, 0f);

        // 개별 프로필이 없을 때 쓰는 기본 프리뷰 크기다.
        [SerializeField, Min(0.001f)] private float visibleItemScale = 0.12f;

        // 슬롯 안에 보여주기 위해 런타임에 생성한 아이템 프리뷰다.
        private GameObject visibleItemInstance;

        // 현재 이 슬롯을 바라보는 플레이어 holder다.
        private IItemHolder focusedItemHolder;

        // 초점 상태 캐시다. glowOutlineRoot 활성화 계산에 사용한다.
        private bool hasInteractionFocus;
        private NetworkToolBoxStorageCoordinator networkCoordinator;
        private int networkSlotIndex = -1;

        public string InteractionPrompt => interactionPrompt;
        public UtilityItemPrefabData InitialStoredItemPrefabData => storedItemPrefabData;
        public bool IsNetworkManaged => networkCoordinator != null && networkCoordinator.IsSpawned;

        public void BindNetworkCoordinator(
            NetworkToolBoxStorageCoordinator coordinator,
            int slotIndex)
        {
            networkCoordinator = coordinator;
            networkSlotIndex = slotIndex;
        }

        public void ApplyNetworkStoredItem(UtilityItemPrefabData itemPrefabData)
        {
            storedItemPrefabData = itemPrefabData;
            RefreshVisual();
        }

        public bool TryReceiveDelivery(UtilityItemPrefabData itemPrefabData)
        {
            if (IsNetworkManaged)
            {
                return networkCoordinator.TryReceiveDeliveryServer(this, itemPrefabData);
            }

            if (storedItemPrefabData != null || !CanDisplayItem(itemPrefabData))
            {
                return false;
            }

            storedItemPrefabData = itemPrefabData;
            RefreshVisual();
            Debug.Log($"PHS_TOOL_BOX_SLOT_DELIVERY_RECEIVED slot={name} item={itemPrefabData.ItemId}");
            return true;
        }

        private void Awake()
        {
            networkCoordinator ??= GetComponentInParent<NetworkToolBoxStorageCoordinator>();
            RefreshVisual();
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            // 빈손+빈 슬롯이면 할 일이 없고, 든 아이템이 있으면 표시 가능한 프리팹인지 검사한다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_SLOT_INTERACT_FAILED reason=itemHolder_missing slot={name}");
                return false;
            }

            if (!ValidateSetup())
            {
                return false;
            }

            if (IsNetworkManaged)
            {
                return networkCoordinator.CanRequestInteraction(this, itemHolder);
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
            // 현재 손 상태와 슬롯 상태에 따라 꺼내기, 보관, 교체 중 하나만 수행한다.
            if (!CanInteract(itemHolder))
            {
                return;
            }

            if (IsNetworkManaged)
            {
                networkCoordinator.RequestInteraction(this, itemHolder);
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
            // Scanner가 바라보는 동안 호출된다. 실제 glow 가능 여부는 CanHighlightInteract에서 다시 계산한다.
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
            // 하이라이트는 "실제로 누르면 상호작용 가능한 상태"일 때만 켠다.
            if (itemHolder == null || visualRoot == null || slotCollider == null)
            {
                return false;
            }

            if (IsNetworkManaged)
            {
                return networkCoordinator.CanRequestInteraction(this, itemHolder);
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
            // 슬롯 아이템을 비우고 플레이어 손에 새로 생성한다.
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
            // 플레이어 손 아이템을 소비한 뒤 슬롯 데이터로 저장한다.
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
            // 슬롯 아이템과 손 아이템을 맞바꾼다. 먼저 손 아이템 소비가 성공해야 슬롯 상태를 바꾼다.
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
            // 슬롯은 visualRoot와 slotCollider가 있어야 프리뷰/초점 동작이 가능하다.
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
            // 보관 슬롯 프리뷰는 heldPrefab을 복제해서 보여준다.
            // 따라서 heldPrefab과 UtilityItemObject 연결이 필수다.
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
            // 저장된 아이템 데이터 기준으로 슬롯 프리뷰를 다시 만든다.
            // 기존 프리뷰는 매번 제거해서 보관/교체 상태와 표시가 어긋나지 않게 한다.
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
            CenterVisibleItem(visualPosition);

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
            // 초점 중이어도 현재 손/슬롯 상태에서 상호작용 불가면 glow를 끈다.
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
            // 아이템별 프로필이 있으면 기본 위치/회전/크기 대신 해당 값을 사용한다.
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

        private void CenterVisibleItem(Vector3 targetLocalCenter)
        {
            if (visibleItemInstance == null || visualRoot == null)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_CENTER_FAILED reason=visibleItem_missing slot={name}");
                return;
            }

            var renderers = visibleItemInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_CENTER_FAILED reason=renderer_missing slot={name} item={storedItemPrefabData.ItemId}");
                return;
            }

            var hasBounds = false;
            var localBounds = new Bounds();
            foreach (var itemRenderer in renderers)
            {
                if (itemRenderer == null)
                {
                    continue;
                }

                EncapsulateRendererBounds(itemRenderer, ref localBounds, ref hasBounds);
            }

            if (!hasBounds)
            {
                Debug.LogError($"PHS_TOOL_BOX_SLOT_CENTER_FAILED reason=bounds_missing slot={name} item={storedItemPrefabData.ItemId}");
                return;
            }

            visibleItemInstance.transform.localPosition += targetLocalCenter - localBounds.center;
        }

        private void EncapsulateRendererBounds(Renderer itemRenderer, ref Bounds localBounds, ref bool hasBounds)
        {
            var worldBounds = itemRenderer.bounds;
            var min = worldBounds.min;
            var max = worldBounds.max;
            EncapsulateLocalPoint(new Vector3(min.x, min.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(new Vector3(max.x, max.y, max.z), ref localBounds, ref hasBounds);
        }

        private void EncapsulateLocalPoint(Vector3 worldPoint, ref Bounds localBounds, ref bool hasBounds)
        {
            var localPoint = visualRoot.InverseTransformPoint(worldPoint);
            if (!hasBounds)
            {
                localBounds = new Bounds(localPoint, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(localPoint);
        }
    }
}
