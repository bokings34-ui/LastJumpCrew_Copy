using LastJumpCrew.Common;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Doors
{
    [DisallowMultipleComponent]
    public sealed class PHSShipDoorTarget : MonoBehaviour, IDamageable, IRepairable
    {
        [SerializeField] private PHSNetworkShipDoorCoordinator coordinator;
        [SerializeField] private int doorIndex = -1;

        public bool IsAlive => coordinator != null
            && !coordinator.GetState(doorIndex).Destroyed;
        public bool CanRepair => coordinator != null
            && coordinator.CanRepair(doorIndex);
        public float CurrentIntegrity => coordinator == null
            ? 0f : coordinator.GetState(doorIndex).Integrity;
        public float MaxIntegrity => coordinator == null
            ? 0f : coordinator.MaximumIntegrity;

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (attacker == null
                || attacker.GetComponentInParent<EnemyBase>() == null)
            {
                return;
            }
            coordinator?.ApplyEnemyDamageServer(doorIndex, amount, attacker);
        }

        public bool ApplyRepair(float amount, GameObject repairer)
        {
            return coordinator != null
                && coordinator.TryRepairServer(doorIndex, amount, repairer);
        }

        public void Initialize(PHSNetworkShipDoorCoordinator owner, int index)
        {
            coordinator = owner;
            doorIndex = index;
        }
    }
}
