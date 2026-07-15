using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class RoomSessionInfo
    {
        public RoomSessionInfo(
            string id,
            string name,
            int playerCount,
            int maxPlayers,
            bool hasPassword)
        {
            Id = id;
            Name = name;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            HasPassword = hasPassword;
        }

        public string Id { get; }
        public string Name { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }
        public bool HasPassword { get; }
    }

    public interface IMultiplayerRoomService
    {
        event Action RoomsChanged;
        event Action SessionJoined;
        event Action<string> OperationFailed;

        IReadOnlyList<RoomSessionInfo> Rooms { get; }
        string SessionCode { get; }
        bool IsHost { get; }
        bool IsActive { get; }

        Task<bool> InitializeAsync();
        Task<bool> RefreshRoomsAsync();
        Task<bool> CreateRoomAsync(string roomName, int maxPlayers, string password);
        Task<bool> JoinRoomAsync(string sessionId, string password);
        Task<bool> JoinRoomByCodeAsync(string sessionCode, string password);
        Task<bool> LeaveRoomAsync();
    }
}
