using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames
{
    public interface IEventMiniGameTerminal
    {
        EventId ConfiguredEventId { get; }
        MiniGameType ConfiguredMiniGameType { get; }
        Vector3 WorldPosition { get; }
        bool IsConfigured { get; }
    }
}
