using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 정적 데이터(아이템/이벤트/구역)를 소유하는 매니저.
    /// 정적 데이터는 게임 중 불변이라 네트워크 동기화 대상이 아니다.
    ///
    /// ⚠️ ItemData / EventData / ZoneData (ScriptableObject)는 담당자 부재로 아직 미생성.
    ///    해당 SO 타입이 생기면 아래 주석 3세트를 해제하면 연결이 완료된다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        // --- SO 타입(ItemData/EventData/ZoneData) 생성 후 주석 해제 ---
        // [SerializeField] private ItemData[]  itemDatas;
        // [SerializeField] private EventData[] eventDatas;
        [SerializeField] private ZoneData[]  zoneDatas;

        // public DataRepository<ItemData>  Items  { get; private set; }
        // public DataRepository<EventData> Events { get; private set; }
        public DataRepository<ZoneData>  Zones  { get; private set; }

        public void Init()
        {
            // Items  = new DataRepository<ItemData>(itemDatas);
            // Events = new DataRepository<EventData>(eventDatas);
            Zones = new DataRepository<ZoneData>(zoneDatas);
            Debug.Log("[DataManager] Init 완료 (SO 연결은 담당자 복귀 후 활성화)");
        }
    }
}
