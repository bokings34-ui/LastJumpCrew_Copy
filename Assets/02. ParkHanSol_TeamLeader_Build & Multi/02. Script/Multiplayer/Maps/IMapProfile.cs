using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public enum PHSMapSceneMode : byte
    {
        SharedSceneEnvironment = 0,
        SeparateScene = 1
    }

    public enum PHSMapSkyboxMode : byte
    {
        ProfileMaterials = 0,
        DedicatedSceneGameplayWithProfileArrival = 1
    }

    public interface IMapProfile
    {
        int MapId { get; }
        string DisplayName { get; }
        bool Selectable { get; }
        int Difficulty { get; }
        float StageTimeLimitSeconds { get; }
        int ClearRewardCredits { get; }
        bool IsWarpMaintenance { get; }
        bool IsShopPortalProfile { get; }
        bool AdvancesStageTime { get; }
        bool AllowsEventGeneration { get; }
        bool AllowsDebrisGeneration { get; }
        bool AllowsShopPortal { get; }
        PHSMapSceneMode SceneMode { get; }
        string SceneName { get; }
        GameObject EnvironmentRootPrefab { get; }
        PHSMapSkyboxMode SkyboxMode { get; }
        Material GameplaySkybox { get; }
        Material ArrivalSkybox { get; }
        int IncidentPressureCapacity { get; }
        IReadOnlyList<PHSMapEventWeight> ExternalThreatWeights { get; }
        float ExternalThreatIntervalMinSeconds { get; }
        float ExternalThreatIntervalMaxSeconds { get; }
        int MaximumActiveExternalThreats { get; }
        IReadOnlyList<PHSMapShipAccidentWeight> InternalAccidentWeights { get; }
        float InternalAccidentIntervalMinSeconds { get; }
        float InternalAccidentIntervalMaxSeconds { get; }
        int MaximumActiveInternalAccidents { get; }
        float InternalModuleDamageMultiplier { get; }
        float InternalShipDamageMultiplier { get; }

        bool TryValidate(out string reason);
    }
}
