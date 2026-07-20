using LastJumpCrew.Common;
using ParkInteraction = LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    // 아이템 프리팹 루트에 붙는 런타임 컴포넌트다.
    // 플레이어가 아이템을 들거나 내려놓을 때 IHoldableItem 인터페이스를 통해 이 컴포넌트를 호출한다.
    // 아이템별 기능은 여기에서 바로 늘리지 말고, 필요할 때 별도 컴포넌트로 붙이는 방향을 기본으로 둔다.
    public sealed class UtilityItemObject : MonoBehaviour, IHoldableItem, ParkInteraction.IInteractable
    {
        // 이 오브젝트가 어떤 아이템 데이터인지 알려주는 필수 참조다.
        // prefab root의 UtilityItemObject에 연결되어 있어야 플레이어 HUD, 툴박스 보관, 드롭 처리에서 같은 아이템으로 인식된다.
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        // 현재 이 아이템을 들고 있는 holder다.
        // null이면 바닥/보관함/씬에 놓인 상태로 본다.
        // 네트워크 소유권이나 실제 장비 슬롯 로직은 아직 여기에서 처리하지 않는다.
        private IItemHolder currentHolder;
        private bool heldRigidbodyStateCached;
        private bool cachedUseGravity;
        private bool cachedIsKinematic;

        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
        public string ItemId => itemPrefabData == null ? string.Empty : itemPrefabData.ItemId;
        public string DisplayName => itemPrefabData == null ? string.Empty : itemPrefabData.DisplayName;

        // 현재는 아이템 루트 transform을 그대로 잡는 지점으로 쓴다.
        // 추후 손잡이 위치가 따로 필요하면 GripPoint 같은 child를 만들고 여기 반환값을 바꾸면 된다.
        public Transform HoldTransform => transform;
        public string InteractionPrompt => "Pick Up";

        // holder 참조만 기준으로 든 상태를 판단한다.
        // 물리 상태나 parent 여부로 판단하면 보관함/프리뷰/네트워크 상황에서 꼬일 수 있어 단순 상태값으로 둔다.
        public bool IsHeld => currentHolder != null;

        public bool CanInteract(ParkInteraction.IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_ITEM_PICKUP_FAILED reason=itemHolder_missing item={name}");
                return false;
            }

            if (IsHeld)
            {
                Debug.LogWarning($"PHS_ITEM_PICKUP_FAILED reason=already_held item={name}");
                return false;
            }

            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=itemData_missing item={name}");
                return false;
            }

            if (TryGetSpawnedNetworkObject(out _))
            {
                if (itemHolder is not ParkInteraction.INetworkItemPickupRequester pickupRequester)
                {
                    Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=network_requester_missing item={name}");
                    return false;
                }

                return pickupRequester.CanRequestNetworkPickup(this);
            }

            if (TryGetComponent<ParkInteraction.DebrisItem>(out var debrisItem))
            {
                if (itemHolder is not ParkInteraction.IDebrisHolder debrisHolder)
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=holder_unsupported debris={name}");
                    return false;
                }

                return debrisHolder.CanHoldDebris(debrisItem);
            }

            return itemHolder.CanReplaceHeldItem(itemPrefabData);
        }

        public void Interact(ParkInteraction.IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            if (TryGetSpawnedNetworkObject(out _))
            {
                ((ParkInteraction.INetworkItemPickupRequester)itemHolder).RequestNetworkPickup(this);
                return;
            }

            if (TryGetComponent<ParkInteraction.DebrisItem>(out var debrisItem))
            {
                if (itemHolder is not ParkInteraction.IDebrisHolder debrisHolder)
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=holder_unsupported debris={name}");
                    return;
                }

                if (!debrisHolder.TryHoldDebris(debrisItem))
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=holder_rejected debris={name}");
                }

                return;
            }

            itemHolder.ReplaceHeldItem(itemPrefabData, transform);
            Destroy(gameObject);
            Debug.Log($"PHS_ITEM_PICKED_UP item={itemPrefabData.ItemId}");
        }

        private bool TryGetSpawnedNetworkObject(out NetworkObject itemNetworkObject)
        {
            itemNetworkObject = GetComponent<NetworkObject>();
            return itemNetworkObject != null && itemNetworkObject.IsSpawned;
        }

        // 플레이어가 아이템을 획득해서 손에 붙였을 때 호출된다.
        // 여기서는 소유자 기록과 물리 비활성화만 담당한다.
        // HUD 갱신은 holder 쪽에서 UtilityItemPrefabData를 보고 처리한다.
        public void OnPickedUp(IItemHolder holder)
        {
            // itemPrefabData가 없으면 이 아이템은 ID/이름/아이콘을 알 수 없다.
            // 이 경우 조용히 대체하지 않고 로그를 남겨 prefab 연결 문제를 드러낸다.
            if (itemPrefabData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=itemData_missing item={name}");
                return;
            }

            // holder가 없으면 누가 들고 있는지 추적할 수 없다.
            // 드롭/교체 상태가 꼬이지 않도록 실패 처리한다.
            if (holder == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED reason=holder_missing item={name}");
                return;
            }

            currentHolder = holder;

            if (TryGetComponent<Rigidbody>(out var rigidbody))
            {
                cachedUseGravity = rigidbody.useGravity;
                cachedIsKinematic = rigidbody.isKinematic;
                heldRigidbodyStateCached = true;
                // 손에 들린 아이템은 물리 시뮬레이션을 끈다.
                // 중력/충돌 힘이 켜진 채 손에 붙으면 캐릭터나 카메라가 튀는 문제가 생긴다.
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
            }
        }

        // 아이템을 바닥에 내려놓았을 때 호출된다.
        // holder 상태를 비우고, 드롭 위치로 이동한 뒤 물리를 다시 켠다.
        public void OnDropped(Vector3 dropPosition)
        {
            currentHolder = null;
            transform.position = dropPosition;

            if (TryGetComponent<Rigidbody>(out var rigidbody))
            {
                if (heldRigidbodyStateCached)
                {
                    rigidbody.useGravity = cachedUseGravity;
                    rigidbody.isKinematic = cachedIsKinematic;
                    heldRigidbodyStateCached = false;
                }
            }
        }
    }
}
