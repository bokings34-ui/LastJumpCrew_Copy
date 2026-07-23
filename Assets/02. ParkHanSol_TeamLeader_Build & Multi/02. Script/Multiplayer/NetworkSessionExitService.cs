using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkSessionExitService : INetworkSessionExitService
    {
        private const int ManagerDestroyFrameLimit = 8;
        private static Task<bool> activeExitTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeExitTask = null;
        }

        public Task<bool> LeaveToLobbyAsync(string lobbySceneName)
        {
            if (activeExitTask != null)
            {
                return activeExitTask;
            }

            if (string.IsNullOrWhiteSpace(lobbySceneName)
                || !Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                Debug.LogError(
                    $"PHS_SESSION_EXIT_FAILED reason=lobby_scene_not_in_build scene={lobbySceneName}");
                return Task.FromResult(false);
            }

            activeExitTask = LeaveToLobbyInternalAsync(lobbySceneName);
            return activeExitTask;
        }

        private static async Task<bool> LeaveToLobbyInternalAsync(
            string lobbySceneName)
        {
            try
            {
                var networkManager = NetworkManager.Singleton;
                var roomService = networkManager == null
                    ? null
                    : networkManager.GetComponent<MultiplayerRoomService>();
                if (roomService != null
                    && roomService.IsActive
                    && !await roomService.LeaveRoomAsync())
                {
                    activeExitTask = null;
                    Debug.LogError("PHS_SESSION_EXIT_FAILED reason=room_leave_failed");
                    return false;
                }

                if (ProximityVoiceChatSession.ActiveSession != null)
                {
                    await ProximityVoiceChatSession.ActiveSession.LeaveAsync();
                }

                if (networkManager != null && networkManager.IsListening)
                {
                    networkManager.Shutdown();
                }

                if (networkManager != null)
                {
                    UnityEngine.Object.Destroy(networkManager.gameObject);
                    for (var frame = 0;
                         frame < ManagerDestroyFrameLimit && networkManager != null;
                         frame++)
                    {
                        await Task.Yield();
                    }

                    if (networkManager != null)
                    {
                        activeExitTask = null;
                        Debug.LogError(
                            "PHS_SESSION_EXIT_FAILED reason=network_manager_destroy_timeout");
                        return false;
                    }
                }

                var loadOperation = SceneManager.LoadSceneAsync(
                    lobbySceneName,
                    LoadSceneMode.Single);
                if (loadOperation == null)
                {
                    activeExitTask = null;
                    Debug.LogError(
                        "PHS_SESSION_EXIT_FAILED reason=lobby_load_not_started");
                    return false;
                }

                while (!loadOperation.isDone)
                {
                    await Task.Yield();
                }

                activeExitTask = null;
                return true;
            }
            catch (Exception exception)
            {
                activeExitTask = null;
                Debug.LogError(
                    $"PHS_SESSION_EXIT_FAILED reason=exception type={exception.GetType().Name}");
                return false;
            }
        }
    }
}
