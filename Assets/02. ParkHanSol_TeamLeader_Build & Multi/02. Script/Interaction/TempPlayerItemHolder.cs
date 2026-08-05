using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 테스트 플레이어의 손 아이템 상태를 관리한다.
    // 아이템 지급, 줍기, 드롭, 소비, HUD 갱신이 이 컴포넌트를 통해 흐른다.
    public sealed class TempPlayerItemHolder :
        MonoBehaviour,
        IItemHolder,
        IDebrisHolder,
        INetworkItemPickupRequester,
        LastJumpCrew.Common.IItemHolder
    {
        [Header("Hold Points")]

        // 로컬 1인칭 화면에 보이는 아이템 위치다.
        [SerializeField]
        private Transform holdPoint;

        // 멀티 원격/외부 시점에서 보이는 아이템 위치다.
        [SerializeField]
        private Transform visibleHandHoldPoint;

        // 아이템을 내려놓을 기준 위치다.
        // 비어 있으면 플레이어 transform 기준으로 배치한다.
        [SerializeField]
        private Transform dropPoint;

        // dropPoint 기준 로컬 드롭 오프셋이다.
        [SerializeField]
        private Vector3 droppedLocalOffset =
            new(0f, 0f, 1f);

        [Header("Drop Motion")]

        [SerializeField]
        private ItemDropMotionProfile dropMotionProfile;

        [Header("Held Item Scale")]

        // 1인칭 화면에서 보이는 아이템 크기 배율이다.
        [SerializeField, Min(0.01f)]
        private float firstPersonHeldItemScale = 0.42f;

        // 다른 플레이어에게 보이는 아이템 크기 배율이다.
        [SerializeField, Min(0.01f)]
        private float worldHeldItemScale = 0.32f;

        // 들고 있는 아이템 이름/아이콘/내구도 표시용 HUD presenter다.
        [SerializeField]
        private ParkHanSolPlayHudMockPresenter playHudPresenter;

        // 현재 손에 생성된 아이템 프리팹 인스턴스다.
        private GameObject heldItemInstance;

        // heldItemInstance 루트의 UtilityItemObject 캐시다.
        private UtilityItemObject currentItemObject;

        // 현재 아이템의 데이터 캐시다. 빈손이면 null이다.
        // 변경: UtilityItemPrefabData → UtilityItemDataSO
        private UtilityItemDataSO currentItemPrefabData;

        private DebrisItem heldDebris;
        private Vector3 heldDebrisWorldScale;
        private Collider[] heldDebrisColliders;
        private bool[] heldDebrisColliderStates;
        private bool[] heldDebrisTriggerStates;
        private NetworkObject heldDebrisNetworkObject;
        private bool heldDebrisAutoObjectParentSync;
        private NetworkObject networkObject;
        private NetworkPlayerItemRecord networkItemRecord;
        private NetworkPlayerItemLifecycle networkItemLifecycle;

        // 변수 이름은 기존 코드를 최대한 유지하기 위해 그대로 두고, 반환 타입만 UtilityItemDataSO로 변경
        public UtilityItemDataSO CurrentItemPrefabData => currentItemPrefabData;

        public ItemDropMotionProfile DropMotionProfile => dropMotionProfile;

        public DebrisItem HeldDebris => heldDebris;
     
        public float HeldDebrisMass => heldDebris == null ? 0f : heldDebris.Mass;
        public Transform HeldPresentationTransform => heldItemInstance == null
            ? null
            : heldItemInstance.transform;
        public Vector3 DropPosition
        {
            get
            {
                var source = dropPoint == null ? transform : dropPoint;
                return source.TransformPoint(droppedLocalOffset);
            }
        }
        LastJumpCrew.Common.IHoldableItem LastJumpCrew.Common.IItemHolder.CurrentItem => currentItemObject;
        public bool HasItem => IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned
            ? !string.IsNullOrEmpty(networkItemRecord.HeldItemId)
            : currentItemPrefabData != null;

        private Transform ActiveHoldPoint => ShouldUseFirstPersonHoldPoint() ? holdPoint : visibleHandHoldPoint;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
           
            networkItemRecord = GetComponent<NetworkPlayerItemRecord>();
          
            networkItemLifecycle = GetComponent<NetworkPlayerItemLifecycle>();
       
            if (holdPoint == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_SETUP_FAILED " + $"reason=first_person_hold_point_missing " + $"player={name}", this);
            }
            if (visibleHandHoldPoint == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_SETUP_FAILED " + $"reason=world_hold_point_missing " + $"player={name}", this);
            }
        }
        private void OnEnable()
        {
            if (networkItemRecord != null)
            {
                networkItemRecord.HeldItemChanged += HandleNetworkHeldItemChanged;
                networkItemRecord.DurabilityChanged += HandleNetworkDurabilityChanged;
            }
        }
        private void Start()
        {
            SynchronizeNetworkHeldPresentation();
        }

        private void OnDisable()
        {
            if (networkItemRecord != null)
            {
                networkItemRecord.HeldItemChanged -= HandleNetworkHeldItemChanged;
                networkItemRecord.DurabilityChanged -= HandleNetworkDurabilityChanged;
            }
        }

        // 변경: UtilityItemPrefabData → UtilityItemDataSO
        public bool CanReplaceHeldItem(UtilityItemDataSO itemPrefabData)
        {
            // 손 위치, 아이템 데이터, HandPrefab 참조만 검사한다.
            // 실제 교체/드롭은 ReplaceHeldItem에서 수행한다.
            if (ActiveHoldPoint == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=holdPoint_missing " + $"player={name}");
                return false;
            }
            if (itemPrefabData == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=itemData_missing " + $"player={name}");
                return false;
            }

            // 변경: HasHeldPrefab → HasHandPrefab
            if (!itemPrefabData.HasHandPrefab)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=handPrefab_missing " + $"item={itemPrefabData.ItemId}");
                return false;
            }

            if (!itemPrefabData.TryGetHeldPose(ShouldUseFirstPersonHoldPoint(), out _))
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=held_pose_invalid " + $"item={itemPrefabData.ItemId}", itemPrefabData);
                return false;
            }

            if (IsNetworkSessionActive()
                && networkItemRecord != null
                && networkItemRecord.IsSpawned
                && !string.IsNullOrEmpty(networkItemRecord.HeldItemId)
                && (networkObject == null
                    || networkObject.NetworkManager == null
                    || !networkObject.NetworkManager.IsServer))
            {
                // 월드 아이템 획득은 INetworkItemPickupRequester RPC 경로가 교체를 처리한다.
                // 직접 ReplaceHeldItem을 호출하는 자판기/툴박스는 서버 권한 경로가 없으므로 클라이언트 로컬 교체를 열지 않는다.
                return false;
            }
            return true;
        }
        public void GetDropPose(out Vector3 position, out Quaternion rotation)
        {
            var source = dropPoint == null ? transform : dropPoint;
            position = source.TransformPoint(droppedLocalOffset);
            rotation = source.rotation;
        }

        public void ReplaceHeldItem(UtilityItemDataSO itemPrefabData, Transform interactionSource)
        {
            if (!CanReplaceHeldItem(itemPrefabData))
            {
                return;
            }
            if (IsNetworkSessionActive() && networkObject != null && networkObject.IsSpawned)
            {
                if (networkObject.NetworkManager != null && networkObject.NetworkManager.IsServer)
                {
                    if (networkItemLifecycle == null || !networkItemLifecycle.TryAssignHeldItemServer(itemPrefabData))
                    {
                        Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=network_assign_rejected " + $"player={name} " + $"item={itemPrefabData.ItemId}");
                    }
                    return;
                }
                if (!networkObject.IsOwner)
                {
                    Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=owner_required " + $"player={name} " + $"item={itemPrefabData.ItemId}");
                    return;
                }
            }
            // 한 손에 하나만 들 수 있으므로
            // 기존 아이템을 먼저 월드에 내려놓는다.
            if (!TryPlaceCurrentItem())
            {
                return;
            }
            var activeHoldPoint = ActiveHoldPoint;
            // 변경: HeldPrefab → HandPrefab
            // 아이템 프리팹은 손 위치 자식으로 생성하고,
            // 부모 스케일을 보정한다.
            heldItemInstance = Instantiate(itemPrefabData.HandPrefab, activeHoldPoint);

            heldItemInstance.name = itemPrefabData.HandPrefab.name;
            if (!TryApplyHeldItemPose(heldItemInstance.transform, itemPrefabData, activeHoldPoint, itemPrefabData.HandPrefab.transform.localScale))
            {
                Destroy(heldItemInstance);

                heldItemInstance = null;
                return;
            }

            currentItemObject = heldItemInstance.GetComponent<UtilityItemObject>();
            currentItemPrefabData = itemPrefabData;
            if (currentItemObject == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=utilityItemObject_missing " + $"item={itemPrefabData.ItemId}");
                Destroy(heldItemInstance);

                heldItemInstance = null;
                currentItemPrefabData = null;

                return;
            }

            currentItemObject.OnPickedUp(this);

            StopHeldDebrisMotion();
            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_HELD " + $"player={name} " + $"item={itemPrefabData.ItemId}");
        }

        public bool CanHold(LastJumpCrew.Common.IHoldableItem item)
        {
            if (item is not UtilityItemObject utilityItemObject)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=unsupported_item_type " + $"player={name}");
                return false;
            }

            // 변경: ItemPrefabData → ItemData
            return CanReplaceHeldItem(utilityItemObject.ItemData);
        }

        public bool CanRequestNetworkPickup(UtilityItemObject itemObject)
        {
            return networkItemLifecycle != null && networkItemLifecycle.CanRequestNetworkPickup(itemObject);
        }

        public void RequestNetworkPickup(
            UtilityItemObject itemObject)
        {
            if (networkItemLifecycle == null)
            {
                Debug.LogError($"PHS_NETWORK_ITEM_PICKUP_REJECTED " + $"reason=lifecycle_missing " + $"player={name}");
                return;
            }

            networkItemLifecycle.RequestNetworkPickup(itemObject);
        }

        public void Hold(LastJumpCrew.Common.IHoldableItem item)
        {
            if (item is not UtilityItemObject utilityItemObject)
            {
                Debug.LogError($"PHS_TEMP_ITEM_HOLD_FAILED " + $"reason=unsupported_item_type " + $"player={name}");
                return;
            }

            if (utilityItemObject.TryGetComponent<DebrisItem>(out var debrisItem))
            {
                TryHoldDebris(debrisItem);
                return;
            }
            // 변경: ItemPrefabData → ItemData
            ReplaceHeldItem(utilityItemObject.ItemData, utilityItemObject.transform);
        }

        public bool TryHoldDebris(DebrisItem debrisItem)
        {
            if (!CanHoldDebris(debrisItem))
            {
                return false;
            }

            var itemObject = debrisItem.GetComponent<UtilityItemObject>();
            if (!TryPlaceCurrentItem())
            {
                return false;
            }

            var activeHoldPoint = ActiveHoldPoint;

            heldDebris = debrisItem;
            heldItemInstance = debrisItem.gameObject;
            currentItemObject = itemObject;
            // 변경: ItemPrefabData → ItemData
            currentItemPrefabData = itemObject.ItemData;

            heldDebrisWorldScale = debrisItem.transform.lossyScale;

            CacheAndPrepareHeldDebrisColliders();
            heldDebrisNetworkObject = debrisItem.GetComponent<NetworkObject>();
            if (!IsNetworkSessionActive() && heldDebrisNetworkObject != null)
            {
                heldDebrisAutoObjectParentSync =
                    heldDebrisNetworkObject.AutoObjectParentSync;
                heldDebrisNetworkObject.AutoObjectParentSync = false;
            }

            heldItemInstance.transform.SetParent(activeHoldPoint, false);

            if (!TryApplyHeldItemPose(heldItemInstance.transform, currentItemPrefabData, activeHoldPoint, heldDebrisWorldScale))
            {
                heldItemInstance.transform.SetParent(null, true);
                RestoreHeldDebrisColliders();
                ClearHeldDebrisState();

                heldItemInstance = null;
                currentItemObject = null;
                currentItemPrefabData = null;

                return false;
            }

            StopHeldDebrisMotion();

            currentItemObject.OnPickedUp(this);

            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_DEBRIS_HELD " + $"player={name} " + $"debris={debrisItem.name} " + $"mass={debrisItem.Mass:F2} " + $"value={debrisItem.Value}");

            return true;
        }

        public bool CanHoldDebris(DebrisItem debrisItem)
        {
            if (debrisItem == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=debris_missing " + $"player={name}");
                return false;
            }

            if (ActiveHoldPoint == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=hold_point_missing " + $"player={name}");
                return false;
            }

            var itemObject = debrisItem.GetComponent<UtilityItemObject>();
            // 변경: ItemPrefabData → ItemData
            if (itemObject == null || itemObject.ItemData == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=item_setup_invalid " + $"player={name} " + $"debris={debrisItem.name}");
                return false;
            }

            var colliders = debrisItem.GetComponentsInChildren<Collider>(true);

            foreach (var targetCollider in colliders)
            {
                if (targetCollider is MeshCollider meshCollider && !meshCollider.convex)
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED " + $"reason=non_convex_mesh_collider " + $"player={name} " + $"debris={debrisItem.name} " + $"collider={targetCollider.name}");
                 
                    return false;
                }
            }

            return true;
        }

        public void Drop()
        {
            PlaceHeldItem();
        }

        public void PlaceHeldItem()
        {
            if (IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned && !string.IsNullOrEmpty(networkItemRecord.HeldItemId))
            {
                var source = dropPoint == null ? transform : dropPoint;

                var position = source.TransformPoint(droppedLocalOffset);
      
                if (networkItemLifecycle == null || !networkItemLifecycle.RequestPlaceHeldItem(position, source.rotation))
                {
                    Debug.LogWarning($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=network_request_rejected " + $"player={name}");
                }

                return;
            }

            TryPlaceCurrentItem();
        }

        public bool TryConsumeHeldItem(string itemId)
        {
            // 소비는 정확한 itemId만 허용한다.
            // 잘못된 아이템이 사라지면 원인 추적이 어려우므로
            // 실패 로그를 남긴다.
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"PHS_TEMP_ITEM_CONSUME_FAILED " + $"reason=itemId_missing " + $"player={name}");

                return false;
            }

            if (currentItemPrefabData == null)
            {
                if (IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned && string.IsNullOrEmpty(networkItemRecord.HeldItemId))
                {
                    return true;
                }

                Debug.LogWarning($"PHS_TEMP_ITEM_CONSUME_FAILED " + $"reason=heldItem_missing " + $"player={name} " + $"item={itemId}");
                return false;
            }

            if (currentItemPrefabData.ItemId != itemId)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_CONSUME_FAILED " + $"reason=wrong_item " + $"player={name} " + $"expected={itemId} " + $"actual={currentItemPrefabData.ItemId}");

                return false;
            }

            if (heldItemInstance == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_CONSUME_FAILED " + $"reason=heldItemInstance_missing " + $"player={name} " + $"item={itemId}");

                return false;
            }

            heldItemInstance.SetActive(false);
            Destroy(heldItemInstance);

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            ClearHeldDebrisState();
            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_CONSUMED player={name} item={itemId}");
            return true;
        }

        public void BindPlayHudPresenter(ParkHanSolPlayHudMockPresenter presenter)   
        {
            playHudPresenter = presenter;

            RefreshHeldItemHud();
        }

        private bool TryPlaceCurrentItem()
        {
            if (currentItemPrefabData == null)
            {
                return true;
            }

            if (heldDebris != null)
            {
                return TryPlaceHeldDebris();
            }

            if (dropMotionProfile == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=drop_motion_profile_missing " + $"player={name}");

                return false;
            }

            if (IsNetworkSessionActive())
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=network_lifecycle_required " + $"player={name} " + $"item={currentItemPrefabData.ItemId}", this);
                return false;
            }

            var placedPrefab = currentItemPrefabData.DroppedPrefab;

            if (placedPrefab == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=placedPrefab_missing " + $"item={currentItemPrefabData.ItemId}");
                return false;
            }

            var source = dropPoint == null ? transform : dropPoint;

            var position = source.TransformPoint(droppedLocalOffset);

            var droppedItemInstance =Instantiate(placedPrefab, position, source.rotation);

            var droppedItemObject = droppedItemInstance.GetComponent<UtilityItemObject>();
 
            var droppedRigidbody = droppedItemInstance.GetComponent<Rigidbody>();
   
            if (droppedItemObject == null || droppedRigidbody == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=drop_contract_invalid " + $"item={currentItemPrefabData.ItemId} " + $"itemObject={droppedItemObject != null} " + $"rigidbody={droppedRigidbody != null}");
  
                Destroy(droppedItemInstance);
                return false;
            }

            if (!dropMotionProfile.TryResolveFloorPlacement(
                    droppedRigidbody,
                    position,
                    source.rotation,
                    transform.root,
                    out var resolvedPosition,
                    out var resolvedRotation))
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=floor_placement_rejected " + $"item={currentItemPrefabData.ItemId}");
                Destroy(droppedItemInstance);
                return false;
            }

            droppedItemInstance.transform.SetPositionAndRotation(
                resolvedPosition,
                resolvedRotation);
            droppedItemObject.OnDropped(resolvedPosition);

            if (!dropMotionProfile.TryApply(droppedRigidbody, resolvedRotation))
            {
                Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED " + $"reason=drop_motion_rejected " + $"item={currentItemPrefabData.ItemId}");

                Destroy(droppedItemInstance);
                return false;
            }

            Debug.Log($"PHS_TEMP_ITEM_PLACED player={name} item={currentItemPrefabData.ItemId}");
            if (heldItemInstance != null)
            {
                Destroy(heldItemInstance);
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;
            ReportHeldItemRecord();
            RefreshHeldItemHud();
            return true;
        }

        private bool TryPlaceHeldDebris()
        {
            if (heldDebris == null || heldItemInstance == null || currentItemObject == null)
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=held_state_invalid " + $"player={name}");
                return false;
            }

            if (dropMotionProfile == null)
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=drop_motion_profile_missing " + $"player={name}");
                return false;
            }

            if (!heldDebris.TryGetComponent<Rigidbody>(out var debrisRigidbody))
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=rigidbody_missing " + $"player={name} " + $"debris={heldDebris.name}");
                return false;
            }

            var source = dropPoint == null ? transform : dropPoint;

            var position = source.TransformPoint(droppedLocalOffset);

            var debrisName = heldDebris.name;

            heldItemInstance.transform.SetParent(null, true);

            heldItemInstance.transform.localScale = heldDebrisWorldScale;
      
            RestoreHeldDebrisColliders();

            if (!dropMotionProfile.TryResolveFloorPlacement(
                    debrisRigidbody,
                    position,
                    source.rotation,
                    transform.root,
                    out var resolvedPosition,
                    out var resolvedRotation))
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=floor_placement_rejected " + $"player={name} " + $"debris={debrisName}");
                return false;
            }

            heldItemInstance.transform.SetPositionAndRotation(
                resolvedPosition,
                resolvedRotation);

            currentItemObject.OnDropped(resolvedPosition);

            if (!dropMotionProfile.TryApply(
                    debrisRigidbody,
                    resolvedRotation))
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=drop_motion_rejected " + $"player={name} " + $"debris={debrisName}");

                return false;
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            ClearHeldDebrisState();
            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_DEBRIS_PLACED " + $"player={name} " + $"debris={debrisName}");

            return true;
        }

        private void CacheAndPrepareHeldDebrisColliders()
        {
            heldDebrisColliders = heldItemInstance.GetComponentsInChildren<Collider>(true);
 
            heldDebrisColliderStates = new bool[heldDebrisColliders.Length];

            heldDebrisTriggerStates = new bool[heldDebrisColliders.Length];
            for (var index = 0; index < heldDebrisColliders.Length; index++)
            {
                heldDebrisColliderStates[index] = heldDebrisColliders[index].enabled;

                heldDebrisTriggerStates[index] = heldDebrisColliders[index].isTrigger;
          
                heldDebrisColliders[index].isTrigger = true;
            }
        }

        private void StopHeldDebrisMotion()
        {
            if (heldDebris == null || !heldDebris.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                return;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;

            rigidbody.Sleep();
        }

        private void RestoreHeldDebrisColliders()
        {
            if (heldDebrisColliders == null
                || heldDebrisColliderStates == null
                || heldDebrisTriggerStates == null
                || heldDebrisColliders.Length
                    != heldDebrisColliderStates.Length
                || heldDebrisColliders.Length
                    != heldDebrisTriggerStates.Length)
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED " + $"reason=collider_state_invalid " + $"player={name}");

                return;
            }

            for (var index = 0; index < heldDebrisColliders.Length; index++)
            {
                if (heldDebrisColliders[index] != null)
                {
                    heldDebrisColliders[index].enabled = heldDebrisColliderStates[index];
           
                    heldDebrisColliders[index].isTrigger = heldDebrisTriggerStates[index];
                }
            }
        }

        private void ClearHeldDebrisState()
        {
            if (heldDebrisNetworkObject != null)
            {
                heldDebrisNetworkObject.AutoObjectParentSync =
                    heldDebrisAutoObjectParentSync;
            }

            heldDebris = null;
            heldDebrisWorldScale = Vector3.one;
            heldDebrisColliders = null;
            heldDebrisColliderStates = null;
            heldDebrisTriggerStates = null;
            heldDebrisNetworkObject = null;
            heldDebrisAutoObjectParentSync = false;
        }

        private void HandleNetworkHeldItemChanged(string itemId)
        {
            if (!IsNetworkSessionActive())
            {
                return;
            }

            if (string.IsNullOrEmpty(itemId))
            {
                if (heldItemInstance == null && currentItemPrefabData == null)
                {
                    return;
                }
                ClearHeldPresentation();
                return;
            }

            var catalog = networkItemLifecycle == null ? null : networkItemLifecycle.ItemCatalog;

            if (catalog == null || !catalog.TryGetById(itemId, out var itemData))
            {
                Debug.LogError($"PHS_NETWORK_ITEM_PRESENTATION_FAILED " + $"reason=item_not_in_catalog " + $"player={name} " + $"item={itemId}");

                ClearHeldPresentation();
                return;
            }

            ClearHeldPresentation();

            var activeHoldPoint = ActiveHoldPoint;

            // 변경: HasHeldPrefab → HasHandPrefab
            if (activeHoldPoint == null || !itemData.HasHandPrefab)
            {
                Debug.LogError($"PHS_NETWORK_ITEM_PRESENTATION_FAILED " + $"reason=hand_prefab_contract " + $"player={name} " + $"item={itemId}");
                return;
            }

            // 변경: HeldPrefab → HandPrefab
            heldItemInstance = Instantiate(itemData.HandPrefab, activeHoldPoint);
        
            heldItemInstance.name = itemData.HandPrefab.name;

            if (!TryApplyHeldItemPose(heldItemInstance.transform, itemData, activeHoldPoint, itemData.HandPrefab.transform.localScale))
            {
                ClearHeldPresentation();
                return;
            }

            currentItemObject = heldItemInstance.GetComponent<UtilityItemObject>();

            currentItemPrefabData = itemData;
    
            if (currentItemObject == null)
            {
                Debug.LogError($"PHS_NETWORK_ITEM_PRESENTATION_FAILED " + $"reason=utility_item_missing " + $"player={name} " + $"item={itemId}");

                ClearHeldPresentation();
                return;
            }

            heldDebris = heldItemInstance.GetComponent<DebrisItem>();

            if (heldDebris != null)
            {
                // 변경: HeldPrefab → HandPrefab
                heldDebrisWorldScale = itemData.HandPrefab.transform.localScale;

                CacheAndPrepareHeldDebrisColliders();
            }
            else
            {
                foreach (var itemCollider in heldItemInstance.GetComponentsInChildren<Collider>(true))
                {
                    itemCollider.enabled = false;
                }
            }

            StopHeldDebrisMotion();

            currentItemObject.OnPickedUp(this);

            RefreshHeldItemHud();

            Debug.Log($"PHS_NETWORK_ITEM_PRESENTED " + $"player={name} " + $"owner={networkObject.OwnerClientId} " + $"item={itemId}");

        }

        private void HandleNetworkDurabilityChanged(int currentDurability)
        {
            if (IsNetworkSessionActive() && currentItemPrefabData != null)
            {
                RefreshHeldItemHud();
            }
        }

        private void SynchronizeNetworkHeldPresentation()
        {
            if (IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned)
            {
                HandleNetworkHeldItemChanged(networkItemRecord.HeldItemId);
            }
        }

        private void ClearHeldPresentation()
        {
            if (heldItemInstance != null)
            {
                heldItemInstance.SetActive(false);
                Destroy(heldItemInstance);
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            ClearHeldDebrisState();
            RefreshHeldItemHud();
        }

        private void RefreshHeldItemHud()
        {
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            {
                return;
            }

            // HUD 참조가 빠진 경우 자동 생성하지 않고
            // 로그로 Inspector 연결 문제를 드러낸다.
            if (playHudPresenter == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_UI_FAILED " + $"reason=playHudPresenter_missing " + $"player={name}");
                return;
            }

            if (currentItemPrefabData == null)
            {
                playHudPresenter.ClearHeldItem();
                return;
            }

            // 변경: HasDurability → UsesDurability
            var currentDurability = currentItemPrefabData.UsesDurability ? currentItemPrefabData.MaxDurability : 0;

            if (IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned)
            {
                currentDurability = networkItemRecord.CurrentDurability;
            }

            // HUD의 SetHeldItem 매개변수도
            // UtilityItemDataSO로 변경되어 있어야 한다.
            playHudPresenter.SetHeldItem(currentItemPrefabData, currentDurability);
        }

        private void ReportHeldItemRecord()
        {
            if (networkItemRecord == null)
            {
                if (networkObject != null
                    && networkObject.IsSpawned)
                {
                    Debug.LogError($"PHS_ITEM_RECORD_FAILED " + $"reason=record_component_missing " + $"player={name}", this);
                }

                return;
            }

            if (networkItemRecord.IsSpawned && !networkItemRecord.IsServer)
            {
                return;
            }

            // 변경: HasDurability → UsesDurability
            networkItemRecord.ReportHeldItem(
                currentItemPrefabData == null
                    ? string.Empty
                    : currentItemPrefabData.ItemId,

                currentItemPrefabData != null
                && currentItemPrefabData.UsesDurability
                    ? currentItemPrefabData.MaxDurability
                    : 0);
        }

        private Vector3 GetCompensatedHeldItemScale(Vector3 prefabLocalScale, Transform activeHoldPoint, float scaleMultiplier)
        {
            // 손 본의 lossyScale 때문에 아이템 크기가
            // 찌그러지는 것을 줄이기 위한 보정이다.
            var holdPointScale = activeHoldPoint.lossyScale;
       
            if (Mathf.Approximately(holdPointScale.x, 0f) || Mathf.Approximately(holdPointScale.y, 0f) || Mathf.Approximately(holdPointScale.z, 0f))
            {
                Debug.LogError($"PHS_TEMP_ITEM_SCALE_FAILED " + $"reason=holdPoint_scale_zero " + $"player={name} " + $"holdPoint={activeHoldPoint.name}");

                return prefabLocalScale * scaleMultiplier;
            }

            return new Vector3(prefabLocalScale.x / holdPointScale.x, prefabLocalScale.y / holdPointScale.y, prefabLocalScale.z / holdPointScale.z) * scaleMultiplier;
        
        }

        // 변경: UtilityItemPrefabData → UtilityItemDataSO
        private bool TryApplyHeldItemPose(Transform itemTransform, UtilityItemDataSO itemData, Transform activeHoldPoint, Vector3 sourceScale)
        {
            var firstPerson = ShouldUseFirstPersonHoldPoint();

            if (itemTransform == null || itemData == null || activeHoldPoint == null || !itemData.TryGetHeldPose(firstPerson, out var heldPose))
            {
                Debug.LogError($"PHS_TEMP_ITEM_POSE_FAILED " + $"player={name} " + $"item={(itemData == null ? "missing" : itemData.ItemId)} " + $"firstPerson={firstPerson}", this);

                return false;
            }

            itemTransform.SetLocalPositionAndRotation(heldPose.LocalPosition, heldPose.LocalRotation);

            itemTransform.localScale = GetCompensatedHeldItemScale(sourceScale, activeHoldPoint, GetHeldItemScaleMultiplier() * heldPose.ScaleMultiplier);

            return true;
        }

        private bool ShouldUseFirstPersonHoldPoint()
        {
            return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
        }

        private float GetHeldItemScaleMultiplier()
        {
            return ShouldUseFirstPersonHoldPoint() ? firstPersonHeldItemScale : worldHeldItemScale;
        }

        public bool IsHoldingItem(string itemId)
        {
            if (IsNetworkSessionActive() && networkItemRecord != null && networkItemRecord.IsSpawned)
            {
                return !string.IsNullOrWhiteSpace(itemId) && networkItemRecord.HeldItemId == itemId;
            }

            return !string.IsNullOrWhiteSpace(itemId) && currentItemPrefabData != null && currentItemPrefabData.ItemId == itemId;
        }

        public bool TryCreateThrownItem(Vector3 spawnPosition, Quaternion spawnRotation, out GameObject thrownItemInstance)
        {
            return TryCreateThrownItem(spawnPosition, spawnRotation, UtilityItemActionKind.None, out thrownItemInstance, out _);
        }

        public bool TryCreateThrownItem(Vector3 spawnPosition, Quaternion spawnRotation, UtilityItemActionKind actionKind, out GameObject thrownItemInstance, out int actionAmount)
        {
            thrownItemInstance = null;
            actionAmount = 0;

            if (IsNetworkSessionActive())
            {
                if (networkObject == null
                    || !networkObject.IsSpawned
                    || networkObject.NetworkManager == null
                    || !networkObject.NetworkManager.IsServer
                    || networkItemLifecycle == null
                    || networkItemRecord == null
                    || !networkItemRecord.IsSpawned
                    || string.IsNullOrEmpty(
                        networkItemRecord.HeldItemId))
                {
                    Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=server_contract " + $"player={name}");
     
                    return false;
                }

                var networkItemId = networkItemRecord.HeldItemId;
          
                var expectedRevision = networkItemRecord.Revision;

                var droppedDurability = networkItemRecord.CurrentDurability;

                if (actionKind != UtilityItemActionKind.None)
                {
                    if (!networkItemLifecycle.TryResolveHeldItemActionServer(networkItemId, expectedRevision, actionKind, out var actionProfile))
                    {
                        return false;
                    }

                    actionAmount = actionProfile.Amount;

                    droppedDurability = Mathf.Max(0, droppedDurability - actionProfile.DurabilityCost);
                }

                if (!networkItemLifecycle.TryCreateDroppedItemServer(networkItemId, droppedDurability, spawnPosition, spawnRotation, out var spawnedItem)
                    || spawnedItem == null)
                {
                    return false;
                }

                if (!networkItemRecord.TryConsumeHeldItemServer(networkItemId, expectedRevision))
                {
                    if (spawnedItem.IsSpawned)
                    {
                        spawnedItem.Despawn(true);
                    }

                    Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=record_consume_failed " + $"player={name} " + $"item={networkItemId}");

                    return false;
                }

                thrownItemInstance =
                    spawnedItem.gameObject;

                Debug.Log($"PHS_TEMP_ITEM_THROW_CREATED " + $"player={name} " + $"item={networkItemId} " + $"networkObjectId={spawnedItem.NetworkObjectId} " + $"position={spawnPosition}");

                return true;
            }

            if (currentItemPrefabData == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=held_item_missing " + $"player={name}");

                return false;
            }

            if (actionKind != UtilityItemActionKind.None)
            {
                if (!currentItemPrefabData.TryGetActionProfile(actionKind, out var actionProfile))
                {
                    Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=action_profile_missing " + $"player={name} " + $"item={currentItemPrefabData.ItemId} " + $"action={actionKind}", this);

                    return false;
                }

                actionAmount = actionProfile.Amount;
            }

            var networkManager = NetworkManager.Singleton;

            var networkSessionActive = networkManager != null && networkManager.IsListening;

            if (networkSessionActive && !networkManager.IsServer)
            {
                return false;
            }

            if (heldDebris != null)
            {
                return TryReleaseHeldDebrisForThrow(spawnPosition, spawnRotation, networkSessionActive, out thrownItemInstance);
            }

            /*
             * 원본 동작을 유지하기 위해 투척 시에도
             * DroppedPrefab을 사용한다.
             *
             * 새 ThrownPrefab을 사용하는 구조는
             * 전투 컨트롤러와 Lifecycle까지 함께 변경한 뒤 적용한다.
             */
            if (!currentItemPrefabData.HasDroppedPrefab)
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=dropped_prefab_missing " + $"player={name} " + $"item={currentItemPrefabData.ItemId}");
                return false;
            }

            var thrownItemId = currentItemPrefabData.ItemId;
         
            thrownItemInstance = Instantiate(currentItemPrefabData.DroppedPrefab, spawnPosition, spawnRotation);

            if (thrownItemInstance == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=instantiate_failed " + $"player={name} " + $"item={thrownItemId}");

                return false;
            }

            var thrownItemObject = thrownItemInstance.GetComponent<UtilityItemObject>();

            var thrownBody = thrownItemInstance.GetComponent<Rigidbody>();

            var thrownNetworkObject = thrownItemInstance.GetComponent<NetworkObject>();

            if (thrownItemObject == null || thrownBody == null || (networkSessionActive && thrownNetworkObject == null))
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=required_component_missing " + $"player={name} " + $"item={thrownItemId}");

                Destroy(thrownItemInstance);

                thrownItemInstance = null;
                return false;
            }

            thrownItemObject.OnDropped(spawnPosition);

            if (networkSessionActive && !thrownNetworkObject.IsSpawned)
            {
                thrownNetworkObject.Spawn();
            }

            if (heldItemInstance != null)
            {
                heldItemInstance.SetActive(false);
                Destroy(heldItemInstance);
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            ClearHeldDebrisState();
            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_THROW_CREATED " + $"player={name} " + $"item={thrownItemId} " + $"position={spawnPosition}");

            return true;
        }

        private bool TryReleaseHeldDebrisForThrow(Vector3 spawnPosition, Quaternion spawnRotation, bool networkSessionActive, out GameObject thrownItemInstance)
        {
            thrownItemInstance = null;

            if (heldDebris == null || currentItemObject == null)
            {
                Debug.LogError($"PHS_DEBRIS_THROW_FAILED " + $"reason=held_state_invalid " + $"player={name}");
                return false;
            }

            var debrisObject = heldDebris.gameObject;

            var thrownNetworkObject = heldDebris.GetComponent<NetworkObject>();

            var debrisName = heldDebris.name;
           
            thrownItemInstance = debrisObject;

            thrownItemInstance.transform.SetParent(null, true);

            thrownItemInstance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            thrownItemInstance.transform.localScale = heldDebrisWorldScale;

            RestoreHeldDebrisColliders();

            currentItemObject.OnDropped(spawnPosition);

            if (networkSessionActive && thrownNetworkObject != null && !thrownNetworkObject.IsSpawned)
            {
                thrownNetworkObject.Spawn();
            }

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            ClearHeldDebrisState();
            ReportHeldItemRecord();
            RefreshHeldItemHud();

            Debug.Log($"PHS_DEBRIS_THROW_RELEASED " + $"player={name} " + $"debris={debrisName} "  + $"position={spawnPosition}");

            return true;
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
       
            return networkManager != null && networkManager.IsListening;
        }
    }
}
