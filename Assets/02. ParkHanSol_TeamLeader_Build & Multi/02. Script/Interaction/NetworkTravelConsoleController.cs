using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public enum TravelConsoleDestination : byte
    {
        None,
        DebrisCollection,
        Shop
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTravelConsoleController : NetworkBehaviour
    {
        [Header("Destination Scenes")]
        [SerializeField] private string debrisSceneName = "PHS_DebrisCollectionScene";
        [SerializeField] private string shopSceneName = "PHS_ExteriorShopScene";
        [SerializeField, Min(1f)] private float serverInteractionDistance = 4f;

        [Header("World Screens")]
        [SerializeField] private TMP_Text debrisScreenText;
        [SerializeField] private TMP_Text shopScreenText;
        [SerializeField] private TMP_Text actionScreenText;

        [Header("Button Renderers")]
        [SerializeField] private Renderer debrisButtonRenderer;
        [SerializeField] private Renderer shopButtonRenderer;
        [SerializeField] private Renderer actionButtonRenderer;

        [Header("Button Materials")]
        [SerializeField] private Material idleButtonMaterial;
        [SerializeField] private Material debrisSelectedMaterial;
        [SerializeField] private Material shopSelectedMaterial;
        [SerializeField] private Material actionReadyMaterial;
        [SerializeField] private Material disabledButtonMaterial;

        private readonly NetworkVariable<TravelConsoleDestination> synchronizedDestination = new(
            TravelConsoleDestination.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private bool setupValid;
        private bool sceneLoadRequested;

        public TravelConsoleDestination SelectedDestination => synchronizedDestination.Value;

        public string ActionPrompt
        {
            get
            {
                var runFlow = NetworkRunFlowCoordinator.Instance;
                return runFlow != null && runFlow.Phase == NetworkRunPhase.WarpReady
                    ? "워프 기동"
                    : "선택 목적지로 이동";
            }
        }

        private void Awake()
        {
            setupValid = debrisScreenText != null
                && shopScreenText != null
                && actionScreenText != null
                && debrisButtonRenderer != null
                && shopButtonRenderer != null
                && actionButtonRenderer != null
                && idleButtonMaterial != null
                && debrisSelectedMaterial != null
                && shopSelectedMaterial != null
                && actionReadyMaterial != null
                && disabledButtonMaterial != null
                && !string.IsNullOrWhiteSpace(debrisSceneName)
                && !string.IsNullOrWhiteSpace(shopSceneName);
            if (!setupValid)
            {
                Debug.LogError($"PHS_TRAVEL_CONSOLE_SETUP_FAILED console={name}", this);
                enabled = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            synchronizedDestination.OnValueChanged += HandleDestinationChanged;
            RefreshPresentation();
        }

        public override void OnNetworkDespawn()
        {
            synchronizedDestination.OnValueChanged -= HandleDestinationChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            RefreshPresentation();
        }

        public bool CanSelectDestination(TravelConsoleDestination destination)
        {
            if (!setupValid || !IsSpawned || destination == TravelConsoleDestination.None)
            {
                return false;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (destination == TravelConsoleDestination.DebrisCollection)
            {
                return runFlow != null
                    && runFlow.Phase != NetworkRunPhase.Shop
                    && runFlow.Phase != NetworkRunPhase.FinalShop
                    && runFlow.Phase != NetworkRunPhase.Clear
                    && runFlow.Phase != NetworkRunPhase.GameOver;
            }

            return runFlow != null
                && (runFlow.Phase == NetworkRunPhase.Shop
                    || runFlow.Phase == NetworkRunPhase.FinalShop);
        }

        public void RequestSelectDestination(IItemHolder itemHolder, TravelConsoleDestination destination)
        {
            if (!CanSelectDestination(destination)
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() is not { } player)
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=destination_locked destination={destination}", this);
                return;
            }

            if (IsServer)
            {
                if (!TryGetNearbyPlayer(player.OwnerClientId, out _))
                {
                    Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=player_out_of_range clientId={player.OwnerClientId}", this);
                    return;
                }

                SetDestination(destination);
                return;
            }

            RequestSelectDestinationServerRpc(destination);
        }

        public bool CanExecute(IItemHolder itemHolder)
        {
            if (!setupValid || !IsSpawned || SelectedDestination == TravelConsoleDestination.None
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() == null)
            {
                return false;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null)
            {
                return false;
            }

            if (runFlow.Phase == NetworkRunPhase.WarpReady)
            {
                return true;
            }

            if (runFlow.Phase == NetworkRunPhase.Charging)
            {
                return SelectedDestination == TravelConsoleDestination.DebrisCollection;
            }

            if (runFlow.Phase == NetworkRunPhase.Shop || runFlow.Phase == NetworkRunPhase.FinalShop)
            {
                return SelectedDestination == TravelConsoleDestination.Shop;
            }

            return false;
        }

        public void Execute(IItemHolder itemHolder)
        {
            if (!CanExecute(itemHolder))
            {
                Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason=selection_or_phase_invalid destination={SelectedDestination}", this);
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            if (IsServer)
            {
                ExecuteOnServer(player.OwnerClientId);
                return;
            }

            RequestExecuteServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSelectDestinationServerRpc(
            TravelConsoleDestination destination,
            ServerRpcParams rpcParams = default)
        {
            if (!TryGetNearbyPlayer(rpcParams.Receive.SenderClientId, out _))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=player_out_of_range clientId={rpcParams.Receive.SenderClientId}", this);
                return;
            }

            if (!CanSelectDestination(destination))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=destination_locked destination={destination}", this);
                return;
            }

            SetDestination(destination);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestExecuteServerRpc(ServerRpcParams rpcParams = default)
        {
            ExecuteOnServer(rpcParams.Receive.SenderClientId);
        }

        private void ExecuteOnServer(ulong clientId)
        {
            if (!IsServer || sceneLoadRequested)
            {
                return;
            }

            if (!TryGetNearbyPlayer(clientId, out _))
            {
                Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason=player_out_of_range clientId={clientId}", this);
                return;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null)
            {
                Debug.LogError("PHS_TRAVEL_EXECUTE_FAILED reason=run_flow_missing", this);
                return;
            }

            if (runFlow.Phase == NetworkRunPhase.WarpReady)
            {
                if (!runFlow.TryActivateWarp(clientId, out var warpReason))
                {
                    Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason={warpReason}", this);
                }
                return;
            }

            if (runFlow.Phase == NetworkRunPhase.Charging
                && SelectedDestination == TravelConsoleDestination.DebrisCollection)
            {
                LoadNetworkScene(debrisSceneName);
                return;
            }

            if ((runFlow.Phase == NetworkRunPhase.Shop || runFlow.Phase == NetworkRunPhase.FinalShop)
                && SelectedDestination == TravelConsoleDestination.Shop)
            {
                var adapter = FindAnyObjectByType<ShopRunFlowAdapter>(FindObjectsInactive.Include);
                var shopReason = "shop_adapter_missing";
                if (adapter == null || !adapter.CanEnterShop(out shopReason))
                {
                    Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason={shopReason}", this);
                    return;
                }

                LoadNetworkScene(shopSceneName);
                return;
            }

            Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason=phase_or_destination_invalid phase={runFlow.Phase} destination={SelectedDestination}", this);
        }

        private void LoadNetworkScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"PHS_TRAVEL_EXECUTE_FAILED reason=scene_not_in_build scene={sceneName}", this);
                return;
            }

            sceneLoadRequested = true;
            var status = NetworkManager.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                sceneLoadRequested = false;
                Debug.LogError($"PHS_TRAVEL_EXECUTE_FAILED reason={status} scene={sceneName}", this);
                return;
            }

            Debug.Log($"PHS_TRAVEL_SCENE_LOAD scene={sceneName} destination={SelectedDestination}");
        }

        private bool TryGetNearbyPlayer(ulong clientId, out NetworkObject playerObject)
        {
            playerObject = null;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                || client.PlayerObject == null)
            {
                return false;
            }

            playerObject = client.PlayerObject;
            return Vector3.Distance(playerObject.transform.position, transform.position) <= serverInteractionDistance;
        }

        private void SetDestination(TravelConsoleDestination destination)
        {
            synchronizedDestination.Value = destination;
            RefreshPresentation();
            Debug.Log($"PHS_TRAVEL_DESTINATION_SELECTED destination={destination}");
        }

        private void HandleDestinationChanged(
            TravelConsoleDestination previous,
            TravelConsoleDestination current)
        {
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (!setupValid)
            {
                return;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            var shopAvailable = runFlow != null
                && (runFlow.Phase == NetworkRunPhase.Shop
                    || runFlow.Phase == NetworkRunPhase.FinalShop);

            debrisScreenText.text = SelectedDestination == TravelConsoleDestination.DebrisCollection
                ? "데브리 회수존\n선택됨"
                : "데브리 회수존\n선택 가능";
            shopScreenText.text = shopAvailable
                ? SelectedDestination == TravelConsoleDestination.Shop
                    ? "상점\n선택됨"
                    : "상점\n선택 가능"
                : "상점\n3번째 워프 후 활성";

            debrisButtonRenderer.sharedMaterial = SelectedDestination == TravelConsoleDestination.DebrisCollection
                ? debrisSelectedMaterial
                : idleButtonMaterial;
            shopButtonRenderer.sharedMaterial = !shopAvailable
                ? disabledButtonMaterial
                : SelectedDestination == TravelConsoleDestination.Shop
                    ? shopSelectedMaterial
                    : idleButtonMaterial;

            var canExecute = runFlow != null
                && SelectedDestination != TravelConsoleDestination.None
                && (runFlow.Phase == NetworkRunPhase.WarpReady
                    || runFlow.Phase == NetworkRunPhase.Charging
                        && SelectedDestination == TravelConsoleDestination.DebrisCollection
                    || (runFlow.Phase == NetworkRunPhase.Shop || runFlow.Phase == NetworkRunPhase.FinalShop)
                        && SelectedDestination == TravelConsoleDestination.Shop);
            actionButtonRenderer.sharedMaterial = canExecute
                ? actionReadyMaterial
                : disabledButtonMaterial;

            if (runFlow == null)
            {
                actionScreenText.text = "이동 시스템\n오프라인";
            }
            else if (SelectedDestination == TravelConsoleDestination.None)
            {
                actionScreenText.text = "목적지를 먼저\n선택하십시오";
            }
            else if (runFlow.Phase == NetworkRunPhase.WarpReady)
            {
                actionScreenText.text = "워프 준비 완료\n실행 버튼 입력";
            }
            else if (canExecute)
            {
                actionScreenText.text = $"{GetDestinationLabel(SelectedDestination)}\n실행 버튼 입력";
            }
            else
            {
                actionScreenText.text = $"워프 충전\n{Mathf.RoundToInt(runFlow.WarpChargeNormalized * 100f)}%";
            }
        }

        private static string GetDestinationLabel(TravelConsoleDestination destination)
        {
            return destination == TravelConsoleDestination.Shop ? "상점 이동" : "데브리 회수존 이동";
        }
    }
}
