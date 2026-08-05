using UnityEngine;
using LastJumpCrew.ParkHanSol.Interaction;
using CommonInteraction = LastJumpCrew.Common;

namespace SM
{
    public class BatterySocket : MonoBehaviour, CommonInteraction.IInteractable, CommonInteraction.IRequireHeldItem, IBatteryUseTarget
    {
        private const string BatteryItemId = "battery_pack";

        [Header("배터리 비주얼 (PowerOff 시 삭제, 장착 시 재생성)")]
        [SerializeField] private GameObject batteryVisual;

        [Header("사운드")]
        [SerializeField] private AudioSource audioSource;

        private PowerOffEvent _boundEvent;
        private bool _hasBattery;

        public string RequiredItemId => BatteryItemId;

        public bool IsRequirementMet(CommonInteraction.IItemHolder itemHolder)
        {
            return itemHolder.HasItem && itemHolder.CurrentItem.ItemId == RequiredItemId;
        }

        public string InteractionPrompt => "배터리 장착하기";

        public bool CanInteract(CommonInteraction.IItemHolder itemHolder)
        {
            return !_hasBattery && IsRequirementMet(itemHolder);
        }

        public void Interact(CommonInteraction.IItemHolder itemHolder)
        {
        }

        // IBatteryUseTarget (LastJumpCrew.ParkHanSol.Interaction 소속, using으로 그대로 참조)
        public bool CanUseBattery(CommonInteraction.IItemHolder user)
        {
            return !_hasBattery && IsRequirementMet(user);
        }

        public bool TryUseBattery(CommonInteraction.IItemHolder user)
        {
            if (!CanUseBattery(user)) return false;

            var evt = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;
            if (evt == null || !evt.IsPowerOffActive) return false;

            _hasBattery = true;
            SetBatteryVisual(true);
            evt.NotifyPowerRestored();

            Debug.Log("<color=lime>[BatterySocket]</color> 배터리 장착 완료, 전력 복구.");
            return true;
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
                    RemoveBattery();
                    if (audioSource != null) audioSource.Play();
                }
                else if (_boundEvent != null && evt == null)
                {
                    if (audioSource != null) audioSource.Stop();
                }

                _boundEvent = evt;
            }
        }

        private void RemoveBattery()
        {
            if (!_hasBattery) return;

            _hasBattery = false;
            SetBatteryVisual(false);

            Debug.Log("<color=orange>[BatterySocket]</color> 배터리 소실, 새 배터리 장착 필요.");
        }

        private void SetBatteryVisual(bool active)
        {
            if (batteryVisual != null)
                batteryVisual.SetActive(active);
        }
    }
}
