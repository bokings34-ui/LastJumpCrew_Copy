namespace LastJumpCrew.Common
{
    /// <summary>
    /// 특정 아이템을 들고 있어야 작동하는 대상의 조건 규칙입니다.
    /// 실제 실행이 아니라 요구 아이템 확인만 담당합니다.
    /// </summary>
    public interface IRequireHeldItem
    {
        /// <summary>
        /// 상호작용에 필요한 아이템 ID입니다.
        /// </summary>
        string RequiredItemId { get; }

        /// <summary>
        /// 현재 홀더가 요구 아이템 조건을 만족하는지 확인합니다.
        /// </summary>
        bool IsRequirementMet(IItemHolder itemHolder);
    }
}
