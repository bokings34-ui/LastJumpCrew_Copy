using UnityEngine;

namespace LastJumpCrew.Common
{
    /// <summary>
    /// 플레이어가 들 수 있는 아이템의 공용 규칙입니다.
    /// 렌치, 배터리, 소화기, 자원상자처럼 손에 붙는 아이템이 구현합니다.
    /// </summary>
    public interface IHoldableItem
    {
        /// <summary>
        /// 기능 판정에 사용하는 아이템 고유 ID입니다.
        /// </summary>
        string ItemId { get; }

        /// <summary>
        /// UI에 표시할 아이템 이름입니다.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 손에 붙일 때 기준이 되는 위치와 회전입니다.
        /// </summary>
        Transform HoldTransform { get; }

        /// <summary>
        /// 아이템이 홀더에게 들어졌을 때 호출됩니다.
        /// </summary>
        void OnPickedUp(IItemHolder holder);

        /// <summary>
        /// 아이템이 월드에 내려놓아졌을 때 호출됩니다.
        /// </summary>
        void OnDropped(Vector3 dropPosition);
    }
}
