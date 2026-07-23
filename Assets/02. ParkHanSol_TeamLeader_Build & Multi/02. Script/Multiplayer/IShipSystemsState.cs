using System;
using LastJumpCrew.SeoBoGyeong;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IShipSystemsState : IShipStatus
    {
        int CurrentShipHp { get; }
        int MaximumShipHp { get; }
        bool IsPowerEnabled { get; }
        bool IsGravityEnabled { get; }
        bool IsBatteryInstalled { get; }
        string LastDamageCause { get; }
        uint Revision { get; }
        int ModuleCount { get; }

        event Action StateChanged;

        bool TryGetModuleSnapshot(
            NetworkShipModuleId moduleId,
            out NetworkShipModuleSnapshot snapshot);

        NetworkShipModuleSnapshot GetModuleSnapshotAt(int index);
    }

    public interface IShipSystemsCommands
    {
        bool TryApplyShipDamage(int amount, out string reason);
        bool TryApplyShipDamage(int amount, string cause, out string reason);
        bool TryDestroyShip(
            GameOverReason gameOverReason,
            string cause,
            out string reason);
        bool TryApplyModuleDamage(
            NetworkShipModuleId moduleId,
            int amount,
            bool causeFault,
            out string reason);
        bool TryApplyModuleDamage(
            NetworkShipModuleId moduleId,
            int amount,
            bool causeFault,
            string cause,
            out string reason);
        bool TryRepairModule(NetworkShipModuleId moduleId, int amount, out string reason);
        bool TryPowerOff(out string reason);
        bool CanRestorePowerWithBattery(out string reason);
        bool TryRestorePowerWithBattery(out string reason);
        bool TrySetGravityEnabled(bool isEnabled, out string reason);
    }

    public interface IShipDockUpgradeCommands
    {
        bool TryRestoreShipDurabilityAtDock(int amount, out string reason);
        bool TryIncreaseMaximumShipHpAtDock(int amount, out string reason);
    }
}
