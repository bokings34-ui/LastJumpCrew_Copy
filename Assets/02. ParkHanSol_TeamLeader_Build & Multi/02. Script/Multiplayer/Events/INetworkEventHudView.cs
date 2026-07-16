using System.Collections.Generic;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public interface INetworkEventHudView
    {
        bool IsConfigured { get; }
        void Apply(PHSNetworkEventHudViewModel viewModel);
        void SetShipMapVisible(bool isVisible);
        void HideOffline();
    }

    public readonly struct PHSNetworkEventRoomViewModel
    {
        public PHSNetworkEventRoomViewModel(string roomId, string statusText, int activeIncidentCount)
        {
            RoomId = roomId ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            ActiveIncidentCount = activeIncidentCount;
        }

        public string RoomId { get; }
        public string StatusText { get; }
        public int ActiveIncidentCount { get; }
        public bool HasActiveIncident => ActiveIncidentCount > 0;
    }

    public sealed class PHSNetworkEventHudViewModel
    {
        public PHSNetworkEventHudViewModel(
            string alertText,
            int activeIncidentCount,
            IReadOnlyList<PHSNetworkEventRoomViewModel> rooms)
        {
            AlertText = alertText ?? string.Empty;
            ActiveIncidentCount = activeIncidentCount;
            Rooms = rooms;
        }

        public string AlertText { get; }
        public int ActiveIncidentCount { get; }
        public bool IsAlertVisible => !string.IsNullOrWhiteSpace(AlertText);
        public IReadOnlyList<PHSNetworkEventRoomViewModel> Rooms { get; }
    }
}
