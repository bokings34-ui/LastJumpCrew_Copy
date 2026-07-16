using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [CreateAssetMenu(
        fileName = "PHS_ShipAccident_New",
        menuName = "LastJumpCrew/ParkHanSol/Ship Accident Definition")]
    public sealed class PHSShipAccidentDefinitionSO : ScriptableObject
    {
        [Header("Identity And Presentation")]
        [SerializeField] private PHSShipAccidentId id;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject presentationPrefab;

        [Header("Repair Contract")]
        [SerializeField] private string requiredItemId;
        [SerializeField, Min(1)] private int requiredRepairProgress = 100;
        [SerializeField, Min(1)] private int repairProgressPerUse = 20;
        [SerializeField, Min(0)] private int moduleRepairOnResolve;

        [Header("Ship Impact")]
        [SerializeField] private NetworkShipModuleId targetModule = NetworkShipModuleId.LifeSupport;
        [SerializeField, Min(0)] private int initialModuleDamage;
        [SerializeField, Min(0)] private int initialShipDamage;
        [SerializeField, Min(0)] private int periodicModuleDamage;
        [SerializeField, Min(0)] private int periodicShipDamage;
        [SerializeField, Min(0.1f)] private float damageIntervalSeconds = 5f;
        [SerializeField] private bool causesModuleFault;

        public PHSShipAccidentId Id => id;
        public string DisplayName => displayName;
        public GameObject PresentationPrefab => presentationPrefab;
        public string RequiredItemId => requiredItemId;
        public int RequiredRepairProgress => requiredRepairProgress;
        public int RepairProgressPerUse => repairProgressPerUse;
        public int ModuleRepairOnResolve => moduleRepairOnResolve;
        public NetworkShipModuleId TargetModule => targetModule;
        public int InitialModuleDamage => initialModuleDamage;
        public int InitialShipDamage => initialShipDamage;
        public int PeriodicModuleDamage => periodicModuleDamage;
        public int PeriodicShipDamage => periodicShipDamage;
        public float DamageIntervalSeconds => damageIntervalSeconds;
        public bool CausesModuleFault => causesModuleFault;

        private void OnValidate()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_DEFINITION_INVALID asset={name} reason={reason}", this);
            }
        }

        public bool TryValidate(out string reason)
        {
            if (id == PHSShipAccidentId.None || !System.Enum.IsDefined(typeof(PHSShipAccidentId), id))
            {
                reason = $"id_invalid:value={(ushort)id}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                reason = "display_name_missing";
                return false;
            }

            if (string.IsNullOrWhiteSpace(requiredItemId))
            {
                reason = "required_item_id_missing";
                return false;
            }

            if (requiredRepairProgress <= 0 || repairProgressPerUse <= 0)
            {
                reason = "repair_progress_not_positive";
                return false;
            }

            if (targetModule == NetworkShipModuleId.None
                || !System.Enum.IsDefined(typeof(NetworkShipModuleId), targetModule))
            {
                reason = $"target_module_invalid:value={(byte)targetModule}";
                return false;
            }

            if (damageIntervalSeconds <= 0f
                || float.IsNaN(damageIntervalSeconds)
                || float.IsInfinity(damageIntervalSeconds))
            {
                reason = $"damage_interval_invalid:value={damageIntervalSeconds}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
