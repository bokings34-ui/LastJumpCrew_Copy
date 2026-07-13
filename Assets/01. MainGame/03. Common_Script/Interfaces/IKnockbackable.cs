using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 넉백을 받을 수 있는 대상의 공용 규칙입니다.
    /// 폭발, 충격파, 적 공격처럼 물리성 반응이 필요한 대상이 구현합니다.
    /// </summary>
    public interface IKnockbackable
    {
        /// <summary>
        /// 현재 넉백을 받을 수 있는 상태인지 나타냅니다.
        /// </summary>
        bool CanReceiveKnockback { get; }

        /// <summary>
        /// 힘 방향과 원인 오브젝트를 받아 넉백을 적용합니다.
        /// </summary>
        void ApplyKnockback(Vector3 direction, float force, GameObject attacker);
    }
}
