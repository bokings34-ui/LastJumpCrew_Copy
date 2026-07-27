namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public interface ILobbyCustomizationProfileStore
    {
        bool TryLoad(
            out LobbyCustomizationProfileSnapshot snapshot,
            out string reason);

        bool TrySave(
            LobbyCustomizationProfileSnapshot snapshot,
            out string reason);
    }
}
