using System.Collections.Generic;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Items;   // UtilityItemObject (팀장 소유, 읽기 전용)
using LastJumpCrew.SeoBoGyeong.Data;   // UtilityItemData

namespace LastJumpCrew.SeoBoGyeong.item
{
    // 경제 계층(int id)과 실제 아이템 오브젝트(UtilityItemObject, string id)를 이어주는 조회 브릿지.
    // 별도 저장 없이 기존 데이터(GameCore.Data.Tools = DataRepository<UtilityItemData>)에서 해석한다.
    //  - int  조회: UtilityItemData.Id 로 데이터/프리팹을 찾는다.
    //  - string 조회: UtilityItemObject.ItemId(= UtilityItemPrefabData.ItemId) 로 경제 데이터를 역매핑한다.
    // string→int 인덱스는 첫 사용 시 한 번 만들어 캐싱한다(정적 데이터라 이후 불변).
    // 이 스크립트는 "조회"만 담당한다. 소환/배송 등 생성 책임은 별도 컴포넌트가 맡는다.
    public class UtilityConnect : MonoBehaviour
    {
        // string(UtilityItemObject.ItemId) → UtilityItemData 역매핑 캐시.
        private Dictionary<string, UtilityItemData> _byItemId;

        // ===== int 조회 =====

        /// <summary>
        /// int id → UtilityItemData → DroppedPrefab 루트의 UtilityItemObject(프리팹 템플릿).
        /// 반환값은 씬 인스턴스가 아니라 프리팹 원본이다. 없으면 false.
        /// </summary>
        public bool TryGetPrefab(int id, out UtilityItemObject prefab)
        {
            prefab = null;

            UtilityItemData data = GetData(id);
            if (data == null) return false;

            return TryGetPrefabFrom(data, out prefab);
        }

        /// <summary>int id 의 정적 데이터(가격·내구도·수리량 등). 없으면 null.</summary>
        public UtilityItemData GetData(int id)
        {
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

        // ===== string 조회 (프리팹 인식 → 경제 데이터) =====

        /// <summary>
        /// UtilityItemObject.ItemId(string, 예: "wrench")로 경제 데이터(UtilityItemData, int)를 찾는다.
        /// 상점 프리팹(박한솔)을 int 경제로 이어줄 때 쓴다. 없으면 false.
        /// </summary>
        public bool TryGetData(string itemId, out UtilityItemData data)
        {
            data = null;
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogError("[UtilityConnect] itemId(string) 가 비어 있음");
                return false;
            }

            BuildIndexIfNeeded();
            if (_byItemId != null && _byItemId.TryGetValue(itemId, out data))
                return true;

            Debug.LogError($"[UtilityConnect] itemId=\"{itemId}\" 에 매핑되는 UtilityItemData 가 없음");
            return false;
        }

        /// <summary>string ItemId → int id 단축 조회. 없으면 false.</summary>
        public bool TryGetId(string itemId, out int id)
        {
            id = 0;
            if (!TryGetData(itemId, out UtilityItemData data)) return false;
            id = data.Id;
            return true;
        }

        // ===== 내부 =====

        // UtilityItemData 의 DroppedPrefab 루트에서 UtilityItemObject 를 얻는다.
        private bool TryGetPrefabFrom(UtilityItemData data, out UtilityItemObject prefab)
        {
            prefab = null;

            if (data.DroppedPrefab == null)
            {
                Debug.LogError($"[UtilityConnect] id={data.Id} 의 DroppedPrefab 이 비어 있음");
                return false;
            }

            if (!data.DroppedPrefab.TryGetComponent(out prefab))
            {
                Debug.LogError($"[UtilityConnect] id={data.Id} 의 DroppedPrefab 루트에 UtilityItemObject 가 없음");
                return false;
            }

            return true;
        }

        // string→UtilityItemData 인덱스를 한 번만 만든다.
        // 각 UtilityItemData 의 DroppedPrefab → UtilityItemObject.ItemId(string) 를 키로 삼는다.
        private void BuildIndexIfNeeded()
        {
            if (_byItemId != null) return;

            _byItemId = new Dictionary<string, UtilityItemData>();

            if (GameCore.Instance == null || GameCore.Instance.Data == null)
            {
                Debug.LogError("[UtilityConnect] GameCore/Data 가 준비되지 않아 인덱스를 만들 수 없음");
                _byItemId = null;   // 다음 호출에서 다시 시도
                return;
            }

            foreach (UtilityItemData data in GameCore.Instance.Data.Tools.All.Values)
            {
                if (data == null) continue;
                if (!TryGetPrefabFrom(data, out UtilityItemObject prefab)) continue;

                string key = prefab.ItemId;
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[UtilityConnect] id={data.Id} 프리팹의 ItemId(string)가 비어 인덱스 제외");
                    continue;
                }

                if (!_byItemId.TryAdd(key, data))
                    Debug.LogError($"[UtilityConnect] 중복 string ItemId=\"{key}\" (id={data.Id}) — 첫 항목 유지");
            }
        }
    }
}
