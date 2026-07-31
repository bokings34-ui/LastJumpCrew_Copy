using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using LastJumpCrew.ParkHanSol.Shop;
using LastJumpCrew.SeoBoGyeong;
using SM;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class P0RuntimeValidationDriver : NetworkBehaviour
    {
        private const string ScenarioFlag = "-phsAutoP0Scenario";
        private const string ItemScenarioFlag = "-phsAutoItemScenario";
        private const string InputOnlyScenarioFlag = "-phsInputOnlyScenario";
        private const string MapSceneName = "PHS_Map_ver1";
        private const string ShopSceneName = "PHS_ExteriorShopScene";
        private const string LocalDebrisEntryPortalName = "PHS_ExteriorDoorTrigger_Left";
        private const string LocalDebrisAlternateEntryPortalName = "PHS_ExteriorDoorTrigger_Right";
        private const string LocalDebrisReturnPortalName = "PHS_ExteriorDebrisReturnPortal";
        private const float DefaultStepTimeout = 90f;

        [Header("P2 Runtime Validation")]
        [SerializeField] private UtilityItemPrefabData validationBatteryItem;
        [SerializeField] private UtilityItemPrefabData validationThrownItem;

        private readonly Dictionary<ulong, string> sceneReports = new();
        private readonly Dictionary<ulong, GaugeReport> gaugeReports = new();
        private readonly Dictionary<ulong, MapChoiceReport> mapChoiceReports = new();
        private readonly Dictionary<ulong, ShopStateReport> shopStateReports = new();
        private readonly Dictionary<ulong, RunFlowReport> runFlowReports = new();
        private readonly Dictionary<ulong, StageClockReport> stageClockReports = new();
        private readonly Dictionary<ulong, EconomyReport> economyReports = new();
        private readonly Dictionary<ulong, IncidentReport> incidentReports = new();
        private readonly Dictionary<ulong, DebrisSaleStateReport> debrisSaleStateReports = new();
        private readonly Dictionary<ulong, EventSnapshotReport> eventSnapshotReports = new();
        private readonly Dictionary<ulong, EventTerminalReport> eventTerminalReports = new();
        private readonly Dictionary<ulong, ShipPowerReport> shipPowerReports = new();
        private readonly Dictionary<ulong, int> oxygenHealthReports = new();
        private readonly Dictionary<ulong, bool> thrownItemReports = new();
        private readonly Dictionary<ulong, bool> remoteHeldItemReports = new();
        private readonly HashSet<ulong> eventObservationReadyClients = new();

        private uint activeProbeToken;
        private int expectedClientCount;
        private ulong activeRemoteItemClientId = ulong.MaxValue;
        private bool remoteItemRequestReported;
        private bool remoteItemRequestIssued;
        private bool remoteThrowRequestReported;
        private bool remoteThrowRequestIssued;
        private bool remotePrimaryUseRequestReported;
        private bool remotePrimaryUseRequestIssued;
        private bool remoteItemPositionReported;
        private Vector3 remoteItemPosition;
        private bool farSelectProbeReported;
        private bool farSelectRequestIssued;
        private bool localPortalProbeReported;
        private bool localPortalRequestIssued;
        private bool farEventTerminalProbeReported;
        private bool farEventTerminalRequestIssued;
        private bool scenarioRunning;
        private bool scenarioFinished;
        private ulong observedTerminalInstanceId;
        private bool observedTerminalState;
        private bool observedTerminalRemoved;
        private uint observedTerminalRevision;
        private ulong activeObservedInstanceId;
        private NetworkEventCoordinator observedEventCoordinator;
        private uint initialStageClockSequence;
        private uint eventRepairRequestSequence;
        private int validatedIncidentCommandCount;
        private uint validatedIncidentRevision;
        private int previousMiniGameConsequenceContentId;

        private readonly struct GaugeReport
        {
            public GaugeReport(float value, NetworkRunPhase phase)
            {
                Value = value;
                Phase = phase;
            }

            public float Value { get; }
            public NetworkRunPhase Phase { get; }
        }

        private readonly struct MapChoiceReport
        {
            public MapChoiceReport(
                int leftZoneId,
                int rightZoneId,
                bool ready,
                bool randomLedgerFound,
                ulong runSeed,
                uint algorithmVersion,
                uint randomRevision)
            {
                LeftZoneId = leftZoneId;
                RightZoneId = rightZoneId;
                Ready = ready;
                RandomLedgerFound = randomLedgerFound;
                RunSeed = runSeed;
                AlgorithmVersion = algorithmVersion;
                RandomRevision = randomRevision;
            }

            public int LeftZoneId { get; }
            public int RightZoneId { get; }
            public bool Ready { get; }
            public bool RandomLedgerFound { get; }
            public ulong RunSeed { get; }
            public uint AlgorithmVersion { get; }
            public uint RandomRevision { get; }
        }

        private readonly struct ShopStateReport
        {
            public ShopStateReport(string offerSignature, int displayedCount, NetworkPlayerGravityMode gravityMode)
            {
                OfferSignature = offerSignature;
                DisplayedCount = displayedCount;
                GravityMode = gravityMode;
            }

            public string OfferSignature { get; }
            public int DisplayedCount { get; }
            public NetworkPlayerGravityMode GravityMode { get; }
        }

        private readonly struct RunFlowReport
        {
            public RunFlowReport(
                NetworkRunPhase phase,
                int clearedZoneCount,
                int completedShopCycleCount,
                bool finalShopPending)
            {
                Phase = phase;
                ClearedZoneCount = clearedZoneCount;
                CompletedShopCycleCount = completedShopCycleCount;
                FinalShopPending = finalShopPending;
            }

            public NetworkRunPhase Phase { get; }
            public int ClearedZoneCount { get; }
            public int CompletedShopCycleCount { get; }
            public bool FinalShopPending { get; }
        }

        private readonly struct StageClockReport
        {
            public StageClockReport(
                bool found,
                int mapId,
                uint stageSequence,
                uint revision,
                NetworkRunStageClockState state,
                float remainingSeconds)
            {
                Found = found;
                MapId = mapId;
                StageSequence = stageSequence;
                Revision = revision;
                State = state;
                RemainingSeconds = remainingSeconds;
            }

            public bool Found { get; }
            public int MapId { get; }
            public uint StageSequence { get; }
            public uint Revision { get; }
            public NetworkRunStageClockState State { get; }
            public float RemainingSeconds { get; }
        }

        private readonly struct DebrisSaleStateReport
        {
            public DebrisSaleStateReport(int credits, uint revision, string heldItemId)
            {
                Credits = credits;
                Revision = revision;
                HeldItemId = heldItemId;
            }

            public int Credits { get; }
            public uint Revision { get; }
            public string HeldItemId { get; }
        }

        private readonly struct EconomyReport
        {
            public EconomyReport(
                bool found,
                int credits,
                uint revision,
                int pendingCount,
                int claimedCount,
                int deliveredCount,
                string lastTransactionId,
                NetworkRunEconomyTransactionKind lastTransactionKind)
            {
                Found = found;
                Credits = credits;
                Revision = revision;
                PendingCount = pendingCount;
                ClaimedCount = claimedCount;
                DeliveredCount = deliveredCount;
                LastTransactionId = lastTransactionId;
                LastTransactionKind = lastTransactionKind;
            }

            public bool Found { get; }
            public int Credits { get; }
            public uint Revision { get; }
            public int PendingCount { get; }
            public int ClaimedCount { get; }
            public int DeliveredCount { get; }
            public string LastTransactionId { get; }
            public NetworkRunEconomyTransactionKind LastTransactionKind { get; }
        }

        private readonly struct IncidentReport
        {
            public IncidentReport(
                bool found,
                NetworkRunIncidentSnapshot snapshot,
                int commandCount,
                ulong commandSignature)
            {
                Found = found;
                Snapshot = snapshot;
                CommandCount = commandCount;
                CommandSignature = commandSignature;
            }

            public bool Found { get; }
            public NetworkRunIncidentSnapshot Snapshot { get; }
            public int CommandCount { get; }
            public ulong CommandSignature { get; }
        }

        private readonly struct EventSnapshotReport
        {
            public EventSnapshotReport(
                bool found,
                ulong instanceId,
                EventId eventId,
                string roomId,
                EventState state,
                uint revision,
                bool localEventActive,
                int localEffectCount,
                int networkEffectCount,
                int mirrorEffectCount)
            {
                Found = found;
                InstanceId = instanceId;
                EventId = eventId;
                RoomId = roomId;
                State = state;
                Revision = revision;
                LocalEventActive = localEventActive;
                LocalEffectCount = localEffectCount;
                NetworkEffectCount = networkEffectCount;
                MirrorEffectCount = mirrorEffectCount;
            }

            public bool Found { get; }
            public ulong InstanceId { get; }
            public EventId EventId { get; }
            public string RoomId { get; }
            public EventState State { get; }
            public uint Revision { get; }
            public bool LocalEventActive { get; }
            public int LocalEffectCount { get; }
            public int NetworkEffectCount { get; }
            public int MirrorEffectCount { get; }
        }

        private readonly struct EventTerminalReport
        {
            public EventTerminalReport(bool observedTerminal, bool observedRemoved, uint terminalRevision)
            {
                ObservedTerminal = observedTerminal;
                ObservedRemoved = observedRemoved;
                TerminalRevision = terminalRevision;
            }

            public bool ObservedTerminal { get; }
            public bool ObservedRemoved { get; }
            public uint TerminalRevision { get; }
        }

        private readonly struct ExternalMiniGameValidationCase
        {
            public ExternalMiniGameValidationCase(
                PHSMiniGameType miniGameType,
                EventId externalEventId)
            {
                MiniGameType = miniGameType;
                ExternalEventId = externalEventId;
            }

            public PHSMiniGameType MiniGameType { get; }
            public EventId ExternalEventId { get; }
        }

        private readonly struct ShipPowerReport
        {
            public ShipPowerReport(
                bool stateFound,
                bool powerEnabled,
                bool gravityEnabled,
                bool batteryInstalled,
                uint shipRevision,
                uint itemRevision,
                string heldItemId,
                bool powerOffActive,
                bool lightingFound,
                bool blackoutApplied,
                bool emergencyLightingActive,
                float ambientIntensityRatio)
            {
                StateFound = stateFound;
                PowerEnabled = powerEnabled;
                GravityEnabled = gravityEnabled;
                BatteryInstalled = batteryInstalled;
                ShipRevision = shipRevision;
                ItemRevision = itemRevision;
                HeldItemId = heldItemId;
                PowerOffActive = powerOffActive;
                LightingFound = lightingFound;
                BlackoutApplied = blackoutApplied;
                EmergencyLightingActive = emergencyLightingActive;
                AmbientIntensityRatio = ambientIntensityRatio;
            }

            public bool StateFound { get; }
            public bool PowerEnabled { get; }
            public bool GravityEnabled { get; }
            public bool BatteryInstalled { get; }
            public uint ShipRevision { get; }
            public uint ItemRevision { get; }
            public string HeldItemId { get; }
            public bool PowerOffActive { get; }
            public bool LightingFound { get; }
            public bool BlackoutApplied { get; }
            public bool EmergencyLightingActive { get; }
            public float AmbientIntensityRatio { get; }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsServer || OwnerClientId != NetworkManager.ServerClientId || !IsScenarioEnabled())
            {
                return;
            }

            expectedClientCount = Mathf.Max(2, GetCommandLineInt("-phsAutoStartClients", 2));
            scenarioRunning = true;
            if (HasCommandLineFlag(InputOnlyScenarioFlag))
            {
                StartCoroutine(RunInputOnlyServerScenario());
                return;
            }

            if (HasCommandLineFlag(ItemScenarioFlag))
            {
                StartCoroutine(RunItemServerScenario());
                return;
            }

            StartCoroutine(RunServerScenario());
        }

        private IEnumerator RunItemServerScenario()
        {
            Debug.Log($"PHS_ITEM_P0_BEGIN expectedClients={expectedClientCount}", this);

            yield return WaitFor(
                () => NetworkManager != null
                    && NetworkManager.ConnectedClients.Count >= expectedClientCount,
                DefaultStepTimeout,
                "item_clients_not_connected");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                DefaultStepTimeout,
                "item_map_scene_not_loaded");
            if (scenarioFinished) yield break;

            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (remoteClientId == NetworkManager.ServerClientId
                || !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient)
                || remoteClient.PlayerObject == null
                || remoteClient.PlayerObject.GetComponent<NetworkPlayerItemLifecycle>() is not { } lifecycle
                || lifecycle.ItemCatalog == null)
            {
                Fail("item_remote_catalog_missing");
                yield break;
            }

            var itemIds = new[] { "wrench", "fire_extinguisher", "battery_pack" };
            foreach (var itemId in itemIds)
            {
                if (!lifecycle.ItemCatalog.TryGetById(itemId, out var itemData)
                    || itemData == null
                    || !itemData.HasHeldPrefab
                    || !itemData.HasDroppedPrefab
                    || !itemData.HasDurability)
                {
                    Fail($"item_catalog_contract_invalid item={itemId}");
                    yield break;
                }

                yield return RunRemoteOwnedThrownItemValidation(itemData, true);
                if (scenarioFinished) yield break;

                if (string.Equals(
                        itemData.ItemId,
                        "battery_pack",
                        StringComparison.Ordinal))
                {
                    yield return RunRemoteOwnedThrownItemValidation(
                        itemData,
                        false);
                    if (scenarioFinished) yield break;
                }
            }

            Pass($"item_network_lifecycle peers={expectedClientCount} items={itemIds.Length}");
        }

        private IEnumerator RunInputOnlyServerScenario()
        {
            yield return WaitFor(
                () => NetworkManager != null && NetworkManager.ConnectedClients.Count >= expectedClientCount,
                DefaultStepTimeout,
                "input_only_clients_not_connected");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                DefaultStepTimeout,
                "input_only_map_scene_not_loaded");
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_INPUT_SCENE_READY peers={NetworkManager.ConnectedClients.Count} scene={MapSceneName}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            scenarioRunning = false;
            DetachEventLifecycleObservation();
            StopAllCoroutines();
            base.OnNetworkDespawn();
        }

        private IEnumerator RunServerScenario()
        {
            Debug.Log($"PHS_P0_BEGIN expectedClients={expectedClientCount}", this);

            yield return WaitFor(
                () => NetworkManager != null && NetworkManager.ConnectedClients.Count >= expectedClientCount,
                DefaultStepTimeout,
                "clients_not_connected");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                DefaultStepTimeout,
                "map_scene_not_loaded");
            if (scenarioFinished) yield break;

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            yield return RunIncidentLedgerValidation();
            if (scenarioFinished) yield break;

            yield return RunEventLifecycleValidation();
            if (scenarioFinished) yield break;

            yield return RunShipPowerBatteryValidation();
            if (scenarioFinished) yield break;

            yield return RunThrownItemNetworkValidation();
            if (scenarioFinished) yield break;

            var earlyCoordinator = NetworkRunFlowCoordinator.Instance;
            var earlyConsole = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            if (earlyCoordinator == null || earlyConsole == null)
            {
                Fail("early_gate_setup_missing");
                yield break;
            }

            var earlyMapChoicesReady = earlyConsole.TryGetCurrentMapChoices(out _, out _);
            var earlySelectionAccepted = earlyCoordinator.TrySelectNextZone(1, out var earlySelectionReason);
            if (earlyConsole.CanSelectSide(TravelConsoleSide.Left) || earlyMapChoicesReady ||
                earlySelectionAccepted ||
                earlySelectionReason != "warp_safe_required")
            {
                Fail($"early_map_selection_not_blocked reason={earlySelectionReason ?? "none"}");
                yield break;
            }

            Debug.Log($"PHS_P0_EARLY_GATE_OK reason={earlySelectionReason}", this);

            yield return WaitFor(
                () => NetworkRunFlowCoordinator.Instance != null &&
                    NetworkRunFlowCoordinator.Instance.Phase == NetworkRunPhase.Charging,
                DefaultStepTimeout,
                "charging_not_reached_for_debris");
            if (scenarioFinished) yield break;

            yield return ProbeRunningStageClock(
                NetworkRunFlowCoordinator.Instance.ActiveMapId,
                expectedSequence: 0U,
                captureInitialSequence: true);
            if (scenarioFinished) yield break;

            yield return RunDebrisRoundTrip();
            if (scenarioFinished) yield break;

            if (NetworkRunFlowCoordinator.Instance == null
                || !NetworkRunFlowCoordinator.Instance.TryConfigureRuntimeValidationTimings(12f))
            {
                Fail("runtime_validation_timing_setup_failed");
                yield break;
            }

            yield return WaitFor(
                () => NetworkRunFlowCoordinator.Instance != null &&
                    NetworkRunFlowCoordinator.Instance.Phase == NetworkRunPhase.Charging &&
                    NetworkRunFlowCoordinator.Instance.WarpChargeNormalized >= 0.2f,
                DefaultStepTimeout,
                "charging_gauge_not_reached");
            if (scenarioFinished) yield break;

            yield return ProbeGauge();
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => NetworkRunFlowCoordinator.Instance != null &&
                    NetworkRunFlowCoordinator.Instance.Phase == NetworkRunPhase.WarpReady,
                DefaultStepTimeout,
                "warp_ready_not_reached");
            if (scenarioFinished) yield break;

            var warpReadyCoordinator = NetworkRunFlowCoordinator.Instance;
            if (!warpReadyCoordinator.TryActivateWarp(
                    NetworkManager.ServerClientId,
                    out var warpSafeEntryReason))
            {
                Fail($"warp_safe_entry_failed reason={warpSafeEntryReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => warpReadyCoordinator.Phase == NetworkRunPhase.WarpSafe,
                10f,
                "warp_safe_not_reached");
            if (scenarioFinished) yield break;

            yield return ProbePausedStageClock(warpReadyCoordinator.ActiveMapId);
            if (scenarioFinished) yield break;

            yield return ProbeMapChoices();
            if (scenarioFinished) yield break;

            yield return ProbeFarSelectRejection();
            if (scenarioFinished) yield break;

            if (!TryAcquireMapSceneReferences(out var coordinator, out _))
            {
                Fail("post_debris_map_references_missing");
                yield break;
            }

            if (coordinator.TryActivateWarp(NetworkManager.ServerClientId, out var unsafeReason) ||
                unsafeReason != "next_map_not_selected")
            {
                Fail($"unsafe_warp_not_rejected reason={unsafeReason ?? "none"}");
                yield break;
            }

            if (coordinator.RequiresAllConnectedAlivePlayersSafe || !coordinator.IsWarpSafetySatisfied)
            {
                Fail("phase_based_warp_safety_not_configured");
                yield break;
            }

            var safePlayerCountBeforeWarp = coordinator.SafePlayerCount;
            var requiredSafePlayerCountBeforeWarp = coordinator.RequiredSafePlayerCount;
            Debug.Log(
                $"PHS_P0_SAFE_OK safe={safePlayerCountBeforeWarp}/{requiredSafePlayerCountBeforeWarp}",
                this);

            var firstChoices = mapChoiceReports.Values.First();
            if (!coordinator.TrySelectNextZone(firstChoices.LeftZoneId, out var selectionReason))
            {
                Fail($"zone_selection_failed reason={selectionReason ?? "none"}");
                yield break;
            }

            if (!coordinator.TryActivateWarp(NetworkManager.ServerClientId, out var warpReason))
            {
                Fail($"safe_warp_failed reason={warpReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.ClearedZoneCount >= 1,
                10f,
                "first_zone_clear_not_recorded");
            if (scenarioFinished) yield break;

            for (var expectedClearedZones = 2; expectedClearedZones <= 9; expectedClearedZones++)
            {
                if (!TryAcquireMapSceneReferences(out coordinator, out _))
                {
                    Fail($"map_cycle_references_missing cycle={expectedClearedZones}");
                    yield break;
                }

                yield return RunAdditionalWarpCycle(coordinator, expectedClearedZones);
                if (scenarioFinished) yield break;

                var isFinalShop = expectedClearedZones == GameLoopState.TOTAL_ZONES;
                if (expectedClearedZones % GameLoopState.SHOP_INTERVAL != 0 && !isFinalShop)
                {
                    continue;
                }

                var expectedShopCycles = expectedClearedZones / GameLoopState.SHOP_INTERVAL;
                var expectedShopPhase = isFinalShop
                    ? NetworkRunPhase.FinalShop
                    : NetworkRunPhase.Shop;
                yield return WaitFor(
                    () => NetworkRunFlowCoordinator.Instance != null &&
                        NetworkRunFlowCoordinator.Instance.Phase == expectedShopPhase &&
                        NetworkRunFlowCoordinator.Instance.ClearedZoneCount == expectedClearedZones &&
                        NetworkRunFlowCoordinator.Instance.CompletedShopCycleCount == expectedShopCycles,
                    10f,
                    $"shop_phase_missing cycle={expectedClearedZones} expectedPhase={expectedShopPhase}");
                if (scenarioFinished) yield break;

                yield return WaitFor(
                    IsShopEntryReady,
                    30f,
                    $"shop_entry_not_ready cycle={expectedClearedZones}");
                if (scenarioFinished) yield break;

                NetworkScenePortalInteractable shopEntryPortal = null;
                var shopAutoLoaded = SceneManager.GetActiveScene().name == ShopSceneName;
                if (!shopAutoLoaded &&
                    (!TryAcquireMapSceneReferences(out coordinator, out _) ||
                     !TryFindScenePortal(ShopSceneName, out shopEntryPortal)))
                {
                    Fail($"shop_portal_invalid cycle={expectedClearedZones}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P0_SHOP_PHASE_OK zones={expectedClearedZones} cycles={expectedShopCycles} " +
                    $"phase={expectedShopPhase} autoLoaded={shopAutoLoaded}",
                    this);

                yield return RunShopRoundTrip(
                    shopEntryPortal,
                    expectedClearedZones,
                    expectedShopCycles,
                    expectedShopPhase,
                    validatePurchaseAtomicity: expectedClearedZones == GameLoopState.SHOP_INTERVAL);
                if (scenarioFinished) yield break;

                if (!TryAcquireMapSceneReferences(out coordinator, out _))
                {
                    Fail($"map_return_references_missing cycle={expectedClearedZones}");
                    yield break;
                }
            }

            yield return ProbeRunFlowState(NetworkRunPhase.Clear, 9, 3, finalShopPending: false);
            if (scenarioFinished) yield break;

            var gaugeValues = gaugeReports.Values.Select(report => report.Value).ToArray();
            var gaugeDelta = gaugeValues.Max() - gaugeValues.Min();
            Pass(
                $"mapPeers={sceneReports.Count} gaugePeers={gaugeReports.Count} gaugeDelta={gaugeDelta:F3} " +
                $"choicePeers={mapChoiceReports.Count} left={firstChoices.LeftZoneId} right={firstChoices.RightZoneId} " +
                $"rngSeed={firstChoices.RunSeed} rngAlgorithm={firstChoices.AlgorithmVersion} " +
                $"unsafeReject={unsafeReason} safe={safePlayerCountBeforeWarp}/{requiredSafePlayerCountBeforeWarp} " +
                $"zones={coordinator.ClearedZoneCount} shopCycles={coordinator.CompletedShopCycleCount} " +
                $"runPhase={coordinator.Phase} runPeers={runFlowReports.Count} " +
                $"incidentCommands={validatedIncidentCommandCount} " +
                $"incidentRevision={validatedIncidentRevision} incidentPeers={incidentReports.Count} " +
                "events=3 miniGameApiOutcomes=6 eventPeers=2 farEventReject=true");
        }

        private IEnumerator RunIncidentLedgerValidation()
        {
            yield return WaitFor(
                IsIncidentLedgerValidationReady,
                15f,
                "incident_ledger_validation_setup_not_ready");
            if (scenarioFinished) yield break;

            var root = NetworkRunSessionRoot.Instance;
            var director = root.IncidentDirector;
            var ledger = root.Incidents;
            var schedulingWasEnabled = director.SchedulingEnabled;
            if (!director.TrySetSchedulingEnabledServer(
                    false,
                    out var pauseReason))
            {
                Fail(
                    $"incident_validation_pause_failed reason={pauseReason ?? "none"}");
                yield break;
            }

            var validationSucceeded = false;
            var validationReason = "incident_validation_unknown";
            var expectedSnapshot = default(NetworkRunIncidentSnapshot);
            var expectedCommandCount = 0;
            var expectedCommandSignature = 0UL;
            try
            {
                validationSucceeded = TryExerciseIncidentLedgerServer(
                    ledger,
                    director,
                    out expectedSnapshot,
                    out expectedCommandCount,
                    out expectedCommandSignature,
                    out validationReason);
            }
            catch (Exception exception)
            {
                validationReason =
                    $"incident_validation_exception:{exception.GetType().Name}";
            }

            if (!validationSucceeded)
            {
                TryCancelValidationIncidentCommands(ledger);
                var restored = director.TrySetSchedulingEnabledServer(
                    schedulingWasEnabled,
                    out var restoreReason);
                Fail(
                    restored
                        ? validationReason
                        : $"{validationReason};incident_restore_failed:" +
                          $"{restoreReason ?? "none"}");
                yield break;
            }

            yield return ProbeIncidentState(
                expectedSnapshot,
                expectedCommandCount,
                expectedCommandSignature);

            var restoreSucceeded = director.TrySetSchedulingEnabledServer(
                schedulingWasEnabled,
                out var finalRestoreReason);
            if (scenarioFinished) yield break;
            if (!restoreSucceeded)
            {
                Fail(
                    $"incident_validation_restore_failed " +
                    $"reason={finalRestoreReason ?? "none"}");
                yield break;
            }

            validatedIncidentCommandCount = expectedCommandCount;
            validatedIncidentRevision = expectedSnapshot.Revision;
            Debug.Log(
                $"PHS_P0_INCIDENT_LEDGER_OK peers={incidentReports.Count} " +
                $"commands={expectedCommandCount} revision={expectedSnapshot.Revision} " +
                $"issued={expectedSnapshot.StageIssuedCount} " +
                $"resolved={expectedSnapshot.StageResolvedCount} " +
                $"signature={expectedCommandSignature:X16}",
                this);
        }

        private bool IsIncidentLedgerValidationReady()
        {
            var root = NetworkRunSessionRoot.Instance;
            if (root == null
                || !root.IsSpawned
                || !root.IsServer
                || root.IncidentDirector == null
                || root.Incidents == null
                || !root.Incidents.IsSpawned
                || !root.Incidents.IsServer
                || !root.IncidentDirector.IsConfigured
                || root.IncidentDirector.Definition == null)
            {
                return false;
            }

            var snapshot = root.Incidents.Snapshot;
            var definition = root.IncidentDirector.Definition;
            return snapshot.State == NetworkRunIncidentStageState.Active
                && snapshot.MapId == definition.MapId
                && snapshot.StageSequence == definition.StageSequence;
        }

        private bool TryExerciseIncidentLedgerServer(
            NetworkRunIncidentLedger ledger,
            PHSNetworkIncidentDirector director,
            out NetworkRunIncidentSnapshot finalSnapshot,
            out int finalCommandCount,
            out ulong finalCommandSignature,
            out string reason)
        {
            finalSnapshot = default;
            finalCommandCount = 0;
            finalCommandSignature = 0UL;
            var initial = ledger.Snapshot;
            var definition = director.Definition;
            var initialCommandCount = ledger.CommandCount;
            if (initial.State != NetworkRunIncidentStageState.Active
                || initial.PressureCapacity != 3
                || definition.PressureCapacity != 3
                || definition.MaximumActiveExternal != 1
                || definition.MaximumActiveInternal != 2)
            {
                reason =
                    $"incident_validation_capacity_contract_mismatch:" +
                    $"state={initial.State}:pressure={initial.PressureCapacity}/" +
                    $"{definition.PressureCapacity}:external=" +
                    $"{definition.MaximumActiveExternal}:internal=" +
                    $"{definition.MaximumActiveInternal}";
                return false;
            }

            if (initial.ReservedPressure != 0
                || initial.ActivePressure != 0
                || initial.ActiveExternalCount != 0
                || initial.ActiveInternalCount != 0
                || !initial.ActiveWarpChargeMultiplier.Equals(1f))
            {
                reason =
                    $"incident_validation_ledger_not_idle:" +
                    $"reserved={initial.ReservedPressure}:active={initial.ActivePressure}:" +
                    $"external={initial.ActiveExternalCount}:" +
                    $"internal={initial.ActiveInternalCount}:" +
                    $"multiplier={initial.ActiveWarpChargeMultiplier}";
                return false;
            }

            var externalRequest = CreateValidationIncidentRequest(
                initial,
                "external",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.Enemy,
                int.MaxValue,
                1,
                0.5f);
            var beforeMutation = ledger.Snapshot;
            if (!ledger.TryReserveCommandServer(
                    in externalRequest,
                    out var externalCommand,
                    out var operationReason))
            {
                reason =
                    $"incident_external_reserve_failed:{operationReason ?? "none"}";
                return false;
            }

            var current = ledger.Snapshot;
            if (externalCommand.CommandId != beforeMutation.NextCommandId
                || externalCommand.State != NetworkRunIncidentCommandState.Pending
                || current.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || current.ReservedPressure != 1
                || current.ActivePressure != 0
                || current.ActiveExternalCount != 1
                || current.ActiveInternalCount != 0
                || current.StageIssuedCount
                    != NextNonZeroSequence(beforeMutation.StageIssuedCount)
                || ledger.CommandCount != initialCommandCount + 1)
            {
                reason = "incident_external_reserve_state_invalid";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            var commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryReserveCommandServer(
                    in externalRequest,
                    out var externalReplay,
                    out operationReason)
                || !externalReplay.Equals(externalCommand)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_request_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var conflictingRequest = externalRequest;
            conflictingRequest.ContentId = int.MaxValue - 1;
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (ledger.TryReserveCommandServer(
                    in conflictingRequest,
                    out _,
                    out operationReason)
                || operationReason != "request_id_conflict"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_request_conflict_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var externalCapRequest = CreateValidationIncidentRequest(
                initial,
                "external_cap",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.Meteor,
                int.MaxValue - 2,
                1,
                1f);
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (ledger.TryReserveCommandServer(
                    in externalCapRequest,
                    out _,
                    out operationReason)
                || operationReason != "external_command_cap_reached"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_external_cap_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var executorId = NetworkObjectId;
            beforeMutation = ledger.Snapshot;
            if (!ledger.TryClaimCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    out var claimedExternal,
                    out operationReason)
                || claimedExternal.State
                    != NetworkRunIncidentCommandState.Claimed
                || claimedExternal.ExecutorNetworkObjectId != executorId
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 1
                || ledger.Snapshot.ActivePressure != 0)
            {
                reason =
                    $"incident_claim_failed:{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryClaimCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    out var claimReplay,
                    out operationReason)
                || !claimReplay.Equals(claimedExternal)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_claim_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            const ulong ExternalRuntimeInstanceId = 0xF000000000000001UL;
            const string ExternalTargetId = "p0_validation_external";
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            var commandSignatureBeforeGuard =
                ComputeIncidentCommandSignature(ledger);
            if (ledger.TryActivateCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    ExternalRuntimeInstanceId,
                    string.Empty,
                    out operationReason)
                || operationReason != "target_id_required"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay)
                || ComputeIncidentCommandSignature(ledger)
                    != commandSignatureBeforeGuard)
            {
                reason =
                    $"incident_empty_target_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            if (!ledger.TryActivateCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    ExternalRuntimeInstanceId,
                    ExternalTargetId,
                    out operationReason)
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 0
                || ledger.Snapshot.ActivePressure != 1
                || !ledger.Snapshot.ActiveWarpChargeMultiplier.Equals(0.5f)
                || !ledger.TryGetCommand(
                    externalCommand.CommandId,
                    out var activeExternal)
                || activeExternal.State
                    != NetworkRunIncidentCommandState.Active
                || activeExternal.RuntimeInstanceId
                    != ExternalRuntimeInstanceId
                || activeExternal.TargetId.ToString() != ExternalTargetId)
            {
                reason =
                    $"incident_activate_failed:{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryActivateCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    ExternalRuntimeInstanceId,
                    ExternalTargetId,
                    out operationReason)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_activate_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            const string ExternalOutcomeId = "p0_validation_resolved";
            beforeMutation = ledger.Snapshot;
            if (!ledger.TryCompleteCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    true,
                    ExternalOutcomeId,
                    out operationReason)
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 0
                || ledger.Snapshot.ActivePressure != 0
                || ledger.Snapshot.ActiveExternalCount != 0
                || !ledger.Snapshot.ActiveWarpChargeMultiplier.Equals(1f)
                || !ledger.TryGetCommand(
                    externalCommand.CommandId,
                    out var completedExternal)
                || completedExternal.State
                    != NetworkRunIncidentCommandState.Resolved
                || completedExternal.OutcomeId.ToString()
                    != ExternalOutcomeId)
            {
                reason =
                    $"incident_complete_failed:{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryCompleteCommandServer(
                    externalCommand.CommandId,
                    executorId,
                    true,
                    ExternalOutcomeId,
                    out operationReason)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_complete_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var invalidMultiplierRequest = CreateValidationIncidentRequest(
                initial,
                "multiplier_out_of_range",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.Meteor,
                int.MaxValue - 3,
                1,
                1.01f);
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            commandSignatureBeforeGuard =
                ComputeIncidentCommandSignature(ledger);
            if (ledger.TryReserveCommandServer(
                    in invalidMultiplierRequest,
                    out _,
                    out operationReason)
                || operationReason != "warp_charge_multiplier_out_of_range"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay)
                || ComputeIncidentCommandSignature(ledger)
                    != commandSignatureBeforeGuard)
            {
                reason =
                    $"incident_multiplier_range_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var internalFirstRequest = CreateValidationIncidentRequest(
                initial,
                "internal_1",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Fire,
                int.MaxValue - 10,
                1,
                0.8f);
            var internalSecondRequest = CreateValidationIncidentRequest(
                initial,
                "internal_2",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Oxygen,
                int.MaxValue - 11,
                1,
                0.8f);
            if (!ledger.TryReserveCommandServer(
                    in internalFirstRequest,
                    out var internalFirst,
                    out operationReason)
                || !ledger.TryReserveCommandServer(
                    in internalSecondRequest,
                    out var internalSecond,
                    out operationReason)
                || ledger.Snapshot.ReservedPressure != 2
                || ledger.Snapshot.ActivePressure != 0
                || ledger.Snapshot.ActiveExternalCount != 0
                || ledger.Snapshot.ActiveInternalCount != 2)
            {
                reason =
                    $"incident_internal_two_reserve_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var internalCapRequest = CreateValidationIncidentRequest(
                initial,
                "internal_cap",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Power,
                int.MaxValue - 12,
                1,
                1f);
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (ledger.TryReserveCommandServer(
                    in internalCapRequest,
                    out _,
                    out operationReason)
                || operationReason != "internal_command_cap_reached"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_internal_cap_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            const string CancelReason = "p0_validation_cancel";
            beforeMutation = ledger.Snapshot;
            if (!ledger.TryCancelCommandServer(
                    internalFirst.CommandId,
                    CancelReason,
                    out operationReason)
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 1
                || ledger.Snapshot.ActiveInternalCount != 1)
            {
                reason =
                    $"incident_pending_cancel_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryCancelCommandServer(
                    internalFirst.CommandId,
                    CancelReason,
                    out operationReason)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_cancel_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            if (!ledger.TryCancelCommandServer(
                    internalSecond.CommandId,
                    CancelReason,
                    out operationReason)
                || ledger.Snapshot.ReservedPressure != 0
                || ledger.Snapshot.ActivePressure != 0
                || ledger.Snapshot.ActiveInternalCount != 0)
            {
                reason =
                    $"incident_second_internal_cancel_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            const ulong InternalRuntimeInstanceId = 0xF000000000000002UL;
            const string InternalTargetId = "p0_validation_internal";
            var pressureRequest = CreateValidationIncidentRequest(
                initial,
                "pressure_3",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Hull,
                int.MaxValue - 20,
                3,
                0f);
            pressureRequest.TargetId =
                new FixedString64Bytes(InternalTargetId);
            if (!ledger.TryReserveCommandServer(
                    in pressureRequest,
                    out var pressureCommand,
                    out operationReason)
                || ledger.Snapshot.ReservedPressure != 3
                || ledger.Snapshot.ActivePressure != 0
                || ledger.Snapshot.ActiveInternalCount != 1)
            {
                reason =
                    $"incident_total_pressure_reserve_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            var pressureOverflowRequest = CreateValidationIncidentRequest(
                initial,
                "pressure_overflow",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Device,
                int.MaxValue - 21,
                1,
                1f);
            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (ledger.TryReserveCommandServer(
                    in pressureOverflowRequest,
                    out _,
                    out operationReason)
                || operationReason
                    != "incident_pressure_capacity_exceeded"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_total_pressure_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            if (!ledger.TryClaimCommandServer(
                    pressureCommand.CommandId,
                    executorId,
                    out _,
                    out operationReason))
            {
                reason =
                    $"incident_pressure_claim_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            commandSignatureBeforeGuard =
                ComputeIncidentCommandSignature(ledger);
            if (ledger.TryActivateCommandServer(
                    pressureCommand.CommandId,
                    executorId,
                    InternalRuntimeInstanceId,
                    "p0_validation_internal_conflict",
                    out operationReason)
                || operationReason != "target_id_conflict"
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay)
                || ComputeIncidentCommandSignature(ledger)
                    != commandSignatureBeforeGuard)
            {
                reason =
                    $"incident_fixed_target_guard_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            if (!ledger.TryActivateCommandServer(
                    pressureCommand.CommandId,
                    executorId,
                    InternalRuntimeInstanceId,
                    InternalTargetId,
                    out operationReason)
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 0
                || ledger.Snapshot.ActivePressure != 3
                || !ledger.Snapshot.ActiveWarpChargeMultiplier.Equals(0f)
                || !ledger.TryGetCommand(
                    pressureCommand.CommandId,
                    out var activePressureCommand)
                || activePressureCommand.TargetId.ToString()
                    != InternalTargetId)
            {
                reason =
                    $"incident_pressure_activate_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            const string ActiveCancelReason = "p0_validation_active_cancel";
            beforeMutation = ledger.Snapshot;
            if (!ledger.TryCancelCommandServer(
                    pressureCommand.CommandId,
                    ActiveCancelReason,
                    out operationReason)
                || ledger.Snapshot.Revision
                    != NextNonZeroSequence(beforeMutation.Revision)
                || ledger.Snapshot.ReservedPressure != 0
                || ledger.Snapshot.ActivePressure != 0
                || ledger.Snapshot.ActiveInternalCount != 0
                || !ledger.Snapshot.ActiveWarpChargeMultiplier.Equals(1f))
            {
                reason =
                    $"incident_active_cancel_failed:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            beforeMutation = ledger.Snapshot;
            commandCountBeforeReplay = ledger.CommandCount;
            if (!ledger.TryCancelCommandServer(
                    pressureCommand.CommandId,
                    ActiveCancelReason,
                    out operationReason)
                || !IsIncidentLedgerUnchanged(
                    ledger,
                    beforeMutation,
                    commandCountBeforeReplay))
            {
                reason =
                    $"incident_active_cancel_replay_not_idempotent:" +
                    $"{operationReason ?? "none"}";
                return false;
            }

            current = ledger.Snapshot;
            if (current.State != NetworkRunIncidentStageState.Active
                || current.MapId != initial.MapId
                || current.StageSequence != initial.StageSequence
                || current.PressureCapacity != 3
                || current.ReservedPressure != 0
                || current.ActivePressure != 0
                || current.ActiveExternalCount != 0
                || current.ActiveInternalCount != 0
                || !current.ActiveWarpChargeMultiplier.Equals(1f)
                || current.StageIssuedCount
                    != AdvanceNonZeroSequence(
                        initial.StageIssuedCount,
                        4)
                || current.StageResolvedCount
                    != AdvanceNonZeroSequence(
                        initial.StageResolvedCount,
                        4)
                || current.NextCommandId
                    != AdvanceNonZeroCommandId(
                        initial.NextCommandId,
                        4)
                || current.Revision
                    != AdvanceNonZeroSequence(initial.Revision, 12)
                || ledger.CommandCount != initialCommandCount + 4)
            {
                reason =
                    $"incident_final_invariant_failed:" +
                    $"revision={current.Revision}:issued={current.StageIssuedCount}:" +
                    $"resolved={current.StageResolvedCount}:commands={ledger.CommandCount}:" +
                    $"next={current.NextCommandId}";
                return false;
            }

            finalSnapshot = current;
            finalCommandCount = ledger.CommandCount;
            finalCommandSignature = ComputeIncidentCommandSignature(ledger);
            reason = null;
            return true;
        }

        private static NetworkRunIncidentRequest CreateValidationIncidentRequest(
            NetworkRunIncidentSnapshot stage,
            string suffix,
            NetworkRunIncidentChannel channel,
            NetworkRunIncidentPayloadKind payloadKind,
            NetworkRunIncidentFamily family,
            int contentId,
            ushort pressureCost,
            float warpChargeMultiplier)
        {
            return new NetworkRunIncidentRequest(
                new FixedString64Bytes(
                    $"p0i:{stage.StageSequence}:{suffix}"),
                0UL,
                stage.StageSequence,
                stage.MapId,
                channel,
                payloadKind,
                family,
                contentId,
                NetworkRunIncidentSourceKind.Validation,
                pressureCost,
                warpChargeMultiplier,
                default);
        }

        private static bool IsIncidentLedgerUnchanged(
            NetworkRunIncidentLedger ledger,
            NetworkRunIncidentSnapshot expectedSnapshot,
            int expectedCommandCount)
        {
            return ledger.Snapshot.Equals(expectedSnapshot)
                && ledger.CommandCount == expectedCommandCount;
        }

        private static void TryCancelValidationIncidentCommands(
            NetworkRunIncidentLedger ledger)
        {
            if (ledger == null || !ledger.IsSpawned || !ledger.IsServer)
            {
                return;
            }

            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var command = ledger.GetCommandAt(index);
                if (!command.IsTerminal
                    && command.RequestId.ToString().StartsWith(
                        "p0i:",
                        StringComparison.Ordinal))
                {
                    ledger.TryCancelCommandServer(
                        command.CommandId,
                        "p0_validation_cleanup",
                        out _);
                }
            }
        }

        private IEnumerator RunEventLifecycleValidation()
        {
            var coordinator = FindAnyObjectByType<NetworkEventCoordinator>(FindObjectsInactive.Include);
            var manager = EventManager.Peek();
            if (coordinator == null || !coordinator.IsSpawned || !coordinator.IsServer ||
                manager == null || !manager.HasRuntimeBridge() || !manager.IsRuntimeAuthority())
            {
                Fail("event_lifecycle_setup_missing");
                yield break;
            }

            var eventIds = new[] { EventId.Fire, EventId.OxygenLeak, EventId.EnemySpawn };
            foreach (var eventId in eventIds)
            {
                yield return ValidateEventLifecycle(coordinator, manager, eventId);
                if (scenarioFinished) yield break;
            }

            yield return RunExternalMiniGameApiContractValidation(coordinator, manager);
            if (scenarioFinished) yield break;

            yield return ProbeFarEventTerminalRejection(coordinator, manager);
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P0_EVENT_LIFECYCLE_OK events={eventIds.Length} peers={expectedClientCount} " +
                "externalMiniGameOutcomes=6 clientLocalActive=false clientGameplayEffects=0 clientMirrors=true terminalRemoved=true",
                this);
        }

        private IEnumerator RunExternalMiniGameApiContractValidation(
            NetworkEventCoordinator eventCoordinator,
            EventManager eventManager)
        {
            previousMiniGameConsequenceContentId = 0;
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    NetworkManager.ServerClientId,
                    out var hostClient)
                || hostClient.PlayerObject == null)
            {
                Fail("p1_minigame_host_player_missing");
                yield break;
            }

            var validationCases = new[]
            {
                new ExternalMiniGameValidationCase(
                    PHSMiniGameType.Cannon,
                    EventId.MeteorAttack),
                new ExternalMiniGameValidationCase(
                    PHSMiniGameType.WireFix,
                    EventId.EmpAttack),
                new ExternalMiniGameValidationCase(
                    PHSMiniGameType.PowerSync,
                    EventId.EnemyScout)
            };

            Debug.Log(
                "PHS_P1_MINIGAME_API_CONTRACT_BEGIN mode=headless_api_contract uiInteraction=false authority=server",
                this);

            foreach (var validationCase in validationCases)
            {
                var terminal = FindObjectsByType<PHSFinalMiniGameTerminal>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate != null &&
                        candidate.ConfiguredMiniGameType == validationCase.MiniGameType);
                if (terminal == null)
                {
                    Fail($"p1_minigame_terminal_missing type={validationCase.MiniGameType}");
                    yield break;
                }

                yield return ValidateExternalMiniGameApiOutcome(
                    eventCoordinator,
                    eventManager,
                    hostClient.PlayerObject,
                    terminal,
                    validationCase,
                    succeeded: true);
                if (scenarioFinished) yield break;

                yield return ValidateExternalMiniGameApiOutcome(
                    eventCoordinator,
                    eventManager,
                    hostClient.PlayerObject,
                    terminal,
                    validationCase,
                    succeeded: false);
                if (scenarioFinished) yield break;
            }

            Debug.Log(
                $"PHS_P1_MINIGAME_API_CONTRACT_OK combinations={validationCases.Length} outcomes=6 " +
                $"peers={expectedClientCount} uiInteraction=false",
                this);
        }

        private IEnumerator ValidateExternalMiniGameApiOutcome(
            NetworkEventCoordinator eventCoordinator,
            EventManager eventManager,
            NetworkObject hostPlayer,
            PHSFinalMiniGameTerminal terminal,
            ExternalMiniGameValidationCase validationCase,
            bool succeeded)
        {
            var outcomeLabel = succeeded ? "success" : "failure";
            var runRoot = NetworkRunSessionRoot.Instance;
            var incidentLedger = runRoot == null ? null : runRoot.Incidents;
            var incidentGateway = FindAnyObjectByType<PHSIncidentRequestGateway>(
                FindObjectsInactive.Include);
            var consequenceSelector =
                FindAnyObjectByType<PHSIncidentConsequenceSelector>(
                    FindObjectsInactive.Include);
            var accidentCoordinator =
                FindAnyObjectByType<PHSNetworkShipAccidentCoordinator>(
                    FindObjectsInactive.Include);
            var incidentConsumer =
                FindAnyObjectByType<PHSMapIncidentCommandConsumer>(
                    FindObjectsInactive.Include);
            if (runRoot == null
                || incidentLedger == null
                || incidentGateway == null
                || consequenceSelector == null
                || accidentCoordinator == null
                || incidentConsumer == null
                || incidentConsumer.IncidentLayout == null
                || !incidentGateway.IsReady
                || !accidentCoordinator.IsSpawned
                || !accidentCoordinator.IsServer)
            {
                Fail(
                    $"p1_minigame_incident_pipeline_missing " +
                    $"event={validationCase.ExternalEventId} " +
                    $"outcome={outcomeLabel}");
                yield break;
            }

            if (eventCoordinator.IsEventActive(validationCase.ExternalEventId)
                || eventManager.IsActive(validationCase.ExternalEventId)
                || accidentCoordinator.ActiveAccidentCount != 0)
            {
                Fail(
                    $"p1_minigame_preexisting_event type={validationCase.MiniGameType} outcome={outcomeLabel}");
                yield break;
            }

            SetPlayerPosition(hostPlayer, terminal.transform.position);
            yield return null;
            var terminalDistance = Vector3.Distance(hostPlayer.transform.position, terminal.transform.position);
            if (terminalDistance > 4f)
            {
                Fail(
                    $"p1_minigame_terminal_distance_invalid type={validationCase.MiniGameType} " +
                    $"distance={terminalDistance:F3}");
                yield break;
            }

            yield return WaitFor(
                () => HasAvailableExternalMiniGameLocation(
                    incidentConsumer,
                    validationCase.ExternalEventId),
                DefaultStepTimeout,
                $"p1_minigame_external_location_not_ready " +
                $"event={validationCase.ExternalEventId} outcome={outcomeLabel}");
            if (scenarioFinished) yield break;

            if (!incidentGateway.TrySubmitTerminalEventServer(
                    validationCase.ExternalEventId,
                    null,
                    out var parentCommand,
                    out var submitReason))
            {
                Fail(
                    $"p1_minigame_external_submit_failed " +
                    $"event={validationCase.ExternalEventId} " +
                    $"outcome={outcomeLabel} reason={submitReason}");
                yield break;
            }

            yield return WaitFor(
                () => incidentLedger.TryGetCommand(
                        parentCommand.CommandId,
                        out var currentParent)
                    && currentParent.State
                        == NetworkRunIncidentCommandState.Active
                    && currentParent.RuntimeInstanceId != 0UL
                    && eventCoordinator.TryGetSnapshot(
                        currentParent.RuntimeInstanceId,
                        out var snapshot)
                    && snapshot.EventId
                        == validationCase.ExternalEventId
                    && snapshot.State == EventState.InProgress,
                5f,
                $"p1_minigame_external_not_in_progress event={validationCase.ExternalEventId} " +
                $"outcome={outcomeLabel}");
            if (scenarioFinished) yield break;

            if (!incidentLedger.TryGetCommand(
                    parentCommand.CommandId,
                    out parentCommand)
                || parentCommand.RuntimeInstanceId == 0UL)
            {
                Fail(
                    $"p1_minigame_parent_command_activation_missing " +
                    $"command={parentCommand.CommandId}");
                yield break;
            }

            var externalInstanceId = parentCommand.RuntimeInstanceId;

            yield return ProbeEventSnapshot(
                validationCase.ExternalEventId,
                externalInstanceId,
                requireHostLocalEffect: false);
            if (scenarioFinished) yield break;

            var externalActiveSnapshot = eventSnapshotReports.Values.First();
            yield return BeginEventTerminalObservation(
                externalInstanceId,
                $"p1_minigame_external_observation_not_ready event={validationCase.ExternalEventId} " +
                $"outcome={outcomeLabel}");
            if (scenarioFinished) yield break;

            SetPlayerPosition(hostPlayer, terminal.transform.position);
            terminalDistance = Vector3.Distance(
                hostPlayer.transform.position,
                terminal.transform.position);
            if (!eventCoordinator.RequestMiniGameResult(validationCase.ExternalEventId, succeeded))
            {
                Fail(
                    $"p1_minigame_result_rejected type={validationCase.MiniGameType} " +
                    $"event={validationCase.ExternalEventId} outcome={outcomeLabel} " +
                    $"distance={terminalDistance:F3}");
                yield break;
            }

            var expectedTerminalState = succeeded ? EventState.Resolve : EventState.Fail;
            yield return WaitFor(
                () => eventCoordinator.TryGetSnapshot(externalInstanceId, out var snapshot)
                    && snapshot.State == expectedTerminalState,
                5f,
                $"p1_minigame_terminal_state_missing event={validationCase.ExternalEventId} " +
                $"outcome={outcomeLabel} expected={expectedTerminalState}");
            if (scenarioFinished) yield break;

            yield return new WaitForSecondsRealtime(0.75f);
            yield return ProbeEventTerminal(externalInstanceId, externalActiveSnapshot.Revision);
            if (scenarioFinished) yield break;

            if (eventCoordinator.TryGetSnapshot(externalInstanceId, out _)
                || eventManager.IsInstanceActive(externalInstanceId))
            {
                Fail(
                    $"p1_minigame_external_not_removed event={validationCase.ExternalEventId} " +
                    $"outcome={outcomeLabel}");
                yield break;
            }

            if (succeeded)
            {
                yield return WaitFor(
                    () => incidentLedger.TryGetCommand(
                            parentCommand.CommandId,
                            out var resolvedParent)
                        && resolvedParent.State
                            == NetworkRunIncidentCommandState.Resolved,
                    5f,
                    $"p1_minigame_parent_not_resolved " +
                    $"command={parentCommand.CommandId}");
                if (scenarioFinished) yield break;

                if (CountConsequenceCommands(
                        incidentLedger,
                        parentCommand.CommandId) != 0)
                {
                    Fail(
                        $"p1_minigame_success_created_consequence " +
                        $"parent={parentCommand.CommandId}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P1_MINIGAME_OUTCOME_OK type={validationCase.MiniGameType} " +
                    $"event={validationCase.ExternalEventId} outcome=success distance={terminalDistance:F3} " +
                    $"peers={eventTerminalReports.Count} uiInteraction=false",
                    this);
                yield break;
            }

            yield return WaitFor(
                () => incidentLedger.TryGetCommand(
                        parentCommand.CommandId,
                        out var failedParent)
                    && failedParent.State
                        == NetworkRunIncidentCommandState.Failed
                    && TryGetSingleConsequenceCommand(
                        incidentLedger,
                        parentCommand.CommandId,
                        out var consequence)
                    && consequence.State
                        == NetworkRunIncidentCommandState.Active
                    && consequence.RuntimeInstanceId != 0UL
                    && HasActiveAccident(
                        accidentCoordinator,
                        consequence.RuntimeInstanceId,
                        consequence.ContentId),
                DefaultStepTimeout,
                $"p1_minigame_consequence_not_active " +
                $"parent={parentCommand.CommandId}");
            if (scenarioFinished) yield break;

            if (!TryGetSingleConsequenceCommand(
                    incidentLedger,
                    parentCommand.CommandId,
                    out var consequenceCommand))
            {
                Fail(
                    $"p1_minigame_consequence_command_missing " +
                    $"parent={parentCommand.CommandId}");
                yield break;
            }

            var internalEntries = runRoot.IncidentDirector?.Definition?.InternalEntries;
            if (internalEntries != null
                && internalEntries.Count > 1
                && previousMiniGameConsequenceContentId
                    == consequenceCommand.ContentId)
            {
                Fail(
                    $"p1_minigame_consequence_repeated " +
                    $"content={consequenceCommand.ContentId} " +
                    $"parent={parentCommand.CommandId}");
                yield break;
            }

            previousMiniGameConsequenceContentId =
                consequenceCommand.ContentId;

            var replaySnapshot = incidentLedger.Snapshot;
            var replayCommandCount = incidentLedger.CommandCount;
            if (!consequenceSelector.TryRequestForFailedExternalEventServer(
                    parentCommand.CommandId,
                    out var replayCommand,
                    out var replayReason)
                || replayCommand.CommandId != consequenceCommand.CommandId
                || !incidentLedger.Snapshot.Equals(replaySnapshot)
                || incidentLedger.CommandCount != replayCommandCount)
            {
                Fail(
                    $"p1_minigame_consequence_replay_not_idempotent " +
                    $"parent={parentCommand.CommandId} " +
                    $"reason={replayReason}");
                yield break;
            }

            var synchronizedIncidentSnapshot = incidentLedger.Snapshot;
            var synchronizedIncidentCount = incidentLedger.CommandCount;
            var synchronizedIncidentSignature =
                ComputeIncidentCommandSignature(incidentLedger);
            yield return ProbeIncidentState(
                synchronizedIncidentSnapshot,
                synchronizedIncidentCount,
                synchronizedIncidentSignature);
            if (scenarioFinished) yield break;

            if (!accidentCoordinator.TryTerminateAccidentServer(
                    (uint)consequenceCommand.RuntimeInstanceId,
                    "p1_minigame_consequence_cleanup",
                    out var terminateReason))
            {
                Fail(
                    $"p1_minigame_consequence_cleanup_rejected " +
                    $"command={consequenceCommand.CommandId} " +
                    $"reason={terminateReason}");
                yield break;
            }

            yield return WaitFor(
                () => incidentLedger.TryGetCommand(
                        consequenceCommand.CommandId,
                        out var terminatedConsequence)
                    && terminatedConsequence.IsTerminal
                    && !HasActiveAccident(
                        accidentCoordinator,
                        consequenceCommand.RuntimeInstanceId,
                        consequenceCommand.ContentId),
                5f,
                $"p1_minigame_consequence_cleanup_incomplete " +
                $"command={consequenceCommand.CommandId}");
            if (scenarioFinished) yield break;

            if (!TryResetShipAfterConsequence(
                    NetworkShipSystemsState.Instance,
                    out var resetReason))
            {
                Fail(
                    $"p1_minigame_consequence_ship_reset_failed " +
                    $"reason={resetReason}");
                yield break;
            }

            Debug.Log(
                $"PHS_P1_MINIGAME_OUTCOME_OK type={validationCase.MiniGameType} " +
                $"event={validationCase.ExternalEventId} outcome=failure " +
                $"parent={parentCommand.CommandId} " +
                $"consequence={consequenceCommand.ContentId} " +
                $"consequenceCommand={consequenceCommand.CommandId} " +
                $"distance={terminalDistance:F3} " +
                $"eventPeers={eventTerminalReports.Count} " +
                $"incidentPeers={incidentReports.Count} uiInteraction=false",
                this);
        }

        private static int CountConsequenceCommands(
            NetworkRunIncidentLedger ledger,
            ulong parentCommandId)
        {
            var count = 0;
            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var command = ledger.GetCommandAt(index);
                if (command.ParentCommandId == parentCommandId
                    && command.SourceKind
                        == NetworkRunIncidentSourceKind.Consequence)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryGetSingleConsequenceCommand(
            NetworkRunIncidentLedger ledger,
            ulong parentCommandId,
            out NetworkRunIncidentCommand consequenceCommand)
        {
            consequenceCommand = default;
            var found = false;
            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var command = ledger.GetCommandAt(index);
                if (command.ParentCommandId != parentCommandId
                    || command.SourceKind
                        != NetworkRunIncidentSourceKind.Consequence)
                {
                    continue;
                }

                if (found)
                {
                    consequenceCommand = default;
                    return false;
                }

                consequenceCommand = command;
                found = true;
            }

            return found;
        }

        private static bool HasActiveAccident(
            PHSNetworkShipAccidentCoordinator coordinator,
            ulong runtimeInstanceId,
            int contentId)
        {
            if (runtimeInstanceId == 0UL
                || runtimeInstanceId > uint.MaxValue)
            {
                return false;
            }

            for (var index = 0; index < coordinator.ActiveAccidentCount; index++)
            {
                var accident = coordinator.GetActiveAccidentAt(index);
                if (accident.InstanceId == (uint)runtimeInstanceId
                    && (int)accident.AccidentId == contentId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAvailableExternalMiniGameLocation(
            PHSMapIncidentCommandConsumer incidentConsumer,
            EventId eventId)
        {
            if (incidentConsumer == null
                || incidentConsumer.IncidentLayout == null
                || NetworkManager == null
                || !TryResolveExternalIncidentFamily(eventId, out var family))
            {
                return false;
            }

            var query = new IncidentLocationQuery(
                NetworkRunIncidentChannel.External,
                family,
                (int)eventId,
                NetworkShipModuleId.None,
                IncidentLocationKind.None,
                IncidentLocationCapability.None,
                null,
                null,
                NetworkManager.ServerTime.Time,
                true);
            return incidentConsumer.IncidentLayout.Locations.Any(location =>
                location != null
                && location.RuntimeTarget is ShipRoom
                && location.Supports(query));
        }

        private static bool TryResolveExternalIncidentFamily(
            EventId eventId,
            out NetworkRunIncidentFamily family)
        {
            switch (eventId)
            {
                case EventId.EnemyScout:
                    family = NetworkRunIncidentFamily.Enemy;
                    return true;
                case EventId.MeteorAttack:
                    family = NetworkRunIncidentFamily.Meteor;
                    return true;
                case EventId.EmpAttack:
                    family = NetworkRunIncidentFamily.EMP;
                    return true;
                default:
                    family = NetworkRunIncidentFamily.None;
                    return false;
            }
        }

        private static bool TryResetShipAfterConsequence(
            NetworkShipSystemsState shipState,
            out string reason)
        {
            if (shipState == null || !shipState.IsSpawned || !shipState.IsServer)
            {
                reason = "ship_state_missing";
                return false;
            }

            foreach (var moduleId in new[]
                     {
                         NetworkShipModuleId.Power,
                         NetworkShipModuleId.Gravity,
                         NetworkShipModuleId.LifeSupport,
                         NetworkShipModuleId.Engine
                     })
            {
                if (shipState.TryGetModuleSnapshot(moduleId, out var module)
                    && (module.CurrentHp < module.MaximumHp || module.IsFaulted)
                    && !shipState.TryRepairModule(moduleId, 1000, out reason))
                {
                    return false;
                }
            }

            if (!shipState.IsPowerEnabled
                && !shipState.TryRestorePowerWithBattery(out reason))
            {
                return false;
            }

            if (!shipState.IsGravityEnabled
                && !shipState.TryRestoreGravityAfterRepair(out reason))
            {
                return false;
            }

            if (shipState.CurrentShipHp < shipState.MaximumShipHp
                && !shipState.TryRestoreShipDurabilityAtDock(
                    shipState.MaximumShipHp - shipState.CurrentShipHp,
                    out reason))
            {
                return false;
            }

            reason = null;
            return true;
        }

        private IEnumerator BeginEventTerminalObservation(ulong instanceId, string failureReason)
        {
            activeObservedInstanceId = instanceId;
            eventObservationReadyClients.Clear();
            BeginEventLifecycleObservationClientRpc(instanceId);
            yield return WaitFor(
                () => eventObservationReadyClients.Count >= expectedClientCount,
                5f,
                failureReason);
        }

        private static bool TryFindActiveEventSnapshot(
            NetworkEventCoordinator coordinator,
            EventId eventId,
            out NetworkEventLifecycleSnapshot snapshot)
        {
            for (var index = 0; coordinator.TryGetSnapshotAt(index, out var candidate); index++)
            {
                if (candidate.EventId == eventId && !candidate.IsTerminal)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private IEnumerator RunShipPowerBatteryValidation()
        {
            var shipState = NetworkShipSystemsState.Instance;
            var accidentCoordinator =
                FindAnyObjectByType<PHSNetworkShipAccidentCoordinator>(
                    FindObjectsInactive.Include);
            var batterySocket = FindObjectsByType<BatteryInsertPowerStationSocket>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(socket => socket != null && socket.IsSpawned);
            if (validationBatteryItem == null || !validationBatteryItem.HasHeldPrefab
                || string.IsNullOrWhiteSpace(validationBatteryItem.ItemId)
                || shipState == null || !shipState.IsSpawned || !shipState.IsServer
                || accidentCoordinator == null
                || !accidentCoordinator.IsSpawned
                || !accidentCoordinator.IsServer
                || batterySocket == null
                || !NetworkManager.ConnectedClients.TryGetValue(
                    NetworkManager.ServerClientId,
                    out var hostClient)
                || hostClient.PlayerObject == null)
            {
                Fail("p2_ship_power_setup_missing");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var holder = playerObject.GetComponent<TempPlayerItemHolder>();
            var itemRecord = playerObject.GetComponent<NetworkPlayerItemRecord>();
            if (holder == null || itemRecord == null || !itemRecord.IsSpawned
                || holder.HasItem || !string.IsNullOrEmpty(itemRecord.HeldItemId)
                || accidentCoordinator.ActiveAccidentCount != 0)
            {
                Fail("p2_ship_power_player_or_accident_state_invalid");
                yield break;
            }

            var powerAnchorIds = new List<string>();
            if (!accidentCoordinator.TryCopyAvailableCompatibleAnchorIdsServer(
                    PHSShipAccidentId.PowerFailure,
                    powerAnchorIds,
                    out var anchorReason)
                || powerAnchorIds.Count == 0)
            {
                Fail(
                    $"p2_power_failure_anchor_missing " +
                    $"reason={anchorReason}");
                yield break;
            }

            if (!accidentCoordinator.TrySpawnAccidentServer(
                    PHSShipAccidentId.PowerFailure,
                    powerAnchorIds[0],
                    out var powerFailureInstanceId,
                    out var spawnReason))
            {
                Fail(
                    $"p2_power_failure_spawn_failed " +
                    $"reason={spawnReason}");
                yield break;
            }

            yield return WaitFor(
                () => HasActiveAccident(
                        accidentCoordinator,
                        powerFailureInstanceId,
                        (int)PHSShipAccidentId.PowerFailure)
                    && !shipState.IsPowerEnabled
                    && !shipState.IsGravityEnabled
                    && !shipState.IsBatteryInstalled,
                5f,
                "p2_power_off_not_applied");
            if (scenarioFinished) yield break;

            yield return ProbeShipPowerVisualState(true);
            if (scenarioFinished) yield break;

            var itemRevisionBeforeHold = itemRecord.Revision;
            holder.ReplaceHeldItem(validationBatteryItem, playerObject.transform);
            yield return WaitFor(
                () => holder.HasItem
                    && itemRecord.HeldItemId == validationBatteryItem.ItemId
                    && itemRecord.Revision == itemRevisionBeforeHold + 1U,
                5f,
                $"p2_battery_record_not_held item={validationBatteryItem.ItemId}");
            if (scenarioFinished) yield break;

            var heldItemRevision = itemRecord.Revision;
            SetPlayerPosition(playerObject, batterySocket.transform.position);
            yield return null;
            if (!batterySocket.CanInteract(holder))
            {
                Fail(
                    $"p2_battery_socket_interaction_unavailable item={itemRecord.HeldItemId} " +
                    $"revision={itemRecord.Revision}");
                yield break;
            }

            batterySocket.Interact(holder);
            yield return WaitFor(
                () => shipState.IsPowerEnabled
                    && shipState.IsGravityEnabled
                    && shipState.IsBatteryInstalled
                    && string.IsNullOrEmpty(itemRecord.HeldItemId)
                    && itemRecord.Revision == heldItemRevision + 1U
                    && !holder.HasItem,
                10f,
                $"p2_battery_restore_not_committed ship={shipState.IsPowerEnabled}/" +
                $"{shipState.IsGravityEnabled}/{shipState.IsBatteryInstalled} " +
                $"held={itemRecord.HeldItemId} revision={itemRecord.Revision}/{heldItemRevision + 1U}");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => !HasActiveAccident(
                    accidentCoordinator,
                    powerFailureInstanceId,
                    (int)PHSShipAccidentId.PowerFailure),
                5f,
                "p2_power_failure_not_resolved");
            if (scenarioFinished) yield break;

            var consumedItemRevision = itemRecord.Revision;
            if (itemRecord.TryConsumeHeldItemServer(validationBatteryItem.ItemId, heldItemRevision)
                || itemRecord.Revision != consumedItemRevision)
            {
                Fail(
                    $"p2_battery_consumed_more_than_once heldRevision={heldItemRevision} " +
                    $"currentRevision={itemRecord.Revision}");
                yield break;
            }

            var restoredShipRevision = shipState.Revision;
            if (shipState.TryRestorePowerWithBattery(out var repeatedRestoreReason)
                || repeatedRestoreReason != "power_already_restored"
                || shipState.Revision != restoredShipRevision)
            {
                Fail(
                    $"p2_duplicate_restore_not_rejected reason={repeatedRestoreReason ?? "none"} " +
                    $"revision={shipState.Revision}/{restoredShipRevision}");
                yield break;
            }

            yield return ProbeShipPowerState(
                restoredShipRevision,
                consumedItemRevision);
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P2_SHIP_POWER_BATTERY_OK item={validationBatteryItem.ItemId} " +
                $"shipRevision={restoredShipRevision} itemRevision={consumedItemRevision} " +
                $"peers={shipPowerReports.Count} blackoutRestored=true " +
                $"duplicateReason={repeatedRestoreReason}",
                this);
        }

        private IEnumerator ProbeShipPowerVisualState(bool expectedBlackout)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (scenarioRunning && !scenarioFinished
                && Time.realtimeSinceStartup < deadline)
            {
                shipPowerReports.Clear();
                var token = ++activeProbeToken;
                ProbeShipPowerStateClientRpc(token);

                var probeDeadline = Mathf.Min(
                    deadline,
                    Time.realtimeSinceStartup + 2f);
                while (shipPowerReports.Count < expectedClientCount
                    && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (shipPowerReports.Count >= expectedClientCount
                    && shipPowerReports.Values.All(report =>
                        report.StateFound
                        && report.LightingFound
                        && report.BlackoutApplied == expectedBlackout
                        && report.EmergencyLightingActive == expectedBlackout
                        && Mathf.Abs(
                            report.AmbientIntensityRatio
                            - (expectedBlackout ? 0.12f : 1f)) <= 0.03f))
                {
                    Debug.Log(
                        $"PHS_P2_SHIP_POWER_VISUAL_OK blackout={expectedBlackout} " +
                        $"peers={shipPowerReports.Count} ambientRatio=" +
                        $"{shipPowerReports.Values.First().AmbientIntensityRatio:F3}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var reports = shipPowerReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    shipPowerReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:found={report.Value.LightingFound}," +
                        $"blackout={report.Value.BlackoutApplied}," +
                        $"emergency={report.Value.EmergencyLightingActive}," +
                        $"ambient={report.Value.AmbientIntensityRatio:F3}"));
            Fail(
                $"p2_ship_power_visual_sync_timeout blackout={expectedBlackout} " +
                $"reports={reports}");
        }

        private IEnumerator ProbeShipPowerState(uint expectedShipRevision, uint expectedItemRevision)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                shipPowerReports.Clear();
                var token = ++activeProbeToken;
                ProbeShipPowerStateClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (shipPowerReports.Count < expectedClientCount
                    && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (shipPowerReports.Count >= expectedClientCount
                    && shipPowerReports.Values.All(report =>
                        report.StateFound
                        && report.PowerEnabled
                        && report.GravityEnabled
                        && report.BatteryInstalled
                        && report.ShipRevision == expectedShipRevision
                        && report.ItemRevision == expectedItemRevision
                        && string.IsNullOrEmpty(report.HeldItemId)
                        && !report.PowerOffActive
                        && report.LightingFound
                        && !report.BlackoutApplied
                        && !report.EmergencyLightingActive
                        && Mathf.Abs(report.AmbientIntensityRatio - 1f) <= 0.03f))
                {
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var reports = shipPowerReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    shipPowerReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:found={report.Value.StateFound}," +
                        $"power={report.Value.PowerEnabled},gravity={report.Value.GravityEnabled}," +
                        $"battery={report.Value.BatteryInstalled},shipRev={report.Value.ShipRevision}," +
                        $"itemRev={report.Value.ItemRevision},held={report.Value.HeldItemId}," +
                        $"powerOff={report.Value.PowerOffActive}," +
                        $"lighting={report.Value.LightingFound},blackout={report.Value.BlackoutApplied}," +
                        $"emergency={report.Value.EmergencyLightingActive}," +
                        $"ambient={report.Value.AmbientIntensityRatio:F3}"));
            Fail(
                $"p2_ship_power_peer_sync_timeout expectedShipRevision={expectedShipRevision} " +
                $"expectedItemRevision={expectedItemRevision} reports={reports}");
        }

        private IEnumerator ValidateEventLifecycle(
            NetworkEventCoordinator coordinator,
            EventManager manager,
            EventId eventId)
        {
            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null)
            {
                Fail($"event_ship_systems_missing event={eventId}");
                yield break;
            }

            var shipRevisionBeforeEffect = shipSystems.Revision;
            if (coordinator.IsEventActive(eventId) || manager.IsActive(eventId))
            {
                Fail($"event_preexisting_active event={eventId}");
                yield break;
            }

            if (!coordinator.TrySpawnEventServer(eventId, out var instanceId) || instanceId == 0UL)
            {
                Fail($"event_server_spawn_failed event={eventId}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.TryGetSnapshot(instanceId, out var snapshot) &&
                    snapshot.EventId == eventId &&
                    snapshot.State == EventState.InProgress,
                5f,
                $"event_snapshot_not_in_progress event={eventId} instance={instanceId}");
            if (scenarioFinished) yield break;

            yield return ProbeEventSnapshot(eventId, instanceId);
            if (scenarioFinished) yield break;

            if (shipSystems.Revision <= shipRevisionBeforeEffect)
            {
                Fail(
                    $"event_ship_impact_missing event={eventId} before={shipRevisionBeforeEffect} after={shipSystems.Revision}");
                yield break;
            }

            var activeSnapshot = eventSnapshotReports.Values.First();
            activeObservedInstanceId = instanceId;
            eventObservationReadyClients.Clear();
            BeginEventLifecycleObservationClientRpc(instanceId);
            yield return WaitFor(
                () => eventObservationReadyClients.Count >= expectedClientCount,
                5f,
                $"event_observation_not_ready event={eventId} instance={instanceId}");
            if (scenarioFinished) yield break;

            if (eventId == EventId.Fire || eventId == EventId.OxygenLeak)
            {
                yield return ValidateServerAuthoritativeEventRepair(coordinator, eventId, instanceId);
                if (scenarioFinished) yield break;
            }
            else if (!coordinator.TryTerminateAllServer())
            {
                Fail($"event_server_terminate_failed event={eventId} instance={instanceId}");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.75f);
            yield return ProbeEventTerminal(instanceId, activeSnapshot.Revision);
            if (scenarioFinished) yield break;

            if (coordinator.TryGetSnapshot(instanceId, out _) || manager.IsInstanceActive(instanceId))
            {
                Fail($"event_terminal_not_removed event={eventId} instance={instanceId}");
                yield break;
            }

            Debug.Log(
                $"PHS_P0_EVENT_OK event={eventId} instance={instanceId} room={activeSnapshot.RoomId} " +
                $"activeRevision={activeSnapshot.Revision} terminalRevision={eventTerminalReports.Values.First().TerminalRevision} " +
                $"peers={eventSnapshotReports.Count} shipImpactRevision={shipSystems.Revision}",
                this);
        }

        private IEnumerator ValidateServerAuthoritativeEventRepair(
            NetworkEventCoordinator coordinator,
            EventId eventId,
            ulong instanceId)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(
                    NetworkManager.ServerClientId,
                    out var hostClient)
                || hostClient.PlayerObject == null)
            {
                Fail($"event_repair_host_missing event={eventId}");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var itemRecord = playerObject.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null
                || !coordinator.TryGetRepairTargetServer(instanceId, out var repairTarget))
            {
                Fail($"event_repair_contract_missing event={eventId} instance={instanceId}");
                yield break;
            }

            var originalPosition = playerObject.transform.position;
            if (eventId == EventId.OxygenLeak)
            {
                yield return ValidateOxygenSuffocation(
                    playerObject,
                    repairTarget);
                if (scenarioFinished) yield break;
            }

            var requiredItemId = eventId == EventId.Fire ? "fire_extinguisher" : "wrench";
            var wrongItemId = eventId == EventId.Fire ? "wrench" : "fire_extinguisher";
            itemRecord.ReportHeldItem(wrongItemId, 100);
            if (coordinator.RequestEffectRepair(
                    repairTarget,
                    itemRecord,
                    NextEventRepairRequestSequence()))
            {
                Fail($"event_repair_wrong_item_accepted event={eventId}");
                yield break;
            }

            itemRecord.ReportHeldItem(requiredItemId, 100);
            SetPlayerPosition(playerObject, repairTarget is IEventRepairableEffect serverTarget
                ? serverTarget.RepairPosition + Vector3.right * 10f
                : originalPosition + Vector3.right * 10f);
            if (coordinator.RequestEffectRepair(
                    repairTarget,
                    itemRecord,
                    NextEventRepairRequestSequence()))
            {
                Fail($"event_repair_far_request_accepted event={eventId}");
                yield break;
            }

            var targetPosition = repairTarget is IEventRepairableEffect authoritativeTarget
                ? authoritativeTarget.RepairPosition
                : originalPosition;
            SetPlayerPosition(playerObject, targetPosition);
            var requestSequence = NextEventRepairRequestSequence();
            var acceptedStepCount = 1U;
            if (!coordinator.RequestEffectRepair(repairTarget, itemRecord, requestSequence))
            {
                Fail($"event_repair_first_step_rejected event={eventId}");
                yield break;
            }

            if (coordinator.RequestEffectRepair(repairTarget, itemRecord, requestSequence))
            {
                Fail($"event_repair_duplicate_sequence_accepted event={eventId}");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + 10f;
            while (coordinator.IsEventActive(eventId) && Time.realtimeSinceStartup < deadline)
            {
                requestSequence = NextEventRepairRequestSequence();
                if (!coordinator.RequestEffectRepair(repairTarget, itemRecord, requestSequence))
                {
                    Fail(
                        $"event_repair_step_rejected event={eventId} sequence={requestSequence}");
                    yield break;
                }

                acceptedStepCount++;
                yield return null;
            }

            itemRecord.ReportHeldItem(string.Empty, 0);
            SetPlayerPosition(playerObject, originalPosition);
            if (coordinator.IsEventActive(eventId))
            {
                Fail($"event_repair_timeout event={eventId} sequence={requestSequence}");
                yield break;
            }

            Debug.Log(
                $"PHS_P1_EVENT_REPAIR_OK event={eventId} instance={instanceId} item={requiredItemId} " +
                $"steps={acceptedStepCount} wrongItemReject=true farReject=true duplicateReject=true authority=server",
                this);
        }

        private IEnumerator ProbeEventSnapshot(
            EventId eventId,
            ulong instanceId,
            bool requireHostLocalEffect = true)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                eventSnapshotReports.Clear();
                var token = ++activeProbeToken;
                ProbeEventSnapshotClientRpc(token, eventId, instanceId);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (eventSnapshotReports.Count < expectedClientCount &&
                    Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (eventSnapshotReports.Count >= expectedClientCount &&
                    eventSnapshotReports.TryGetValue(NetworkManager.ServerClientId, out var hostReport))
                {
                    var first = eventSnapshotReports.Values.First();
                    var snapshotsMatch = first.Found &&
                        first.InstanceId == instanceId &&
                        first.EventId == eventId &&
                        first.State == EventState.InProgress &&
                        first.Revision >= 2U &&
                        !string.IsNullOrWhiteSpace(first.RoomId) &&
                        eventSnapshotReports.Values.All(report =>
                            report.Found &&
                            report.InstanceId == first.InstanceId &&
                            report.EventId == first.EventId &&
                            report.RoomId == first.RoomId &&
                            report.State == first.State &&
                            report.Revision == first.Revision);
                    var remoteClean = eventSnapshotReports
                        .Where(pair => pair.Key != NetworkManager.ServerClientId)
                        .All(pair => !pair.Value.LocalEventActive && pair.Value.LocalEffectCount == 0);
                    var hostExecutionValid = hostReport.LocalEventActive &&
                        (!requireHostLocalEffect || hostReport.LocalEffectCount > 0);
                    var effectReplicationValid = !requireHostLocalEffect
                        || hostReport.NetworkEffectCount > 0
                        && hostReport.MirrorEffectCount == 0
                        && eventSnapshotReports.Values.All(report =>
                            report.NetworkEffectCount == hostReport.NetworkEffectCount)
                        && eventSnapshotReports
                            .Where(pair => pair.Key != NetworkManager.ServerClientId)
                            .All(pair => pair.Value.MirrorEffectCount > 0);
                    if (snapshotsMatch && hostExecutionValid && remoteClean && effectReplicationValid)
                    {
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            var details = eventSnapshotReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    eventSnapshotReports.Select(pair =>
                        $"{pair.Key}:found={pair.Value.Found},active={pair.Value.LocalEventActive}," +
                        $"effects={pair.Value.LocalEffectCount},netEffects={pair.Value.NetworkEffectCount}," +
                        $"mirrors={pair.Value.MirrorEffectCount},state={pair.Value.State},rev={pair.Value.Revision}"));
            Fail(
                $"event_peer_snapshot_mismatch event={eventId} instance={instanceId} " +
                $"requireHostLocalEffect={requireHostLocalEffect} reports={details}");
        }

        private IEnumerator ProbeEventTerminal(ulong instanceId, uint activeRevision)
        {
            eventTerminalReports.Clear();
            var token = ++activeProbeToken;
            ProbeEventTerminalClientRpc(token, instanceId);
            yield return WaitFor(
                () => eventTerminalReports.Count >= expectedClientCount,
                5f,
                $"event_terminal_reports_missing instance={instanceId}");
            if (scenarioFinished) yield break;

            var first = eventTerminalReports.Values.First();
            if (!first.ObservedTerminal || !first.ObservedRemoved || first.TerminalRevision <= activeRevision ||
                eventTerminalReports.Values.Any(report =>
                    !report.ObservedTerminal ||
                    !report.ObservedRemoved ||
                    report.TerminalRevision != first.TerminalRevision))
            {
                Fail(
                    $"event_terminal_peer_mismatch instance={instanceId} activeRevision={activeRevision} " +
                    $"reports={string.Join(";", eventTerminalReports.Select(pair => $"{pair.Key}:terminal={pair.Value.ObservedTerminal},removed={pair.Value.ObservedRemoved},rev={pair.Value.TerminalRevision}"))}");
            }
        }

        private IEnumerator ProbeFarEventTerminalRejection(
            NetworkEventCoordinator coordinator,
            EventManager manager)
        {
            var terminal = FindObjectsByType<PHSFinalMiniGameTerminal>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.ConfiguredEventId == EventId.MeteorAttack);
            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (terminal == null || remoteClientId == NetworkManager.ServerClientId ||
                !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient) ||
                remoteClient.PlayerObject == null)
            {
                Fail("far_event_terminal_probe_setup_missing");
                yield break;
            }

            if (!coordinator.TrySpawnEventServer(EventId.MeteorAttack, out _))
            {
                Fail("far_event_terminal_event_spawn_failed");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.IsEventActive(EventId.MeteorAttack)
                    && manager.IsActive(EventId.MeteorAttack),
                5f,
                "far_event_terminal_event_not_active");
            if (scenarioFinished) yield break;

            var playerObject = remoteClient.PlayerObject;
            var originalPosition = playerObject.transform.position;
            var originalRotation = playerObject.transform.rotation;
            SetPlayerPosition(
                playerObject,
                terminal.transform.position + Vector3.up * 100f + Vector3.forward * 100f);

            farEventTerminalProbeReported = false;
            farEventTerminalRequestIssued = false;
            var token = ++activeProbeToken;
            ProbeFarEventTerminalClientRpc(
                token,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                });

            yield return WaitFor(
                () => farEventTerminalProbeReported,
                5f,
                "far_event_terminal_client_probe_timeout");
            if (scenarioFinished) yield break;

            yield return new WaitForSecondsRealtime(1f);
            SetPlayerPosition(playerObject, originalPosition, originalRotation);

            if (!farEventTerminalRequestIssued
                || !coordinator.IsEventActive(EventId.MeteorAttack)
                || !manager.IsActive(EventId.MeteorAttack))
            {
                Fail(
                    $"far_event_terminal_not_rejected issued={farEventTerminalRequestIssued} " +
                    $"snapshotActive={coordinator.IsEventActive(EventId.MeteorAttack)} " +
                    $"localActive={manager.IsActive(EventId.MeteorAttack)}");
                yield break;
            }

            if (!coordinator.TryTerminateAllServer())
            {
                Fail("far_event_terminal_cleanup_rejected");
                yield break;
            }

            Debug.Log($"PHS_P0_FAR_EVENT_TERMINAL_REJECTED client={remoteClientId}", this);
        }

        private IEnumerator RunShopRoundTrip(
            NetworkScenePortalInteractable entryPortal,
            int expectedClearedZones,
            int expectedShopCycles,
            NetworkRunPhase expectedShopPhase,
            bool validatePurchaseAtomicity)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var hostClient) ||
                hostClient.PlayerObject == null)
            {
                Fail("shop_entry_player_missing");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var holder = playerObject.GetComponent<TempPlayerItemHolder>();
            if (holder == null)
            {
                Fail("shop_entry_holder_missing");
                yield break;
            }

            if (SceneManager.GetActiveScene().name != ShopSceneName)
            {
                if (entryPortal == null)
                {
                    Fail("shop_entry_portal_missing");
                    yield break;
                }

                SetPlayerPosition(playerObject, entryPortal.transform.position);
                yield return null;
                entryPortal.Interact(holder);
                RequestRemoteShopTransitionVotes();
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == ShopSceneName,
                    30f,
                    "shop_scene_not_loaded");
                if (scenarioFinished) yield break;
            }

            yield return ProbeScenes(ShopSceneName);
            if (scenarioFinished) yield break;

            yield return ProbeShopState();
            if (scenarioFinished) yield break;

            yield return ProbeRunFlowState(
                expectedShopPhase,
                expectedClearedZones,
                expectedShopCycles,
                finalShopPending: expectedShopPhase == NetworkRunPhase.FinalShop);
            if (scenarioFinished) yield break;

            var display = FindAnyObjectByType<ShopRandomDisplayController>(FindObjectsInactive.Include);
            if (display == null)
            {
                Fail($"shop_display_missing cycle={expectedShopCycles}");
                yield break;
            }

            var initialCount = display.DisplayedProductCount;
            var initialSignature = GetShopOfferSignature(display);
            var expectedDeliveryCredits = -1;
            var minimumDeliveredAfterReturn = -1;
            display.PopulateDisplays();
            if (display.DisplayedProductCount != initialCount ||
                GetShopOfferSignature(display) != initialSignature)
            {
                Fail("shop_initial_restock_guard_failed");
                yield break;
            }

            if (validatePurchaseAtomicity)
            {
                var purchaseService = FindAnyObjectByType<ShopPurchaseService>(FindObjectsInactive.Include);
                var deliveryService = SessionPurchaseDeliveryService.Instance;
                var economyLedger = NetworkRunSessionRoot.Instance?.Economy;
                if (purchaseService == null
                    || deliveryService == null
                    || economyLedger == null
                    || !economyLedger.IsSpawned
                    || economyLedger.Revision == 0U)
                {
                    Fail("shop_purchase_services_or_economy_ledger_missing");
                    yield break;
                }

                var product = Enumerable.Range(0, display.SlotCount)
                    .Select(display.GetDisplayedProductAt)
                    .FirstOrDefault(candidate => candidate != null);
                if (product == null || product.PurchasePrice <= 0)
                {
                    Fail("shop_display_product_missing");
                    yield break;
                }

                var originalCredits = purchaseService.AvailableCredits;
                var setupDebit = originalCredits >= product.PurchasePrice
                    ? originalCredits - product.PurchasePrice + 1
                    : 0;
                if (setupDebit > 0
                    && !economyLedger.TrySpendCreditsServer(
                        $"p0:insufficient:debit:{expectedClearedZones}:{economyLedger.Revision}",
                        setupDebit,
                        NetworkRunEconomyTransactionKind.PenaltyDebit,
                        NetworkManager.ServerClientId,
                        out var setupDebitReason))
                {
                    Fail($"shop_insufficient_setup_debit_failed reason={setupDebitReason}");
                    yield break;
                }

                var creditsBeforeFailure = purchaseService.AvailableCredits;
                var pendingBeforeFailure = deliveryService.PendingCount;
                var revisionBeforeFailure = economyLedger.Revision;
                var deliveryEntriesBeforeFailure = economyLedger.DeliveryEntryCount;
                var excessiveRequests = new[]
                {
                    new ShopPurchaseRequest(
                        $"p0_fail_{expectedClearedZones}_{revisionBeforeFailure}",
                        product)
                };
                var failureAccepted = purchaseService.TryPurchase(
                    excessiveRequests,
                    out var failureResult);
                var creditsAfterFailure = purchaseService.AvailableCredits;
                var pendingAfterFailure = deliveryService.PendingCount;
                var revisionAfterFailure = economyLedger.Revision;
                var deliveryEntriesAfterFailure = economyLedger.DeliveryEntryCount;
                var failureWasAtomic =
                    !failureAccepted
                    && !failureResult.Success
                    && failureResult.Reason == "insufficient_credits"
                    && creditsAfterFailure == creditsBeforeFailure
                    && pendingAfterFailure == pendingBeforeFailure
                    && revisionAfterFailure == revisionBeforeFailure
                    && deliveryEntriesAfterFailure == deliveryEntriesBeforeFailure
                    && display.DisplayedProductCount == initialCount
                    && GetShopOfferSignature(display) == initialSignature;

                if (setupDebit > 0
                    && !economyLedger.TryAddCreditsServer(
                        $"p0:insufficient:refund:{expectedClearedZones}:{economyLedger.Revision}",
                        setupDebit,
                        NetworkRunEconomyTransactionKind.RefundCredit,
                        NetworkManager.ServerClientId,
                        out var setupRefundReason))
                {
                    Fail($"shop_insufficient_setup_refund_failed reason={setupRefundReason}");
                    yield break;
                }

                if (!failureWasAtomic || purchaseService.AvailableCredits != originalCredits)
                {
                    Fail(
                        $"shop_insufficient_atomicity_failed accepted={failureAccepted} reason={failureResult.Reason ?? "none"} " +
                        $"credits={creditsAfterFailure}/{creditsBeforeFailure} restored={purchaseService.AvailableCredits}/{originalCredits} " +
                        $"pending={pendingAfterFailure}/{pendingBeforeFailure}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P0_SHOP_INSUFFICIENT_OK credits={creditsBeforeFailure} total={failureResult.TotalPrice}",
                    this);

                var creditsBeforeSuccess = purchaseService.AvailableCredits;
                var pendingBeforeSuccess = deliveryService.PendingCount;
                var revisionBeforeSuccess = economyLedger.Revision;
                var walletRevisionBeforeSuccess = economyLedger.Snapshot.WalletRevision;
                var deliveryRevisionBeforeSuccess = economyLedger.Snapshot.DeliveryRevision;
                var deliveredBeforeSuccess = economyLedger.Snapshot.DeliveredCount;
                var successAccepted = purchaseService.TryPurchase(
                    new[] { new ShopPurchaseRequest("p0_success", product) },
                    out var successResult);
                if (!successAccepted || !successResult.Success || successResult.PurchasedCount != 1 ||
                    purchaseService.AvailableCredits != creditsBeforeSuccess - product.PurchasePrice ||
                    deliveryService.PendingCount != pendingBeforeSuccess + 1 ||
                    economyLedger.Revision != revisionBeforeSuccess + 1U ||
                    economyLedger.Snapshot.WalletRevision != walletRevisionBeforeSuccess + 1U ||
                    economyLedger.Snapshot.DeliveryRevision != deliveryRevisionBeforeSuccess + 1U ||
                    economyLedger.Snapshot.LastTransactionKind
                        != NetworkRunEconomyTransactionKind.PurchaseDebit ||
                    economyLedger.Snapshot.LastTransactionId.ToString() != "p0_success" ||
                    display.DisplayedProductCount != initialCount - 1)
                {
                    Fail(
                        $"shop_success_atomicity_failed accepted={successAccepted} reason={successResult.Reason ?? "none"} " +
                        $"credits={purchaseService.AvailableCredits} pending={deliveryService.PendingCount} " +
                        $"displayed={display.DisplayedProductCount}/{initialCount - 1}");
                    yield break;
                }

                var snapshotBeforeDuplicate = economyLedger.Snapshot;
                var entriesBeforeDuplicate = economyLedger.DeliveryEntryCount;
                var duplicateAccepted = economyLedger.TryCommitPurchaseServer(
                    "p0_duplicate_probe",
                    new[] { "p0_success" },
                    new[] { product.ItemPrefabData.ItemId },
                    product.PurchasePrice,
                    NetworkManager.ServerClientId,
                    out var duplicateReason);
                if (duplicateAccepted
                    || duplicateReason != "purchase_already_committed"
                    || !economyLedger.Snapshot.Equals(snapshotBeforeDuplicate)
                    || economyLedger.DeliveryEntryCount != entriesBeforeDuplicate)
                {
                    Fail(
                        $"shop_purchase_idempotency_failed accepted={duplicateAccepted} reason={duplicateReason ?? "none"} " +
                        $"entries={economyLedger.DeliveryEntryCount}/{entriesBeforeDuplicate}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P0_SHOP_PURCHASE_IDEMPOTENCY_OK purchase=p0_success reason={duplicateReason}",
                    this);

                expectedDeliveryCredits = purchaseService.AvailableCredits;
                minimumDeliveredAfterReturn = deliveredBeforeSuccess + 1;
                yield return ProbeEconomyState(
                    expectedDeliveryCredits,
                    pendingBeforeSuccess + 1,
                    0,
                    deliveredBeforeSuccess,
                    NetworkRunEconomyTransactionKind.PurchaseDebit,
                    "p0_success",
                    "purchase_pending");
                if (scenarioFinished) yield break;

                var postPurchaseSignature = GetShopOfferSignature(display);
                display.PopulateDisplays();
                if (display.DisplayedProductCount != initialCount - 1 ||
                    GetShopOfferSignature(display) != postPurchaseSignature)
                {
                    Fail("shop_post_purchase_restock_guard_failed");
                    yield break;
                }

                yield return ProbeShopState(initialCount - 1);
                if (scenarioFinished) yield break;

                Debug.Log(
                    $"PHS_P0_SHOP_PURCHASE_OK credits={purchaseService.AvailableCredits} " +
                    $"pending={deliveryService.PendingCount} displayed={display.DisplayedProductCount}",
                    this);
            }
            else
            {
                Debug.Log(
                    $"PHS_P0_SHOP_PRESENTATION_OK cycle={expectedShopCycles} " +
                    $"displayed={initialCount} phase={expectedShopPhase}",
                    this);
            }

            if (!TryFindScenePortal(MapSceneName, out var returnPortal))
            {
                Fail("shop_return_portal_missing");
                yield break;
            }

            SetPlayerPosition(playerObject, returnPortal.transform.position);
            yield return null;
            returnPortal.Interact(holder);
            RequestRemoteShopTransitionVotes();
            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                30f,
                "shop_return_map_not_loaded");
            if (scenarioFinished) yield break;

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            if (minimumDeliveredAfterReturn >= 0)
            {
                yield return WaitFor(
                    () =>
                    {
                        var economy = NetworkRunSessionRoot.Instance?.Economy;
                        return economy != null
                            && economy.Snapshot.PendingDeliveryCount == 0
                            && economy.Snapshot.ClaimedDeliveryCount == 0
                            && economy.Snapshot.DeliveredCount >= minimumDeliveredAfterReturn;
                    },
                    10f,
                    $"shop_delivery_not_completed expectedDelivered={minimumDeliveredAfterReturn}");
                if (scenarioFinished) yield break;

                yield return ProbeEconomyState(
                    expectedDeliveryCredits,
                    0,
                    0,
                    minimumDeliveredAfterReturn,
                    NetworkRunEconomyTransactionKind.PurchaseDebit,
                    "p0_success",
                    "purchase_delivered");
                if (scenarioFinished) yield break;
            }

            yield return WaitFor(
                () => NetworkRunFlowCoordinator.Instance != null &&
                    NetworkRunFlowCoordinator.Instance.ClearedZoneCount == expectedClearedZones &&
                    NetworkRunFlowCoordinator.Instance.CompletedShopCycleCount ==
                        (expectedShopPhase == NetworkRunPhase.FinalShop ? 3 : expectedShopCycles) &&
                    (expectedShopPhase == NetworkRunPhase.FinalShop
                        ? NetworkRunFlowCoordinator.Instance.Phase == NetworkRunPhase.Clear
                        : NetworkRunFlowCoordinator.Instance.Phase is NetworkRunPhase.Rearming or NetworkRunPhase.Charging),
                10f,
                $"shop_return_phase_invalid cycle={expectedShopCycles} expectedShopPhase={expectedShopPhase}");
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P0_SHOP_ROUNDTRIP_OK cycle={expectedShopCycles} " +
                $"returnPhase={NetworkRunFlowCoordinator.Instance.Phase}",
                this);
        }

        private IEnumerator ProbeShopState(int expectedDisplayedCount = -1)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                shopStateReports.Clear();
                var token = ++activeProbeToken;
                ProbeShopStateClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (shopStateReports.Count < expectedClientCount && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (shopStateReports.Count >= expectedClientCount)
                {
                    var first = shopStateReports.Values.First();
                    var countValid = expectedDisplayedCount >= 0
                        ? first.DisplayedCount == expectedDisplayedCount
                        : first.DisplayedCount is >= 8 and <= 10;
                    if (countValid && first.GravityMode == NetworkPlayerGravityMode.ShipGravity &&
                        shopStateReports.Values.All(report =>
                            report.DisplayedCount == first.DisplayedCount &&
                            report.OfferSignature == first.OfferSignature &&
                            report.GravityMode == NetworkPlayerGravityMode.ShipGravity))
                    {
                        Debug.Log(
                            $"PHS_P0_SHOP_STATE_OK peers={shopStateReports.Count} displayed={first.DisplayedCount}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Fail($"shop_state_sync_timeout expected={expectedDisplayedCount}");
        }

        private IEnumerator ProbeEconomyState(
            int expectedCredits,
            int expectedPendingCount,
            int expectedClaimedCount,
            int minimumDeliveredCount,
            NetworkRunEconomyTransactionKind expectedTransactionKind,
            string expectedTransactionId,
            string label)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                economyReports.Clear();
                var token = ++activeProbeToken;
                ProbeEconomyStateClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (economyReports.Count < expectedClientCount
                       && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (economyReports.Count >= expectedClientCount)
                {
                    var first = economyReports.Values.First();
                    if (first.Found
                        && first.Credits == expectedCredits
                        && first.Revision > 0U
                        && first.PendingCount == expectedPendingCount
                        && first.ClaimedCount == expectedClaimedCount
                        && first.DeliveredCount >= minimumDeliveredCount
                        && first.LastTransactionKind == expectedTransactionKind
                        && first.LastTransactionId == expectedTransactionId
                        && economyReports.Values.All(report =>
                            report.Found
                            && report.Credits == first.Credits
                            && report.Revision == first.Revision
                            && report.PendingCount == first.PendingCount
                            && report.ClaimedCount == first.ClaimedCount
                            && report.DeliveredCount == first.DeliveredCount
                            && report.LastTransactionKind == first.LastTransactionKind
                            && report.LastTransactionId == first.LastTransactionId))
                    {
                        Debug.Log(
                            $"PHS_P0_ECONOMY_SYNC_OK label={label} peers={economyReports.Count} " +
                            $"credits={first.Credits} revision={first.Revision} pending={first.PendingCount} " +
                            $"claimed={first.ClaimedCount} delivered={first.DeliveredCount} " +
                            $"transaction={first.LastTransactionId} kind={first.LastTransactionKind}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var reports = economyReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    economyReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:found={report.Value.Found},credits={report.Value.Credits}," +
                        $"revision={report.Value.Revision},pending={report.Value.PendingCount}," +
                        $"claimed={report.Value.ClaimedCount},delivered={report.Value.DeliveredCount}," +
                        $"transaction={report.Value.LastTransactionId},kind={report.Value.LastTransactionKind}"));
            Fail(
                $"economy_peer_sync_timeout label={label} credits={expectedCredits} " +
                $"pending={expectedPendingCount} claimed={expectedClaimedCount} " +
                $"minimumDelivered={minimumDeliveredCount} transaction={expectedTransactionId} " +
                $"kind={expectedTransactionKind} reports={reports}");
        }

        private IEnumerator ProbeIncidentState(
            NetworkRunIncidentSnapshot expectedSnapshot,
            int expectedCommandCount,
            ulong expectedCommandSignature)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning
                   && !scenarioFinished
                   && Time.realtimeSinceStartup < deadline)
            {
                incidentReports.Clear();
                var token = ++activeProbeToken;
                ProbeIncidentStateClientRpc(token);

                var probeDeadline = Mathf.Min(
                    deadline,
                    Time.realtimeSinceStartup + 2f);
                while (incidentReports.Count < expectedClientCount
                       && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (incidentReports.Count >= expectedClientCount
                    && incidentReports.Values.All(report =>
                        report.Found
                        && report.Snapshot.Equals(expectedSnapshot)
                        && report.CommandCount == expectedCommandCount
                        && report.CommandSignature
                            == expectedCommandSignature))
                {
                    Debug.Log(
                        $"PHS_P0_INCIDENT_SYNC_OK peers={incidentReports.Count} " +
                        $"stage={expectedSnapshot.StageSequence} " +
                        $"revision={expectedSnapshot.Revision} " +
                        $"commands={expectedCommandCount} " +
                        $"signature={expectedCommandSignature:X16}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            var reports = incidentReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    incidentReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:found={report.Value.Found}," +
                        $"stage={report.Value.Snapshot.StageSequence}," +
                        $"revision={report.Value.Snapshot.Revision}," +
                        $"reserved={report.Value.Snapshot.ReservedPressure}," +
                        $"active={report.Value.Snapshot.ActivePressure}," +
                        $"commands={report.Value.CommandCount}," +
                        $"signature={report.Value.CommandSignature:X16}"));
            Fail(
                $"incident_peer_sync_timeout stage={expectedSnapshot.StageSequence} " +
                $"revision={expectedSnapshot.Revision} commands={expectedCommandCount} " +
                $"signature={expectedCommandSignature:X16} reports={reports}");
        }

        private IEnumerator ProbeRunFlowState(
            NetworkRunPhase expectedPhase,
            int expectedClearedZones,
            int expectedShopCycles,
            bool finalShopPending)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                runFlowReports.Clear();
                var token = ++activeProbeToken;
                ProbeRunFlowStateClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (runFlowReports.Count < expectedClientCount && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (runFlowReports.Count >= expectedClientCount &&
                    runFlowReports.Values.All(report =>
                        report.Phase == expectedPhase &&
                        report.ClearedZoneCount == expectedClearedZones &&
                        report.CompletedShopCycleCount == expectedShopCycles &&
                        report.FinalShopPending == finalShopPending))
                {
                    Debug.Log(
                        $"PHS_P0_RUN_FLOW_SYNC_OK peers={runFlowReports.Count} phase={expectedPhase} " +
                        $"cleared={expectedClearedZones} shopCycles={expectedShopCycles} " +
                        $"finalPending={finalShopPending}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.2f);
            }

            var reports = runFlowReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    runFlowReports.Select(pair =>
                        $"{pair.Key}:phase={pair.Value.Phase},cleared={pair.Value.ClearedZoneCount}," +
                        $"cycles={pair.Value.CompletedShopCycleCount},final={pair.Value.FinalShopPending}"));
            Fail(
                $"run_flow_peer_mismatch expectedPhase={expectedPhase} cleared={expectedClearedZones} " +
                $"cycles={expectedShopCycles} final={finalShopPending} reports={reports}");
        }

        private IEnumerator ProbeRunningStageClock(
            int expectedMapId,
            uint expectedSequence,
            bool captureInitialSequence)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                stageClockReports.Clear();
                var token = ++activeProbeToken;
                ProbeStageClockClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (stageClockReports.Count < expectedClientCount &&
                       Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (TryValidateStageClockReports(
                        NetworkRunStageClockState.Running,
                        expectedMapId,
                        expectedSequence,
                        out var first,
                        out var remainingDelta))
                {
                    if (captureInitialSequence)
                    {
                        initialStageClockSequence = first.StageSequence;
                    }

                    Debug.Log(
                        $"PHS_P0_STAGE_CLOCK_RUNNING_OK peers={stageClockReports.Count} " +
                        $"map={first.MapId} sequence={first.StageSequence} revision={first.Revision} " +
                        $"remainingDelta={remainingDelta:F3}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Fail(
                $"stage_clock_running_sync_timeout expectedMap={expectedMapId} " +
                $"expectedSequence={expectedSequence} reports={DescribeStageClockReports()}");
        }

        private IEnumerator ProbePausedStageClock(int expectedMapId)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            Dictionary<ulong, StageClockReport> baselineReports = null;
            StageClockReport baseline = default;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                stageClockReports.Clear();
                var token = ++activeProbeToken;
                ProbeStageClockClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (stageClockReports.Count < expectedClientCount &&
                       Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (TryValidateStageClockReports(
                        NetworkRunStageClockState.Paused,
                        expectedMapId,
                        initialStageClockSequence,
                        out baseline,
                        out _))
                {
                    baselineReports = stageClockReports.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value);
                    break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            if (baselineReports == null)
            {
                Fail(
                    $"stage_clock_paused_sync_timeout expectedMap={expectedMapId} " +
                    $"expectedSequence={initialStageClockSequence} reports={DescribeStageClockReports()}");
                yield break;
            }

            yield return new WaitForSecondsRealtime(1.5f);
            if (scenarioFinished) yield break;

            deadline = Time.realtimeSinceStartup + 10f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                stageClockReports.Clear();
                var token = ++activeProbeToken;
                ProbeStageClockClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (stageClockReports.Count < expectedClientCount &&
                       Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (TryValidateStageClockReports(
                        NetworkRunStageClockState.Paused,
                        expectedMapId,
                        baseline.StageSequence,
                        out var current,
                        out var peerRemainingDelta)
                    && current.Revision == baseline.Revision
                    && stageClockReports.Count == baselineReports.Count
                    && stageClockReports.Keys.All(baselineReports.ContainsKey))
                {
                    var stableDelta = stageClockReports.Max(pair =>
                        Mathf.Abs(
                            pair.Value.RemainingSeconds -
                            baselineReports[pair.Key].RemainingSeconds));
                    if (stableDelta <= 0.1f)
                    {
                        Debug.Log(
                            $"PHS_P0_STAGE_CLOCK_PAUSED_OK peers={stageClockReports.Count} " +
                            $"map={current.MapId} sequence={current.StageSequence} revision={current.Revision} " +
                            $"peerDelta={peerRemainingDelta:F3} stableDelta={stableDelta:F3}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Fail(
                $"stage_clock_paused_not_stable expectedMap={expectedMapId} " +
                $"sequence={baseline.StageSequence} revision={baseline.Revision} " +
                $"reports={DescribeStageClockReports()}");
        }

        private bool TryValidateStageClockReports(
            NetworkRunStageClockState expectedState,
            int expectedMapId,
            uint expectedSequence,
            out StageClockReport first,
            out float remainingDelta)
        {
            first = default;
            remainingDelta = float.PositiveInfinity;
            if (stageClockReports.Count < expectedClientCount)
            {
                return false;
            }

            first = stageClockReports.Values.First();
            var referenceReport = first;
            if (!referenceReport.Found
                || referenceReport.MapId != expectedMapId
                || referenceReport.StageSequence == 0U
                || referenceReport.Revision == 0U
                || referenceReport.State != expectedState
                || (expectedSequence != 0U && referenceReport.StageSequence != expectedSequence)
                || (expectedState == NetworkRunStageClockState.Running && referenceReport.RemainingSeconds <= 0f)
                || stageClockReports.Values.Any(report =>
                    !report.Found
                    || report.MapId != referenceReport.MapId
                    || report.StageSequence != referenceReport.StageSequence
                    || report.Revision != referenceReport.Revision
                    || report.State != referenceReport.State))
            {
                return false;
            }

            var remainingValues = stageClockReports.Values
                .Select(report => report.RemainingSeconds)
                .ToArray();
            remainingDelta = remainingValues.Max() - remainingValues.Min();
            return remainingDelta <= 1f;
        }

        private string DescribeStageClockReports()
        {
            return stageClockReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    stageClockReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:found={report.Value.Found},map={report.Value.MapId}," +
                        $"sequence={report.Value.StageSequence},revision={report.Value.Revision}," +
                        $"state={report.Value.State},remaining={report.Value.RemainingSeconds:F3}"));
        }

        private static string GetShopOfferSignature(ShopRandomDisplayController display)
        {
            return string.Join(
                "|",
                Enumerable.Range(0, display.SlotCount)
                    .Select(index => display.GetDisplayedProductAt(index)?.OfferId ?? "-"));
        }

        private static bool TryAcquireMapSceneReferences(
            out NetworkRunFlowCoordinator coordinator,
            out NetworkTravelConsoleController console)
        {
            coordinator = NetworkRunFlowCoordinator.Instance;
            console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            return SceneManager.GetActiveScene().name == MapSceneName &&
                coordinator != null &&
                console != null;
        }

        private static bool IsShopEntryReady()
        {
            if (SceneManager.GetActiveScene().name == ShopSceneName)
            {
                return true;
            }

            return TryAcquireMapSceneReferences(out _, out _) &&
                TryFindScenePortal(ShopSceneName, out _);
        }

        private static bool TryFindScenePortal(
            string destinationSceneName,
            out NetworkScenePortalInteractable portal)
        {
            var activeScene = SceneManager.GetActiveScene();
            portal = FindObjectsByType<NetworkScenePortalInteractable>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate.gameObject.scene == activeScene &&
                    candidate.DestinationSceneName == destinationSceneName);
            return portal != null;
        }

        private IEnumerator RunAdditionalWarpCycle(
            NetworkRunFlowCoordinator coordinator,
            int expectedClearedZones)
        {
            yield return WaitFor(
                () => coordinator.Phase == NetworkRunPhase.Charging,
                DefaultStepTimeout,
                $"cycle_charging_not_reached cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            if (initialStageClockSequence != 0U)
            {
                yield return ProbeRunningStageClock(
                    coordinator.ActiveMapId,
                    AdvanceNonZeroSequence(
                        initialStageClockSequence,
                        expectedClearedZones - 1),
                    captureInitialSequence: false);
                if (scenarioFinished) yield break;
            }

            if (coordinator.TrySelectNextZone(expectedClearedZones, out var earlyReason) ||
                earlyReason != "warp_safe_required")
            {
                Fail($"cycle_early_selection_not_blocked cycle={expectedClearedZones} reason={earlyReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.Phase == NetworkRunPhase.WarpReady,
                DefaultStepTimeout,
                $"cycle_warp_ready_not_reached cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            if (!coordinator.TryActivateWarp(
                    NetworkManager.ServerClientId,
                    out var warpSafeEntryReason))
            {
                Fail(
                    $"cycle_warp_safe_entry_failed cycle={expectedClearedZones} " +
                    $"reason={warpSafeEntryReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.Phase == NetworkRunPhase.WarpSafe,
                10f,
                $"cycle_warp_safe_not_reached cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            yield return ProbeMapChoices();
            if (scenarioFinished) yield break;

            if (coordinator.TryActivateWarp(NetworkManager.ServerClientId, out var unsafeReason) ||
                unsafeReason != "next_map_not_selected")
            {
                Fail($"cycle_unsafe_warp_not_rejected cycle={expectedClearedZones} reason={unsafeReason ?? "none"}");
                yield break;
            }

            var choices = mapChoiceReports.Values.First();
            var selectionAccepted = coordinator.TrySelectNextZone(choices.LeftZoneId, out var selectionReason);
            string warpReason = null;
            var warpAccepted = selectionAccepted &&
                coordinator.TryActivateWarp(NetworkManager.ServerClientId, out warpReason);
            if (!selectionAccepted || !warpAccepted)
            {
                Fail(
                    $"cycle_warp_failed cycle={expectedClearedZones} " +
                    $"selection={selectionReason ?? "none"} warp={warpReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.ClearedZoneCount >= expectedClearedZones,
                10f,
                $"cycle_clear_not_recorded cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P0_WARP_CYCLE_OK cycle={expectedClearedZones} zone={choices.LeftZoneId} " +
                $"cleared={coordinator.ClearedZoneCount}",
                this);
        }

        private IEnumerator RunDebrisRoundTrip()
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var hostClient) ||
                hostClient.PlayerObject == null)
            {
                Fail("debris_entry_setup_missing");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var holder = playerObject.GetComponent<TempPlayerItemHolder>();
            var playerController = playerObject.GetComponent<NetworkPlayerController>();
            if (holder == null || playerController == null)
            {
                Fail("debris_entry_holder_missing");
                yield break;
            }

            if (!TryFindLocalDebrisPortals(out var entryPortal, out var returnPortal))
            {
                Fail("debris_local_portal_pair_invalid");
                yield break;
            }

            yield return ValidateRemoteLocalPortal(entryPortal, "entry");
            if (scenarioFinished) yield break;

            SetPlayerPosition(playerObject, entryPortal.transform.position);
            yield return null;
            if (Vector3.Distance(
                    playerObject.transform.position,
                    entryPortal.Destination.position) > 2f)
            {
                playerController.RequestLocalPortalTeleport(entryPortal.name);
            }
            yield return WaitFor(
                () => Vector3.Distance(playerObject.transform.position, entryPortal.Destination.position) <= 2f,
                10f,
                "debris_local_entry_teleport_failed");
            if (scenarioFinished) yield break;

            if (SceneManager.GetActiveScene().name != MapSceneName)
            {
                Fail($"debris_local_entry_scene_changed scene={SceneManager.GetActiveScene().name}");
                yield break;
            }

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            Debug.Log("PHS_P0_DEBRIS_LOCAL_ENTRY_OK scene=PHS_Map_ver1", this);

            yield return ValidatePhysicalDebrisSale(playerObject, holder);
            if (scenarioFinished) yield break;

            yield return ValidateRemoteLocalPortal(returnPortal, "return");
            if (scenarioFinished) yield break;

            SetPlayerPosition(playerObject, returnPortal.transform.position);
            yield return null;
            if (Vector3.Distance(
                    playerObject.transform.position,
                    returnPortal.Destination.position) > 2f)
            {
                playerController.RequestLocalPortalTeleport(returnPortal.name);
            }
            yield return WaitFor(
                () => Vector3.Distance(playerObject.transform.position, returnPortal.Destination.position) <= 2f,
                10f,
                "debris_local_return_teleport_failed");
            if (scenarioFinished) yield break;

            if (SceneManager.GetActiveScene().name != MapSceneName)
            {
                Fail($"debris_local_return_scene_changed scene={SceneManager.GetActiveScene().name}");
                yield break;
            }

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            Debug.Log($"PHS_P0_DEBRIS_ROUNDTRIP_OK peers={expectedClientCount}", this);
        }

        private IEnumerator RunThrownItemNetworkValidation()
        {
            if (validationThrownItem == null
                || !validationThrownItem.HasHeldPrefab
                || !validationThrownItem.HasDroppedPrefab
                || string.IsNullOrWhiteSpace(validationThrownItem.ItemId)
                || !NetworkManager.ConnectedClients.TryGetValue(
                    NetworkManager.ServerClientId,
                    out var hostClient)
                || hostClient.PlayerObject == null)
            {
                Fail("thrown_item_validation_setup_missing");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var holder = playerObject.GetComponent<TempPlayerItemHolder>();
            if (holder == null)
            {
                Fail("thrown_item_holder_missing");
                yield break;
            }

            holder.ReplaceHeldItem(validationThrownItem, playerObject.transform);
            yield return null;
            if (!holder.IsHoldingItem(validationThrownItem.ItemId))
            {
                Fail($"thrown_item_hold_failed item={validationThrownItem.ItemId}");
                yield break;
            }

            var spawnPosition = playerObject.transform.position
                + playerObject.transform.forward * 2f
                + Vector3.up;
            if (!holder.TryCreateThrownItem(
                    spawnPosition,
                    playerObject.transform.rotation,
                    out var thrownItem)
                || thrownItem == null)
            {
                Fail($"thrown_item_create_failed item={validationThrownItem.ItemId}");
                yield break;
            }

            var networkObject = thrownItem.GetComponent<NetworkObject>();
            if (networkObject == null
                || !networkObject.IsSpawned
                || thrownItem.GetComponent<UtilityItemObject>() == null
                || thrownItem.GetComponent<Rigidbody>() == null
                || thrownItem.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null
                || thrownItem.GetComponent<ThrownItemImpact>() == null)
            {
                Fail($"thrown_item_contract_invalid item={validationThrownItem.ItemId}");
                yield break;
            }

            thrownItemReports.Clear();
            var token = ++activeProbeToken;
            ProbeThrownItemClientRpc(token, networkObject.NetworkObjectId, validationThrownItem.ItemId);
            yield return WaitFor(
                () => thrownItemReports.Count >= expectedClientCount,
                10f,
                "thrown_item_peer_probe_timeout");
            if (scenarioFinished) yield break;

            if (thrownItemReports.Values.Any(valid => !valid))
            {
                Fail($"thrown_item_peer_contract_invalid item={validationThrownItem.ItemId}");
                yield break;
            }

            var networkObjectId = networkObject.NetworkObjectId;
            networkObject.Despawn(true);
            Debug.Log(
                $"PHS_P0_THROW_NETWORK_OK item={validationThrownItem.ItemId} " +
                $"networkObjectId={networkObjectId} peers={thrownItemReports.Count}",
                this);

            yield return RunRemoteOwnedThrownItemValidation(validationThrownItem, false);
        }

        private IEnumerator RunRemoteOwnedThrownItemValidation(
            UtilityItemPrefabData itemData,
            bool exercisePrimaryUse)
        {
            if (itemData == null)
            {
                Fail("remote_item_data_missing");
                yield break;
            }

            var expectedDurability = itemData.HasDurability
                ? itemData.MaxDurability
                : 0;

            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (remoteClientId == NetworkManager.ServerClientId
                || !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient)
                || remoteClient.PlayerObject == null)
            {
                Fail("remote_item_owner_missing");
                yield break;
            }

            var remotePlayer = remoteClient.PlayerObject;
            var remoteLifecycle = remotePlayer.GetComponent<NetworkPlayerItemLifecycle>();
            var remoteRecord = remotePlayer.GetComponent<NetworkPlayerItemRecord>();
            if (remoteLifecycle == null
                || remoteRecord == null
                || !remoteRecord.IsSpawned
                || !string.IsNullOrEmpty(remoteRecord.HeldItemId))
            {
                Fail(
                    $"remote_item_player_contract_invalid client={remoteClientId} " +
                    $"held={remoteRecord?.HeldItemId ?? "record_missing"}");
                yield break;
            }

            var gameplayContext = GameplaySceneContext.FindForScene(SceneManager.GetActiveScene());
            if (gameplayContext == null
                || !gameplayContext.TryGetSpawnPoint(
                    remoteClientId,
                    out var expectedSpawnPoint,
                    out _)
                || expectedSpawnPoint == null)
            {
                Fail($"remote_item_spawn_point_missing client={remoteClientId}");
                yield break;
            }

            yield return WaitFor(
                () => Vector3.Distance(remotePlayer.transform.position, expectedSpawnPoint.position) <= 1f,
                10f,
                "remote_item_server_spawn_not_ready");
            if (scenarioFinished) yield break;

            activeRemoteItemClientId = remoteClientId;
            var positionSynchronized = false;
            var positionProbeDeadline = Time.realtimeSinceStartup + 10f;
            while (!positionSynchronized
                && Time.realtimeSinceStartup < positionProbeDeadline)
            {
                remoteItemPositionReported = false;
                remoteItemPosition = default;
                var positionProbeToken = ++activeProbeToken;
                ProbeRemoteItemPositionClientRpc(
                    positionProbeToken,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                    });

                var responseDeadline = Time.realtimeSinceStartup + 2f;
                while (!remoteItemPositionReported
                    && Time.realtimeSinceStartup < responseDeadline)
                {
                    yield return null;
                }

                positionSynchronized = remoteItemPositionReported
                    && Vector3.Distance(
                        remotePlayer.transform.position,
                        remoteItemPosition) <= 1f;
                if (!positionSynchronized)
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                }
            }

            Debug.Log(
                $"PHS_ITEM_REMOTE_POSITION client={remoteClientId} " +
                $"server={remotePlayer.transform.position} owner={remoteItemPosition} " +
                $"distance={Vector3.Distance(remotePlayer.transform.position, remoteItemPosition):F3}",
                this);

            if (!positionSynchronized)
            {
                Fail(remoteItemPositionReported
                    ? "remote_item_position_not_synchronized"
                    : "remote_item_position_probe_timeout");
                yield break;
            }

            var pickupPosition = remoteItemPosition + Vector3.up * 0.5f;
            if (!remoteLifecycle.TryCreateDroppedItemServer(
                    itemData.ItemId,
                    pickupPosition,
                    remotePlayer.transform.rotation,
                    out var pickupNetworkObject)
                || pickupNetworkObject == null
                || !pickupNetworkObject.IsSpawned)
            {
                Fail($"remote_item_pickup_spawn_failed client={remoteClientId}");
                yield break;
            }

            remoteItemRequestReported = false;
            remoteItemRequestIssued = false;
            var pickupNetworkObjectId = pickupNetworkObject.NetworkObjectId;
            var requestToken = ++activeProbeToken;
            RequestRemoteItemPickupClientRpc(
                requestToken,
                pickupNetworkObjectId,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                });
            yield return WaitFor(
                () => remoteItemRequestReported,
                10f,
                "remote_item_pickup_request_timeout");
            if (scenarioFinished) yield break;

            if (!remoteItemRequestIssued)
            {
                Fail($"remote_item_pickup_request_not_issued client={remoteClientId}");
                yield break;
            }

            yield return WaitFor(
                () => string.Equals(
                        remoteRecord.HeldItemId,
                        itemData.ItemId,
                        StringComparison.Ordinal)
                    && remoteRecord.CurrentDurability == expectedDurability
                    && !NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(
                        pickupNetworkObjectId),
                10f,
                "remote_item_pickup_not_committed");
            if (scenarioFinished) yield break;

            remoteHeldItemReports.Clear();
            var heldProbeToken = ++activeProbeToken;
            ProbeRemoteHeldItemClientRpc(
                heldProbeToken,
                remoteClientId,
                pickupNetworkObjectId,
                itemData.ItemId,
                expectedDurability);
            yield return WaitFor(
                () => remoteHeldItemReports.Count >= expectedClientCount,
                10f,
                "remote_item_held_peer_probe_timeout");
            if (scenarioFinished) yield break;

            if (remoteHeldItemReports.Values.Any(valid => !valid))
            {
                Fail($"remote_item_held_peer_contract_invalid client={remoteClientId}");
                yield break;
            }

            if (exercisePrimaryUse)
            {
                var knownPrimaryUseNetworkObjectIds =
                    NetworkManager.SpawnManager.SpawnedObjects.Keys.ToHashSet();
                remotePrimaryUseRequestReported = false;
                remotePrimaryUseRequestIssued = false;
                var primaryUseToken = ++activeProbeToken;
                RequestRemoteItemPrimaryUseClientRpc(
                    primaryUseToken,
                    itemData.ItemId,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                    });
                yield return WaitFor(
                    () => remotePrimaryUseRequestReported,
                    10f,
                    $"remote_item_primary_use_request_timeout:{itemData.ItemId}");
                if (scenarioFinished) yield break;

                if (!remotePrimaryUseRequestIssued)
                {
                    Fail($"remote_item_primary_use_not_issued item={itemData.ItemId}");
                    yield break;
                }

                if (string.Equals(itemData.ItemId, "battery_pack", StringComparison.Ordinal))
                {
                    NetworkObject usedBatteryNetworkObject = null;
                    yield return WaitFor(
                        () => string.IsNullOrEmpty(remoteRecord.HeldItemId)
                            && remoteRecord.CurrentDurability == 0
                            && TryFindNewSpawnedUtilityItem(
                                knownPrimaryUseNetworkObjectIds,
                                itemData.ItemId,
                                out usedBatteryNetworkObject),
                        10f,
                        "remote_battery_primary_use_not_committed");
                    if (scenarioFinished) yield break;

                    var durabilityState = usedBatteryNetworkObject
                        .GetComponent<NetworkUtilityItemDurabilityState>();
                    var batteryImpact = usedBatteryNetworkObject
                        .GetComponent<BatteryThrownImpact>();
                    if (durabilityState == null
                        || durabilityState.CurrentDurability != expectedDurability
                        || batteryImpact == null
                        || batteryImpact.WasAttackThrow
                        || batteryImpact.HasExploded)
                    {
                        Fail("remote_battery_primary_use_contract_invalid");
                        yield break;
                    }

                    yield return new WaitForSecondsRealtime(0.5f);
                    if (!usedBatteryNetworkObject.IsSpawned
                        || durabilityState.CurrentDurability != expectedDurability
                        || batteryImpact.HasExploded)
                    {
                        Fail("remote_battery_safe_place_exploded");
                        yield break;
                    }

                    var usedBatteryNetworkObjectId = usedBatteryNetworkObject.NetworkObjectId;
                    usedBatteryNetworkObject.Despawn(true);
                    activeRemoteItemClientId = ulong.MaxValue;
                    Debug.Log(
                        $"PHS_ITEM_BATTERY_SAFE_USE_OK client={remoteClientId} " +
                        $"item={itemData.ItemId} durability={expectedDurability} " +
                        $"networkObjectId={usedBatteryNetworkObjectId}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
                if (!string.Equals(remoteRecord.HeldItemId, itemData.ItemId, StringComparison.Ordinal)
                    || remoteRecord.CurrentDurability != expectedDurability)
                {
                    Fail($"remote_item_primary_use_state_invalid item={itemData.ItemId}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_ITEM_REMOTE_PRIMARY_USE_OK client={remoteClientId} " +
                    $"item={itemData.ItemId} durability={remoteRecord.CurrentDurability}",
                    this);
            }

            var isBatteryThrow = string.Equals(
                itemData.ItemId,
                "battery_pack",
                StringComparison.Ordinal);
            NetworkObject batteryReactionTarget = null;
            Vector3 batteryReactionTargetOriginalPosition = default;
            StatusEffectController batteryStatusReceiver = null;
            NetworkPlayerKnockbackReceiver batteryKnockbackReceiver = null;
            uint batteryKnockbackCountBefore = 0;
            if (isBatteryThrow)
            {
                var remoteCombat = remoteClient.PlayerObject
                    .GetComponent<NetworkPlayerCombatController>();
                batteryReactionTarget = NetworkManager.ConnectedClients[
                    NetworkManager.ServerClientId].PlayerObject;
                batteryStatusReceiver = batteryReactionTarget == null
                    ? null
                    : batteryReactionTarget.GetComponent<StatusEffectController>();
                batteryKnockbackReceiver = batteryReactionTarget == null
                    ? null
                    : batteryReactionTarget
                        .GetComponent<NetworkPlayerKnockbackReceiver>();
                if (remoteCombat == null
                    || remoteCombat.GeneralThrowOrigin == null
                    || batteryReactionTarget == null
                    || batteryStatusReceiver == null
                    || batteryKnockbackReceiver == null)
                {
                    Fail("battery_player_reaction_setup_missing");
                    yield break;
                }

                batteryReactionTargetOriginalPosition =
                    batteryReactionTarget.transform.position;
                batteryKnockbackCountBefore =
                    batteryKnockbackReceiver.AppliedCount;
                SetPlayerPosition(
                    batteryReactionTarget,
                    remoteClient.PlayerObject.transform.position
                    + remoteCombat.GeneralThrowOrigin.forward * 1.5f);
                yield return new WaitForSecondsRealtime(0.25f);
            }

            var knownNetworkObjectIds = NetworkManager.SpawnManager.SpawnedObjects.Keys.ToHashSet();
            remoteThrowRequestReported = false;
            remoteThrowRequestIssued = false;
            var throwRequestToken = ++activeProbeToken;
            RequestRemoteItemThrowClientRpc(
                throwRequestToken,
                itemData.ItemId,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                });
            yield return WaitFor(
                () => remoteThrowRequestReported,
                10f,
                "remote_item_throw_request_timeout");
            if (scenarioFinished) yield break;

            if (!remoteThrowRequestIssued)
            {
                Fail($"remote_item_throw_request_not_issued client={remoteClientId}");
                yield break;
            }

            NetworkObject remoteThrownNetworkObject = null;
            yield return WaitFor(
                () => string.IsNullOrEmpty(remoteRecord.HeldItemId)
                    && TryFindNewSpawnedUtilityItem(
                        knownNetworkObjectIds,
                        itemData.ItemId,
                        out remoteThrownNetworkObject),
                10f,
                "remote_item_throw_not_committed");
            if (scenarioFinished) yield break;

            if (isBatteryThrow)
            {
                var batteryImpact = remoteThrownNetworkObject
                    .GetComponent<BatteryThrownImpact>();
                if (batteryImpact == null || !batteryImpact.WasAttackThrow)
                {
                    Fail("battery_attack_throw_not_armed");
                    yield break;
                }

                yield return WaitFor(
                    () => batteryImpact.HasExploded
                        && batteryStatusReceiver.IsShocked
                        && batteryKnockbackReceiver.AppliedCount
                            > batteryKnockbackCountBefore,
                    5f,
                    "battery_player_reaction_timeout");
                if (scenarioFinished) yield break;

                SetPlayerPosition(
                    batteryReactionTarget,
                    batteryReactionTargetOriginalPosition);
                Debug.Log(
                    $"PHS_P0_BATTERY_PLAYER_REACTION_OK " +
                    $"target={batteryReactionTarget.name} " +
                    $"shocked={batteryStatusReceiver.IsShocked} " +
                    $"knockbacks={batteryKnockbackReceiver.AppliedCount}",
                    this);
            }

            thrownItemReports.Clear();
            var throwProbeToken = ++activeProbeToken;
            var expectedThrownDurability = isBatteryThrow
                ? 0
                : expectedDurability;
            ProbeRemoteThrownItemClientRpc(
                throwProbeToken,
                remoteClientId,
                remoteThrownNetworkObject.NetworkObjectId,
                itemData.ItemId,
                itemData.HasDurability,
                expectedThrownDurability);
            yield return WaitFor(
                () => thrownItemReports.Count >= expectedClientCount,
                10f,
                "remote_item_throw_peer_probe_timeout");
            if (scenarioFinished) yield break;

            if (thrownItemReports.Values.Any(valid => !valid))
            {
                Fail($"remote_item_throw_peer_contract_invalid client={remoteClientId}");
                yield break;
            }

            var thrownNetworkObjectId = remoteThrownNetworkObject.NetworkObjectId;
            remoteThrownNetworkObject.Despawn(true);
            activeRemoteItemClientId = ulong.MaxValue;
            Debug.Log(
                $"PHS_P0_REMOTE_ITEM_OWNERSHIP_OK client={remoteClientId} " +
                $"item={itemData.ItemId} pickupNetworkObjectId={pickupNetworkObjectId} " +
                $"thrownNetworkObjectId={thrownNetworkObjectId} peers={thrownItemReports.Count} " +
                $"heldVisualNetworkObjects=0 durability={expectedThrownDurability} " +
                $"recordRevision={remoteRecord.Revision}",
                this);
        }

        private IEnumerator ValidateOxygenSuffocation(
            NetworkObject playerObject,
            IEventRepairTargetHandle repairTarget)
        {
            var lifeState = playerObject.GetComponent<NetworkPlayerLifeState>();
            var repairableEffect = repairTarget as IEventRepairableEffect;
            var matchingZones = FindObjectsByType<PHSOxygenDeprivationZone>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(zone => repairableEffect != null
                    && Vector3.SqrMagnitude(
                        zone.RepairPosition - repairableEffect.RepairPosition) < 0.01f)
                .ToArray();
            if (lifeState == null
                || repairableEffect == null
                || matchingZones.Length != 1
                || matchingZones[0].GetComponent<BoxCollider>() is not { } zoneBounds)
            {
                Fail("oxygen_suffocation_contract_missing");
                yield break;
            }

            var healthBeforeExposure = lifeState.CurrentHealth;
            SetPlayerPosition(playerObject, zoneBounds.bounds.center);
            yield return WaitFor(
                () => lifeState.CurrentHealth < healthBeforeExposure,
                5f,
                "oxygen_suffocation_damage_timeout");
            if (scenarioFinished) yield break;

            var damagedHealth = lifeState.CurrentHealth;
            var outsidePosition = zoneBounds.bounds.center
                + Vector3.right * (zoneBounds.bounds.extents.x + 5f);
            SetPlayerPosition(playerObject, outsidePosition);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return ProbeOxygenHealth(
                playerObject.NetworkObjectId,
                damagedHealth,
                "after_exposure");
            if (scenarioFinished) yield break;

            yield return new WaitForSecondsRealtime(1.75f);
            if (lifeState.CurrentHealth != damagedHealth)
            {
                Fail(
                    $"oxygen_suffocation_outside_damage before={damagedHealth} " +
                    $"after={lifeState.CurrentHealth}");
                yield break;
            }

            yield return ProbeOxygenHealth(
                playerObject.NetworkObjectId,
                damagedHealth,
                "outside");
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P1_OXYGEN_SUFFOCATION_OK damage={healthBeforeExposure - damagedHealth} " +
                $"health={damagedHealth} peers={oxygenHealthReports.Count} outsideStable=true",
                this);
        }

        private IEnumerator ProbeOxygenHealth(
            ulong playerNetworkObjectId,
            int expectedHealth,
            string label)
        {
            oxygenHealthReports.Clear();
            var token = ++activeProbeToken;
            ProbeOxygenHealthClientRpc(token, playerNetworkObjectId);
            yield return WaitFor(
                () => oxygenHealthReports.Count >= expectedClientCount,
                5f,
                $"oxygen_health_peer_timeout label={label}");
            if (scenarioFinished) yield break;

            if (oxygenHealthReports.Values.Any(health => health != expectedHealth))
            {
                var reports = string.Join(
                    ",",
                    oxygenHealthReports.Select(pair => $"{pair.Key}:{pair.Value}"));
                Fail(
                    $"oxygen_health_peer_mismatch label={label} " +
                    $"expected={expectedHealth} reports={reports}");
            }
        }

        private bool TryFindNewSpawnedUtilityItem(
            HashSet<ulong> knownNetworkObjectIds,
            string expectedItemId,
            out NetworkObject foundNetworkObject)
        {
            foundNetworkObject = null;
            if (NetworkManager == null || NetworkManager.SpawnManager == null)
            {
                return false;
            }

            foreach (var pair in NetworkManager.SpawnManager.SpawnedObjects)
            {
                if (knownNetworkObjectIds.Contains(pair.Key)
                    || pair.Value == null
                    || !pair.Value.IsSpawned
                    || pair.Value.GetComponent<UtilityItemObject>() is not { } itemObject
                    || itemObject.ItemPrefabData == null
                    || !string.Equals(
                        itemObject.ItemPrefabData.ItemId,
                        expectedItemId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                foundNetworkObject = pair.Value;
                return true;
            }

            return false;
        }

        private static bool TryFindLocalDebrisPortals(
            out ExteriorTestTeleportInteractable entryPortal,
            out ExteriorTestTeleportInteractable returnPortal)
        {
            entryPortal = null;
            returnPortal = null;
            var activeScene = SceneManager.GetActiveScene();
            var portals = FindObjectsByType<ExteriorTestTeleportInteractable>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(portal =>
                    portal != null
                    && portal.isActiveAndEnabled
                    && portal.Destination != null
                    && portal.gameObject.scene == activeScene)
                .ToArray();
            entryPortal = portals.SingleOrDefault(portal =>
                string.Equals(portal.name, LocalDebrisEntryPortalName, StringComparison.Ordinal));
            var alternateEntryPortal = portals.SingleOrDefault(portal =>
                string.Equals(
                    portal.name,
                    LocalDebrisAlternateEntryPortalName,
                    StringComparison.Ordinal));
            returnPortal = portals.SingleOrDefault(portal =>
                string.Equals(portal.name, LocalDebrisReturnPortalName, StringComparison.Ordinal));
            return entryPortal != null
                && alternateEntryPortal != null
                && returnPortal != null
                && entryPortal != returnPortal
                && entryPortal.TeleportsOnTriggerEnter
                && alternateEntryPortal.TeleportsOnTriggerEnter
                && returnPortal.TeleportsOnTriggerEnter
                && entryPortal.DoorTrigger != null
                && alternateEntryPortal.DoorTrigger != null
                && returnPortal.DoorTrigger != null
                && Vector3.Distance(entryPortal.Destination.position, returnPortal.Destination.position) > 1f;
        }

        private IEnumerator ValidateRemoteLocalPortal(
            ExteriorTestTeleportInteractable portal,
            string label)
        {
            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (portal == null ||
                remoteClientId == NetworkManager.ServerClientId ||
                !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient) ||
                remoteClient.PlayerObject == null)
            {
                Fail($"debris_local_{label}_remote_setup_missing");
                yield break;
            }

            SetPlayerPosition(remoteClient.PlayerObject, portal.transform.position);
            yield return null;

            localPortalProbeReported = false;
            localPortalRequestIssued = false;
            var token = ++activeProbeToken;
            ProbeLocalPortalClientRpc(
                token,
                portal.name,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                });
            yield return WaitFor(
                () => localPortalProbeReported,
                5f,
                $"debris_local_{label}_remote_probe_timeout");
            if (scenarioFinished) yield break;

            if (!localPortalRequestIssued)
            {
                Fail($"debris_local_{label}_remote_request_not_issued");
                yield break;
            }

            yield return WaitFor(
                () => Vector3.Distance(
                    remoteClient.PlayerObject.transform.position,
                    portal.Destination.position) <= 2f,
                10f,
                $"debris_local_{label}_remote_teleport_failed");
            if (scenarioFinished) yield break;

            Debug.Log($"PHS_P0_DEBRIS_LOCAL_{label.ToUpperInvariant()}_REMOTE_OK client={remoteClientId}", this);
        }

        private IEnumerator ValidatePhysicalDebrisSale(
            NetworkObject playerObject,
            TempPlayerItemHolder holder)
        {
            var sellZone = FindAnyObjectByType<DebrisSellZone>(FindObjectsInactive.Include);
            var sellTrigger = sellZone == null ? null : sellZone.GetComponent<BoxCollider>();
            var wallet = FindAnyObjectByType<ShopEconomyWalletAdapter>(FindObjectsInactive.Include);
            var itemRecord = playerObject.GetComponent<NetworkPlayerItemRecord>();
            var debris = FindObjectsByType<DebrisItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.Value > 0
                    && candidate.name.StartsWith("PHS_Debris_", StringComparison.Ordinal));
            var itemObject = debris == null ? null : debris.GetComponent<LastJumpCrew.ParkHanSol.Items.UtilityItemObject>();
            var itemData = itemObject == null ? null : itemObject.ItemPrefabData;
            if (sellZone == null || sellTrigger == null || !sellTrigger.isTrigger || wallet == null ||
                itemRecord == null || debris == null || itemData == null ||
                string.IsNullOrWhiteSpace(itemData.ItemId) || itemData.Price <= 0)
            {
                Fail("debris_sale_setup_invalid");
                yield break;
            }

            yield return WaitFor(() => wallet.IsReady, 10f, "debris_sale_wallet_not_ready");
            if (scenarioFinished) yield break;

            var creditsBefore = wallet.Credits;
            var revisionBeforeHold = itemRecord.Revision;
            var debrisEntityId = debris.GetEntityId().ToString();
            var itemId = itemData.ItemId;
            var itemValue = itemData.Price;
            if (!holder.TryHoldDebris(debris))
            {
                Fail($"debris_sale_hold_failed entity={debrisEntityId} item={itemId}");
                yield break;
            }

            yield return WaitFor(
                () => itemRecord.HeldItemId == itemId && itemRecord.Revision > revisionBeforeHold,
                5f,
                $"debris_sale_record_not_held item={itemId}");
            if (scenarioFinished) yield break;

            var saleRevision = itemRecord.Revision;
            SetPlayerPosition(playerObject, sellTrigger.bounds.center);
            yield return WaitFor(
                () => wallet.Credits == creditsBefore + itemValue &&
                    string.IsNullOrEmpty(itemRecord.HeldItemId) &&
                    itemRecord.Revision == saleRevision + 1,
                10f,
                $"debris_sale_first_commit_failed item={itemId} credits={wallet.Credits}/{creditsBefore + itemValue} " +
                $"held={itemRecord.HeldItemId} revision={itemRecord.Revision}/{saleRevision + 1}");
            if (scenarioFinished) yield break;

            var creditsAfterFirstSale = wallet.Credits;
            var revisionAfterFirstSale = itemRecord.Revision;
            if (itemRecord.TryConsumeHeldItemServer(itemId, saleRevision))
            {
                Fail($"debris_sale_revision_replay_accepted item={itemId} revision={saleRevision}");
                yield break;
            }

            var completeSaleMethod = typeof(DebrisSellZone).GetMethod(
                "TryCompleteNetworkSale",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (completeSaleMethod == null)
            {
                Fail("debris_sale_authority_method_missing");
                yield break;
            }

            var replayArguments = new object[] { NetworkManager.ServerClientId, itemId, null };
            var replayAccepted = (bool)completeSaleMethod.Invoke(sellZone, replayArguments);
            var replayReason = replayArguments[2] as string;
            yield return new WaitForSecondsRealtime(0.5f);
            if (replayAccepted || string.IsNullOrWhiteSpace(replayReason) ||
                wallet.Credits != creditsAfterFirstSale || itemRecord.Revision != revisionAfterFirstSale)
            {
                Fail(
                    $"debris_sale_duplicate_replay_failed accepted={replayAccepted} reason={replayReason ?? "none"} " +
                    $"credits={wallet.Credits}/{creditsAfterFirstSale} " +
                    $"revision={itemRecord.Revision}/{revisionAfterFirstSale}");
                yield break;
            }

            yield return ProbeDebrisSaleState(creditsAfterFirstSale, revisionAfterFirstSale);
            if (scenarioFinished) yield break;

            var economy = NetworkRunSessionRoot.Instance?.Economy;
            if (economy == null || economy.Revision == 0U)
            {
                Fail("debris_sale_economy_ledger_missing");
                yield break;
            }

            yield return ProbeEconomyState(
                creditsAfterFirstSale,
                economy.Snapshot.PendingDeliveryCount,
                economy.Snapshot.ClaimedDeliveryCount,
                economy.Snapshot.DeliveredCount,
                NetworkRunEconomyTransactionKind.SaleCredit,
                $"debris_sale:held:{NetworkManager.ServerClientId}:{saleRevision}",
                "debris_sale");
            if (scenarioFinished) yield break;

            Debug.Log(
                $"PHS_P0_DEBRIS_DUPLICATE_SALE_OK entity={debrisEntityId} item={itemId} " +
                $"revision={saleRevision}->{revisionAfterFirstSale} credits={creditsBefore}->{creditsAfterFirstSale} " +
                $"replayReason={replayReason}",
                this);
        }

        private IEnumerator ProbeDebrisSaleState(int expectedCredits, uint expectedRevision)
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                debrisSaleStateReports.Clear();
                var token = ++activeProbeToken;
                ProbeDebrisSaleStateClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (debrisSaleStateReports.Count < expectedClientCount &&
                       Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (debrisSaleStateReports.Count >= expectedClientCount &&
                    debrisSaleStateReports.Values.All(report =>
                        report.Credits == expectedCredits &&
                        report.Revision == expectedRevision &&
                        string.IsNullOrEmpty(report.HeldItemId)))
                {
                    Debug.Log(
                        $"PHS_P0_DEBRIS_SALE_SYNC_OK peers={debrisSaleStateReports.Count} " +
                        $"credits={expectedCredits} revision={expectedRevision}",
                        this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var reports = string.Join(
                ",",
                debrisSaleStateReports.OrderBy(report => report.Key)
                    .Select(report =>
                        $"{report.Key}:{report.Value.Credits}:{report.Value.Revision}:{report.Value.HeldItemId}"));
            Fail(
                $"debris_sale_peer_sync_timeout credits={expectedCredits} revision={expectedRevision} " +
                $"reports={reports}");
        }

        private IEnumerator ProbeScenes(string expectedSceneName)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                sceneReports.Clear();
                var token = ++activeProbeToken;
                ProbeSceneClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (sceneReports.Count < expectedClientCount && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (sceneReports.Count >= expectedClientCount &&
                    sceneReports.Values.All(sceneName => sceneName == expectedSceneName))
                {
                    Debug.Log($"PHS_P0_SCENE_OK peers={sceneReports.Count} scene={expectedSceneName}", this);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(0.5f);
            }

            var reports = string.Join(",", sceneReports.OrderBy(report => report.Key)
                .Select(report => $"{report.Key}:{report.Value}"));
            Fail($"peer_scene_timeout expected={expectedSceneName} reports={reports}");
        }

        private IEnumerator ProbeGauge()
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                gaugeReports.Clear();
                var token = ++activeProbeToken;
                ProbeGaugeClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (gaugeReports.Count < expectedClientCount && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (gaugeReports.Count >= expectedClientCount &&
                    gaugeReports.Values.All(report => report.Phase == NetworkRunPhase.Charging))
                {
                    var values = gaugeReports.Values.Select(report => report.Value).ToArray();
                    var delta = values.Max() - values.Min();
                    if (delta <= 0.12f)
                    {
                        Debug.Log(
                            $"PHS_P0_GAUGE_OK peers={gaugeReports.Count} min={values.Min():F3} max={values.Max():F3}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Fail("peer_gauge_sync_timeout");
        }

        private IEnumerator ProbeMapChoices()
        {
            var deadline = Time.realtimeSinceStartup + 15f;
            var isolationFailureReason = "not_checked";
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                mapChoiceReports.Clear();
                var token = ++activeProbeToken;
                ProbeMapChoicesClientRpc(token);

                var probeDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 2f);
                while (mapChoiceReports.Count < expectedClientCount && Time.realtimeSinceStartup < probeDeadline)
                {
                    yield return null;
                }

                if (mapChoiceReports.Count >= expectedClientCount)
                {
                    var first = mapChoiceReports.Values.First();
                    var randomScopeIsolated = ValidateRandomScopeIsolation(
                        first.LeftZoneId,
                        first.RightZoneId,
                        out isolationFailureReason);
                    if (first.Ready
                        && first.LeftZoneId > 0
                        && first.RightZoneId > 0
                        && first.LeftZoneId != first.RightZoneId
                        && first.RandomLedgerFound
                        && first.RunSeed != 0UL
                        && first.AlgorithmVersion
                            == NetworkRunRandomLedger.CurrentAlgorithmVersion
                        && first.RandomRevision > 0U
                        && mapChoiceReports.Values.All(report =>
                            report.Ready
                            && report.LeftZoneId == first.LeftZoneId
                            && report.RightZoneId == first.RightZoneId
                            && report.RandomLedgerFound
                            && report.RunSeed == first.RunSeed
                            && report.AlgorithmVersion == first.AlgorithmVersion
                            && report.RandomRevision == first.RandomRevision)
                        && randomScopeIsolated)
                    {
                        Debug.Log(
                            $"PHS_P0_RNG_MAP_CHOICE_OK peers={mapChoiceReports.Count} " +
                            $"left={first.LeftZoneId} right={first.RightZoneId} seed={first.RunSeed} " +
                            $"algorithm={first.AlgorithmVersion} revision={first.RandomRevision}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var reports = mapChoiceReports.Count == 0
                ? "none"
                : string.Join(
                    ";",
                    mapChoiceReports.OrderBy(report => report.Key).Select(report =>
                        $"{report.Key}:ready={report.Value.Ready},left={report.Value.LeftZoneId}," +
                        $"right={report.Value.RightZoneId},rng={report.Value.RandomLedgerFound}," +
                        $"seed={report.Value.RunSeed},algorithm={report.Value.AlgorithmVersion}," +
                        $"revision={report.Value.RandomRevision}"));
            Fail(
                $"peer_map_choice_sync_timeout isolation={isolationFailureReason} reports={reports}");
        }

        private bool ValidateRandomScopeIsolation(
            int actualLeftMapId,
            int actualRightMapId,
            out string reason)
        {
            var runSessionRoot = NetworkRunSessionRoot.Instance;
            var runFlow = NetworkRunFlowCoordinator.Instance;
            var console = FindAnyObjectByType<NetworkTravelConsoleController>(
                FindObjectsInactive.Include);
            if (runSessionRoot == null
                || runSessionRoot.Rng == null
                || runFlow == null
                || console == null
                || console.SelectableMapCount < 2)
            {
                reason = "run_random_validation_context_missing";
                return false;
            }

            var scopeKey = (ulong)(runFlow.ClearedZoneCount + 1);
            if (!runSessionRoot.Rng.TryCreateServerScope(
                    NetworkRunRandomStream.MapChoice,
                    scopeKey,
                    out var firstMapScope,
                    out reason))
            {
                return false;
            }

            var expectedLeftIndex = firstMapScope.NextInt(
                0,
                console.SelectableMapCount);
            var expectedRightDraw = firstMapScope.NextInt(
                0,
                console.SelectableMapCount - 1);
            var expectedRightIndex = expectedRightDraw;
            if (expectedRightIndex >= expectedLeftIndex)
            {
                expectedRightIndex++;
            }

            if (!console.TryGetSelectableMapIdAt(
                    expectedLeftIndex,
                    out var expectedLeftMapId)
                || !console.TryGetSelectableMapIdAt(
                    expectedRightIndex,
                    out var expectedRightMapId))
            {
                reason = "expected_map_choice_resolution_failed";
                return false;
            }

            if (actualLeftMapId != expectedLeftMapId
                || actualRightMapId != expectedRightMapId)
            {
                reason =
                    $"map_choice_not_from_ledger:" +
                    $"{actualLeftMapId},{actualRightMapId}!=" +
                    $"{expectedLeftMapId},{expectedRightMapId}";
                return false;
            }

            if (!runSessionRoot.Rng.TryCreateServerScope(
                    NetworkRunRandomStream.ExternalThreat,
                    scopeKey,
                    out var otherStreamScope,
                    out reason))
            {
                return false;
            }

            otherStreamScope.NextInt(0, console.SelectableMapCount);
            otherStreamScope.NextInt(0, console.SelectableMapCount - 1);
            if (!runSessionRoot.Rng.TryCreateServerScope(
                    NetworkRunRandomStream.MapChoice,
                    scopeKey,
                    out var repeatedMapScope,
                    out reason))
            {
                return false;
            }

            if (repeatedMapScope.NextInt(0, console.SelectableMapCount)
                    != expectedLeftIndex
                || repeatedMapScope.NextInt(0, console.SelectableMapCount - 1)
                    != expectedRightDraw)
            {
                reason = "map_scope_changed_after_other_stream";
                return false;
            }

            reason = null;
            return true;
        }

        private IEnumerator ProbeFarSelectRejection()
        {
            var console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (console == null || remoteClientId == NetworkManager.ServerClientId ||
                !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient) ||
                remoteClient.PlayerObject == null)
            {
                Fail("far_select_probe_setup_missing");
                yield break;
            }

            var playerObject = remoteClient.PlayerObject;
            var originalPosition = playerObject.transform.position;
            var originalRotation = playerObject.transform.rotation;
            SetPlayerPosition(playerObject, console.transform.position + Vector3.up * 100f + Vector3.forward * 100f);

            farSelectProbeReported = false;
            farSelectRequestIssued = false;
            var token = ++activeProbeToken;
            ProbeFarSelectClientRpc(
                token,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new[] { remoteClientId } }
                });

            yield return WaitFor(() => farSelectProbeReported, 5f, "far_select_client_probe_timeout");
            if (scenarioFinished) yield break;

            yield return new WaitForSecondsRealtime(1f);
            SetPlayerPosition(playerObject, originalPosition, originalRotation);

            if (!farSelectRequestIssued || console.SelectedDestination != TravelConsoleDestination.None ||
                SceneManager.GetActiveScene().name != MapSceneName)
            {
                Fail(
                    $"far_select_not_rejected issued={farSelectRequestIssued} " +
                    $"destination={console.SelectedDestination} scene={SceneManager.GetActiveScene().name}");
                yield break;
            }

            Debug.Log($"PHS_P0_FAR_SELECT_REJECTED client={remoteClientId}", this);
        }

        private IEnumerator WaitFor(Func<bool> predicate, float timeoutSeconds, string failureReason)
        {
            var deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, timeoutSeconds);
            while (scenarioRunning && !scenarioFinished && Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    yield break;
                }

                yield return null;
            }

            if (!scenarioFinished)
            {
                Fail(failureReason);
            }
        }

        private void MoveConnectedPlayersIntoSafeZone(BoxCollider safeTrigger)
        {
            var center = safeTrigger.bounds.center;
            var index = 0;
            foreach (var pair in NetworkManager.ConnectedClients.OrderBy(pair => pair.Key))
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                var offset = safeTrigger.transform.right * ((index - (expectedClientCount - 1) * 0.5f) * 1.25f);
                SetPlayerPosition(playerObject, center + offset);

                index++;
            }

            Physics.SyncTransforms();
            Debug.Log($"PHS_P0_SAFE_MOVE players={index} center={center}", this);
        }

        private void MoveConnectedPlayersOutsideSafeZone(BoxCollider safeTrigger)
        {
            var center = safeTrigger.bounds.center;
            var outside = center + Vector3.forward * (safeTrigger.bounds.extents.z + 8f);
            var index = 0;
            foreach (var pair in NetworkManager.ConnectedClients.OrderBy(pair => pair.Key))
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                var offset = Vector3.right * ((index - (expectedClientCount - 1) * 0.5f) * 1.25f);
                SetPlayerPosition(playerObject, outside + offset);
                index++;
            }

            Physics.SyncTransforms();
            Debug.Log($"PHS_P0_SAFE_EXIT players={index} position={outside}", this);
        }

        private static void SetPlayerPosition(
            NetworkObject playerObject,
            Vector3 position,
            Quaternion? rotation = null)
        {
            var controller = playerObject.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            playerObject.transform.SetPositionAndRotation(
                position,
                rotation ?? playerObject.transform.rotation);
            if (controller != null)
            {
                controller.enabled = true;
            }

            Physics.SyncTransforms();
        }

        private static void MovePlayerWithCharacterController(NetworkObject playerObject, Vector3 position)
        {
            var controller = playerObject.GetComponent<CharacterController>();
            if (controller == null || !controller.enabled)
            {
                playerObject.transform.position = position;
            }
            else
            {
                controller.Move(position - playerObject.transform.position);
            }

            Physics.SyncTransforms();
        }

        [ClientRpc]
        private void SubmitShopTransitionVoteClientRpc(ClientRpcParams clientRpcParams = default)
        {
            StartCoroutine(SubmitShopTransitionVoteWhenReady());
        }

        private IEnumerator SubmitShopTransitionVoteWhenReady()
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var voteCoordinator = NetworkShopTransitionVoteCoordinator.Instance;
                if (voteCoordinator != null && voteCoordinator.IsVoteActive)
                {
                    voteCoordinator.SubmitLocalVote(true);
                    Debug.Log(
                        $"PHS_P0_SHOP_VOTE_CLIENT_OK client={NetworkManager.LocalClientId}",
                        this);
                    yield break;
                }

                yield return null;
            }

            Debug.LogError(
                $"PHS_P0_SHOP_VOTE_CLIENT_FAILED reason=vote_not_active client={NetworkManager.LocalClientId}",
                this);
        }

        private void RequestRemoteShopTransitionVotes()
        {
            var remoteClientIds = NetworkManager.ConnectedClientsIds
                .Where(clientId => clientId != NetworkManager.ServerClientId)
                .ToArray();
            if (remoteClientIds.Length == 0)
            {
                return;
            }

            SubmitShopTransitionVoteClientRpc(
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = remoteClientIds }
                });
        }

        [ClientRpc]
        private void ProbeOxygenHealthClientRpc(
            uint token,
            ulong playerNetworkObjectId)
        {
            if (!IsScenarioEnabled()) return;

            var health = -1;
            var spawnManager = NetworkManager == null
                ? null
                : NetworkManager.SpawnManager;
            if (spawnManager != null
                && spawnManager.SpawnedObjects.TryGetValue(
                    playerNetworkObjectId,
                    out var playerObject)
                && playerObject.GetComponent<NetworkPlayerLifeState>() is { } lifeState)
            {
                health = lifeState.CurrentHealth;
            }

            ReportOxygenHealthServerRpc(token, health);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportOxygenHealthServerRpc(
            uint token,
            int health,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            oxygenHealthReports[rpcParams.Receive.SenderClientId] = health;
        }

        [ClientRpc]
        private void ProbeEventSnapshotClientRpc(uint token, EventId eventId, ulong instanceId)
        {
            if (!IsScenarioEnabled()) return;

            var coordinator = FindAnyObjectByType<NetworkEventCoordinator>(FindObjectsInactive.Include);
            var snapshot = default(NetworkEventLifecycleSnapshot);
            var found = coordinator != null && coordinator.TryGetSnapshot(instanceId, out snapshot);
            var manager = EventManager.Peek();
            var effectSnapshots = new List<NetworkEventEffectSnapshot>();
            coordinator?.CopyEffectSnapshotsTo(effectSnapshots);
            var networkEffectCount = effectSnapshots.Count(effect =>
                effect.EventInstanceId == instanceId && effect.IsActive);
            var presenter = FindAnyObjectByType<NetworkEventEffectMirrorPresenter>(FindObjectsInactive.Include);
            ReportEventSnapshotServerRpc(
                token,
                found,
                found ? snapshot.InstanceId : 0UL,
                found ? snapshot.EventId : eventId,
                found ? snapshot.RoomId.ToString() : string.Empty,
                found ? snapshot.State : EventState.Ready,
                found ? snapshot.Revision : 0U,
                manager != null && manager.IsActive(eventId),
                CountLocalEventEffects(eventId),
                networkEffectCount,
                presenter == null ? 0 : presenter.ActiveMirrorCount);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportEventSnapshotServerRpc(
            uint token,
            bool found,
            ulong instanceId,
            EventId eventId,
            string roomId,
            EventState state,
            uint revision,
            bool localEventActive,
            int localEffectCount,
            int networkEffectCount,
            int mirrorEffectCount,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            eventSnapshotReports[rpcParams.Receive.SenderClientId] = new EventSnapshotReport(
                found,
                instanceId,
                eventId,
                roomId,
                state,
                revision,
                localEventActive,
                localEffectCount,
                networkEffectCount,
                mirrorEffectCount);
        }

        [ClientRpc]
        private void BeginEventLifecycleObservationClientRpc(ulong instanceId)
        {
            if (!IsScenarioEnabled()) return;

            DetachEventLifecycleObservation();
            observedTerminalInstanceId = instanceId;
            observedTerminalState = false;
            observedTerminalRemoved = false;
            observedTerminalRevision = 0U;
            observedEventCoordinator = FindAnyObjectByType<NetworkEventCoordinator>(FindObjectsInactive.Include);
            if (observedEventCoordinator != null)
            {
                observedEventCoordinator.LifecycleSnapshotsChanged += HandleObservedLifecycleChanged;
                CaptureObservedLifecycle();
            }

            ReportEventObservationReadyServerRpc(instanceId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportEventObservationReadyServerRpc(
            ulong instanceId,
            ServerRpcParams rpcParams = default)
        {
            if (!IsServer || !scenarioRunning || scenarioFinished || instanceId != activeObservedInstanceId)
            {
                return;
            }

            eventObservationReadyClients.Add(rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void ProbeEventTerminalClientRpc(uint token, ulong instanceId)
        {
            if (!IsScenarioEnabled()) return;

            if (observedTerminalInstanceId == instanceId)
            {
                CaptureObservedLifecycle();
            }

            ReportEventTerminalServerRpc(
                token,
                instanceId,
                observedTerminalState,
                observedTerminalRemoved,
                observedTerminalRevision);
            DetachEventLifecycleObservation();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportEventTerminalServerRpc(
            uint token,
            ulong instanceId,
            bool observedTerminal,
            bool observedRemoved,
            uint terminalRevision,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token) || instanceId != activeObservedInstanceId) return;
            eventTerminalReports[rpcParams.Receive.SenderClientId] = new EventTerminalReport(
                observedTerminal,
                observedRemoved,
                terminalRevision);
        }

        [ClientRpc]
        private void ProbeFarEventTerminalClientRpc(
            uint token,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var coordinator = FindAnyObjectByType<NetworkEventCoordinator>(FindObjectsInactive.Include);
            var issued = coordinator != null
                && coordinator.RequestMiniGameResult(EventId.MeteorAttack, true);
            ReportFarEventTerminalProbeServerRpc(token, issued);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportFarEventTerminalProbeServerRpc(
            uint token,
            bool requestIssued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token) || rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;
            farEventTerminalRequestIssued = requestIssued;
            farEventTerminalProbeReported = true;
        }

        private void HandleObservedLifecycleChanged()
        {
            CaptureObservedLifecycle();
        }

        private void CaptureObservedLifecycle()
        {
            if (observedEventCoordinator == null || observedTerminalInstanceId == 0UL)
            {
                return;
            }

            if (observedEventCoordinator.TryGetSnapshot(observedTerminalInstanceId, out var snapshot))
            {
                if (snapshot.IsTerminal)
                {
                    observedTerminalState = true;
                    observedTerminalRevision = snapshot.Revision;
                }

                return;
            }

            if (observedTerminalState)
            {
                observedTerminalRemoved = true;
            }
        }

        private void DetachEventLifecycleObservation()
        {
            if (observedEventCoordinator != null)
            {
                observedEventCoordinator.LifecycleSnapshotsChanged -= HandleObservedLifecycleChanged;
            }

            observedEventCoordinator = null;
            observedTerminalInstanceId = 0UL;
        }

        private static int CountLocalEventEffects(EventId eventId)
        {
            return eventId switch
            {
                EventId.Fire => FindObjectsByType<FireEffectInstance>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length,
                EventId.OxygenLeak => FindObjectsByType<OxygenLeakEffectInstance>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length,
                EventId.EnemySpawn => FindObjectsByType<EnemyBase>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Length,
                _ => 0
            };
        }

        [ClientRpc]
        private void ProbeLocalPortalClientRpc(
            uint token,
            string portalName,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var portal = FindObjectsByType<ExteriorTestTeleportInteractable>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.isActiveAndEnabled
                    && string.Equals(candidate.name, portalName, StringComparison.Ordinal));
            var localPlayer = FindObjectsByType<NetworkPlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(player => player.IsOwner);
            var alreadyTeleported = portal != null
                && localPlayer != null
                && portal.Destination != null
                && Vector3.Distance(
                    localPlayer.transform.position,
                    portal.Destination.position) <= 2f;
            var issued = portal != null
                && localPlayer != null
                && portal.TeleportsOnTriggerEnter
                && portal.DoorTrigger != null;
            if (issued && !alreadyTeleported)
            {
                localPlayer.RequestLocalPortalTeleport(portal.name);
            }

            ReportLocalPortalProbeServerRpc(token, issued);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportLocalPortalProbeServerRpc(
            uint token,
            bool issued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            localPortalProbeReported = true;
            localPortalRequestIssued = issued;
        }

        [ClientRpc]
        private void ProbeRemoteItemPositionClientRpc(
            uint token,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var localLifecycle = FindObjectsByType<NetworkPlayerItemLifecycle>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(lifecycle => lifecycle.IsSpawned && lifecycle.IsOwner);
            ReportRemoteItemPositionServerRpc(
                token,
                localLifecycle != null,
                localLifecycle == null ? default : localLifecycle.transform.position);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRemoteItemPositionServerRpc(
            uint token,
            bool found,
            Vector3 position,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)
                || rpcParams.Receive.SenderClientId != activeRemoteItemClientId
                || !found)
            {
                return;
            }

            remoteItemPosition = position;
            remoteItemPositionReported = true;
        }

        [ClientRpc]
        private void RequestRemoteItemPickupClientRpc(
            uint token,
            ulong targetNetworkObjectId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;
            StartCoroutine(RequestRemoteItemPickupWhenReady(token, targetNetworkObjectId));
        }

        private IEnumerator RequestRemoteItemPickupWhenReady(
            uint token,
            ulong targetNetworkObjectId)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var localLifecycle = FindObjectsByType<NetworkPlayerItemLifecycle>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(lifecycle => lifecycle.IsSpawned && lifecycle.IsOwner);
                var spawnManager = NetworkManager == null ? null : NetworkManager.SpawnManager;
                if (localLifecycle != null
                    && spawnManager != null
                    && spawnManager.SpawnedObjects.TryGetValue(
                        targetNetworkObjectId,
                        out var targetNetworkObject)
                    && targetNetworkObject != null
                    && targetNetworkObject.GetComponent<UtilityItemObject>() is { } itemObject
                    && localLifecycle.CanRequestNetworkPickup(itemObject))
                {
                    localLifecycle.RequestNetworkPickup(itemObject);
                    ReportRemoteItemPickupRequestServerRpc(token, true);
                    yield break;
                }

                yield return null;
            }

            ReportRemoteItemPickupRequestServerRpc(token, false);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRemoteItemPickupRequestServerRpc(
            uint token,
            bool issued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)
                || rpcParams.Receive.SenderClientId != activeRemoteItemClientId)
            {
                return;
            }

            remoteItemRequestReported = true;
            remoteItemRequestIssued = issued;
        }

        [ClientRpc]
        private void ProbeRemoteHeldItemClientRpc(
            uint token,
            ulong ownerClientId,
            ulong despawnedNetworkObjectId,
            string expectedItemId,
            int expectedDurability)
        {
            if (!IsScenarioEnabled()) return;
            StartCoroutine(ProbeRemoteHeldItemWhenReady(
                token,
                ownerClientId,
                despawnedNetworkObjectId,
                expectedItemId,
                expectedDurability));
        }

        private IEnumerator ProbeRemoteHeldItemWhenReady(
            uint token,
            ulong ownerClientId,
            ulong despawnedNetworkObjectId,
            string expectedItemId,
            int expectedDurability)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var itemRecord = FindObjectsByType<NetworkPlayerItemRecord>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(record =>
                        record.IsSpawned && record.OwnerClientId == ownerClientId);
                var holder = itemRecord == null
                    ? null
                    : itemRecord.GetComponent<TempPlayerItemHolder>();
                var heldVisual = holder == null
                    ? null
                    : holder.GetComponentsInChildren<UtilityItemObject>(true)
                        .FirstOrDefault(itemObject =>
                            itemObject.ItemPrefabData != null
                            && string.Equals(
                                itemObject.ItemPrefabData.ItemId,
                                expectedItemId,
                                StringComparison.Ordinal));
                var targetDespawned = NetworkManager != null
                    && NetworkManager.SpawnManager != null
                    && !NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(
                        despawnedNetworkObjectId);
                var valid = targetDespawned
                    && itemRecord != null
                    && string.Equals(
                        itemRecord.HeldItemId,
                        expectedItemId,
                        StringComparison.Ordinal)
                    && itemRecord.CurrentDurability == expectedDurability
                    && holder != null
                    && holder.CurrentItemPrefabData != null
                    && string.Equals(
                        holder.CurrentItemPrefabData.ItemId,
                        expectedItemId,
                        StringComparison.Ordinal)
                    && heldVisual != null
                    && heldVisual.GetComponentsInChildren<NetworkObject>(true).Length == 0;
                if (valid)
                {
                    ReportRemoteHeldItemProbeServerRpc(token, true);
                    yield break;
                }

                yield return null;
            }

            ReportRemoteHeldItemProbeServerRpc(token, false);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRemoteHeldItemProbeServerRpc(
            uint token,
            bool valid,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            remoteHeldItemReports[rpcParams.Receive.SenderClientId] = valid;
        }

        [ClientRpc]
        private void RequestRemoteItemPrimaryUseClientRpc(
            uint token,
            string expectedItemId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var localCombatController = FindObjectsByType<NetworkPlayerCombatController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.IsSpawned && controller.IsOwner);
            var holder = localCombatController == null
                ? null
                : localCombatController.GetComponent<TempPlayerItemHolder>();
            var issued = localCombatController != null
                && holder != null
                && holder.IsHoldingItem(expectedItemId);
            if (issued)
            {
                switch (expectedItemId)
                {
                    case "wrench":
                        localCombatController.RequestWrenchAttack();
                        break;
                    case "fire_extinguisher":
                        localCombatController.RequestExtinguisherSpray();
                        break;
                    case "battery_pack":
                        var heldItemComponent =
                            ((LastJumpCrew.Common.IItemHolder)holder).CurrentItem
                            as Component;
                        var batteryUse = heldItemComponent == null
                            ? null
                            : heldItemComponent
                                .GetComponents<MonoBehaviour>()
                                .OfType<IUsableItem>()
                                .FirstOrDefault();
                        if (batteryUse == null
                            || !batteryUse.CanUse(holder, null))
                        {
                            issued = false;
                            break;
                        }

                        batteryUse.Use(holder, null);
                        break;
                    default:
                        issued = false;
                        break;
                }
            }

            ReportRemoteItemPrimaryUseRequestServerRpc(token, issued);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRemoteItemPrimaryUseRequestServerRpc(
            uint token,
            bool issued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)
                || rpcParams.Receive.SenderClientId != activeRemoteItemClientId)
            {
                return;
            }

            remotePrimaryUseRequestReported = true;
            remotePrimaryUseRequestIssued = issued;
        }

        [ClientRpc]
        private void RequestRemoteItemThrowClientRpc(
            uint token,
            string expectedItemId,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var localCombatController = FindObjectsByType<NetworkPlayerCombatController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.IsSpawned && controller.IsOwner);
            var holder = localCombatController == null
                ? null
                : localCombatController.GetComponent<TempPlayerItemHolder>();
            var issued = localCombatController != null
                && holder != null
                && holder.IsHoldingItem(expectedItemId);
            if (issued)
            {
                localCombatController.RequestThrowHeldItem(0.5f);
            }

            ReportRemoteItemThrowRequestServerRpc(token, issued);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRemoteItemThrowRequestServerRpc(
            uint token,
            bool issued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)
                || rpcParams.Receive.SenderClientId != activeRemoteItemClientId)
            {
                return;
            }

            remoteThrowRequestReported = true;
            remoteThrowRequestIssued = issued;
        }

        [ClientRpc]
        private void ProbeRemoteThrownItemClientRpc(
            uint token,
            ulong ownerClientId,
            ulong networkObjectId,
            string expectedItemId,
            bool expectsDurability,
            int expectedDurability)
        {
            if (!IsScenarioEnabled()) return;
            StartCoroutine(ProbeRemoteThrownItemWhenReady(
                token,
                ownerClientId,
                networkObjectId,
                expectedItemId,
                expectsDurability,
                expectedDurability));
        }

        private IEnumerator ProbeRemoteThrownItemWhenReady(
            uint token,
            ulong ownerClientId,
            ulong networkObjectId,
            string expectedItemId,
            bool expectsDurability,
            int expectedDurability)
        {
            var deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var itemRecord = FindObjectsByType<NetworkPlayerItemRecord>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(record =>
                        record.IsSpawned && record.OwnerClientId == ownerClientId);
                var holder = itemRecord == null
                    ? null
                    : itemRecord.GetComponent<TempPlayerItemHolder>();
                var heldVisual = holder == null
                    ? null
                    : holder.GetComponentsInChildren<UtilityItemObject>(true)
                        .FirstOrDefault(itemObject =>
                            itemObject.ItemPrefabData != null
                            && string.Equals(
                                itemObject.ItemPrefabData.ItemId,
                                expectedItemId,
                                StringComparison.Ordinal));
                var durabilityState = NetworkManager == null
                    || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                        networkObjectId,
                        out var durabilityNetworkObject)
                    ? null
                    : durabilityNetworkObject.GetComponent<NetworkUtilityItemDurabilityState>();
                var durabilityValid = !expectsDurability
                    || durabilityState != null
                    && durabilityState.CurrentDurability == expectedDurability;
                var valid = NetworkManager != null
                    && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                        networkObjectId,
                        out var networkObject)
                    && networkObject != null
                    && networkObject.IsSpawned
                    && networkObject.GetComponent<UtilityItemObject>() is { } itemObject
                    && itemObject.ItemPrefabData != null
                    && string.Equals(
                        itemObject.ItemPrefabData.ItemId,
                        expectedItemId,
                        StringComparison.Ordinal)
                    && durabilityValid
                    && networkObject.GetComponent<Rigidbody>() != null
                    && networkObject.GetComponent<Unity.Netcode.Components.NetworkTransform>() != null
                    && networkObject.GetComponent<ThrownItemImpact>() != null
                    && itemRecord != null
                    && string.IsNullOrEmpty(itemRecord.HeldItemId)
                    && holder != null
                    && holder.CurrentItemPrefabData == null
                    && heldVisual == null;
                if (valid)
                {
                    ReportThrownItemProbeServerRpc(token, true);
                    yield break;
                }

                yield return null;
            }

            ReportThrownItemProbeServerRpc(token, false);
        }

        [ClientRpc]
        private void ProbeThrownItemClientRpc(
            uint token,
            ulong networkObjectId,
            string expectedItemId)
        {
            if (!IsScenarioEnabled()) return;

            var valid = NetworkManager != null
                && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId,
                    out var networkObject)
                && networkObject != null
                && networkObject.IsSpawned
                && networkObject.GetComponent<UtilityItemObject>() is { } itemObject
                && itemObject.ItemPrefabData != null
                && string.Equals(
                    itemObject.ItemPrefabData.ItemId,
                    expectedItemId,
                    StringComparison.Ordinal)
                && networkObject.GetComponent<Rigidbody>() != null
                && networkObject.GetComponent<Unity.Netcode.Components.NetworkTransform>() != null
                && networkObject.GetComponent<ThrownItemImpact>() != null;
            ReportThrownItemProbeServerRpc(token, valid);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportThrownItemProbeServerRpc(
            uint token,
            bool valid,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            thrownItemReports[rpcParams.Receive.SenderClientId] = valid;
        }

        [ClientRpc]
        private void ProbeSceneClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;
            ReportSceneServerRpc(token, SceneManager.GetActiveScene().name);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportSceneServerRpc(uint token, string sceneName, ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            sceneReports[rpcParams.Receive.SenderClientId] = sceneName;
        }

        [ClientRpc]
        private void ProbeGaugeClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;
            var coordinator = NetworkRunFlowCoordinator.Instance;
            ReportGaugeServerRpc(
                token,
                coordinator == null ? -1f : coordinator.WarpChargeNormalized,
                coordinator == null ? NetworkRunPhase.Waiting : coordinator.Phase);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportGaugeServerRpc(
            uint token,
            float value,
            NetworkRunPhase phase,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            gaugeReports[rpcParams.Receive.SenderClientId] = new GaugeReport(value, phase);
        }

        [ClientRpc]
        private void ProbeMapChoicesClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;
            var console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            var left = 0;
            var right = 0;
            var ready = console != null && console.TryGetCurrentMapChoices(out left, out right);
            var randomLedger = NetworkRunSessionRoot.Instance?.Rng;
            var randomLedgerFound = randomLedger != null
                && randomLedger.IsSpawned
                && randomLedger.Snapshot.Revision > 0U;
            var randomSnapshot = randomLedgerFound
                ? randomLedger.Snapshot
                : default;
            ReportMapChoicesServerRpc(
                token,
                left,
                right,
                ready,
                randomLedgerFound,
                randomSnapshot.RunSeed,
                randomSnapshot.AlgorithmVersion,
                randomSnapshot.Revision);
        }

        [ClientRpc]
        private void ProbeFarSelectClientRpc(uint token, ClientRpcParams clientRpcParams = default)
        {
            if (!IsScenarioEnabled()) return;

            var console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            var localPlayer = FindObjectsByType<NetworkPlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(player => player.IsOwner);
            var holder = localPlayer == null ? null : localPlayer.GetComponent<TempPlayerItemHolder>();
            var issued = console != null && holder != null && console.CanSelectSide(TravelConsoleSide.Left);
            if (issued)
            {
                console.RequestSelectSide(holder, TravelConsoleSide.Left);
            }

            ReportFarSelectProbeServerRpc(token, issued);
        }

        [ClientRpc]
        private void ProbeShopStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var display = FindAnyObjectByType<ShopRandomDisplayController>(FindObjectsInactive.Include);
            var localPlayer = FindObjectsByType<NetworkPlayerController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(player => player.IsOwner);
            ReportShopStateServerRpc(
                token,
                display == null ? string.Empty : GetShopOfferSignature(display),
                display == null ? -1 : display.DisplayedProductCount,
                localPlayer == null ? NetworkPlayerGravityMode.Spacewalk : localPlayer.GravityMode);
        }

        [ClientRpc]
        private void ProbeRunFlowStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var coordinator = NetworkRunFlowCoordinator.Instance;
            ReportRunFlowStateServerRpc(
                token,
                coordinator == null ? NetworkRunPhase.Waiting : coordinator.Phase,
                coordinator == null ? -1 : coordinator.ClearedZoneCount,
                coordinator == null ? -1 : coordinator.CompletedShopCycleCount,
                coordinator != null && coordinator.IsFinalShopPending);
        }

        [ClientRpc]
        private void ProbeStageClockClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var stageClock = NetworkRunSessionRoot.Instance?.StageClock;
            var found = stageClock != null && stageClock.IsSpawned;
            ReportStageClockServerRpc(
                token,
                found,
                found ? stageClock.MapId : 0,
                found ? stageClock.StageSequence : 0U,
                found ? stageClock.Revision : 0U,
                found ? stageClock.State : NetworkRunStageClockState.Stopped,
                found ? stageClock.RemainingSeconds : 0f);
        }

        [ClientRpc]
        private void ProbeIncidentStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var incidentLedger = NetworkRunSessionRoot.Instance?.Incidents;
            var found = incidentLedger != null
                && incidentLedger.IsSpawned
                && incidentLedger.Snapshot.Revision > 0U;
            ReportIncidentStateServerRpc(
                token,
                found,
                found ? incidentLedger.Snapshot : default,
                found ? incidentLedger.CommandCount : -1,
                found
                    ? ComputeIncidentCommandSignature(incidentLedger)
                    : 0UL);
        }

        [ClientRpc]
        private void ProbeEconomyStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var economy = NetworkRunSessionRoot.Instance?.Economy;
            var found = economy != null
                && economy.IsSpawned
                && economy.Revision > 0U;
            var snapshot = found ? economy.Snapshot : default;
            ReportEconomyStateServerRpc(
                token,
                found,
                found ? snapshot.Credits : int.MinValue,
                found ? snapshot.Revision : 0U,
                found ? snapshot.PendingDeliveryCount : -1,
                found ? snapshot.ClaimedDeliveryCount : -1,
                found ? snapshot.DeliveredCount : -1,
                found ? snapshot.LastTransactionId.ToString() : string.Empty,
                found ? snapshot.LastTransactionKind : NetworkRunEconomyTransactionKind.None);
        }

        [ClientRpc]
        private void ProbeDebrisSaleStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var wallet = FindAnyObjectByType<ShopEconomyWalletAdapter>(FindObjectsInactive.Include);
            var hostRecord = FindObjectsByType<NetworkPlayerItemRecord>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(record => record.IsSpawned && record.OwnerClientId == NetworkManager.ServerClientId);
            ReportDebrisSaleStateServerRpc(
                token,
                wallet == null || !wallet.IsReady ? int.MinValue : wallet.Credits,
                hostRecord == null ? uint.MaxValue : hostRecord.Revision,
                hostRecord == null ? "record_missing" : hostRecord.HeldItemId);
        }

        [ClientRpc]
        private void ProbeShipPowerStateClientRpc(uint token)
        {
            if (!IsScenarioEnabled()) return;

            var shipState = NetworkShipSystemsState.Instance;
            var eventCoordinator = NetworkEventCoordinator.Instance;
            var hostRecord = FindObjectsByType<NetworkPlayerItemRecord>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(record => record.IsSpawned
                    && record.OwnerClientId == NetworkManager.ServerClientId);
            var lighting = FindObjectsByType<PHSShipPowerFailureLighting>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(controller => controller.isActiveAndEnabled);
            ReportShipPowerStateServerRpc(
                token,
                shipState != null && shipState.IsSpawned,
                shipState != null && shipState.IsPowerEnabled,
                shipState != null && shipState.IsGravityEnabled,
                shipState != null && shipState.IsBatteryInstalled,
                shipState == null ? uint.MaxValue : shipState.Revision,
                hostRecord == null ? uint.MaxValue : hostRecord.Revision,
                hostRecord == null ? "record_missing" : hostRecord.HeldItemId,
                eventCoordinator != null && eventCoordinator.IsEventActive(EventId.PowerOff),
                lighting != null,
                lighting != null && lighting.IsBlackoutApplied,
                lighting != null && lighting.IsEmergencyLightingActive,
                lighting == null ? -1f : lighting.CurrentAmbientIntensityRatio);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportShipPowerStateServerRpc(
            uint token,
            bool stateFound,
            bool powerEnabled,
            bool gravityEnabled,
            bool batteryInstalled,
            uint shipRevision,
            uint itemRevision,
            string heldItemId,
            bool powerOffActive,
            bool lightingFound,
            bool blackoutApplied,
            bool emergencyLightingActive,
            float ambientIntensityRatio,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            shipPowerReports[rpcParams.Receive.SenderClientId] = new ShipPowerReport(
                stateFound,
                powerEnabled,
                gravityEnabled,
                batteryInstalled,
                shipRevision,
                itemRevision,
                heldItemId,
                powerOffActive,
                lightingFound,
                blackoutApplied,
                emergencyLightingActive,
                ambientIntensityRatio);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportDebrisSaleStateServerRpc(
            uint token,
            int credits,
            uint revision,
            string heldItemId,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            debrisSaleStateReports[rpcParams.Receive.SenderClientId] =
                new DebrisSaleStateReport(credits, revision, heldItemId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportIncidentStateServerRpc(
            uint token,
            bool found,
            NetworkRunIncidentSnapshot snapshot,
            int commandCount,
            ulong commandSignature,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            incidentReports[rpcParams.Receive.SenderClientId] =
                new IncidentReport(
                    found,
                    snapshot,
                    commandCount,
                    commandSignature);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportEconomyStateServerRpc(
            uint token,
            bool found,
            int credits,
            uint revision,
            int pendingCount,
            int claimedCount,
            int deliveredCount,
            string lastTransactionId,
            NetworkRunEconomyTransactionKind lastTransactionKind,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            economyReports[rpcParams.Receive.SenderClientId] = new EconomyReport(
                found,
                credits,
                revision,
                pendingCount,
                claimedCount,
                deliveredCount,
                lastTransactionId,
                lastTransactionKind);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportShopStateServerRpc(
            uint token,
            string offerSignature,
            int displayedCount,
            NetworkPlayerGravityMode gravityMode,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            shopStateReports[rpcParams.Receive.SenderClientId] =
                new ShopStateReport(offerSignature, displayedCount, gravityMode);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportRunFlowStateServerRpc(
            uint token,
            NetworkRunPhase phase,
            int clearedZoneCount,
            int completedShopCycleCount,
            bool finalShopPending,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            runFlowReports[rpcParams.Receive.SenderClientId] = new RunFlowReport(
                phase,
                clearedZoneCount,
                completedShopCycleCount,
                finalShopPending);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportStageClockServerRpc(
            uint token,
            bool found,
            int mapId,
            uint stageSequence,
            uint revision,
            NetworkRunStageClockState state,
            float remainingSeconds,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            stageClockReports[rpcParams.Receive.SenderClientId] = new StageClockReport(
                found,
                mapId,
                stageSequence,
                revision,
                state,
                remainingSeconds);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportFarSelectProbeServerRpc(
            uint token,
            bool requestIssued,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token) || rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId) return;
            farSelectRequestIssued = requestIssued;
            farSelectProbeReported = true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportMapChoicesServerRpc(
            uint token,
            int leftZoneId,
            int rightZoneId,
            bool ready,
            bool randomLedgerFound,
            ulong runSeed,
            uint algorithmVersion,
            uint randomRevision,
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            mapChoiceReports[rpcParams.Receive.SenderClientId] =
                new MapChoiceReport(
                    leftZoneId,
                    rightZoneId,
                    ready,
                    randomLedgerFound,
                    runSeed,
                    algorithmVersion,
                    randomRevision);
        }

        private static ulong ComputeIncidentCommandSignature(
            NetworkRunIncidentLedger ledger)
        {
            const ulong OffsetBasis = 14695981039346656037UL;
            var signature = OffsetBasis;
            MixIncidentSignature(
                ref signature,
                (ulong)ledger.CommandCount);
            for (var index = 0; index < ledger.CommandCount; index++)
            {
                var command = ledger.GetCommandAt(index);
                MixIncidentSignature(ref signature, command.CommandId);
                MixIncidentSignature(ref signature, command.RequestId.ToString());
                MixIncidentSignature(ref signature, command.ParentCommandId);
                MixIncidentSignature(ref signature, command.StageSequence);
                MixIncidentSignature(
                    ref signature,
                    unchecked((ulong)(long)command.MapId));
                MixIncidentSignature(
                    ref signature,
                    (byte)command.Channel);
                MixIncidentSignature(
                    ref signature,
                    (byte)command.PayloadKind);
                MixIncidentSignature(
                    ref signature,
                    (byte)command.IncidentFamily);
                MixIncidentSignature(
                    ref signature,
                    unchecked((ulong)(long)command.ContentId));
                MixIncidentSignature(
                    ref signature,
                    (byte)command.SourceKind);
                MixIncidentSignature(ref signature, command.PressureCost);
                MixIncidentSignature(
                    ref signature,
                    BitConverter.GetBytes(command.WarpChargeMultiplier));
                MixIncidentSignature(
                    ref signature,
                    (byte)command.State);
                MixIncidentSignature(
                    ref signature,
                    command.ExecutorNetworkObjectId);
                MixIncidentSignature(
                    ref signature,
                    command.RuntimeInstanceId);
                MixIncidentSignature(ref signature, command.TargetId.ToString());
                MixIncidentSignature(ref signature, command.OutcomeId.ToString());
                MixIncidentSignature(ref signature, command.CancelReason.ToString());
                MixIncidentSignature(ref signature, command.Revision);
                MixIncidentSignature(ref signature, command.StateRevision);
                MixIncidentSignature(
                    ref signature,
                    BitConverter.GetBytes(command.ChangedAtServerTime));
            }

            return signature;
        }

        private static void MixIncidentSignature(
            ref ulong signature,
            ulong value)
        {
            const ulong Prime = 1099511628211UL;
            for (var shift = 0; shift < 64; shift += 8)
            {
                signature ^= (byte)(value >> shift);
                signature *= Prime;
            }
        }

        private static void MixIncidentSignature(
            ref ulong signature,
            byte value)
        {
            const ulong Prime = 1099511628211UL;
            signature ^= value;
            signature *= Prime;
        }

        private static void MixIncidentSignature(
            ref ulong signature,
            byte[] values)
        {
            MixIncidentSignature(ref signature, (ulong)values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                MixIncidentSignature(ref signature, values[index]);
            }
        }

        private static void MixIncidentSignature(
            ref ulong signature,
            string value)
        {
            value ??= string.Empty;
            MixIncidentSignature(ref signature, (ulong)value.Length);
            for (var index = 0; index < value.Length; index++)
            {
                MixIncidentSignature(
                    ref signature,
                    (ulong)value[index]);
            }
        }

        private bool AcceptProbe(uint token)
        {
            return IsServer && scenarioRunning && !scenarioFinished && token == activeProbeToken;
        }

        private uint NextEventRepairRequestSequence()
        {
            eventRepairRequestSequence =
                NextNonZeroSequence(eventRepairRequestSequence);
            return eventRepairRequestSequence;
        }

        private static uint NextNonZeroSequence(uint sequence)
        {
            sequence++;
            return sequence == 0U ? 1U : sequence;
        }

        private static ulong AdvanceNonZeroCommandId(
            ulong commandId,
            int steps)
        {
            for (var index = 0; index < steps; index++)
            {
                commandId++;
                if (commandId == 0UL)
                {
                    commandId = 1UL;
                }
            }

            return commandId;
        }

        private static uint AdvanceNonZeroSequence(uint sequence, int steps)
        {
            for (var index = 0; index < steps; index++)
            {
                sequence = NextNonZeroSequence(sequence);
            }

            return sequence;
        }

        private void Pass(string details)
        {
            if (scenarioFinished) return;
            scenarioFinished = true;
            scenarioRunning = false;
            Debug.Log($"PHS_P0_RESULT PASS {details}", this);
        }

        private void Fail(string reason)
        {
            if (scenarioFinished) return;
            scenarioFinished = true;
            scenarioRunning = false;
            Debug.LogError($"PHS_P0_RESULT FAIL reason={reason}", this);
        }

        private static bool IsScenarioEnabled()
        {
            return Debug.isDebugBuild
                && (HasCommandLineFlag(ScenarioFlag)
                    || HasCommandLineFlag(ItemScenarioFlag)
                    || HasCommandLineFlag(InputOnlyScenarioFlag));
        }

        private static bool HasCommandLineFlag(string flag)
        {
            return Environment.GetCommandLineArgs().Any(
                argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
        }

        private static int GetCommandLineInt(string key, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], key, StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[index + 1], out var value))
                {
                    return value;
                }
            }

            return fallback;
        }
    }
}
