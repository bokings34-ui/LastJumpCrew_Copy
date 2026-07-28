namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    public interface INetworkLobbyCustomizationService :
        ILobbyCustomizationService
    {
        PersonalLobbyCustomizationCreditsWallet PersonalCreditsWallet { get; }
    }
}
