using LastJumpCrew.SeBoGyeong.Data;
using UnityEngine;



namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 정적 데이터(구역/아이템)를 소유하는 매니저. 정적 데이터는 게임 중 불변이라 네트워크 동기화 대상이 아니다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        private ItemData[] itemDatas;
        private UtilityItemData[] toolDatas;
        private ZoneData[] zoneDatas;

        // TryGet, Get, All의 함수로 접근 가능
        public DataRepository<ItemData>  Items  { get; private set; }
        public DataRepository<UtilityItemData> Tools { get; private set; }
        public DataRepository<ZoneData>  Zones  { get; private set; }

        public void Init()
        {
            itemDatas = Resources.LoadAll<ItemData>("Data/Items");
            // UtilityItemData는 ItemData를 상속 + 아이템 타입 & 내구도
            toolDatas = Resources.LoadAll<UtilityItemData>("Data/Items");
            zoneDatas = Resources.LoadAll<ZoneData>("Data/Zones");

            Items = new DataRepository<ItemData>(itemDatas);
            Tools = new DataRepository<UtilityItemData>(toolDatas);

            Zones = new DataRepository<ZoneData>(zoneDatas);


            Debug.Log("[DataManager] Init 완료 (Zone 활성 / Item 활성)");
            //Debug.Log($"[DataManager] Items : {string.Join(',', Items.All)}");
            //Debug.Log($"[DataManager] Zones : {string.Join(',', Zones.All)}");
            Debug.Log($"[DataManager/TEST]아이템ID: 2101 ={Items.Get(2101).DisplayName}");
        }
    }
}
