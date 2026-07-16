using System;
using Unity.Collections;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum NetworkShipModuleId : byte
    {
        None = 0,
        Power = 1,
        Gravity = 2,
        LifeSupport = 3,
        Engine = 4
    }

    public enum NetworkShipModuleRepairCondition : byte
    {
        Operational = 0,
        Damaged = 1,
        Faulted = 2,
        Destroyed = 3
    }

    public struct NetworkShipModuleSnapshot :
        INetworkSerializable,
        IEquatable<NetworkShipModuleSnapshot>
    {
        public NetworkShipModuleId ModuleId;
        public int CurrentHp;
        public int MaximumHp;
        public bool IsFaulted;
        public NetworkShipModuleRepairCondition RepairCondition;
        public FixedString64Bytes LastDamageCause;
        public uint Revision;

        public NetworkShipModuleSnapshot(
            NetworkShipModuleId moduleId,
            int currentHp,
            int maximumHp,
            bool isFaulted,
            NetworkShipModuleRepairCondition repairCondition,
            FixedString64Bytes lastDamageCause,
            uint revision)
        {
            ModuleId = moduleId;
            CurrentHp = currentHp;
            MaximumHp = maximumHp;
            IsFaulted = isFaulted;
            RepairCondition = repairCondition;
            LastDamageCause = lastDamageCause;
            Revision = revision;
        }

        public bool IsOperational => CurrentHp > 0 && !IsFaulted;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref ModuleId);
            serializer.SerializeValue(ref CurrentHp);
            serializer.SerializeValue(ref MaximumHp);
            serializer.SerializeValue(ref IsFaulted);
            serializer.SerializeValue(ref RepairCondition);
            serializer.SerializeValue(ref LastDamageCause);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(NetworkShipModuleSnapshot other)
        {
            return ModuleId == other.ModuleId
                && CurrentHp == other.CurrentHp
                && MaximumHp == other.MaximumHp
                && IsFaulted == other.IsFaulted
                && RepairCondition == other.RepairCondition
                && LastDamageCause.Equals(other.LastDamageCause)
                && Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkShipModuleSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                (byte)ModuleId,
                CurrentHp,
                MaximumHp,
                IsFaulted,
                (byte)RepairCondition,
                LastDamageCause,
                Revision);
        }
    }
}
