namespace LastJumpCrew.ParkHanSol.Items
{
    // 소화기 사용 기능이다. 실제 소화 판정은 여기에 추가한다.
    public sealed class FireExtinguisherUsableItem : EventRepairUsableItem
    {
        protected override string RequiredItemId => "fire_extinguisher";
        protected override SM.EventEffectKind RequiredEffectKind => SM.EventEffectKind.Fire;
    }
}
