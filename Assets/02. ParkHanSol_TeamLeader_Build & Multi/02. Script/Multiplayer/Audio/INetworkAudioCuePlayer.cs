namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    public interface INetworkAudioCuePlayer
    {
        bool TryPlay(NetworkAudioCue cue, out string failureReason);
    }

    public interface IPositionedNetworkAudioCuePlayer :
        INetworkAudioCuePlayer
    {
        bool TryPlayAt(
            NetworkAudioCue cue,
            UnityEngine.Vector3 position,
            out string failureReason);
    }
}
