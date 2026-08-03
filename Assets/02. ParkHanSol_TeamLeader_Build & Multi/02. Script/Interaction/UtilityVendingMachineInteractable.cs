using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 자판기 오브젝트에 붙는 상호작용 컴포넌트다.
    // 연결된 UtilityVendingMachineData의 아이템을 플레이어 손에 지급한다.
    public sealed class UtilityVendingMachineInteractable : MonoBehaviour, IInteractable, LastJumpCrew.Common.IInteractable
    {
        // 어떤 아이템을 지급할지 담은 ScriptableObject다. Inspector에서 직접 연결한다.
        [SerializeField] private UtilityVendingMachineData vendingMachineData;

        // 상호작용 UI에 보여줄 문구다. 현재 기본값은 키 안내용 F다.
        [SerializeField] private string interactionPrompt = "F";

        public string InteractionPrompt => interactionPrompt;
        public UtilityVendingMachineData VendingMachineData => vendingMachineData;

        public bool CanInteract(IItemHolder itemHolder)
        {
            // 지급 전에 holder와 데이터 참조를 모두 검사해서 Inspector 연결 누락을 로그로 드러낸다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            return itemHolder.CanReplaceHeldItem(itemPrefabData);
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 실제 지급 시에도 CanInteract와 같은 검사를 반복한다.
            // 외부에서 CanInteract 없이 바로 호출해도 잘못된 참조가 조용히 통과하지 않게 한다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return;
            }

            if (!itemHolder.CanReplaceHeldItem(itemPrefabData))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={itemPrefabData.ItemId}");
                return;
            }

            itemHolder.ReplaceHeldItem(itemPrefabData, transform);
        }

        bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            // 공용 Common 인터페이스를 쓰는 다른 시스템에서도 같은 자판기를 사용할 수 있게 연결한다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetCommonItem(out var item))
            {
                return false;
            }

            return itemHolder.CanHold(item);
        }

        void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetCommonItem(out var item))
            {
                return;
            }

            if (!itemHolder.CanHold(item))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={item.ItemId}");
                return;
            }

            itemHolder.Hold(item);
        }

        private bool TryGetItemPrefabData(out UtilityItemDataSO itemPrefabData)
        {
            itemPrefabData = null;

            // 자판기 asset 또는 asset 내부 아이템 참조가 빠지면 지급할 대상이 없으므로 실패 처리한다.
            if (vendingMachineData == null)
            {
                Debug.LogWarning($"PHS_VENDING_DATA_MISSING target={name}");
                return false;
            }

            itemPrefabData = vendingMachineData.ItemPrefabData;
            if (itemPrefabData == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_DATA_MISSING target={name} vendingData={vendingMachineData.name}");
                return false;
            }

            return true;
        }

        private bool TryGetCommonItem(out LastJumpCrew.Common.IHoldableItem item)
        {
            item = null;

            // Common 경로는 프리팹 루트에 IHoldableItem 구현체가 있어야 한다.
            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            if (itemPrefabData.HandPrefab == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_PREFAB_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            item = itemPrefabData.HandPrefab.GetComponent<LastJumpCrew.Common.IHoldableItem>();
            if (item == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_CONTRACT_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }
    }
}
