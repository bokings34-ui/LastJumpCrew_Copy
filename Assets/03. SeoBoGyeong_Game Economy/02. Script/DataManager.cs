using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 정적 데이터(구역/아이템)를 소유하는 매니저. 정적 데이터는 게임 중 불변이라 네트워크 동기화 대상이 아니다.
    /// 이벤트(사고) 데이터는 노석민 영역(EventRegistrySO)이 소유하므로 여기서 소유하지 않는다.
    /// ⚠️ ItemData(ScriptableObject)는 담당자 부재로 아직 미생성. 생기면 아래 Item 주석을 해제한다.
    /// </summary>
    public class DataManager : MonoBehaviour
    {
        // --- ItemData(SO) 생성 후 주석 해제. 이벤트 데이터는 노석민 영역(EventRegistrySO) 소유라 여기 없음 ---
        // [SerializeField] private ItemData[]  itemDatas;
        [SerializeField] private ZoneData[]  zoneDatas;

        // public DataRepository<ItemData>  Items  { get; private set; }
        public DataRepository<ZoneData>  Zones  { get; private set; }

        public void Init()
        {
            // Items = new DataRepository<ItemData>(itemDatas);
            Zones = new DataRepository<ZoneData>(zoneDatas);
            Debug.Log("[DataManager] Init 완료 (Zone 활성 / Item 은 담당자 복귀 후)");
        }
    }
}
