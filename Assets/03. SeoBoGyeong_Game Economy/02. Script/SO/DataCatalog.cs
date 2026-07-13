using System.Collections.Generic;
using LastJumpCrew.SeoBoGyeong.Data;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 정적 데이터 카탈로그. Resources 경로 문자열 대신 눈에 보이는 참조로 SO를 모아
    /// DataManager 에 공급한다(인스펙터 연결).
    /// 리스트 채우기는 에디터의 "폴더 스캔 자동 등록" 버튼(DataCatalogEditor) 사용.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DataCatalog",
        menuName = "LastJumpCrew/SeoBoGyeong/Data Catalog")]
    public class DataCatalog : ScriptableObject
    {
        [Header("아이템 (UtilityItemData 포함)")]
        [SerializeField] private List<ItemData> items = new();

        [Header("구역")]
        [SerializeField] private List<ZoneData> zones = new();

        public IReadOnlyList<ItemData> Items => items;
        public IReadOnlyList<ZoneData> Zones => zones;
    }
}
