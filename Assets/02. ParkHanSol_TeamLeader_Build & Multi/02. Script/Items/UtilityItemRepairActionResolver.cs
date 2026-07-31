using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;

namespace LastJumpCrew.ParkHanSol.Items
{
    public static class UtilityItemRepairActionResolver
    {
        public static bool IsInstantCompleteItem(
            string itemId,
            UtilityItemActionKind actionKind)
        {
            return itemId == "auto_repair_kit"
                && actionKind is UtilityItemActionKind.DeviceRepair
                    or UtilityItemActionKind.HullBreachRepair
                    or UtilityItemActionKind.SteamLeakRepair
                    or UtilityItemActionKind.OxygenLeakRepair
                    or UtilityItemActionKind.OxygenGeneratorRepair
                    or UtilityItemActionKind.GravityGeneratorRepair;
        }

        public static bool TryResolve(
            PHSShipAccidentId accidentId,
            out UtilityItemActionKind actionKind)
        {
            actionKind = accidentId switch
            {
                PHSShipAccidentId.PowerFailure => UtilityItemActionKind.PowerRestore,
                PHSShipAccidentId.DeviceFailure => UtilityItemActionKind.DeviceRepair,
                PHSShipAccidentId.HullBreach => UtilityItemActionKind.HullBreachRepair,
                PHSShipAccidentId.SteamLeak => UtilityItemActionKind.SteamLeakRepair,
                PHSShipAccidentId.OxygenFailure => UtilityItemActionKind.OxygenGeneratorRepair,
                PHSShipAccidentId.GravityGeneratorFailure => UtilityItemActionKind.GravityGeneratorRepair,
                _ => UtilityItemActionKind.None
            };
            return actionKind != UtilityItemActionKind.None;
        }

        public static bool TryResolve(
            EventEffectKind effectKind,
            out UtilityItemActionKind actionKind)
        {
            actionKind = effectKind switch
            {
                EventEffectKind.Fire => UtilityItemActionKind.FireSuppression,
                EventEffectKind.OxygenLeak => UtilityItemActionKind.OxygenLeakRepair,
                EventEffectKind.EngineBreak => UtilityItemActionKind.DeviceRepair,
                _ => UtilityItemActionKind.None
            };
            return actionKind != UtilityItemActionKind.None;
        }
    }
}
