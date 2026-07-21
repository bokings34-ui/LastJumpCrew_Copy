namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    public interface IShipAccidentPresentation
    {
        void ApplySnapshot(in NetworkShipAccidentSnapshot snapshot);

        float BeginClear();
    }
}
