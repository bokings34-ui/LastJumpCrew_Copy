using UnityEngine;
namespace LastJumpCrew.SeoBoGyeong
{
    [CreateAssetMenu(
    fileName = "Zone_New",
    menuName = "LastJumpCrew/SeoBoGyeong/Zone")]

    public class ZoneData : ScriptableObject, IGameData
    {
        [Header("Zone Info")]
        public int id;
        public string zoneName;

        int IGameData.Id => id;

        // 추가되야할 정보 : 이벤트 확률 가중치, 보상, 난이도 등
    }
}
