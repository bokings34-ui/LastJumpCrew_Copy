using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkRunFlowCoordinator : NetworkBehaviour, IRunFlowStatus
    {
        [Header("Run Rules")]
        [SerializeField, Min(1f)] private float warpChargeSeconds = 45f;
        [SerializeField, Min(0.1f)] private float warpTransitionSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float warpArrivalSeconds = 1f;
        [SerializeField, Min(0f)] private float rearmSeconds = 8f;
        [SerializeField] private PHSMapCatalogSO mapCatalog;
        [SerializeField, Range(PHSMapProfileSO.MinimumMapId, PHSMapProfileSO.MaximumMapId)]
        private int initialMapId = PHSMapProfileSO.MinimumMapId;
        [SerializeField] private string mapSceneName = "PHS_Map_ver1";
        [SerializeField] private string shopSceneName = "PHS_ExteriorShopScene";
        [SerializeField] private bool automaticallyLoadShop;
        [SerializeField] private bool requireAllConnectedAlivePlayersSafe = true;

        private readonly NetworkVariable<NetworkRunPhase> synchronizedPhase = new(
            NetworkRunPhase.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> synchronizedWarpCharge = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedClearedZones = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedShopCycles = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedSelectedNextMapId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedActiveMapId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> synchronizedActiveMapRevision = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedSafePlayers = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedRequiredSafePlayers = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedFinalShopPending = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly HashSet<ulong> safePlayerIds = new();
        private readonly HashSet<ulong> debrisPlayerIds = new();
        private readonly HashSet<ulong> warpRevivePlayerIds = new();
        private IGameStateProvider gameState;
        private IGameCommands gameCommands;
        private float chargeElapsed;
        private float rearmElapsed;
        private float nextBindAttemptTime;
        private bool sceneLoadRequested;
        private bool finalShopCompleted;
        private float scheduledWarpExecutionTime = -1f;
        private float scheduledWarpArrivalEndTime = -1f;
        private float scheduledWarpReviveTime = -1f;
        private int pendingNextMapId;

        public static NetworkRunFlowCoordinator Instance { get; private set; }

        public NetworkRunPhase Phase => synchronizedPhase.Value;
        public float WarpChargeNormalized => synchronizedWarpCharge.Value;
        public int ClearedZoneCount => synchronizedClearedZones.Value;
        public int CompletedShopCycleCount => synchronizedShopCycles.Value;
        public int SelectedNextMapId => synchronizedSelectedNextMapId.Value;
        public int ActiveMapId => synchronizedActiveMapId.Value;
        public int SelectedZoneId => ActiveMapId;
        public int SafePlayerCount => synchronizedSafePlayers.Value;
        public int RequiredSafePlayerCount => synchronizedRequiredSafePlayers.Value;
        public bool IsFinalShopPending => synchronizedFinalShopPending.Value;
        public bool RequiresAllConnectedAlivePlayersSafe => requireAllConnectedAlivePlayersSafe;
        public bool IsWarpSafetySatisfied => !requireAllConnectedAlivePlayersSafe ||
            (RequiredSafePlayerCount > 0 && SafePlayerCount >= RequiredSafePlayerCount);
        public event Action<NetworkRunPhase, NetworkRunPhase> PhaseChanged;
        public event Action<int, int> ActiveMapChanged;
        public event Action<int> ActiveMapCommitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError($"PHS_RUN_FLOW_SETUP_FAILED reason=duplicate_coordinator current={name} existing={Instance.name}", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (!ValidateMapCatalog())
            {
                enabled = false;
                return;
            }

            synchronizedPhase.OnValueChanged += HandleSynchronizedPhaseChanged;
            synchronizedActiveMapId.OnValueChanged += HandleSynchronizedActiveMapChanged;
            synchronizedActiveMapRevision.OnValueChanged += HandleSynchronizedActiveMapRevisionChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (IsServer)
            {
                synchronizedActiveMapId.Value = initialMapId;
                synchronizedSelectedNextMapId.Value = initialMapId;
                pendingNextMapId = initialMapId;
                synchronizedActiveMapRevision.Value++;
                TryBindGameFlow();
            }
        }

        public override void OnNetworkDespawn()
        {
            synchronizedPhase.OnValueChanged -= HandleSynchronizedPhaseChanged;
            synchronizedActiveMapId.OnValueChanged -= HandleSynchronizedActiveMapChanged;
            synchronizedActiveMapRevision.OnValueChanged -= HandleSynchronizedActiveMapRevisionChanged;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            UnbindGameFlow();
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || OwnerClientId != NetworkManager.ServerClientId)
            {
                return;
            }

            TickScheduledWarpRevives();
            RefreshSafePlayerCount();

            if (synchronizedPhase.Value == NetworkRunPhase.Warping)
            {
                TickScheduledWarpExecution();
                return;
            }

            if (synchronizedPhase.Value == NetworkRunPhase.WarpArrival)
            {
                TickScheduledWarpArrival();
                return;
            }

            if (TryBindGameFlow())
            {
                TickServerFlow(Time.deltaTime);
            }
        }

        public void SetPlayerInsideSafeZone(ulong clientId, bool isInside)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_WARP_SAFE_ZONE_FAILED reason=server_required coordinator={name}", this);
                return;
            }

            if (isInside)
            {
                safePlayerIds.Add(clientId);
                debrisPlayerIds.Remove(clientId);
            }
            else
            {
                safePlayerIds.Remove(clientId);
            }

            RemoveDisconnectedPlayers();
            RefreshSafePlayerCount();
        }

        public bool TryActivateWarp(ulong activatorClientId, out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (synchronizedPhase.Value == NetworkRunPhase.Warping)
            {
                reason = "warp_already_in_progress";
                return false;
            }

            if (gameState == null || gameCommands == null || gameState.Phase != GamePhase.Play)
            {
                reason = "play_phase_required";
                return false;
            }

            RemoveDisconnectedPlayers();
            if (chargeElapsed < warpChargeSeconds)
            {
                reason = "warp_charge_incomplete";
                return false;
            }

            if (requireAllConnectedAlivePlayersSafe &&
                !AreAllConnectedAlivePlayersSafe(out var safePlayers, out var requiredPlayers))
            {
                reason = $"players_not_safe:{safePlayers}/{requiredPlayers}";
                return false;
            }

            scheduledWarpExecutionTime = Time.time + warpTransitionSeconds;
            SetPhase(NetworkRunPhase.Warping);
            Debug.Log(
                $"PHS_RUN_FLOW_WARP_STARTED clientId={activatorClientId} duration={warpTransitionSeconds:0.##}",
                this);
            reason = null;
            return true;
        }

        public bool TrySelectNextZone(int zoneId, out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (synchronizedPhase.Value != NetworkRunPhase.WarpReady)
            {
                reason = "warp_ready_required";
                return false;
            }

            if (zoneId <= 0)
            {
                reason = "invalid_zone_id";
                return false;
            }

            if (!mapCatalog.TryResolve(zoneId, out var profile))
            {
                reason = $"map_profile_missing:{zoneId}";
                Debug.LogError($"PHS_RUN_FLOW_MAP_SELECT_FAILED reason={reason}", this);
                return false;
            }

            if (!profile.Selectable)
            {
                reason = $"map_not_selectable:{zoneId}";
                Debug.LogError($"PHS_RUN_FLOW_MAP_SELECT_FAILED reason={reason}", this);
                return false;
            }

            pendingNextMapId = zoneId;
            synchronizedSelectedNextMapId.Value = zoneId;
            reason = null;
            Debug.Log($"PHS_RUN_FLOW_NEXT_ZONE_SELECTED zone={zoneId}");
            return true;
        }

        public void SetPlayerInsideDebrisZone(ulong clientId, bool isInside)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_DEBRIS_ZONE_FAILED reason=server_required coordinator={name}", this);
                return;
            }

            if (isInside)
            {
                debrisPlayerIds.Add(clientId);
                safePlayerIds.Remove(clientId);
            }
            else
            {
                debrisPlayerIds.Remove(clientId);
            }

            RemoveDisconnectedPlayers();
            RefreshSafePlayerCount();
        }

        public bool TryCompleteFinalShop(out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (!synchronizedFinalShopPending.Value || gameState == null || gameState.Phase != GamePhase.GameClear)
            {
                reason = "final_shop_not_pending";
                return false;
            }

            finalShopCompleted = true;
            synchronizedFinalShopPending.Value = false;
            synchronizedShopCycles.Value = 3;
            synchronizedPhase.Value = NetworkRunPhase.Clear;
            reason = null;
            Debug.Log($"PHS_RUN_FLOW_CLEAR zones={synchronizedClearedZones.Value} shopCycles={synchronizedShopCycles.Value}");
            return true;
        }

        private bool TryBindGameFlow()
        {
            if (gameState != null && gameCommands != null)
            {
                return true;
            }

            if (Time.unscaledTime < nextBindAttemptTime)
            {
                return false;
            }

            nextBindAttemptTime = Time.unscaledTime + 1f;
            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                return false;
            }

            gameState = gameCore.Services.Get<IGameStateProvider>();
            gameCommands = gameCore.Services.Get<IGameCommands>();
            if (gameState == null || gameCommands == null)
            {
                gameState = null;
                gameCommands = null;
                Debug.LogError($"PHS_RUN_FLOW_BIND_FAILED reason=services_missing coordinator={name}", this);
                return false;
            }

            gameState.StateChanged += HandleGameStateChanged;
            MirrorGameState();
            Debug.Log($"PHS_RUN_FLOW_BOUND coordinator={name} phase={gameState.Phase}");
            return true;
        }

        private void UnbindGameFlow()
        {
            if (gameState != null)
            {
                gameState.StateChanged -= HandleGameStateChanged;
            }

            gameState = null;
            gameCommands = null;
        }

        private void TickServerFlow(float deltaTime)
        {
            MirrorGameState();
            switch (gameState.Phase)
            {
                case GamePhase.ZoneSelect:
                    TickRearm(deltaTime);
                    break;
                case GamePhase.Play:
                    TickWarpCharge(deltaTime);
                    break;
                case GamePhase.Shop:
                    SetPhase(NetworkRunPhase.Shop);
                    RequestSceneLoad(shopSceneName);
                    break;
                case GamePhase.GameClear:
                    TickFinalShop();
                    break;
                case GamePhase.GameOver:
                    SetPhase(NetworkRunPhase.GameOver);
                    break;
            }
        }

        private void TickRearm(float deltaTime)
        {
            if (!TryResolveMapScene(ActiveMapId, out _, out var activeMapSceneName))
            {
                return;
            }

            if (SceneManager.GetActiveScene().name != activeMapSceneName)
            {
                return;
            }

            SetPhase(NetworkRunPhase.Rearming);
            rearmElapsed += deltaTime;
            if (rearmElapsed < rearmSeconds)
            {
                return;
            }

            rearmElapsed = 0f;
            chargeElapsed = 0f;
            synchronizedWarpCharge.Value = 0f;
            if (pendingNextMapId <= 0)
            {
                Debug.LogError("PHS_RUN_FLOW_ZONE_START_FAILED reason=next_map_not_selected", this);
                return;
            }

            if (!mapCatalog.TryResolve(pendingNextMapId, out var profile))
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_ZONE_START_FAILED reason=map_profile_missing mapId={pendingNextMapId}",
                    this);
                return;
            }

            if (!profile.Selectable)
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_ZONE_START_FAILED reason=map_not_selectable mapId={pendingNextMapId}",
                    this);
                return;
            }

            var nextZoneId = pendingNextMapId;
            gameCommands.SelectZone(nextZoneId);
            pendingNextMapId = 0;
            synchronizedSelectedNextMapId.Value = 0;
            Debug.Log($"PHS_RUN_FLOW_ZONE_STARTED zone={nextZoneId} cleared={gameState.ClearedZoneCount}");
        }

        private void TickWarpCharge(float deltaTime)
        {
            chargeElapsed = Mathf.Min(warpChargeSeconds, chargeElapsed + deltaTime);
            synchronizedWarpCharge.Value = Mathf.Clamp01(chargeElapsed / warpChargeSeconds);
            SetPhase(chargeElapsed >= warpChargeSeconds
                ? NetworkRunPhase.WarpReady
                : NetworkRunPhase.Charging);
        }

        private void ExecuteReadyWarp()
        {
            KillPlayersLeftInDebrisZone();
            gameCommands.RequestJump();
            scheduledWarpReviveTime = Time.time + 0.5f;
            safePlayerIds.Clear();
            debrisPlayerIds.Clear();
            synchronizedSafePlayers.Value = 0;
            chargeElapsed = 0f;
            synchronizedWarpCharge.Value = 0f;
            MirrorGameState();

            switch (gameState.Phase)
            {
                case GamePhase.ZoneSelect:
                    SetPhase(NetworkRunPhase.WarpArrival);
                    RequestMapRefresh();
                    break;
                case GamePhase.Shop:
                    SetPhase(NetworkRunPhase.Shop);
                    RequestSceneLoad(shopSceneName);
                    break;
                case GamePhase.GameClear:
                    SetPhase(NetworkRunPhase.FinalShop);
                    break;
                case GamePhase.GameOver:
                    SetPhase(NetworkRunPhase.GameOver);
                    break;
                default:
                    SetPhase(NetworkRunPhase.WarpReady);
                    Debug.LogError(
                        $"PHS_RUN_FLOW_WARP_FAILED reason=jump_rejected phase={gameState.Phase}",
                        this);
                    break;
            }

            Debug.Log($"PHS_RUN_FLOW_WARP_COMPLETED cleared={gameState.ClearedZoneCount} phase={gameState.Phase}");
        }

        private void TickScheduledWarpExecution()
        {
            if (scheduledWarpExecutionTime < 0f)
            {
                Debug.LogError("PHS_RUN_FLOW_WARP_FAILED reason=execution_not_scheduled", this);
                return;
            }

            if (Time.time < scheduledWarpExecutionTime)
            {
                return;
            }

            scheduledWarpExecutionTime = -1f;
            ExecuteReadyWarp();
        }

        private void TickScheduledWarpArrival()
        {
            if (scheduledWarpArrivalEndTime < 0f || Time.time < scheduledWarpArrivalEndTime)
            {
                return;
            }

            scheduledWarpArrivalEndTime = -1f;
            SetPhase(NetworkRunPhase.Rearming);
        }

        private void KillPlayersLeftInDebrisZone()
        {
            foreach (var clientId in debrisPlayerIds)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                    || client.PlayerObject == null)
                {
                    Debug.LogError($"PHS_WARP_DEATH_FAILED reason=player_object_missing clientId={clientId}", this);
                    continue;
                }

                var lifeState = client.PlayerObject.GetComponent<NetworkPlayerLifeState>();
                if (lifeState == null)
                {
                    Debug.LogError($"PHS_WARP_DEATH_FAILED reason=life_state_missing clientId={clientId}", client.PlayerObject);
                    continue;
                }

                lifeState.KillForWarp();
                warpRevivePlayerIds.Add(clientId);
            }
        }

        private void TickScheduledWarpRevives()
        {
            if (scheduledWarpReviveTime < 0f || Time.time < scheduledWarpReviveTime)
            {
                return;
            }

            scheduledWarpReviveTime = -1f;
            var revivedPlayerIds = new List<ulong>();
            foreach (var clientId in warpRevivePlayerIds)
            {
                if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                    || client.PlayerObject == null)
                {
                    revivedPlayerIds.Add(clientId);
                    continue;
                }

                var lifeState = client.PlayerObject.GetComponent<NetworkPlayerLifeState>();
                if (lifeState != null && lifeState.IsWaitingForWarpRevive)
                {
                    if (lifeState.TryReviveAfterWarp())
                    {
                        revivedPlayerIds.Add(clientId);
                    }
                }
                else
                {
                    revivedPlayerIds.Add(clientId);
                }
            }

            foreach (var clientId in revivedPlayerIds)
            {
                warpRevivePlayerIds.Remove(clientId);
            }

            if (warpRevivePlayerIds.Count > 0)
            {
                scheduledWarpReviveTime = Time.time + 0.5f;
            }
        }

        private void RemoveDisconnectedPlayers()
        {
            safePlayerIds.RemoveWhere(clientId => !NetworkManager.ConnectedClients.ContainsKey(clientId));
            debrisPlayerIds.RemoveWhere(clientId => !NetworkManager.ConnectedClients.ContainsKey(clientId));
            warpRevivePlayerIds.RemoveWhere(clientId => !NetworkManager.ConnectedClients.ContainsKey(clientId));
        }

        private bool AreAllConnectedAlivePlayersSafe(out int safePlayers, out int requiredPlayers)
        {
            safePlayers = 0;
            requiredPlayers = 0;
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                var lifeState = playerObject.GetComponent<NetworkPlayerLifeState>();
                if (lifeState != null && !lifeState.IsAlive)
                {
                    continue;
                }

                requiredPlayers++;
                if (safePlayerIds.Contains(pair.Key))
                {
                    safePlayers++;
                }
            }

            synchronizedSafePlayers.Value = safePlayers;
            synchronizedRequiredSafePlayers.Value = requiredPlayers;
            return requiredPlayers > 0 && safePlayers == requiredPlayers;
        }

        private void RefreshSafePlayerCount()
        {
            var safePlayers = 0;
            var requiredPlayers = 0;
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                var lifeState = playerObject.GetComponent<NetworkPlayerLifeState>();
                if (lifeState != null && !lifeState.IsAlive)
                {
                    continue;
                }

                requiredPlayers++;
                if (safePlayerIds.Contains(pair.Key))
                {
                    safePlayers++;
                }
            }

            synchronizedSafePlayers.Value = safePlayers;
            synchronizedRequiredSafePlayers.Value = requiredPlayers;
        }

        private void TickFinalShop()
        {
            if (finalShopCompleted)
            {
                synchronizedFinalShopPending.Value = false;
                SetPhase(NetworkRunPhase.Clear);
                return;
            }

            synchronizedFinalShopPending.Value = true;
            SetPhase(NetworkRunPhase.FinalShop);
            RequestSceneLoad(shopSceneName);
        }

        private void HandleGameStateChanged()
        {
            if (IsServer)
            {
                MirrorGameState();
            }
        }

        private void MirrorGameState()
        {
            if (!IsServer || gameState == null)
            {
                return;
            }

            synchronizedClearedZones.Value = gameState.ClearedZoneCount;
            synchronizedShopCycles.Value = Mathf.Clamp(
                gameState.ClearedZoneCount / GameLoopState.SHOP_INTERVAL,
                0,
                3);
        }

        private void RequestSceneLoad(string sceneName)
        {
            if (!automaticallyLoadShop || sceneLoadRequested || SceneManager.GetActiveScene().name == sceneName)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"PHS_RUN_FLOW_SCENE_LOAD_FAILED reason=scene_not_in_build scene={sceneName}", this);
                return;
            }

            sceneLoadRequested = true;
            var status = NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                sceneLoadRequested = false;
                Debug.LogError($"PHS_RUN_FLOW_SCENE_LOAD_FAILED reason={status} scene={sceneName}", this);
                return;
            }

            Debug.Log($"PHS_RUN_FLOW_SCENE_LOAD scene={sceneName}");
        }

        private void RequestMapRefresh()
        {
            if (sceneLoadRequested)
            {
                Debug.LogError("PHS_RUN_FLOW_MAP_REFRESH_FAILED reason=load_already_requested", this);
                return;
            }

            if (!TryResolveMapScene(SelectedNextMapId, out _, out var targetSceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_MAP_REFRESH_FAILED reason=scene_not_in_build scene={targetSceneName}",
                    this);
                return;
            }

            sceneLoadRequested = true;
            var status = NetworkManager.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                sceneLoadRequested = false;
                Debug.LogError(
                    $"PHS_RUN_FLOW_MAP_REFRESH_FAILED reason={status} scene={targetSceneName}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_RUN_FLOW_MAP_REFRESH_STARTED scene={targetSceneName} mapId={SelectedNextMapId}",
                this);
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            sceneLoadRequested = false;
            safePlayerIds.Clear();
            debrisPlayerIds.Clear();
            if (IsServer)
            {
                synchronizedSafePlayers.Value = 0;
                synchronizedRequiredSafePlayers.Value = 0;
                if (synchronizedPhase.Value == NetworkRunPhase.WarpArrival)
                {
                    if (!TryResolveMapScene(SelectedNextMapId, out _, out var targetSceneName))
                    {
                        return;
                    }

                    if (currentScene.name != targetSceneName)
                    {
                        Debug.LogError(
                            $"PHS_RUN_FLOW_MAP_COMMIT_FAILED reason=scene_mismatch expected={targetSceneName} actual={currentScene.name}",
                            this);
                        return;
                    }

                    if (!TryCommitActiveMap())
                    {
                        return;
                    }

                    scheduledWarpArrivalEndTime = Time.time + warpArrivalSeconds;
                    Debug.Log(
                        $"PHS_RUN_FLOW_WARP_ARRIVAL scene={currentScene.name} mapId={ActiveMapId} duration={warpArrivalSeconds:0.##}",
                        this);
                }
            }
        }

        private void HandleSynchronizedPhaseChanged(
            NetworkRunPhase previousPhase,
            NetworkRunPhase currentPhase)
        {
            PhaseChanged?.Invoke(previousPhase, currentPhase);
        }

        private void HandleSynchronizedActiveMapChanged(int previousMapId, int currentMapId)
        {
            ActiveMapChanged?.Invoke(previousMapId, currentMapId);
        }

        private bool TryCommitActiveMap()
        {
            var selectedMapId = synchronizedSelectedNextMapId.Value;
            if (selectedMapId <= 0)
            {
                Debug.LogError("PHS_RUN_FLOW_MAP_COMMIT_FAILED reason=next_map_not_selected", this);
                return false;
            }

            if (!mapCatalog.TryResolve(selectedMapId, out var profile))
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_MAP_COMMIT_FAILED reason=map_profile_missing mapId={selectedMapId}",
                    this);
                return false;
            }

            if (!profile.Selectable)
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_MAP_COMMIT_FAILED reason=map_not_selectable mapId={selectedMapId}",
                    this);
                return false;
            }

            synchronizedActiveMapId.Value = selectedMapId;
            synchronizedActiveMapRevision.Value++;
            Debug.Log($"PHS_RUN_FLOW_MAP_COMMITTED mapId={selectedMapId}", this);
            return true;
        }

        private void HandleSynchronizedActiveMapRevisionChanged(uint previousRevision, uint currentRevision)
        {
            ActiveMapCommitted?.Invoke(ActiveMapId);
        }

        private bool ValidateMapCatalog()
        {
            if (mapCatalog == null)
            {
                Debug.LogError("PHS_RUN_FLOW_SETUP_FAILED reason=map_catalog_missing", this);
                return false;
            }

            if (!mapCatalog.TryValidate(out var catalogReason))
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_SETUP_FAILED reason=map_catalog_invalid detail={catalogReason}",
                    this);
                return false;
            }

            foreach (var profile in mapCatalog.Profiles)
            {
                if (!TryResolveProfileSceneName(profile, out _))
                {
                    return false;
                }
            }

            if (initialMapId <= 0 || !mapCatalog.TryResolve(initialMapId, out _))
            {
                Debug.LogError(
                    $"PHS_RUN_FLOW_SETUP_FAILED reason=initial_map_missing mapId={initialMapId}",
                    this);
                return false;
            }

            return true;
        }

        private bool TryResolveMapScene(
            int mapId,
            out PHSMapProfileSO profile,
            out string sceneName)
        {
            profile = null;
            sceneName = null;
            if (mapId <= 0)
            {
                Debug.LogError("PHS_RUN_FLOW_MAP_SCENE_FAILED reason=map_id_missing", this);
                return false;
            }

            if (!mapCatalog.TryResolve(mapId, out profile))
            {
                Debug.LogError($"PHS_RUN_FLOW_MAP_SCENE_FAILED reason=profile_missing mapId={mapId}", this);
                return false;
            }

            return TryResolveProfileSceneName(profile, out sceneName);
        }

        private bool TryResolveProfileSceneName(PHSMapProfileSO profile, out string sceneName)
        {
            sceneName = null;
            if (profile == null)
            {
                Debug.LogError("PHS_RUN_FLOW_MAP_SCENE_FAILED reason=profile_missing", this);
                return false;
            }

            switch (profile.SceneMode)
            {
                case PHSMapSceneMode.SharedSceneEnvironment:
                    if (!string.Equals(profile.SceneName, mapSceneName, StringComparison.Ordinal))
                    {
                        Debug.LogError(
                            $"PHS_RUN_FLOW_MAP_SCENE_FAILED reason=shared_scene_mismatch mapId={profile.MapId} profile={profile.SceneName} configured={mapSceneName}",
                            this);
                        return false;
                    }

                    sceneName = mapSceneName;
                    return true;
                case PHSMapSceneMode.SeparateScene:
                    if (string.IsNullOrWhiteSpace(profile.SceneName))
                    {
                        Debug.LogError(
                            $"PHS_RUN_FLOW_MAP_SCENE_FAILED reason=separate_scene_missing mapId={profile.MapId}",
                            this);
                        return false;
                    }

                    sceneName = profile.SceneName;
                    return true;
                default:
                    Debug.LogError(
                        $"PHS_RUN_FLOW_MAP_SCENE_FAILED reason=scene_mode_invalid mapId={profile.MapId} mode={(int)profile.SceneMode}",
                        this);
                    return false;
            }
        }

        private void SetPhase(NetworkRunPhase phase)
        {
            if (synchronizedPhase.Value == phase)
            {
                return;
            }

            synchronizedPhase.Value = phase;
            Debug.Log($"PHS_RUN_FLOW_PHASE phase={phase} zones={synchronizedClearedZones.Value} cycles={synchronizedShopCycles.Value}");
        }
    }
}
