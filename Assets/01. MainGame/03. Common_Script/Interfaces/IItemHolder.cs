namespace LastJumpCrew.Common
{
    /// <summary>
    /// 플레이어 또는 보관 장치가 아이템을 들고 있는 상태를 관리하는 공용 규칙입니다.
    /// </summary>
    public interface IItemHolder
    {
        /// <summary>
        /// 현재 들고 있는 아이템입니다. 없으면 null일 수 있습니다.
        /// </summary>
        IHoldableItem CurrentItem { get; }

        /// <summary>
        /// 현재 아이템을 들고 있는지 여부입니다.
        /// </summary>
        bool HasItem { get; }

        /// <summary>
        /// 지정 아이템을 새로 들 수 있는지 판단합니다.
        /// </summary>
        bool CanHold(IHoldableItem item);

        /// <summary>
        /// 지정 아이템을 보유 상태로 전환합니다.
        /// </summary>
        void Hold(IHoldableItem item);

        /// <summary>
        /// 현재 들고 있는 아이템을 내려놓습니다.
        /// </summary>
        void Drop();
    }
}
