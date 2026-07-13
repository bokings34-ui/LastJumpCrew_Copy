using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 피해를 받을 수 있는 대상의 공용 규칙입니다.
    /// 플레이어, 적, 장치, 함선 부품 등이 구현합니다
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 현재 피해 처리가 가능한 살아있는 상태인지 나타냅니다.
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// 지정 피해량과 공격 주체를 받아 체력 감소, 파괴, 사망 처리를 수행합니다.
        /// </summary>
        void ApplyDamage(int amount, GameObject attacker);
    }
}
