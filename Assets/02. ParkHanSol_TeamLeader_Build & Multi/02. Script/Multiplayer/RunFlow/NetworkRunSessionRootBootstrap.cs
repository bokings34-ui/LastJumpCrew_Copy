using System;
using System.Collections;
using System.Collections.Generic;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Inspector-wired lobby bootstrap that spawns the persistent run root once on the server.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkRunSessionRootBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkRunSessionRoot runSessionRootPrefab;

        private NetworkManager networkManager;
        private bool setupValid;
        private bool restartInProgress;
        private uint restartEpoch;
        private string restartSceneName;
        private NetworkRunRestartCoordinator restartCoordinator;
        private readonly HashSet<ulong> restartExcludedClientIds = new HashSet<ulong>();

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            setupValid = ValidateSetup();
            enabled = setupValid;
        }

        private void OnEnable()
        {
            if (!setupValid)
            {
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            TrySpawnServerRoot();
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
                if (networkManager.SceneManager != null)
                {
                    networkManager.SceneManager.OnLoadEventCompleted -= HandleRestartSceneLoaded;
                }
            }
        }

        private void HandleServerStarted()
        {
            TrySpawnServerRoot();
        }

        private void TrySpawnServerRoot()
        {
            if (!setupValid || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }

            if (NetworkRunSessionRoot.Instance != null)
            {
                return;
            }

            foreach (var spawnedObject in networkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject != null
                    && spawnedObject.TryGetComponent<NetworkRunSessionRoot>(out _))
                {
                    return;
                }
            }

            var prefabNetworkObject = runSessionRootPrefab.GetComponent<NetworkObject>();
            var networkObject = networkManager.SpawnManager.InstantiateAndSpawn(
                prefabNetworkObject,
                NetworkManager.ServerClientId,
                false,
                false);
            if (networkObject == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_SPAWN_FAILED reason=instantiate_and_spawn_failed",
                    this);
                return;
            }

            networkObject.name = runSessionRootPrefab.name;
            ValidatePersistentEventAuthority(networkObject);
            Debug.Log(
                $"PHS_RUN_SESSION_ROOT_SPAWNED prefab={runSessionRootPrefab.name} objectId={networkObject.NetworkObjectId}",
                this);
        }

        private static void ValidatePersistentEventAuthority(NetworkObject sessionRoot)
        {
            var runRoot = sessionRoot.GetComponent<NetworkRunSessionRoot>();
            if (runRoot == null)
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SPAWN_FAILED reason=persistent_event_authority_invalid " +
                    "detail=session_root_missing",
                    sessionRoot);
                return;
            }

            if (!runRoot.TryValidatePersistentEventAuthority(out var authorityReason))
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SPAWN_FAILED reason=persistent_event_authority_invalid " +
                    $"detail={authorityReason}",
                    sessionRoot);
                return;
            }

            Debug.Log(
                $"PHS_RUN_SESSION_ROOT_EVENT_AUTHORITY_OK objectId={sessionRoot.NetworkObjectId} " +
                "spawn_mode=shared_root",
                sessionRoot);
        }

        internal bool CanBeginRestartServer(
            NetworkRunRestartCoordinator coordinator,
            out string reason)
        {
            if (!setupValid
                || networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer
                || !networkManager.IsHost)
            {
                reason = "host_network_session_required";
                return false;
            }

            if (restartInProgress)
            {
                reason = $"restart_already_in_progress:{restartEpoch}";
                return false;
            }

            var root = NetworkRunSessionRoot.Instance;
            if (coordinator == null
                || root == null
                || root.Restart != coordinator
                || !root.IsSpawned)
            {
                reason = "active_restart_root_mismatch";
                return false;
            }

            if (networkManager.SceneManager == null)
            {
                reason = "network_scene_manager_missing";
                return false;
            }

            if (!root.RunFlow.TryResolveInitialMapScene(out var sceneName, out reason))
            {
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                reason = $"initial_map_scene_not_in_build:{sceneName}";
                return false;
            }

            var playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
            if (playerPrefab == null
                || playerPrefab.GetComponent<NetworkObject>() == null
                || playerPrefab.GetComponent<NetworkPlayerController>() == null)
            {
                reason = "player_prefab_contract_invalid";
                return false;
            }

            foreach (var pair in networkManager.ConnectedClients)
            {
                if (pair.Value.PlayerObject == null || !pair.Value.PlayerObject.IsSpawned)
                {
                    reason = $"connected_player_object_missing:{pair.Key}";
                    return false;
                }
            }

            if (GameCore.Instance == null
                || GameCore.Instance.Services == null
                || GameCore.Instance.Commands == null)
            {
                reason = "game_loop_commands_missing";
                return false;
            }

            reason = null;
            return true;
        }

        internal bool TryBeginRestartServer(
            NetworkRunRestartCoordinator coordinator,
            out string reason)
        {
            if (!CanBeginRestartServer(coordinator, out reason))
            {
                return false;
            }

            var root = NetworkRunSessionRoot.Instance;
            if (!root.RunFlow.TryResolveInitialMapScene(out restartSceneName, out reason))
            {
                return false;
            }

            restartEpoch = IncrementNonZero(coordinator.RestartEpoch);
            restartCoordinator = coordinator;
            restartInProgress = true;
            restartExcludedClientIds.Clear();
            coordinator.BeginLoadingServer(restartEpoch);
            networkManager.SceneManager.OnLoadEventCompleted -= HandleRestartSceneLoaded;
            networkManager.SceneManager.OnLoadEventCompleted += HandleRestartSceneLoaded;

            var status = networkManager.SceneManager.LoadScene(
                restartSceneName,
                LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                networkManager.SceneManager.OnLoadEventCompleted -= HandleRestartSceneLoaded;
                restartCoordinator.FailServer(
                    restartEpoch,
                    $"scene_load_not_started:{status}:{restartSceneName}");
                reason = $"scene_load_not_started:{status}";
                return false;
            }

            reason = null;
            Debug.Log(
                $"PHS_RUN_RESTART_STARTED epoch={restartEpoch} scene={restartSceneName} " +
                $"players={networkManager.ConnectedClients.Count}",
                this);
            return true;
        }

        private void HandleRestartSceneLoaded(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (!restartInProgress || sceneName != restartSceneName)
            {
                return;
            }

            networkManager.SceneManager.OnLoadEventCompleted -= HandleRestartSceneLoaded;
            if (loadSceneMode != LoadSceneMode.Single)
            {
                FailRestartServer($"load_mode_mismatch:{loadSceneMode}");
                return;
            }

            for (var index = 0; index < clientsTimedOut.Count; index++)
            {
                var clientId = clientsTimedOut[index];
                if (clientId == NetworkManager.ServerClientId)
                {
                    FailRestartServer("server_scene_load_timed_out");
                    return;
                }

                restartExcludedClientIds.Add(clientId);
                if (networkManager.ConnectedClients.ContainsKey(clientId))
                {
                    networkManager.DisconnectClient(
                        clientId,
                        $"Run restart scene load timed out. epoch={restartEpoch}");
                    Debug.LogError(
                        $"PHS_RUN_RESTART_CLIENT_DISCONNECTED epoch={restartEpoch} " +
                        $"clientId={clientId} reason=scene_load_timed_out",
                        this);
                }
            }

            foreach (var clientId in networkManager.ConnectedClientsIds)
            {
                if (restartExcludedClientIds.Contains(clientId))
                {
                    continue;
                }

                if (!clientsCompleted.Contains(clientId)
                    && !clientsTimedOut.Contains(clientId))
                {
                    FailRestartServer($"client_scene_barrier_missing:{clientId}");
                    return;
                }
            }

            restartCoordinator.BeginCommitServer(restartEpoch);
            StartCoroutine(CommitFreshRunNextFrame());
        }

        private IEnumerator CommitFreshRunNextFrame()
        {
            yield return null;

            var oldRoot = NetworkRunSessionRoot.Instance;
            var oldPlayers = new List<NetworkObject>();
            foreach (var pair in networkManager.ConnectedClients)
            {
                if (pair.Value.PlayerObject != null)
                {
                    oldPlayers.Add(pair.Value.PlayerObject);
                }
            }

            for (var index = 0; index < oldPlayers.Count; index++)
            {
                if (oldPlayers[index] != null && oldPlayers[index].IsSpawned)
                {
                    oldPlayers[index].Despawn(true);
                }
            }

            if (oldRoot != null && oldRoot.IsSpawned)
            {
                oldRoot.NetworkObject.Despawn(true);
            }

            // Let NGO finish the old authority despawn before publishing its replacement.
            yield return null;

            if (!TryPrepareFreshObjects(
                    out var freshRoot,
                    out var freshPlayers,
                    out var prepareReason))
            {
                FailRestartServer($"prepare_failed:{prepareReason}");
                yield break;
            }

            try
            {
                GameCore.Instance.Commands.StartGame();

                freshRoot.NetworkObject.SpawnWithOwnership(
                    NetworkManager.ServerClientId,
                    false);
                freshRoot.name = runSessionRootPrefab.name;
                if (!freshRoot.IsSpawned || freshRoot.Restart == null)
                {
                    throw new InvalidOperationException("fresh_root_spawn_failed");
                }

                freshRoot.Restart.BeginCommitServer(restartEpoch);
                for (var index = 0; index < freshPlayers.Count; index++)
                {
                    var preparedPlayer = freshPlayers[index];
                    if (restartExcludedClientIds.Contains(preparedPlayer.ClientId)
                        || !networkManager.ConnectedClients.ContainsKey(preparedPlayer.ClientId))
                    {
                        Destroy(preparedPlayer.NetworkObject.gameObject);
                        continue;
                    }

                    preparedPlayer.NetworkObject.SpawnAsPlayerObject(
                        preparedPlayer.ClientId,
                        false);
                    if (!preparedPlayer.NetworkObject.IsSpawned)
                    {
                        throw new InvalidOperationException(
                            $"fresh_player_spawn_failed:{preparedPlayer.ClientId}");
                    }
                }

                freshRoot.Restart.CompleteServer(restartEpoch);
                CompleteRestartServer();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                FailFreshCommit(
                    freshRoot,
                    freshPlayers,
                    $"commit_exception:{exception.GetType().Name}:{exception.Message}");
            }
        }

        private bool TryPrepareFreshObjects(
            out NetworkRunSessionRoot freshRoot,
            out List<PreparedPlayer> freshPlayers,
            out string reason)
        {
            freshRoot = null;
            freshPlayers = new List<PreparedPlayer>();
            var safeZone = NetworkWarpSafeZone.Instance;
            if (safeZone == null)
            {
                reason = "initial_map_safe_zone_missing";
                return false;
            }

            var clientIds = new List<ulong>();
            foreach (var clientId in networkManager.ConnectedClientsIds)
            {
                if (!restartExcludedClientIds.Contains(clientId))
                {
                    clientIds.Add(clientId);
                }
            }

            clientIds.Sort();
            var poses = new List<(Vector3 Position, Quaternion Rotation)>(clientIds.Count);
            for (var slot = 0; slot < clientIds.Count; slot++)
            {
                if (!safeZone.TryGetArrivalPose(slot, out var position, out var rotation))
                {
                    reason = $"initial_map_spawn_pose_missing:{slot}:{clientIds[slot]}";
                    return false;
                }

                poses.Add((position, rotation));
            }

            try
            {
                freshRoot = Instantiate(runSessionRootPrefab);
                if (freshRoot == null
                    || freshRoot.NetworkObject == null
                    || freshRoot.Restart == null)
                {
                    reason = "fresh_root_prefab_contract_invalid";
                    DestroyPreparedObjects(freshRoot, freshPlayers);
                    return false;
                }

                var playerPrefab = networkManager.NetworkConfig.PlayerPrefab;
                for (var index = 0; index < clientIds.Count; index++)
                {
                    var player = Instantiate(
                        playerPrefab,
                        poses[index].Position,
                        poses[index].Rotation);
                    var networkObject = player == null
                        ? null
                        : player.GetComponent<NetworkObject>();
                    if (networkObject == null
                        || player.GetComponent<NetworkPlayerController>() == null)
                    {
                        if (player != null)
                        {
                            Destroy(player);
                        }

                        reason = $"fresh_player_prefab_contract_invalid:{clientIds[index]}";
                        DestroyPreparedObjects(freshRoot, freshPlayers);
                        return false;
                    }

                    freshPlayers.Add(new PreparedPlayer(clientIds[index], networkObject));
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                reason = $"fresh_object_instantiation_exception:{exception.GetType().Name}";
                DestroyPreparedObjects(freshRoot, freshPlayers);
                return false;
            }

            reason = null;
            return true;
        }

        private void FailFreshCommit(
            NetworkRunSessionRoot freshRoot,
            List<PreparedPlayer> freshPlayers,
            string reason)
        {
            for (var index = 0; index < freshPlayers.Count; index++)
            {
                var player = freshPlayers[index].NetworkObject;
                if (player == null)
                {
                    continue;
                }

                if (player.IsSpawned)
                {
                    player.Despawn(true);
                }
                else
                {
                    Destroy(player.gameObject);
                }
            }

            if (freshRoot != null && freshRoot.IsSpawned && freshRoot.Restart != null)
            {
                freshRoot.Restart.FailServer(restartEpoch, reason);
            }
            else
            {
                DestroyPreparedObjects(freshRoot, null);
                Debug.LogError(
                    $"PHS_RUN_RESTART_FAILED epoch={restartEpoch} reason={reason} " +
                    "state_sync=unavailable",
                    this);
            }
        }

        private void FailRestartServer(string reason)
        {
            if (restartCoordinator != null && restartCoordinator.IsSpawned)
            {
                restartCoordinator.FailServer(restartEpoch, reason);
            }
            else
            {
                Debug.LogError(
                    $"PHS_RUN_RESTART_FAILED epoch={restartEpoch} reason={reason} " +
                    "state_sync=unavailable",
                    this);
            }
        }

        private void CompleteRestartServer()
        {
            Debug.Log(
                $"PHS_RUN_RESTART_COMPLETED epoch={restartEpoch} scene={restartSceneName} " +
                $"players={networkManager.ConnectedClients.Count}",
                this);
            restartCoordinator = null;
            restartSceneName = null;
            restartExcludedClientIds.Clear();
            restartInProgress = false;
        }

        private void DestroyPreparedObjects(
            NetworkRunSessionRoot root,
            List<PreparedPlayer> players)
        {
            if (players != null)
            {
                for (var index = 0; index < players.Count; index++)
                {
                    if (players[index].NetworkObject != null)
                    {
                        Destroy(players[index].NetworkObject.gameObject);
                    }
                }
            }

            if (root != null)
            {
                Destroy(root.gameObject);
            }
        }

        private static uint IncrementNonZero(uint value)
        {
            value++;
            return value == 0U ? 1U : value;
        }

        private bool ValidateSetup()
        {
            if (runSessionRootPrefab == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_BOOTSTRAP_FAILED reason=prefab_missing",
                    this);
                return false;
            }

            if (runSessionRootPrefab.GetComponent<NetworkObject>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunFlowCoordinator>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunStageClock>() == null
                || runSessionRootPrefab.GetComponent<NetworkShipSystemsState>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunEconomyLedger>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunRandomLedger>() == null
                || runSessionRootPrefab.GetComponent<NetworkShopTransitionVoteCoordinator>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunRestartCoordinator>() == null
                || runSessionRootPrefab.GetComponent<
                    LastJumpCrew.ParkHanSol.Multiplayer.Events.NetworkEventCoordinator>() == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_BOOTSTRAP_FAILED reason=prefab_contract_invalid",
                    this);
                return false;
            }

            return true;
        }

        private readonly struct PreparedPlayer
        {
            public PreparedPlayer(ulong clientId, NetworkObject networkObject)
            {
                ClientId = clientId;
                NetworkObject = networkObject;
            }

            public ulong ClientId { get; }
            public NetworkObject NetworkObject { get; }
        }
    }
}
