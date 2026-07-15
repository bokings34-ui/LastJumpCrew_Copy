using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using LastJumpCrew.ParkHanSol.Multiplayer;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 플레이어 카메라 정면 Raycast로 상호작용 대상을 찾는 테스트용 스캐너다.
    // F는 상호작용, 좌클릭은 현재 든 아이템 사용, 우클릭은 현재 든 아이템 내려놓기다.
    // 우클릭 내려놓기는 추후 던지기로 교체할 입력 자리다.
    public sealed class TempPlayerInteractionScanner : MonoBehaviour
    {
        // Raycast 시작점이다. 보통 플레이어 카메라를 Inspector에서 연결한다.
        [SerializeField] private Camera interactionCamera;

        // 상호작용 최대 거리다.
        [SerializeField, Min(0.1f)] private float interactDistance = 2.5f;

        // 상호작용 대상 레이어 필터다. Trigger도 감지한다.
        [SerializeField] private LayerMask interactableLayers = ~0;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;

        // 같은 오브젝트에 붙은 아이템 보유 컴포넌트다.
        private IItemHolder itemHolder;

        // 공용 아이템 사용 계약을 처리하기 위한 보유 컴포넌트다.
        private CommonInteraction.IItemHolder commonItemHolder;

        // 멀티에서 내 소유 플레이어만 입력과 초점 표시를 처리하기 위한 NetworkObject다.
        private NetworkObject networkObject;

        // 로컬 일시정지 메뉴가 열린 동안 상호작용 입력을 막기 위한 플레이어 제어기다.
        private NetworkPlayerController playerController;

        // 현재 바라보는 툴박스 슬롯이다. 슬롯 자체 glowOutlineRoot 제어에 사용한다.
        private UtilityToolBoxStorageSlotInteractable focusedToolBoxSlot;

        // 현재 바라보는 일반 상호작용 대상의 외곽선 glow다.
        private InteractableFocusGlow focusedGlow;

        private void Awake()
        {
            itemHolder = GetComponent<IItemHolder>();
            commonItemHolder = GetComponent<CommonInteraction.IItemHolder>();
            networkObject = GetComponent<NetworkObject>();
            playerController = GetComponent<NetworkPlayerController>();
        }

        private void Update()
        {
            // 네트워크 스폰 후 소유자가 아니면 입력/초점 표시를 하지 않는다.
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            {
                ClearToolBoxSlotFocus();
                ClearInteractionPrompt();
                return;
            }

            if (playerController != null && !playerController.CanAcceptLocalInput)
            {
                ClearToolBoxSlotFocus();
                ClearInteractableFocusGlow();
                ClearInteractionPrompt();
                return;
            }

            RefreshToolBoxSlotFocus();
            RefreshInteractableFocusGlow();

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                TryInteract();
            }

            if (Mouse.current == null)
            {
                return;
            }
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (TryInteractWithFocusedToolBoxSlot())
                {
                    return;
                }

                TryUseHeldItem();
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                PlaceHeldItem();
            }
        }

        private void OnDisable()
        {
            ClearToolBoxSlotFocus();
            ClearInteractableFocusGlow();
            ClearInteractionPrompt();
        }

        public void BindPlayHudPresenter(ParkHanSolPlayHudMockPresenter presenter)
        {
            playHudPresenter = presenter;
        }

        private void PlaceHeldItem()
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TEMP_PLACE_FAILED reason=itemHolder_missing player={name}");
                return;
            }

            itemHolder.PlaceHeldItem();
        }

        private void TryUseHeldItem()
        {
            if (commonItemHolder == null)
            {
                Debug.LogWarning($"PHS_TEMP_USE_FAILED reason=commonItemHolder_missing player={name}");
                return;
            }

            var heldItem = commonItemHolder.CurrentItem;
            if (heldItem == null)
            {
                Debug.LogWarning($"PHS_TEMP_USE_FAILED reason=heldItem_missing player={name}");
                return;
            }

            if (!TryGetUsableItem(heldItem, out var usableItem))
            {
                Debug.LogWarning($"PHS_TEMP_USE_FAILED reason=usableItem_missing player={name} item={heldItem.ItemId}");
                return;
            }

            TryGetCommonInteractableTarget(out var target);

            if (!usableItem.CanUse(commonItemHolder, target))
            {
                var targetPrompt = target == null ? "none" : target.InteractionPrompt;
                Debug.LogWarning($"PHS_TEMP_USE_FAILED reason=canUse_false player={name} item={heldItem.ItemId} target={targetPrompt}");
                return;
            }

            usableItem.Use(commonItemHolder, target);
        }

        private bool TryInteractWithFocusedToolBoxSlot()
        {
            // 툴박스 슬롯은 좌클릭에서도 F 상호작용과 같은 공용 보관/꺼내기 규칙을 사용한다.
            // 슬롯을 바라보지 않는 좌클릭은 기존 아이템 사용 입력으로 그대로 넘긴다.
            if (focusedToolBoxSlot == null)
            {
                return false;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_LEFT_CLICK_FAILED reason=itemHolder_missing player={name}");
                return true;
            }

            if (!focusedToolBoxSlot.CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_TOOL_BOX_LEFT_CLICK_FAILED reason=canInteract_false player={name} slot={focusedToolBoxSlot.name}");
                return true;
            }

            focusedToolBoxSlot.Interact(itemHolder);
            return true;
        }

        private static bool TryGetUsableItem(CommonInteraction.IHoldableItem heldItem, out CommonInteraction.IUsableItem usableItem)
        {
            usableItem = null;
            if (heldItem == null)
            {
                return false;
            }

            if (heldItem is CommonInteraction.IUsableItem directUsableItem)
            {
                usableItem = directUsableItem;
                return true;
            }

            if (heldItem is not Component heldItemComponent)
            {
                return false;
            }

            usableItem = heldItemComponent.GetComponent<CommonInteraction.IUsableItem>();
            return usableItem != null;
        }

        private void TryInteract()
        {
            // 카메라 정면으로 Raycast해서 가장 먼저 맞은 IInteractable을 실행한다.
            if (interactionCamera == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=camera_missing player={name}");
                return;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=itemHolder_missing player={name}");
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_TARGET_MISSING player={name}");
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=interactable_missing target={hit.collider.name}");
                return;
            }

            if (!interactable.CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=canInteract_false target={hit.collider.name}");
                return;
            }

            interactable.Interact(itemHolder);
        }

        private bool TryGetCommonInteractableTarget(out CommonInteraction.IInteractable interactable)
        {
            interactable = null;
            if (interactionCamera == null)
            {
                Debug.LogWarning($"PHS_TEMP_USE_FAILED reason=camera_missing player={name}");
                return false;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                return false;
            }

            interactable = hit.collider.GetComponentInParent<CommonInteraction.IInteractable>();
            if (interactable == null)
            {
                return false;
            }

            return true;
        }

        private void RefreshToolBoxSlotFocus()
        {
            // 툴박스 슬롯은 자체 보관 상태와 손 아이템 상태를 보고 하이라이트 여부를 계산한다.
            if (interactionCamera == null || itemHolder == null)
            {
                ClearToolBoxSlotFocus();
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                ClearToolBoxSlotFocus();
                return;
            }

            var toolBoxSlot = hit.collider.GetComponentInParent<UtilityToolBoxStorageSlotInteractable>();
            if (toolBoxSlot == focusedToolBoxSlot)
            {
                focusedToolBoxSlot?.SetInteractionFocus(itemHolder, true);
                return;
            }

            ClearToolBoxSlotFocus();
            focusedToolBoxSlot = toolBoxSlot;
            focusedToolBoxSlot?.SetInteractionFocus(itemHolder, true);
        }

        private void ClearToolBoxSlotFocus()
        {
            if (focusedToolBoxSlot == null)
            {
                return;
            }

            focusedToolBoxSlot.ClearInteractionFocus(itemHolder);
            focusedToolBoxSlot = null;
        }

        private void RefreshInteractableFocusGlow()
        {
            // 일반 아이템/상호작용 대상은 InteractableFocusGlow가 붙어 있고 CanInteract가 true일 때만 빛난다.
            if (interactionCamera == null || itemHolder == null)
            {
                ClearInteractableFocusGlow();
                ClearInteractionPrompt();
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                ClearInteractableFocusGlow();
                ClearInteractionPrompt();
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            var canInteract = interactable != null && interactable.CanInteract(itemHolder);
            playHudPresenter?.SetInteractionPrompt("F", canInteract ? interactable.InteractionPrompt : string.Empty);

            var glow = hit.collider.GetComponentInParent<InteractableFocusGlow>();
            if (glow == null || !canInteract)
            {
                ClearInteractableFocusGlow();
                return;
            }

            if (glow == focusedGlow)
            {
                focusedGlow?.SetFocused(true);
                return;
            }

            ClearInteractableFocusGlow();
            focusedGlow = glow;
            focusedGlow?.SetFocused(true);
        }

        private void ClearInteractableFocusGlow()
        {
            if (focusedGlow == null)
            {
                return;
            }

            focusedGlow.SetFocused(false);
            focusedGlow = null;
        }

        private void ClearInteractionPrompt()
        {
            playHudPresenter?.SetInteractionPrompt(string.Empty, string.Empty);
        }
        
    }
}
