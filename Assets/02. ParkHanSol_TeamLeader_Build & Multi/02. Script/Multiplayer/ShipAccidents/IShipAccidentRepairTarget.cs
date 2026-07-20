using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    public interface IShipAccidentRepairTarget : IInteractable
    {
        uint AccidentInstanceId { get; }
        PHSShipAccidentId AccidentId { get; }
        string RequiredItemId { get; }
        Vector3 RepairPosition { get; }
        bool IsRepairComplete { get; }
    }
}
