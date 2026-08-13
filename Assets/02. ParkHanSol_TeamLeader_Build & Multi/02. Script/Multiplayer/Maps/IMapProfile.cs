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

    public enum PHSMapDifficultyTier : byte
    {
        Easy = 1,
        Normal = 2,
        Hard = 3
    }

    public interface IMapProfile
    {
        int MapId { get; }
        string DisplayName { get; }
        bool Selectable { get; }
        int Difficulty { get; }
        PHSMapDifficultyTier DifficultyTier { get; }
        string DifficultyLabel { get; }
        float DifficultyIntervalMultiplier { get; }
        float DifficultyDamageMultiplier { get; }
        float DifficultyRewardMultiplier { get; }
        int DebrisAmount { get; }
        string DebrisAmountLabel { get; }
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
        System.Collections.Generic.IReadOnlyList<PHSMapEventWeight> ExternalThreatWeights { get; }
        float ExternalThreatIntervalMinSeconds { get; }
        float ExternalThreatIntervalMaxSeconds { get; }
        int MaximumActiveExternalThreats { get; }
        float InternalModuleDamageMultiplier { get; }
        float InternalShipDamageMultiplier { get; }

        bool TryValidate(out string reason);
    }
}
