using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace SM
{
    public abstract class ShipModuleEventDataSO : EventDataSO
    {
        [Header("Repair Contract")]
        [SerializeField] private string requiredItemId = "wrench";
        [SerializeField, Min(1f)] private float requiredRepairProgress = 100f;
        [SerializeField, Min(0)] private int moduleRepairOnResolve;

        [Header("Ship Impact")]
        [SerializeField] private NetworkShipModuleId targetModule = NetworkShipModuleId.LifeSupport;
        [SerializeField, Min(0)] private int initialModuleDamage;
        [SerializeField, Min(0)] private int initialShipDamage;
        [SerializeField, Min(0)] private int periodicModuleDamage;
        [SerializeField, Min(0)] private int periodicShipDamage;
        [SerializeField, Min(0.1f)] private float damageIntervalSeconds = 5f;
        [SerializeField] private bool causesModuleFault;

        public string RequiredItemId => requiredItemId;
        public float RequiredRepairProgress => requiredRepairProgress;
        public int ModuleRepairOnResolve => moduleRepairOnResolve;
        public NetworkShipModuleId TargetModule => targetModule;
        public int InitialModuleDamage => initialModuleDamage;
        public int InitialShipDamage => initialShipDamage;
        public int PeriodicModuleDamage => periodicModuleDamage;
        public int PeriodicShipDamage => periodicShipDamage;
        public float DamageIntervalSeconds => damageIntervalSeconds;
        public bool CausesModuleFault => causesModuleFault;
    }
}
