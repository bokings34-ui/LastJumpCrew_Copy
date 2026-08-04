using System;
using UnityEngine;
using LastJumpCrew.Common;

namespace SM
{
    public class EngineRoomConsole : MonoBehaviour, IInteractable, IRequireHeldItem, IRepairable
    {
        private const string WrenchItemId = "wrench";

        [SerializeField] private AudioSource audioSource;

        private EngineBreakEvent _boundEvent;

        private void Update()
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;

            if (evt != _boundEvent)
            {
                if (_boundEvent == null && evt != null)
                {
                    if (audioSource != null) audioSource.Play();
                }
                else if (_boundEvent != null && evt == null)
                {
                    if (audioSource != null) audioSource.Stop();
                }

                _boundEvent = evt;
            }
        }

        public string RequiredItemId { get { return WrenchItemId; } }

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        public string InteractionPrompt { get { return "렌치로 엔진 수리하기"; } }

        public bool CanInteract(IItemHolder itemHolder)
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
            return evt != null && IsRequirementMet(itemHolder);
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

        // ===== IRepairable 대응 준비 (팀원 IRepairable.cs main 반영 후 주석 해제) =====
        public bool CanRepair
        {
            get
            {
                var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
                return evt != null;
            }
        }

        public float CurrentIntegrity
        {
            get
            {
                var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
                return evt?.RepairProgress ?? 0f;
            }
        }

        public float MaxIntegrity
        {
            get
            {
                var evt = EventManager.Instance.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent;
                return evt?.MaxRepairProgress ?? 0f;
            }
        }

        public bool ApplyRepair(float amount, GameObject repairer)
        {
            if (!CanRepair || amount <= 0f) return false;
            ApplyRepairToEngine(amount); // 기존 메서드 재사용
            return true;
        }
        // ================================================================
    }
}
