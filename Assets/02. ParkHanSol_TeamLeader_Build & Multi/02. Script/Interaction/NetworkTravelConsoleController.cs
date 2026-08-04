using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public enum TravelConsoleDestination : byte
    {
        None = 0,
        DebrisCollection = 1,
        Shop = 2,
        LeftMap = 3,
        RightMap = 4
    }

    public enum TravelConsoleSide : byte
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkTravelConsoleController : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float serverInteractionDistance = 4f;

        [Header("Map Options")]
        [SerializeField] private PHSMapCatalogSO mapCatalog;
        [SerializeField] private PHSMapRuntimeContext mapRuntimeContext;

        [Header("World Screens")]
        [SerializeField] private TMP_Text debrisScreenText;
        [SerializeField] private TMP_Text shopScreenText;
        [SerializeField] private TMP_Text actionScreenText;
        [SerializeField] private Light readyStatusLight;

        [Header("Shop-only Presentation")]
        [SerializeField] private GameObject[] debrisChoiceObjects;

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
        private readonly NetworkVariable<int> synchronizedLeftMapId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> synchronizedRightMapId = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<PHSMapProfileSO> selectableProfiles = new();
        private bool setupValid;
        private NetworkRunPhase lastServerPhase = (NetworkRunPhase)byte.MaxValue;

        public TravelConsoleDestination SelectedDestination => synchronizedDestination.Value;
        internal int SelectableMapCount => selectableProfiles.Count;

        internal bool TryGetSelectableMapIdAt(int index, out int mapId)
        {
            if (index < 0 || index >= selectableProfiles.Count)
            {
                mapId = 0;
                return false;
            }

            mapId = selectableProfiles[index].MapId;
            return mapId > 0;
        }

        public bool TryGetCurrentMapChoices(out int leftZoneId, out int rightZoneId)
        {
            leftZoneId = 0;
            rightZoneId = 0;
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null || runFlow.Phase != NetworkRunPhase.WarpSafe || !AreMapChoicesReady())
            {
                return false;
            }

            leftZoneId = synchronizedLeftMapId.Value;
            rightZoneId = synchronizedRightMapId.Value;
            if (!mapCatalog.TryResolve(leftZoneId, out _)
                || !mapCatalog.TryResolve(rightZoneId, out _))
            {
                return false;
            }

            return leftZoneId > 0 && rightZoneId > 0 && leftZoneId != rightZoneId;
        }

        public string ActionPrompt
        {
            get
            {
                var runFlow = NetworkRunFlowCoordinator.Instance;
                if (runFlow == null)
                {
                    return "이동 시스템 오프라인";
                }

                return runFlow.Phase == NetworkRunPhase.WarpReady
                    ? "안전 구역 진입"
                    : runFlow.Phase == NetworkRunPhase.WarpSafe
                        ? "플레이 공간 워프"
                        : "선택 목적지로 이동";
            }
        }

        private void Awake()
        {
            setupValid = debrisScreenText != null
                && mapRuntimeContext != null
                && shopScreenText != null
                && actionScreenText != null
                && readyStatusLight != null
                && debrisButtonRenderer != null
                && shopButtonRenderer != null
                && actionButtonRenderer != null
                && idleButtonMaterial != null
                && debrisSelectedMaterial != null
                && shopSelectedMaterial != null
                && actionReadyMaterial != null
                && disabledButtonMaterial != null
                && HasValidObjects(debrisChoiceObjects)
                && TryBuildSelectableProfiles();
            if (!setupValid)
            {
                Debug.LogError($"PHS_TRAVEL_CONSOLE_SETUP_FAILED console={name}", this);
                enabled = false;
                return;
            }

            readyStatusLight.enabled = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            synchronizedDestination.OnValueChanged += HandleDestinationChanged;
            synchronizedLeftMapId.OnValueChanged += HandleMapOptionChanged;
            synchronizedRightMapId.OnValueChanged += HandleMapOptionChanged;
            RefreshPresentation();
        }

        public override void OnNetworkDespawn()
        {
            synchronizedDestination.OnValueChanged -= HandleDestinationChanged;
            synchronizedLeftMapId.OnValueChanged -= HandleMapOptionChanged;
            synchronizedRightMapId.OnValueChanged -= HandleMapOptionChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (IsSpawned && IsServer && runFlow != null && runFlow.Phase != lastServerPhase)
            {
                HandleServerPhaseChanged(runFlow.Phase);
            }

            RefreshPresentation();
        }

        public bool CanSelectSide(TravelConsoleSide side)
        {
            return setupValid
                && IsSpawned
                && side != TravelConsoleSide.None
                && TryResolveDestination(side, out _);
        }

        public void RequestSelectSide(IItemHolder itemHolder, TravelConsoleSide side)
        {
            if (!CanSelectSide(side)
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() is not { } player)
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=side_locked side={side}", this);
                return;
            }

            if (IsServer)
            {
                if (!TryGetNearbyPlayer(player.OwnerClientId, out _))
                {
                    Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=player_out_of_range clientId={player.OwnerClientId}", this);
                    return;
                }

                SelectSideOnServer(side);
                return;
            }

            RequestSelectSideServerRpc(side);
        }

        public bool CanExecute(IItemHolder itemHolder)
        {
            if (!setupValid || !IsSpawned
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() == null)
            {
                return false;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            return runFlow != null && IsDestinationExecutable(runFlow);
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
        private void RequestSelectSideServerRpc(
            TravelConsoleSide side,
            ServerRpcParams rpcParams = default)
        {
            if (!TryGetNearbyPlayer(rpcParams.Receive.SenderClientId, out _))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=player_out_of_range clientId={rpcParams.Receive.SenderClientId}", this);
                return;
            }

            if (!CanSelectSide(side))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=side_locked side={side}", this);
                return;
            }

            SelectSideOnServer(side);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestExecuteServerRpc(ServerRpcParams rpcParams = default)
        {
            ExecuteOnServer(rpcParams.Receive.SenderClientId);
        }

        private void SelectSideOnServer(TravelConsoleSide side)
        {
            if (!TryResolveDestination(side, out var destination))
            {
                Debug.LogWarning($"PHS_TRAVEL_SELECT_FAILED reason=destination_unavailable side={side}", this);
                return;
            }

            SetDestination(destination);
        }

        private bool TryResolveDestination(
            TravelConsoleSide side,
            out TravelConsoleDestination destination)
        {
            destination = TravelConsoleDestination.None;
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null)
            {
                return false;
            }

            if (runFlow.Phase == NetworkRunPhase.WarpSafe && AreMapChoicesReady())
            {
                destination = side == TravelConsoleSide.Left
                    ? TravelConsoleDestination.LeftMap
                    : TravelConsoleDestination.RightMap;
                return true;
            }

            return false;
        }

        private bool IsDestinationExecutable(NetworkRunFlowCoordinator runFlow)
        {
            if (runFlow.Phase == NetworkRunPhase.WarpReady)
            {
                return true;
            }

            if (runFlow.Phase == NetworkRunPhase.WarpSafe)
            {
                return (SelectedDestination == TravelConsoleDestination.LeftMap
                        || SelectedDestination == TravelConsoleDestination.RightMap)
                    && TryGetSelectedMapProfile(out _);
            }

            return false;
        }

        private void ExecuteOnServer(ulong clientId)
        {
            if (!IsServer)
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
                if (!runFlow.TryActivateWarp(clientId, out var warpSafeReason))
                {
                    Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason={warpSafeReason}", this);
                }
                return;
            }

            if (runFlow.Phase == NetworkRunPhase.WarpSafe)
            {
                if (!TryGetSelectedMapProfile(out var mapProfile))
                {
                    Debug.LogWarning("PHS_TRAVEL_EXECUTE_FAILED reason=map_choice_missing", this);
                    return;
                }

                if (!runFlow.TrySelectNextZone(mapProfile.MapId, out var selectionReason))
                {
                    Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason={selectionReason}", this);
                    return;
                }

                if (!runFlow.TryActivateWarp(clientId, out var warpReason))
                {
                    Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason={warpReason}", this);
                }
                return;
            }

            Debug.LogWarning($"PHS_TRAVEL_EXECUTE_FAILED reason=phase_or_destination_invalid phase={runFlow.Phase} destination={SelectedDestination}", this);
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
            return Vector3.Distance(playerObject.transform.position, transform.position)
                <= serverInteractionDistance;
        }

        private void HandleServerPhaseChanged(NetworkRunPhase phase)
        {
            lastServerPhase = phase;
            if (phase == NetworkRunPhase.Shop || phase == NetworkRunPhase.FinalShop)
            {
                synchronizedDestination.Value = TravelConsoleDestination.None;
                synchronizedLeftMapId.Value = 0;
                synchronizedRightMapId.Value = 0;
            }
            else if (phase == NetworkRunPhase.WarpSafe)
            {
                synchronizedDestination.Value = TravelConsoleDestination.None;
                RollMapChoices();
            }
            else
            {
                synchronizedDestination.Value = TravelConsoleDestination.None;
                synchronizedLeftMapId.Value = 0;
                synchronizedRightMapId.Value = 0;
            }

            RefreshPresentation();
        }

        private void RollMapChoices()
        {
            synchronizedLeftMapId.Value = 0;
            synchronizedRightMapId.Value = 0;
            if (selectableProfiles.Count < 2)
            {
                Debug.LogError("PHS_TRAVEL_MAP_ROLL_FAILED reason=selectable_profiles_insufficient", this);
                return;
            }

            var runSessionRoot = NetworkRunSessionRoot.Instance;
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runSessionRoot == null
                || runSessionRoot.Rng == null
                || runFlow == null)
            {
                Debug.LogError(
                    "PHS_TRAVEL_MAP_ROLL_FAILED reason=run_random_ledger_missing",
                    this);
                return;
            }

            var scopeKey = (ulong)(runFlow.ClearedZoneCount + 1);
            if (!runSessionRoot.Rng.TryCreateServerScope(
                    NetworkRunRandomStream.MapChoice,
                    scopeKey,
                    out var random,
                    out var reason))
            {
                Debug.LogError(
                    $"PHS_TRAVEL_MAP_ROLL_FAILED reason={reason} scope={scopeKey}",
                    this);
                return;
            }

            var leftIndex = random.NextInt(0, selectableProfiles.Count);
            var rightIndex = random.NextInt(0, selectableProfiles.Count - 1);
            if (rightIndex >= leftIndex)
            {
                rightIndex++;
            }

            var leftProfile = selectableProfiles[leftIndex];
            var rightProfile = selectableProfiles[rightIndex];
            synchronizedLeftMapId.Value = leftProfile.MapId;
            synchronizedRightMapId.Value = rightProfile.MapId;
            Debug.Log(
                $"PHS_TRAVEL_MAP_CHOICES_ROLLED left={leftProfile.MapId} right={rightProfile.MapId} " +
                $"seed={runSessionRoot.Rng.Snapshot.RunSeed} scope={scopeKey}",
                this);
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

        private void HandleMapOptionChanged(int previous, int current)
        {
            RefreshPresentation();
        }

        private bool AreMapChoicesReady()
        {
            var leftMapId = synchronizedLeftMapId.Value;
            var rightMapId = synchronizedRightMapId.Value;
            return leftMapId > 0
                && rightMapId > 0
                && leftMapId != rightMapId
                && mapCatalog.TryResolve(leftMapId, out var leftProfile)
                && leftProfile.Selectable
                && mapCatalog.TryResolve(rightMapId, out var rightProfile)
                && rightProfile.Selectable;
        }

        private bool IsShopPortalAvailable(NetworkRunFlowCoordinator runFlow)
        {
            return runFlow != null
                && (runFlow.Phase == NetworkRunPhase.Shop || runFlow.Phase == NetworkRunPhase.FinalShop)
                && mapRuntimeContext != null
                && mapRuntimeContext.CurrentProfile != null
                && mapRuntimeContext.CurrentProfile.AllowsShopPortal;
        }

        private bool TryGetSelectedMapProfile(out PHSMapProfileSO mapProfile)
        {
            mapProfile = null;
            var mapId = SelectedDestination switch
            {
                TravelConsoleDestination.LeftMap => synchronizedLeftMapId.Value,
                TravelConsoleDestination.RightMap => synchronizedRightMapId.Value,
                _ => 0
            };

            if (mapId <= 0 || !mapCatalog.TryResolve(mapId, out mapProfile))
            {
                return false;
            }

            return mapProfile.Selectable;
        }

        private void RefreshPresentation()
        {
            if (!setupValid)
            {
                return;
            }

            var runFlow = NetworkRunFlowCoordinator.Instance;
            var shopAvailable = IsShopPortalAvailable(runFlow);
            var mapChoicesReady = runFlow != null
                && runFlow.Phase == NetworkRunPhase.WarpSafe
                && AreMapChoicesReady();

            SetObjectsActive(debrisChoiceObjects, !shopAvailable);
            readyStatusLight.enabled = runFlow != null
                && (runFlow.Phase == NetworkRunPhase.WarpReady || mapChoicesReady || shopAvailable);

            if (shopAvailable)
            {
                shopScreenText.text = "상점";
            }
            else if (mapChoicesReady)
            {
                shopScreenText.text = FormatMapOption(
                    ResolveProfileOrLog(synchronizedLeftMapId.Value),
                    SelectedDestination == TravelConsoleDestination.LeftMap);
                debrisScreenText.text = FormatMapOption(
                    ResolveProfileOrLog(synchronizedRightMapId.Value),
                    SelectedDestination == TravelConsoleDestination.RightMap);
            }
            else
            {
                shopScreenText.text = "워프 충전 중\n선택 대기";
                debrisScreenText.text = runFlow != null && runFlow.Phase == NetworkRunPhase.Charging
                    ? "데브리 회수는\n포탈 이용"
                    : "다음 워프\n준비 중";
            }

            shopButtonRenderer.sharedMaterial = GetLeftButtonMaterial(
                runFlow,
                shopAvailable,
                mapChoicesReady);
            debrisButtonRenderer.sharedMaterial = GetRightButtonMaterial(
                runFlow,
                shopAvailable,
                mapChoicesReady);

            var canExecute = runFlow != null && IsDestinationExecutable(runFlow);
            actionButtonRenderer.sharedMaterial = canExecute
                ? actionReadyMaterial
                : disabledButtonMaterial;

            if (runFlow == null)
            {
                actionScreenText.text = "이동 시스템\n오프라인";
            }
            else if (runFlow.Phase == NetworkRunPhase.WarpReady)
            {
                actionScreenText.text = "안전 구역으로 이동";
            }
            else if (canExecute)
            {
                actionScreenText.text = $"{GetDestinationLabel()}\n실행 버튼 입력";
            }
            else if (mapChoicesReady)
            {
                actionScreenText.text = "왼쪽/오른쪽\n맵 선택";
            }
            else if (shopAvailable)
            {
                actionScreenText.text = "상점 선택 후\n실행";
            }
            else
            {
                actionScreenText.text = "워프 충전 중\n선택 대기";
            }
        }

        private Material GetLeftButtonMaterial(
            NetworkRunFlowCoordinator runFlow,
            bool shopAvailable,
            bool mapChoicesReady)
        {
            if (shopAvailable)
            {
                return SelectedDestination == TravelConsoleDestination.Shop
                    ? shopSelectedMaterial
                    : idleButtonMaterial;
            }

            if (mapChoicesReady)
            {
                return SelectedDestination == TravelConsoleDestination.LeftMap
                    ? shopSelectedMaterial
                    : idleButtonMaterial;
            }

            return disabledButtonMaterial;
        }

        private Material GetRightButtonMaterial(
            NetworkRunFlowCoordinator runFlow,
            bool shopAvailable,
            bool mapChoicesReady)
        {
            if (shopAvailable)
            {
                return disabledButtonMaterial;
            }

            if (mapChoicesReady)
            {
                return SelectedDestination == TravelConsoleDestination.RightMap
                    ? debrisSelectedMaterial
                    : idleButtonMaterial;
            }

            return disabledButtonMaterial;
        }

        private string GetDestinationLabel()
        {
            if (SelectedDestination == TravelConsoleDestination.Shop)
            {
                return "상점";
            }

            if (SelectedDestination == TravelConsoleDestination.DebrisCollection)
            {
                return "데브리 회수존";
            }

            return TryGetSelectedMapProfile(out var mapProfile)
                ? mapProfile.DisplayName
                : "선택 목적지";
        }

        private static string FormatMapOption(PHSMapProfileSO mapProfile, bool selected)
        {
            if (mapProfile == null)
            {
                return "맵 데이터 오류";
            }

            var selectionText = selected
                ? "선택됨"
                : $"구역 {mapProfile.MapId}";
            return $"{mapProfile.DisplayName}\n" +
                $"잔해량 {mapProfile.DebrisAmountLabel} · " +
                $"난이도 {mapProfile.DifficultyLabel}\n" +
                selectionText;
        }

        private bool TryBuildSelectableProfiles()
        {
            selectableProfiles.Clear();
            if (mapCatalog == null || mapCatalog.Profiles == null)
            {
                Debug.LogError("PHS_TRAVEL_CONSOLE_SETUP_FAILED reason=map_catalog_missing", this);
                return false;
            }

            if (!mapCatalog.TryValidate(out var catalogReason))
            {
                Debug.LogError(
                    $"PHS_TRAVEL_CONSOLE_SETUP_FAILED reason=map_catalog_invalid detail={catalogReason}",
                    this);
                return false;
            }

            var ids = new HashSet<int>();
            foreach (var profile in mapCatalog.Profiles)
            {
                if (profile == null || profile.MapId <= 0 || !ids.Add(profile.MapId))
                {
                    Debug.LogError("PHS_TRAVEL_CONSOLE_SETUP_FAILED reason=map_catalog_invalid", this);
                    return false;
                }

                if (profile.Selectable)
                {
                    selectableProfiles.Add(profile);
                }
            }

            selectableProfiles.Sort((left, right) => left.MapId.CompareTo(right.MapId));
            if (selectableProfiles.Count < 2)
            {
                Debug.LogError("PHS_TRAVEL_CONSOLE_SETUP_FAILED reason=selectable_profiles_insufficient", this);
                return false;
            }

            return true;
        }

        private PHSMapProfileSO ResolveProfileOrLog(int mapId)
        {
            if (mapCatalog.TryResolve(mapId, out var profile))
            {
                return profile;
            }

            Debug.LogError($"PHS_TRAVEL_PRESENTATION_FAILED reason=map_profile_missing mapId={mapId}", this);
            return null;
        }

        private static bool HasValidObjects(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                return false;
            }

            foreach (var target in objects)
            {
                if (target == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetObjectsActive(GameObject[] objects, bool isActive)
        {
            foreach (var target in objects)
            {
                if (target.activeSelf != isActive)
                {
                    target.SetActive(isActive);
                }
            }
        }
    }
}
