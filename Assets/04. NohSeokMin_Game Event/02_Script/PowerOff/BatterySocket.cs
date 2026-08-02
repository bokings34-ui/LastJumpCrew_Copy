using LastJumpCrew.Common;
using UnityEngine;

namespace SM
{
    // 발전기 배터리 소켓 - PowerOff 발생 시 배터리 제거, 새 배터리 장착 시 전력 복구
    public class BatterySocket : MonoBehaviour, IInteractable, IRequireHeldItem
    {
        [Header("배터리 비주얼 (PowerOff 시 삭제, 장착 시 재생성)")]
        [SerializeField] private GameObject batteryVisual;

        private PowerOffEvent _boundEvent;
        private bool _hasBattery;

        public string RequiredItemId => ItemType.Battery.ToString();

        public bool IsRequirementMet(IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        public string InteractionPrompt => "배터리 장착하기";

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !_hasBattery && IsRequirementMet(itemHolder);
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 실제 처리는 배터리 아이템의 IUsableItem.Use()에서 InsertBattery() 호출
        }

        private void OnEnable()
        {
            _hasBattery = true;
            SetBatteryVisual(true);
        }

        private void Update()
        {
            var evt = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;

            if (evt != _boundEvent)
            {
                if (_boundEvent == null && evt != null)
                {
                    // 새로 PowerOff가 발생한 순간 -> 배터리 제거
                    RemoveBattery();
                }

                _boundEvent = evt;
            }
        }

        private void RemoveBattery()
        {
            if (!_hasBattery) return;

            _hasBattery = false;
            SetBatteryVisual(false);

            Debug.Log("<color=orange>[PowerSocket]</color> 배터리 소실, 새 배터리 장착 필요.");
        }

        // 배터리 아이템이 Use()에서 직접 호출하는 진입점
        public void InsertBattery()
        {
            if (_hasBattery) return;

            var evt = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;
            if (evt == null || !evt.IsPowerOffActive) return;

            _hasBattery = true;
            SetBatteryVisual(true);

            evt.NotifyPowerRestored();

            Debug.Log("<color=lime>[PowerSocket]</color> 배터리 장착 완료, 전력 복구.");
        }

        private void SetBatteryVisual(bool active)
        {
            if (batteryVisual != null)
                batteryVisual.SetActive(active);
        }
    }
}