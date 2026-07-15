using System.Collections.Generic;
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
        [SerializeField, Min(0f)] private float rearmSeconds = 8f;
        [SerializeField] private string mapSceneName = "PHS_Map_ver1";
        [SerializeField] private string shopSceneName = "PHS_ExteriorShopScene";
        [SerializeField] private bool automaticallyLoadShop;

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
        private readonly NetworkVariable<int> synchronizedSafePlayers = new(
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
        private float scheduledWarpReviveTime = -1f;

        public static NetworkRunFlowCoordinator Instance { get; private set; }

        public NetworkRunPhase Phase => synchronizedPhase.Value;
        public float WarpChargeNormalized => synchronizedWarpCharge.Value;
        public int ClearedZoneCount => synchronizedClearedZones.Value;
        public int CompletedShopCycleCount => synchronizedShopCycles.Value;
        public int SafePlayerCount => synchronizedSafePlayers.Value;
        public bool IsFinalShopPending => synchronizedFinalShopPending.Value;

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
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (IsServer)
            {
                TryBindGameFlow();
            }
        }

        public override void OnNetworkDespawn()
        {
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
            synchronizedSafePlayers.Value = safePlayerIds.Count;
        }

        public bool TryActivateWarp(ulong activatorClientId, out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
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

            ExecuteReadyWarp();
            reason = null;
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
            synchronizedSafePlayers.Value = safePlayerIds.Count;
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
            if (SceneManager.GetActiveScene().name != mapSceneName)
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
            var nextZoneId = Mathf.Clamp(gameState.ClearedZoneCount + 1, 1, GameLoopState.TOTAL_ZONES);
            gameCommands.SelectZone(nextZoneId);
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
            Debug.Log($"PHS_RUN_FLOW_WARP_COMPLETED cleared={gameState.ClearedZoneCount} phase={gameState.Phase}");
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

        private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            sceneLoadRequested = false;
            safePlayerIds.Clear();
            debrisPlayerIds.Clear();
            if (IsServer)
            {
                synchronizedSafePlayers.Value = 0;
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
