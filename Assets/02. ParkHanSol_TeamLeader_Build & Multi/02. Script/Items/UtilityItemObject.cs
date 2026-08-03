using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AdaptivePerformance;
using UnityEngine.WSA;
using ParkInteraction = LastJumpCrew.ParkHanSol.Interaction;

namespace LastJumpCrew.ParkHanSol.Items
{
    
    /// 아이템 프리팹 루트에 붙는 런타임 컴포넌트입니다.
    /// 아이템의 실제 정적 정보는 UtilityItemDataSO가 관리하고,
    /// 이 컴포넌트는 월드 아이템의 줍기, 들기, 내려놓기 상태만 관리합니다.
    [DisallowMultipleComponent]
    public sealed class UtilityItemObject : MonoBehaviour, IHoldableItem, ParkInteraction.IInteractable
    {
        [Header("Item Data")]

        [Tooltip("이 프리팹이 어떤 아이템인지 나타내는 데이터입니다. " + "아이템 ID, 표시 이름, 프리팹, 내구도, 사용 설정을 제공합니다.")]
        [SerializeField]
        private UtilityItemDataSO itemData;


        // 현재 이 아이템을 들고 있는 대상입니다.
        // null이면 바닥이나 보관함에 놓인 상태로 봅니다.
        private LastJumpCrew.Common.IItemHolder currentHolder;

        // 아이템을 들기 전 Rigidbody 상태를 복원하기 위해서 필요함.
        private bool heldRigidbodyStateCached;
        private bool cachedUseGravity;
        private bool cachedIsKinematic;


        
        /// 이 오브젝트와 연결된 통합 아이템 데이터입니다.
        public UtilityItemDataSO ItemData => itemData;


        //기존 코드에서 이름 바꾸기 전에 잠시 사용
        public UtilityItemDataSO ItemPrefabData => itemData;


        public string ItemId => itemData == null ? string.Empty : itemData.ItemId;
        public string DisplayName => itemData == null ? string.Empty : itemData.DisplayName;
     
        /// 현재 아이템을 잡는 기준 Transform입니다.
        /// 추후 아이템 손잡이 위치가 따로 필요하면
        /// 프리팹 자식에 GripPoint를 만들고 반환값을 변경하면 됩니다.
        public Transform HoldTransform => transform;


        public string InteractionPrompt => "Pick Up";


        
        /// 현재 holder 참조를 기준으로 들린 상태를 확인합니다.
    
        public bool IsHeld => currentHolder != null;


   
        /// 상호작용을 요청한 플레이어가
        /// 이 아이템을 주울 수 있는지 검사합니다.
        public bool CanInteract(ParkInteraction.IItemHolder itemHolder)  
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_ITEM_PICKUP_FAILED " + $"reason=itemHolder_missing " + $"item={name}", this);
                return false;
            }

            if (IsHeld)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_PICKUP_FAILED " +
                    $"reason=already_held " +
                    $"item={name}",
                    this);

                return false;
            }

            if (itemData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED " + $"reason=itemData_missing " + $"item={name}", this);

                return false;
            }

            ///네트워크에 Spawn된 아이템은
            ///로컬에서 바로 Destroy하거나 holder에 지급하지 않습니다. 
            ///INetworkItemPickupRequester를 통해 서버에
            ///아이템 획득을 요청해야 합니다.
            if (TryGetSpawnedNetworkObject(out _))
            {
                if (itemHolder is not ParkInteraction.INetworkItemPickupRequester pickupRequester)
                {
                    Debug.LogError($"PHS_ITEM_PICKUP_FAILED " + $"reason=network_requester_missing " + $"item={name}", this);
                    return false;
                }

                return pickupRequester.CanRequestNetworkPickup(this);       
            }
            ///파편 아이템은 일반 유틸리티 아이템과 달리
            ///현재 월드 오브젝트 자체를 손에 붙입니다.
            if (TryGetComponent<ParkInteraction.DebrisItem>(out var debrisItem))
            {
                if (itemHolder is not ParkInteraction.IDebrisHolder debrisHolder)
                 
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=holder_unsupported " + $"debris={name}", this);
                    return false;
                }

                return debrisHolder.CanHoldDebris(debrisItem);
                  
            }

            return itemHolder.CanReplaceHeldItem(itemData);
              
        }


        /// <summary>
        /// 플레이어가 이 아이템과 상호작용했을 때 호출됩니다.
        /// </summary>
        public void Interact(
            ParkInteraction.IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            ///네트워크 아이템은 서버에 획득을 요청합니다.
            ///서버에서 검증한 뒤 월드 아이템을 Despawn하고
            ///플레이어의 NetworkPlayerItemRecord를 변경합니다.
            ///
            if (TryGetSpawnedNetworkObject(out _))
            {
                var pickupRequester =
                    (ParkInteraction.INetworkItemPickupRequester)
                    itemHolder;

                pickupRequester
                    .RequestNetworkPickup(this);

                return;
            }

            ///DebrisItem은 프리팹을 새로 만들지 않고
            ///현재 월드 오브젝트 자체를 들도록 처리합니다.
            if (TryGetComponent<ParkInteraction.DebrisItem>(out var debrisItem))
            {
                if (itemHolder is not ParkInteraction.IDebrisHolder debrisHolder)
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=holder_unsupported " + $"debris={name}", this);
                    return;
                }

                if (!debrisHolder.TryHoldDebris(debrisItem))
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=holder_rejected " + $"debris={name}", this);
                }

                return;
            }

            ///싱글 또는 비네트워크 월드 아이템은
            ///holder에게 UtilityItemDataSO를 전달합니다. 
            ///holder는 HandPrefab을 손에 생성하고,
            ///기존 월드 오브젝트는 제거합니다.
            itemHolder.ReplaceHeldItem(itemData, transform);
            Destroy(gameObject);

            Debug.Log($"PHS_ITEM_PICKED_UP " + $"item={itemData.ItemId}", this);
        }


  
        /// 현재 오브젝트에 Spawn된 NetworkObject가 있는지 확인합니다.
        private bool TryGetSpawnedNetworkObject(out NetworkObject itemNetworkObject)
        {
            itemNetworkObject = GetComponent<NetworkObject>();
            

            return itemNetworkObject != null && itemNetworkObject.IsSpawned;
        }


        
        /// 플레이어가 아이템을 획득해서 손에 붙였을 때 호출됩니다.
        /// holder 참조를 저장하고 Rigidbody 물리 시뮬레이션을 끕니다.
        public void OnPickedUp(LastJumpCrew.Common.IItemHolder holder)
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED " + $"reason=itemData_missing " + $"item={name}", this);
                return;
            }

            if (holder == null)
            {
                Debug.LogError($"PHS_ITEM_PICKUP_FAILED " + $"reason=holder_missing " + $"item={name}", this);
                return;
            }

            currentHolder = holder;

            if (!TryGetComponent<Rigidbody>(out var itemRigidbody))
            {
                return;
            }
            ///아이템을 다시 내려놓았을 때 원래 설정으로
            ///복원할 수 있도록 Rigidbody 상태를 저장합니다.
            cachedUseGravity =
                itemRigidbody.useGravity;

            cachedIsKinematic =
                itemRigidbody.isKinematic;

            heldRigidbodyStateCached = true;

            ///손에 붙은 아이템은 물리 시뮬레이션에서 제외
            ///물리가 켜진 상태로 플레이어 손이나 카메라에 붙으면
            ///충돌 때문에 플레이어가 밀리거나 아이템이 흔들릴 수 있음
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            

            itemRigidbody.useGravity = false;
            itemRigidbody.isKinematic = true;
        }
        /// 아이템을 월드에 내려놓았을 때 호출됩니다.
        /// holder 상태를 제거하고 원래 Rigidbody 설정을 복원합니다.
        public void OnDropped(Vector3 dropPosition)
        {
            currentHolder = null;

            transform.position = dropPosition;

            if (!TryGetComponent<Rigidbody>(out var itemRigidbody))          
            {
                return;
            }

            if (!heldRigidbodyStateCached)
            {
                return;
            }

            itemRigidbody.useGravity = cachedUseGravity;
            itemRigidbody.isKinematic = cachedIsKinematic;

            heldRigidbodyStateCached = false;
        }


#if UNITY_EDITOR

        private void OnValidate()
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_UTILITY_ITEM_OBJECT_INVALID " + $"reason=item_data_missing " + $"object={name}", this);

                return;
            }

            if (string.IsNullOrWhiteSpace(itemData.ItemId))   
            {
                Debug.LogError($"PHS_UTILITY_ITEM_OBJECT_INVALID " + $"reason=item_id_missing " + $"object={name} " + $"asset={itemData.name}", itemData);
            }
        }

#endif
    }
}
