using LastJumpCrew.Common;
using UnityEngine;


namespace LastJumpCrew.Common
{
    ///수리를 받을 수 있는 대상의 공용 규칙!!
    ///산소 누출, 화재 사고 등등
    ///수리가 필요한 기계 + 화재쪽에 추가하면 됩니다
    public interface IRepairable
    {
        //현재 수리 받을 수 있는지 나타냅니다.
        //이미 수리 완료 이거나 파괴된 대상이라면 false를 반환
        bool CanRepair {  get; }

        //현재 수리 진행도 or 현재 장치 내구도
        float CurrentIntegrity { get; }

        //수리 가능한 최대 진행도 or 최대 장치 내구도
        float MaxIntegrity { get; }

        bool ApplyRepair(float amount, GameObject repairer);
        //지정된 수리량을 대상에게 적용 amount->적용할 수리량 repairer-> 수리를 진행한 플레이어 또는 오브젝트
        //수리량 실제로 적용되면 true

        //반환 하는 이유는 수리가 끝나 이후에도 계속 수리하게 되면 내구도가 감소하기 때문에 반환함
    }
}