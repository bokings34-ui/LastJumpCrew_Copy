using SM;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames
{
    public interface IEventMiniGameTerminal
    {
        EventId ConfiguredEventId { get; }
        PHSMiniGameType ConfiguredMiniGameType { get; }
        Vector3 WorldPosition { get; }
        bool IsConfigured { get; }
    }
}
