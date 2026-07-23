using System.Threading.Tasks;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface INetworkSessionExitService
    {
        Task<bool> LeaveToLobbyAsync(string lobbySceneName);
    }
}
