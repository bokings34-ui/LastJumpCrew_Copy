using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public interface IGameplaySceneContext
    {
        bool IsGameplayScene { get; }
        bool TryGetSpawnPoint(ulong ownerClientId, out Transform spawnPoint);
        bool TryGetRespawnPoint(out Transform respawnPoint);
    }
}
