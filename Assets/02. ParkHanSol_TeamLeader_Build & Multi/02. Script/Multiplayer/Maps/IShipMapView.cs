using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public enum ShipMapMarkerKind : byte
    {
        Self = 1,
        Teammate = 2,
        Incident = 3,
        Object = 4
    }

    public enum ShipMapIconId : byte
    {
        None = 0,
        Fire = 1,
        PowerFailure = 2,
        DeviceFailure = 3,
        HullBreach = 4,
        SteamLeak = 5,
        OxygenFailure = 6,
        GravityFailure = 7,
        PowerSync = 8,
        Cannon = 9,
        WireFix = 10,
        Warp = 11,
        Battery = 12,
        Wrench = 13,
        FireExtinguisher = 14
    }

    public readonly struct ShipMapMarker
    {
        public ShipMapMarker(
            ShipMapMarkerKind kind,
            Vector2 normalizedPosition,
            string symbol,
            ShipMapIconId iconId = ShipMapIconId.None)
        {
            Kind = kind;
            NormalizedPosition = normalizedPosition;
            Symbol = symbol;
            IconId = iconId;
        }

        public ShipMapMarkerKind Kind { get; }
        public Vector2 NormalizedPosition { get; }
        public string Symbol { get; }
        public ShipMapIconId IconId { get; }
    }

    public readonly struct ShipMapEventDetail
    {
        public ShipMapEventDetail(
            ShipMapIconId iconId,
            string symbol,
            string title,
            string status)
        {
            IconId = iconId;
            Symbol = symbol;
            Title = title;
            Status = status;
        }

        public ShipMapIconId IconId { get; }
        public string Symbol { get; }
        public string Title { get; }
        public string Status { get; }
    }

    public readonly struct ShipMapPresentation
    {
        public ShipMapPresentation(
            IReadOnlyList<ShipMapMarker> markers,
            string mapName,
            int mapId,
            int difficulty,
            string runPhase,
            float warpChargeNormalized,
            float shipHpNormalized,
            IReadOnlyList<ShipMapEventDetail> events)
        {
            Markers = markers;
            MapName = mapName;
            MapId = mapId;
            Difficulty = difficulty;
            RunPhase = runPhase;
            WarpChargeNormalized = warpChargeNormalized;
            ShipHpNormalized = shipHpNormalized;
            Events = events;
        }

        public IReadOnlyList<ShipMapMarker> Markers { get; }
        public string MapName { get; }
        public int MapId { get; }
        public int Difficulty { get; }
        public string RunPhase { get; }
        public float WarpChargeNormalized { get; }
        public float ShipHpNormalized { get; }
        public IReadOnlyList<ShipMapEventDetail> Events { get; }
    }

    public interface IShipMapView
    {
        void SetVisible(bool visible);
        void Render(in ShipMapPresentation presentation);
    }
}
