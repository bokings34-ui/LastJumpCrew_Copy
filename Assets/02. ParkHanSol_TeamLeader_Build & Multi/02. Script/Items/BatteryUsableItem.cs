using LastJumpCrew.ParkHanSol.Interaction;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Items
{
    // 배터리 사용 기능이다. 배터리 대상이면 장착하고, 그 외 대상은 추후 감전/타격으로 확장한다.
    // 현재 PR에서는 BatteryInsertPowerStationSocket만 실제 성공 대상이다.
    public sealed class BatteryUsableItem : UtilityItemUseBehaviour
    {
        protected override void OnUseStarted(CommonInteraction.IItemHolder user, CommonInteraction.IInteractable target)
        {
            // 대상이 배터리 사용 계약을 제공하면 소비/설치 처리를 대상에게 위임한다.
            if (TryGetTarget<IBatteryUseTarget>(target, out var batteryUseTarget)
                && batteryUseTarget.TryUseBattery(user))
            {
                return;
            }

            UnityEngine.Debug.Log($"PHS_BATTERY_USED_NO_VALID_TARGET item={name}");
        }
    }
}
