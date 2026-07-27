using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public enum ShipMapMarkerKind : byte
    {
        Self = 1,
        Teammate = 2,
        Incident = 3
    }

    public readonly struct ShipMapMarker
    {
        public ShipMapMarker(ShipMapMarkerKind kind, Vector2 normalizedPosition)
        {
            Kind = kind;
            NormalizedPosition = normalizedPosition;
        }

        public ShipMapMarkerKind Kind { get; }
        public Vector2 NormalizedPosition { get; }
    }

    public interface IShipMapView
    {
        void SetVisible(bool visible);
        void Render(IReadOnlyList<ShipMapMarker> markers);
    }
}
