namespace LastJumpCrew.ParkHanSol.Interaction
{
    // ParkHanSol 테스트 상호작용 공통 규칙이다.
    // TempPlayerInteractionScanner가 Raycast로 찾은 대상에게 이 인터페이스를 통해 가능 여부와 실행을 요청한다.
    public interface IInteractable
    {
        // 화면 안내에 표시할 상호작용 문구다. 현재는 일부 UI 연결 전이라 데이터만 들고 있다.
        string InteractionPrompt { get; }

        // 현재 플레이어가 가진 아이템 상태로 상호작용 가능한지 먼저 검사한다.
        bool CanInteract(IItemHolder itemHolder);

        // CanInteract가 true인 상황에서 실제 줍기/보관/삽입/계산 동작을 수행한다.
        void Interact(IItemHolder itemHolder);
    }
}
