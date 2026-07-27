using System;
using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    public class EngineRoomConsole : MonoBehaviour, IInteractable, IRequireHeldItem
    {
        public string RequiredItemId { get { return ItemType.Wrench.ToString(); } }

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        public string InteractionPrompt { get { return "렌치로 엔진 수리하기"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
            return evt != null;
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 실제 수리는 렌치 아이템의 IUsableItem.Use()에서 ApplyRepair() 호출로 처리
        }

        // 렌치 아이템이 Use() 안에서 자기 SO의 수리량 값을 넘겨 호출
        public void ApplyRepairToEngine(float amount)
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
            evt?.ApplyRepair(amount);
        }
    }
}