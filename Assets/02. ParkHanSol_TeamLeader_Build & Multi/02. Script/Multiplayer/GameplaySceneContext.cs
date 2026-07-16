using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class GameplaySceneContext : MonoBehaviour, IGameplaySceneContext
    {
        [SerializeField] private Transform spawnPointsRoot;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private bool isGameplayScene = true;

        public bool IsGameplayScene => isActiveAndEnabled && isGameplayScene;

        public static GameplaySceneContext FindForScene(Scene scene)
        {
            foreach (var context in FindObjectsByType<GameplaySceneContext>())
            {
                if (context.gameObject.scene == scene)
                {
                    return context;
                }
            }

            return null;
        }

        public bool TryGetSpawnPoint(ulong ownerClientId, out Transform spawnPoint)
        {
            spawnPoint = null;
            if (spawnPointsRoot == null || spawnPointsRoot.childCount == 0)
            {
                Debug.LogWarning($"PHS_SPAWN_POINT_MISSING scene={gameObject.scene.name} context={name}");
                return false;
            }

            var index = (int)(ownerClientId % (ulong)spawnPointsRoot.childCount);
            spawnPoint = spawnPointsRoot.GetChild(index);
            return spawnPoint != null;
        }

        public bool TryGetRespawnPoint(out Transform respawnPoint)
        {
            respawnPoint = this.respawnPoint;
            if (respawnPoint != null)
            {
                return true;
            }

            Debug.LogError($"PHS_RESPAWN_POINT_MISSING scene={gameObject.scene.name} context={name}", this);
            return false;
        }
    }
}
