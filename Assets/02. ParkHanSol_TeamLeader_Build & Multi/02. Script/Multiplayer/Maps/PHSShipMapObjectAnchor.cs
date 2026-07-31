using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public enum ShipMapObjectKind : byte
    {
        WarpControl = 1,
        Vending = 2,
        ShopPortal = 3,
        SellStation = 4,
        BatteryStation = 5,
        OxygenSystem = 6,
        GravitySystem = 7,
        RepairTerminal = 8,
        Respawn = 9
    }

    [DisallowMultipleComponent]
    public sealed class PHSShipMapObjectAnchor : MonoBehaviour
    {
        [SerializeField] private ShipMapObjectKind kind;
        [SerializeField] private string displayName;
        [SerializeField] private string symbol;
        [SerializeField] private ShipMapIconId iconId;

        public ShipMapObjectKind Kind => kind;
        public string DisplayName => displayName;
        public string Symbol => symbol;
        public ShipMapIconId IconId => iconId;

        public bool TryValidate(out string reason)
        {
            if (!System.Enum.IsDefined(typeof(ShipMapObjectKind), kind))
            {
                reason = $"kind_invalid:{kind}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                reason = "display_name_missing";
                return false;
            }

            if (iconId == ShipMapIconId.None && string.IsNullOrWhiteSpace(symbol))
            {
                reason = "icon_and_symbol_missing";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
