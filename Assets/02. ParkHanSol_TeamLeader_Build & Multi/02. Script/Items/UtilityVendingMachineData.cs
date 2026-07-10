using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    // 자판기 한 대가 어떤 아이템을 지급하는지 정의하는 데이터 asset이다.
    // 씬의 자판기 오브젝트는 UtilityVendingMachineInteractable을 붙이고, 이 asset을 Inspector로 연결한다.
    [CreateAssetMenu(
        fileName = "PHS_UtilityVendingMachineData",
        menuName = "LastJumpCrew/ParkHanSol/Utility Vending Machine Data")]
    public sealed class UtilityVendingMachineData : ScriptableObject
    {
        // 로그/식별용 내부 ID다. 같은 종류 자판기는 같은 ID를 유지한다.
        [SerializeField] private string machineId;

        // UI나 안내 문구에 표시할 이름이다.
        [SerializeField] private string displayName;

        // 이 자판기가 플레이어 손에 지급할 아이템 데이터다.
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        public string MachineId => machineId;
        public string DisplayName => displayName;
        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
    }
}
