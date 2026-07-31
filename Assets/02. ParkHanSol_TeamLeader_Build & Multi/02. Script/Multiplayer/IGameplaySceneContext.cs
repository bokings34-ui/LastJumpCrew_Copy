using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGameplaySceneContext
    {
        bool IsGameplayScene { get; }
        bool TryGetSpawnPoint(
            ulong ownerClientId,
            out Transform spawnPoint,
            out float delaySeconds);
        bool TryGetRespawnPoint(
            ulong ownerClientId,
            out Transform respawnPoint,
            out float delaySeconds);
    }
}
