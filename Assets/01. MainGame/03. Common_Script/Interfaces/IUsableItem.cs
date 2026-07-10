namespace LastJumpCrew.Common
{
    /// <summary>
    /// 손에 든 아이템을 특정 상호작용 대상에게 사용할 때의 공용 규칙입니다.
    /// 예: 렌치 수리, 소화기 진압, 배터리 장착.
    /// </summary>
    public interface IUsableItem
    {
        /// <summary>
        /// 사용자와 대상 기준으로 아이템 사용 가능 여부를 판단합니다.
        /// </summary>
        bool CanUse(IItemHolder user, IInteractable target);

        /// <summary>
        /// 아이템의 실제 사용 효과를 실행합니다.
        /// </summary>
        void Use(IItemHolder user, IInteractable target);
    }
}
