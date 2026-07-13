using System.Linq;
using LastJumpCrew.SeoBoGyeong.Data;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 정적 데이터(구역/아이템)를 소유하는 매니저. 정적 데이터는 게임 중 불변이라 네트워크 동기화 대상이 아니다.
    /// 로드 방식: DataCatalog(SO) 참조 — Resources 경로 문자열 의존 제거(카탈로그는 인스펙터 연결).
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        [Tooltip("정적 데이터 카탈로그(SO). 에디터의 자동 등록 버튼으로 채운다.")]
        [SerializeField] private DataCatalog catalog;

        // TryGet, Get, All의 함수로 접근 가능
        public DataRepository<ItemData> Items { get; private set; }
        public DataRepository<UtilityItemData> Tools { get; private set; }
        public DataRepository<ZoneData> Zones { get; private set; }

        public void Init()
        {
            if (catalog == null)
            {
                Debug.LogError("[DataManager] DataCatalog 가 인스펙터에 연결되지 않았습니다.");
                return;
            }

            Items = new DataRepository<ItemData>(catalog.Items);
            // UtilityItemData 는 ItemData 상속 — Items 에서 타입 필터로 파생(현행 '양쪽 존재' 동작 유지)
            Tools = new DataRepository<UtilityItemData>(catalog.Items.OfType<UtilityItemData>().ToArray());
            Zones = new DataRepository<ZoneData>(catalog.Zones);

            Debug.Log($"[DataManager] Init 완료 — Items {Items.Count} / Tools {Tools.Count} / Zones {Zones.Count}");
        }
    }
}
