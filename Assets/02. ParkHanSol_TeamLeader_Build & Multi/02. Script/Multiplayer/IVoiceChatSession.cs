using System.Threading.Tasks;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IVoiceChatSession
    {
        bool IsInChannel { get; }
        string ActiveChannelName { get; }

        void SetVoiceChannel(string channelName);
        Task<bool> JoinLocalPlayerIfReadyAsync();
        Task<bool> JoinForLocalPlayerAsync(GameObject localPlayer);
        Task LeaveAsync();
        void UpdateLocalPosition(GameObject localPlayer);
    }
}
