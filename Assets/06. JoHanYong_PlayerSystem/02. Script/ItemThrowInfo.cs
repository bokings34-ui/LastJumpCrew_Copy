using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public readonly struct ItemThrowInfo
    {
        public readonly Vector3 Position;
        public readonly Vector3 Direction;
        public readonly float Force;
        public readonly ulong AttackerClientId;

        public ItemThrowInfo(Vector3 position,Vector3 direction,float force,ulong attackerClientId)
        {
            Position = position;
            Direction = direction;
            Force = force;
            AttackerClientId = attackerClientId;
        }
    }
}
