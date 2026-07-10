using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 지속시간이 있는 상태이상을 받을 수 있는 대상의 공용 규칙입니다.
    /// 불, 감전, 기절처럼 적용과 해제가 필요한 상태에 사용합니다.
    /// </summary>
    public interface IStatusEffectReceiver
    {
        /// <summary>
        /// 지정 상태이상을 받을 수 있는지 판단합니다.
        /// </summary>
        bool CanReceiveStatusEffect(string effectId);

        /// <summary>
        /// 지속시간이 있는 상태이상을 적용합니다.
        /// </summary>
        void ApplyStatusEffect(string effectId, float duration, GameObject source);

        /// <summary>
        /// 지정 상태이상을 제거합니다.
        /// </summary>
        void RemoveStatusEffect(string effectId);
    }
}
