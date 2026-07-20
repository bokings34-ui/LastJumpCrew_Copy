namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IRunRandomLedger
    {
        NetworkRunRandomSnapshot Snapshot { get; }

        bool TryCreateServerScope(
            NetworkRunRandomStream stream,
            ulong scopeKey,
            out PHSDeterministicRandom random,
            out string reason);
    }
}
