using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 상태이상을 받을 수 있는 대상의 공통 규칙
    /// </summary>
    public interface IStatusEffectReceiver
    {
        /// <summary>
        /// 현재 상태이상을 받을 수 있는지 
        /// </summary>
        bool CanReceiveStatusEffect(StatusEffectType effectType);

        /// <summary>
        /// 상태이상 적용
        /// </summary>
        void ApplyStatusEffect(StatusEffectRequest request);

        ///
        ///특정 상태이상 제거
        ///
        void RemoveStatusEffect(StatusEffectType effectType);

        /// <summary>
        /// 현재 해당 상태이상이 적용 중인지
        bool HasStatusEffect(StatusEffectType effectType);
    }
}
