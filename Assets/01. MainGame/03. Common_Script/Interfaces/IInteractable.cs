namespace LastJumpCrew.Common
{
    /// <summary>
    /// 플레이어가 상호작용할 수 있는 모든 대상의 공용 규칙입니다.
    /// 문, 장치, 상점, 수리 패널처럼 입력을 받아 동작하는 오브젝트가 구현합니다.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 화면에 보여줄 상호작용 안내 문구입니다.
        /// </summary>
        string InteractionPrompt { get; }

        /// <summary>
        /// 현재 플레이어의 보유 아이템 상태로 상호작용 가능 여부를 판단합니다.
        /// </summary>
        bool CanInteract(IItemHolder itemHolder);

        /// <summary>
        /// 조건을 통과했을 때 실제 상호작용 기능을 실행합니다.
        /// </summary>
        void Interact(IItemHolder itemHolder);
    }
}
