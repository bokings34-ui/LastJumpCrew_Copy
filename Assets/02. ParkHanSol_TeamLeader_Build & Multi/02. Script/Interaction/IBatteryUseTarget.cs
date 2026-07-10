namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 배터리 아이템 사용을 받을 수 있는 대상의 계약이다.
    // 공용 IUsableItem은 대상 타입을 모르기 때문에, 배터리 전용 판정은 이 인터페이스로 분리한다.
    public interface IBatteryUseTarget
    {
        // 실제 소비/설치 전에 현재 손 아이템이 이 대상에 사용 가능한지 확인한다.
        bool CanUseBattery(LastJumpCrew.Common.IItemHolder user);

        // 사용이 확정되면 손 아이템을 소비하고 대상 상태를 바꾼다.
        bool TryUseBattery(LastJumpCrew.Common.IItemHolder user);
    }
}
