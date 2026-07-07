using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_UtilityVendingMachineData",
        menuName = "LastJumpCrew/ParkHanSol/Utility Vending Machine Data")]
    public sealed class UtilityVendingMachineData : ScriptableObject
    {
        [SerializeField] private string machineId;
        [SerializeField] private string displayName;
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        public string MachineId => machineId;
        public string DisplayName => displayName;
        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
    }
}
