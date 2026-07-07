using UnityEngine;

[CreateAssetMenu(
    fileName = "Zone_New",
    menuName = "Game Data/Zone")]

public class ZoneData:ScriptableObject
{
    [Header("Zone Info")]
    public int id;
    public string zoneName;

    // 추가되야할 정보 : 이벤트 확률, 보상, 난이도 등
}
