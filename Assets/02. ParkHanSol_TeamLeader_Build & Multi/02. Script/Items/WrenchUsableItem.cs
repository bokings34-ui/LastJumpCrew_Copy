namespace LastJumpCrew.ParkHanSol.Items
{
    // 렌치 사용 기능이다. 실제 수리 판정은 여기에 추가한다.
    public sealed class WrenchUsableItem : EventRepairUsableItem
    {
        protected override string RequiredItemId => "wrench";
        protected override SM.EventEffectKind RequiredEffectKind => SM.EventEffectKind.OxygenLeak;
    }
}
