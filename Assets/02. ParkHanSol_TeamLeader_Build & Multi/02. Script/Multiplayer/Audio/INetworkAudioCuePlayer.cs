namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    public interface INetworkAudioCuePlayer
    {
        bool TryPlay(NetworkAudioCue cue, out string failureReason);
    }
}
