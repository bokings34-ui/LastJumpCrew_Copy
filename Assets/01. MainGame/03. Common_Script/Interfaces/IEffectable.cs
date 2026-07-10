using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 즉시 효과나 단발 효과를 받을 수 있는 대상의 공용 규칙입니다.
    /// 감전, 화상, 버프, 디버프 같은 효과 ID 기반 처리에 사용합니다.
    /// </summary>
    public interface IEffectable
    {
        /// <summary>
        /// 지정 효과를 받을 수 있는지 판단합니다.
        /// </summary>
        bool CanReceiveEffect(string effectId);

        /// <summary>
        /// 지정 효과를 실제로 적용합니다.
        /// </summary>
        void ApplyEffect(string effectId, GameObject source);
    }
}
