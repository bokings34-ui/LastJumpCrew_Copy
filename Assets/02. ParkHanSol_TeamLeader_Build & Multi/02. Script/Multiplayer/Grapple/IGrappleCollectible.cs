using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGrappleCollectible
    {
        Transform CollectionPoint { get; }
        bool TryCollect(IItemHolder itemHolder);
    }
}
