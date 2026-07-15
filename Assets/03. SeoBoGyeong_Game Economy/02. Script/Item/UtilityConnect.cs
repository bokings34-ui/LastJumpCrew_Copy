using UnityEngine;
using LastJumpCrew.ParkHanSol.Items;   // UtilityItemObject (팀장 소유, 읽기 전용)
using LastJumpCrew.SeoBoGyeong.Data;   // UtilityItemData

namespace LastJumpCrew.SeoBoGyeong.item
{
    // 경제 계층(int id)과 실제 아이템 오브젝트(UtilityItemObject)를 이어주는 조회 브릿지.
    // 별도 레지스트리를 만들지 않고, 기존 저장 데이터(GameCore.Data.Tools = DataRepository<UtilityItemData>)에서 해석한다.
    // id 기준은 UtilityItemData.Id 한 곳으로 통일된다.
    // 이 스크립트는 "조회"만 담당한다. 소환/배송 같은 생성 책임은 별도 컴포넌트가 맡는다(역할 분리).
    public class UtilityConnect : MonoBehaviour
    {
        /// <summary>
        /// int id → UtilityItemData → DroppedPrefab 루트의 UtilityItemObject(프리팹 템플릿)를 반환.
        /// 반환값은 씬 인스턴스가 아니라 프리팹 원본이다.
        /// 데이터/프리팹/컴포넌트가 없으면 false.
        /// </summary>
        public bool TryGetPrefab(int id, out UtilityItemObject prefab)
        {
            prefab = null;

            UtilityItemData data = GetData(id);
            if (data == null) return false;

            if (data.DroppedPrefab == null)
            {
                Debug.LogError($"[UtilityConnect] id={id} 의 DroppedPrefab 이 비어 있음");
                return false;
            }

            // 프리팹 루트에 팀장의 UtilityItemObject 가 붙어 있어야 인식된다.
            if (!data.DroppedPrefab.TryGetComponent(out prefab))
            {
                Debug.LogError($"[UtilityConnect] id={id} 의 DroppedPrefab 루트에 UtilityItemObject 가 없음");
                return false;
            }

            return true;
        }

        /// <summary>int id 의 정적 데이터(가격·내구도·수리량 등). 없으면 null.</summary>
        public UtilityItemData GetData(int id)
        {
            // GameCore 싱글턴과 데이터 매니저가 준비돼 있어야 조회할 수 있다.
            if (GameCore.Instance == null || GameCore.Instance.Data == null)
            {
                Debug.LogError("[UtilityConnect] GameCore/Data 가 준비되지 않음");
                return null;
            }

            UtilityItemData data = GameCore.Instance.Data.Tools.Get(id);
            if (data == null)
                Debug.LogError($"[UtilityConnect] id={id} 에 해당하는 UtilityItemData 가 없음");

            return data;
        }
    }
}
