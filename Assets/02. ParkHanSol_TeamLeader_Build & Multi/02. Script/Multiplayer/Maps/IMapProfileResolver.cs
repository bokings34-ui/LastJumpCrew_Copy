using System.Collections.Generic;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public interface IMapProfileResolver
    {
        IReadOnlyList<PHSMapProfileSO> Profiles { get; }

        bool TryResolve(int mapId, out PHSMapProfileSO profile);
    }
}
