using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class GameplaySceneContext : MonoBehaviour, IGameplaySceneContext
    {
        public const int RequiredNetworkSlotCount = 8;

        [SerializeField] private Transform spawnPointsRoot;
        [SerializeField] private Transform respawnPointsRoot;
        [SerializeField, Min(0f)] private float sequentialSpawnIntervalSeconds = 0.35f;
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

        public bool TryGetSpawnPoint(
            ulong ownerClientId,
            out Transform spawnPoint,
            out float delaySeconds)
        {
            spawnPoint = null;
            delaySeconds = 0f;
            if (!TryGetPlayerSlotIndex(ownerClientId, out var slotIndex))
            {
                return false;
            }

            return TryGetSlot(
                spawnPointsRoot,
                slotIndex,
                "spawn",
                out spawnPoint,
                out delaySeconds);
        }

        public bool TryGetRespawnPoint(
            ulong ownerClientId,
            out Transform respawnPoint,
            out float delaySeconds)
        {
            respawnPoint = null;
            delaySeconds = 0f;
            if (!TryGetPlayerSlotIndex(ownerClientId, out var slotIndex))
            {
                return false;
            }

            return TryGetSlot(
                respawnPointsRoot,
                slotIndex,
                "respawn",
                out respawnPoint,
                out delaySeconds);
        }

        private bool TryGetPlayerSlotIndex(ulong ownerClientId, out int slotIndex)
        {
            slotIndex = -1;
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SLOT_FAILED reason=network_manager_not_listening " +
                    $"scene={gameObject.scene.name} clientId={ownerClientId}",
                    this);
                return false;
            }

            var connectedClientIds = networkManager.ConnectedClientsIds;
            if (connectedClientIds.Count > RequiredNetworkSlotCount)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SLOT_FAILED reason=player_capacity_exceeded " +
                    $"connected={connectedClientIds.Count} capacity={RequiredNetworkSlotCount}",
                    this);
                return false;
            }

            var ownerIsConnected = false;
            slotIndex = 0;
            for (var index = 0; index < connectedClientIds.Count; index++)
            {
                if (connectedClientIds[index] == ownerClientId)
                {
                    ownerIsConnected = true;
                }
                else if (connectedClientIds[index] < ownerClientId)
                {
                    slotIndex++;
                }
            }

            if (ownerIsConnected)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_PLAYER_SLOT_FAILED reason=client_not_connected clientId={ownerClientId}",
                this);
            return false;
        }

        private bool TryGetSlot(
            Transform pointsRoot,
            int slotIndex,
            string slotType,
            out Transform point,
            out float delaySeconds)
        {
            point = null;
            delaySeconds = 0f;
            if (pointsRoot == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SLOT_FAILED reason={slotType}_root_missing " +
                    $"scene={gameObject.scene.name} context={name}",
                    this);
                return false;
            }

            if (pointsRoot.childCount < RequiredNetworkSlotCount)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SLOT_FAILED reason={slotType}_slot_count_invalid " +
                    $"required={RequiredNetworkSlotCount} actual={pointsRoot.childCount}",
                    this);
                return false;
            }

            point = pointsRoot.GetChild(slotIndex);
            if (point == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SLOT_FAILED reason={slotType}_slot_missing index={slotIndex}",
                    this);
                return false;
            }

            delaySeconds = slotIndex * sequentialSpawnIntervalSeconds;
            return true;
        }
    }
}
