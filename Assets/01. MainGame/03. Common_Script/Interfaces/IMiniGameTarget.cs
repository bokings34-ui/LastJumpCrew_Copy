namespace LastJumpCrew.Common
{
    /// <summary>
    /// 미니게임 성공/실패 결과를 받을 장치의 공용 규칙입니다.
    /// 미니게임 로직이 구체 장치 클래스를 직접 몰라도 결과만 전달하게 합니다.
    /// </summary>
    public interface IMiniGameTarget
    {
        /// <summary>
        /// 미니게임과 대상 장치를 연결하는 식별자입니다.
        /// </summary>
        string MiniGameTargetId { get; }

        /// <summary>
        /// 미니게임 성공 결과를 장치에 적용합니다.
        /// </summary>
        void OnMiniGameSucceeded();

        /// <summary>
        /// 미니게임 실패 결과를 장치에 적용합니다.
        /// </summary>
        void OnMiniGameFailed();
    }
}
