using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 아이템을 손에 들 수 있는 플레이어/대상의 최소 계약이다.
    // 자판기, 바닥 아이템, 툴박스, 배터리 소켓은 구현체를 모르고 이 인터페이스만 호출한다.
    public interface IItemHolder
    {
        // 현재 손에 든 아이템 데이터다. null이면 빈손이다.
        UtilityItemDataSO CurrentItemPrefabData { get; }

        // 기존 아이템을 내려놓고 새 아이템을 들 수 있는지 검사한다.
        bool CanReplaceHeldItem(UtilityItemDataSO itemPrefabData);

        // 새 아이템을 손에 생성한다. interactionSource는 로그/드롭 기준 확장용으로 넘긴다.
        void ReplaceHeldItem(UtilityItemDataSO itemPrefabData, Transform interactionSource);

        // 현재 든 아이템을 드롭 프리팹으로 월드에 배치한다.
        void PlaceHeldItem();

        // 특정 itemId를 가진 아이템을 손에서 제거한다. 배터리 삽입/툴박스 보관에서 사용한다.
        bool TryConsumeHeldItem(string itemId);
    }
}
