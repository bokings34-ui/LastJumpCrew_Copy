using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public interface IMapRuntimeContext
    {
        PHSMapProfileSO CurrentProfile { get; }

        event Action<PHSMapProfileSO> CurrentProfileChanged;
    }
}
