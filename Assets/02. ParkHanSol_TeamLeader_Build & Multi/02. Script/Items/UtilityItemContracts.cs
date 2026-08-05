using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [Serializable]
    public struct UtilityHeldItemPose
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField, Min(0.01f)] private float scaleMultiplier;

        public UtilityHeldItemPose(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            float scaleMultiplier)
        {
            this.localPosition = localPosition;
            this.localEulerAngles = localEulerAngles;
            this.scaleMultiplier = scaleMultiplier;
        }

        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => Quaternion.Euler(localEulerAngles);
        public float ScaleMultiplier => scaleMultiplier;
        public bool IsValid => scaleMultiplier > 0f;
    }

    public enum UtilityItemUpgradeEffect
    {
        None,
        RestoreShipHp,
        IncreaseShipMaximumHp,
        IncreaseHookPower,
        IncreaseThrusterDuration,
        IncreasePlayerMaximumHp
    }

    public enum UtilityItemActionKind : byte
    {
        None = 0,
        FireSuppression = 1,
        PowerRestore = 2,
        DeviceRepair = 3,
        HullBreachRepair = 4,
        SteamLeakRepair = 5,
        OxygenLeakRepair = 6,
        OxygenGeneratorRepair = 7,
        GravityGeneratorRepair = 8,
        BatteryDischarge = 9
    }

    [Serializable]
    public struct UtilityItemActionProfile
    {
        [SerializeField] private UtilityItemActionKind actionKind;
        [SerializeField, Min(1)] private int amount;
        [SerializeField, Min(0)] private int durabilityCost;

        public UtilityItemActionKind ActionKind => actionKind;
        public int Amount => amount;
        public int DurabilityCost => durabilityCost;
        public bool IsValid => actionKind != UtilityItemActionKind.None
            && Enum.IsDefined(typeof(UtilityItemActionKind), actionKind)
            && amount > 0
            && durabilityCost >= 0;
    }
}
