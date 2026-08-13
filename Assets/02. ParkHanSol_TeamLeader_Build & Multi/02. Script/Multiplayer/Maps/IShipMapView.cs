using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public enum ShipMapMarkerKind : byte
    {
        Self = 1,
        Teammate = 2,
        Incident = 3,
        Object = 4,
        ExternalInteraction = 5
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
        FireExtinguisher = 14,
        EnemySpawn = 15,
        PatrolZone = 16,
        MeteorZone = 17,
        NebulaZone = 18,
        PlanetZone = 19,
        Vending = 20,
        Player = 21
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
            string status,
            int priority = 0,
            string deduplicationKey = null)
        {
            IconId = iconId;
            Symbol = symbol;
            Title = title;
            Status = status;
            Priority = priority;
            DeduplicationKey = deduplicationKey;
        }

        public ShipMapIconId IconId { get; }
        public string Symbol { get; }
        public string Title { get; }
        public string Status { get; }
        public int Priority { get; }
        public string DeduplicationKey { get; }
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
            int currentShipHp,
            int maximumShipHp,
            IReadOnlyList<ShipMapEventDetail> events)
        {
            Markers = markers;
            MapName = mapName;
            MapId = mapId;
            Difficulty = difficulty;
            RunPhase = runPhase;
            WarpChargeNormalized = warpChargeNormalized;
            ShipHpNormalized = shipHpNormalized;
            CurrentShipHp = currentShipHp;
            MaximumShipHp = maximumShipHp;
            Events = events;
        }

        public IReadOnlyList<ShipMapMarker> Markers { get; }
        public string MapName { get; }
        public int MapId { get; }
        public int Difficulty { get; }
        public string RunPhase { get; }
        public float WarpChargeNormalized { get; }
        public float ShipHpNormalized { get; }
        public int CurrentShipHp { get; }
        public int MaximumShipHp { get; }
        public IReadOnlyList<ShipMapEventDetail> Events { get; }
    }

    public interface IShipMapView
    {
        void SetVisible(bool visible);
        void Render(in ShipMapPresentation presentation);
    }
}
