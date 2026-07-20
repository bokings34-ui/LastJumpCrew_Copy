using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 테스트 플레이어의 손 아이템 상태를 관리한다.
    // 아이템 지급, 줍기, 드롭, 소비, HUD 갱신이 이 컴포넌트를 통해 흐른다.
    public sealed class TempPlayerItemHolder : MonoBehaviour, IItemHolder, IDebrisHolder, LastJumpCrew.Common.IItemHolder
    {
        [Header("Hold Points")]

        // 로컬 1인칭 화면에 보이는 아이템 위치다.
        [SerializeField] private Transform holdPoint;

        // 멀티 원격/외부 시점에서 보이는 아이템 위치다.
        [SerializeField] private Transform visibleHandHoldPoint;

        // 아이템을 내려놓을 기준 위치다. 비어 있으면 플레이어 transform 기준으로 배치한다.
        [SerializeField] private Transform dropPoint;

        // dropPoint 기준 로컬 드롭 오프셋이다.
        [SerializeField] private Vector3 droppedLocalOffset = new(0f, 0f, 1f);

        [Header("Held Item Scale")]

        // 1인칭 화면에서 보이는 아이템 크기 배율이다.
        [SerializeField, Min(0.01f)] private float firstPersonHeldItemScale = 0.42f;

        // 다른 플레이어에게 보이는 아이템 크기 배율이다.
        [SerializeField, Min(0.01f)] private float worldHeldItemScale = 0.32f;

        // 들고 있는 아이템 이름/아이콘/내구도 표시용 HUD presenter다.
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;

        // 현재 손에 생성된 아이템 프리팹 인스턴스다.
        private GameObject heldItemInstance;

        // heldItemInstance 루트의 UtilityItemObject 캐시다.
        private UtilityItemObject currentItemObject;

        // 현재 아이템의 데이터 캐시다. 빈손이면 null이다.
        private UtilityItemPrefabData currentItemPrefabData;
        private DebrisItem heldDebris;
        private Vector3 heldDebrisWorldScale;
        private Collider[] heldDebrisColliders;
        private bool[] heldDebrisColliderStates;
        private bool[] heldDebrisTriggerStates;
        private NetworkObject networkObject;
        private NetworkPlayerItemRecord networkItemRecord;

        public UtilityItemPrefabData CurrentItemPrefabData => currentItemPrefabData;
        public DebrisItem HeldDebris => heldDebris;
        public float HeldDebrisMass => heldDebris == null ? 0f : heldDebris.Mass;
        LastJumpCrew.Common.IHoldableItem LastJumpCrew.Common.IItemHolder.CurrentItem => currentItemObject;
        public bool HasItem => currentItemPrefabData != null;

        private Transform ActiveHoldPoint => ShouldUseFirstPersonHoldPoint() ? holdPoint : visibleHandHoldPoint;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            networkItemRecord = GetComponent<NetworkPlayerItemRecord>();

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
            // 손 위치, 아이템 데이터, heldPrefab 참조만 검사한다.
            // 실제 교체/드롭은 ReplaceHeldItem에서 수행한다.
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

            // 한 손에 하나만 들 수 있으므로 기존 아이템을 먼저 월드에 내려놓는다.
            PlaceCurrentItem();

            var activeHoldPoint = ActiveHoldPoint;

            // 아이템 프리팹은 손 위치 자식으로 생성하고, 부모 스케일을 보정한다.
            heldItemInstance = Instantiate(itemPrefabData.HeldPrefab, activeHoldPoint);
            heldItemInstance.name = itemPrefabData.HeldPrefab.name;
            heldItemInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            heldItemInstance.transform.localScale = GetCompensatedHeldItemScale(
                itemPrefabData.HeldPrefab.transform.localScale,
                activeHoldPoint,
                GetHeldItemScaleMultiplier());
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
            ReportHeldItemRecord();
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

            if (utilityItemObject.TryGetComponent<DebrisItem>(out var debrisItem))
            {
                TryHoldDebris(debrisItem);
                return;
            }

            ReplaceHeldItem(utilityItemObject.ItemPrefabData, utilityItemObject.transform);
        }

        public bool TryHoldDebris(DebrisItem debrisItem)
        {
            if (!CanHoldDebris(debrisItem))
            {
                return false;
            }

            var itemObject = debrisItem.GetComponent<UtilityItemObject>();
            PlaceCurrentItem();

            var activeHoldPoint = ActiveHoldPoint;
            heldDebris = debrisItem;
            heldItemInstance = debrisItem.gameObject;
            currentItemObject = itemObject;
            currentItemPrefabData = itemObject.ItemPrefabData;
            heldDebrisWorldScale = debrisItem.transform.lossyScale;
            CacheAndPrepareHeldDebrisColliders();

            heldItemInstance.transform.SetParent(activeHoldPoint, false);
            heldItemInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            heldItemInstance.transform.localScale = GetCompensatedHeldItemScale(
                heldDebrisWorldScale,
                activeHoldPoint,
                GetHeldItemScaleMultiplier());

            currentItemObject.OnPickedUp(this);
            ReportHeldItemRecord();
            RefreshHeldItemHud();
            Debug.Log($"PHS_DEBRIS_HELD player={name} debris={debrisItem.name} mass={debrisItem.Mass:F2} value={debrisItem.Value}");
            return true;
        }

        public bool CanHoldDebris(DebrisItem debrisItem)
        {
            if (debrisItem == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=debris_missing player={name}");
                return false;
            }

            if (ActiveHoldPoint == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=hold_point_missing player={name}");
                return false;
            }

            var itemObject = debrisItem.GetComponent<UtilityItemObject>();
            if (itemObject == null || itemObject.ItemPrefabData == null)
            {
                Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=item_setup_invalid player={name} debris={debrisItem.name}");
                return false;
            }

            var colliders = debrisItem.GetComponentsInChildren<Collider>(true);
            foreach (var targetCollider in colliders)
            {
                if (targetCollider is MeshCollider meshCollider && !meshCollider.convex)
                {
                    Debug.LogError($"PHS_DEBRIS_HOLD_FAILED reason=non_convex_mesh_collider player={name} debris={debrisItem.name} collider={targetCollider.name}");
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
            PlaceCurrentItem();
        }

        public bool TryConsumeHeldItem(string itemId)
        {
            // 소비는 정확한 itemId만 허용한다.
            // 잘못된 아이템이 사라지면 원인 추적이 어려우므로 실패 로그를 남긴다.
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


            
            ClearHeldItemState();

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

        private void PlaceCurrentItem()
        {
            if (currentItemPrefabData == null)
            {
                return;
            }

            if (heldDebris != null)
            {
                PlaceHeldDebris();
                return;
            }

            // droppedPrefab이 있으면 월드에 새 인스턴스를 만들고 손 인스턴스는 제거한다.
            if (!currentItemPrefabData.HasDroppedPrefab)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_PLACE_FAILED reason=droppedPrefab_missing item={currentItemPrefabData.ItemId}");
            }
            else
            {
                var source = dropPoint == null ? transform : dropPoint;
                var position = source.TransformPoint(droppedLocalOffset);
                var droppedItemInstance = Instantiate(currentItemPrefabData.DroppedPrefab, position, source.rotation);
                var droppedItemObject = droppedItemInstance.GetComponent<UtilityItemObject>();
                if (droppedItemObject == null)
                {
                    Debug.LogError($"PHS_TEMP_ITEM_PLACE_FAILED reason=utilityItemObject_missing item={currentItemPrefabData.ItemId}");
                }
                else
                {
                    droppedItemObject.OnDropped(position);
                }

                Debug.Log($"PHS_TEMP_ITEM_PLACED player={name} item={currentItemPrefabData.ItemId}");
            }

            ClearHeldItemState();
        }
        private void ClearHeldItemState()
        {
            //현재 손의 있는 HeldPrefab을 제거
            if (heldItemInstance != null)
            {
                heldItemInstance.SetActive(false);
                Destroy(heldItemInstance);
            }
            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;

            RefreshHeldItemHud();
        }

            ReportHeldItemRecord();
            RefreshHeldItemHud();
        }

        private void PlaceHeldDebris()
        {
            if (heldDebris == null || heldItemInstance == null || currentItemObject == null)
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED reason=held_state_invalid player={name}");
                return;
            }

            var source = dropPoint == null ? transform : dropPoint;
            var position = source.TransformPoint(droppedLocalOffset);
            var debrisName = heldDebris.name;
            heldItemInstance.transform.SetParent(null, true);
            heldItemInstance.transform.SetPositionAndRotation(position, source.rotation);
            heldItemInstance.transform.localScale = heldDebrisWorldScale;
            RestoreHeldDebrisColliders();
            currentItemObject.OnDropped(position);

            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;
            ClearHeldDebrisState();
            ReportHeldItemRecord();
            RefreshHeldItemHud();
            Debug.Log($"PHS_DEBRIS_PLACED player={name} debris={debrisName}");
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

        private void RestoreHeldDebrisColliders()
        {
            if (heldDebrisColliders == null || heldDebrisColliderStates == null || heldDebrisTriggerStates == null
                || heldDebrisColliders.Length != heldDebrisColliderStates.Length
                || heldDebrisColliders.Length != heldDebrisTriggerStates.Length)
            {
                Debug.LogError($"PHS_DEBRIS_PLACE_FAILED reason=collider_state_invalid player={name}");
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
            heldDebris = null;
            heldDebrisWorldScale = Vector3.one;
            heldDebrisColliders = null;
            heldDebrisColliderStates = null;
            heldDebrisTriggerStates = null;
        }


        private void RefreshHeldItemHud()
        {
            // HUD 참조가 빠진 경우 자동 생성하지 않고 로그로 Inspector 연결 문제를 드러낸다.
            if (playHudPresenter == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_UI_SKIPPED " + $"reason=playHudPresenter_missing " + $"player={name}");
                return;
            }

            if (currentItemPrefabData == null)
            {
                playHudPresenter.ClearHeldItem();
                return;
            }

            playHudPresenter.SetHeldItem(currentItemPrefabData);
        }

        private void ReportHeldItemRecord()
        {
            if (networkItemRecord == null)
            {
                if (networkObject != null && networkObject.IsSpawned)
                {
                    Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=record_component_missing player={name}", this);
                }

                return;
            }

            networkItemRecord.ReportHeldItem(currentItemPrefabData == null
                ? string.Empty
                : currentItemPrefabData.ItemId);
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

        private Vector3 GetCompensatedHeldItemScale(Vector3 prefabLocalScale, Transform activeHoldPoint, float scaleMultiplier)
        {
            // 손 본의 lossyScale 때문에 아이템 크기가 찌그러지는 것을 줄이기 위한 보정이다.
            var holdPointScale = activeHoldPoint.lossyScale;
            if (Mathf.Approximately(holdPointScale.x, 0f)
                || Mathf.Approximately(holdPointScale.y, 0f)
                || Mathf.Approximately(holdPointScale.z, 0f))
            {
                Debug.LogError($"PHS_TEMP_ITEM_SCALE_FAILED reason=holdPoint_scale_zero player={name} holdPoint={activeHoldPoint.name}");
                return prefabLocalScale * scaleMultiplier;
            }

            return new Vector3(
                prefabLocalScale.x / holdPointScale.x,
                prefabLocalScale.y / holdPointScale.y,
                prefabLocalScale.z / holdPointScale.z) * scaleMultiplier;
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
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;   
            }
            if (currentItemPrefabData == null)
            {
                return false;
            }
            return currentItemPrefabData.ItemId == itemId; //들고 있는 아이템과 요구 아이템 Id 같은지 확인
        }
        public bool TryCreateThrownItem(Vector3 spawnPosition, Quaternion spawnRotation, out GameObject thrownItemInstance)
        {
            thrownItemInstance = null;

            if (currentItemPrefabData == null)
            {
                Debug.LogWarning($"PHS_TEMP_ITEM_THROW_FAILED" + $"reason=held_item_missing " + $"player={name}"); 
                return false;   

            }
            if (!currentItemPrefabData.HasDroppedPrefab)
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=dropped_prefab_missing " + $"player={name} " + $"item={currentItemPrefabData.ItemId}");

                return false;
            }
            var thrownItemId = currentItemPrefabData.ItemId;

            thrownItemInstance = Instantiate(currentItemPrefabData.DroppedPrefab, spawnPosition, spawnRotation);

            if(thrownItemInstance == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=instantiate_failed " + $"player={name} " + $"item={thrownItemId}");
                return false;
            }
            var thrownItemObject = thrownItemInstance.GetComponent<UtilityItemObject>();

            if(thrownItemObject == null)
            {
                Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=utility_item_object_missing " + $"item={thrownItemId}");

                Destroy(thrownItemInstance );
                thrownItemInstance = null;

                return false;
            }
            thrownItemObject.OnDropped(spawnPosition); //생성된 아이템을 월드에 떨어진 상태로 전환

            var thrownNetworkObject = thrownItemInstance.GetComponent<NetworkObject>();
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!NetworkManager.Singleton.IsServer) //네트워크 Spawn은 서버에서만 가능
                {
                    Debug.LogError($"PHS_TEMP_ITEM_THROW_FAILED " + $"reason=server_required " + $"player={name} " + $"item={thrownItemId}");

                    Destroy(thrownItemInstance);
                    thrownItemInstance = null;

                    return false;
                }
                if(thrownNetworkObject == null)//멀티에서 DroppedPrefab 생성에는 NetworkObject 필요
                {
                    Debug.LogError("PHS_TEMP_ITEM_THROW_FAILED " + $"reason=network_object_missing " + $"item={thrownItemId}");
                    Destroy(thrownItemInstance);
                    thrownItemInstance = null;

                    return false;
                }
                if (!thrownNetworkObject.IsSpawned)
                {
                    thrownNetworkObject.Spawn();
                }
            }
            ClearHeldItemState();

            Debug.Log($"PHS_TEMP_ITEM_THROW_CREATED " + $"player={name} " + $"item={thrownItemId} " + $"position={spawnPosition}");

            return true;
        }
       
    }
}
