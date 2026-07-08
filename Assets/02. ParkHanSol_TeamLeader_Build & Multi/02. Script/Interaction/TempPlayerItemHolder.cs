using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 테스트 플레이어의 손 아이템 상태를 관리한다.
    // 아이템 지급, 줍기, 드롭, 소비, HUD 갱신이 이 컴포넌트를 통해 흐른다.
    public sealed class TempPlayerItemHolder : MonoBehaviour, IItemHolder, LastJumpCrew.Common.IItemHolder
    {
        // 아이템을 붙일 기본 위치다. visibleHandHoldPoint가 없을 때 대체로 사용한다.
        [SerializeField] private Transform holdPoint;

        // 실제 캐릭터 손 위치다. 비어 있으면 Awake에서 "R_Hand" 자식을 찾아 사용한다.
        [SerializeField] private Transform visibleHandHoldPoint;

        // 아이템을 내려놓을 기준 위치다. 비어 있으면 플레이어 transform 기준으로 배치한다.
        [SerializeField] private Transform dropPoint;

        // dropPoint 기준 로컬 드롭 오프셋이다.
        [SerializeField] private Vector3 droppedLocalOffset = new(0f, 0f, 1f);

        // 들고 있는 아이템 이름/아이콘/내구도 표시용 HUD presenter다.
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;

        // 현재 손에 생성된 아이템 프리팹 인스턴스다.
        private GameObject heldItemInstance;

        // heldItemInstance 루트의 UtilityItemObject 캐시다.
        private UtilityItemObject currentItemObject;

        // 현재 아이템의 데이터 캐시다. 빈손이면 null이다.
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

            // 아이템 프리팹은 손 위치 자식으로 생성하고, 부모 스케일을 보정한다.
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

            Destroy(heldItemInstance);
            heldItemInstance = null;
            currentItemObject = null;
            currentItemPrefabData = null;
            RefreshHeldItemHud();

            Debug.Log($"PHS_TEMP_ITEM_CONSUMED player={name} item={itemId}");
            return true;
        }

        private void PlaceCurrentItem()
        {
            if (currentItemPrefabData == null)
            {
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
            // HUD 참조가 빠진 경우 자동 생성하지 않고 로그로 Inspector 연결 문제를 드러낸다.
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
            // 손 본의 lossyScale 때문에 아이템 크기가 찌그러지는 것을 줄이기 위한 보정이다.
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
