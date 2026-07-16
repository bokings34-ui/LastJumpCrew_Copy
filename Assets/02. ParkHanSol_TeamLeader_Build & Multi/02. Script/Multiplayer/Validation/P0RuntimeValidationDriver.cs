using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Shop;
using SM;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class P0RuntimeValidationDriver : NetworkBehaviour
    {
        private const string ScenarioFlag = "-phsAutoP0Scenario";
        private const string MapSceneName = "PHS_Map_ver1";
        private const string DebrisSceneName = "PHS_DebrisCollectionScene";
        private const string ShopSceneName = "PHS_ExteriorShopScene";
        private const float DefaultStepTimeout = 90f;

        [Header("P2 Runtime Validation")]
        [SerializeField] private UtilityItemPrefabData validationBatteryItem;

        private readonly Dictionary<ulong, string> sceneReports = new();
        private readonly Dictionary<ulong, GaugeReport> gaugeReports = new();
        private readonly Dictionary<ulong, MapChoiceReport> mapChoiceReports = new();
        private readonly Dictionary<ulong, ShopStateReport> shopStateReports = new();
        private readonly Dictionary<ulong, RunFlowReport> runFlowReports = new();
        private readonly Dictionary<ulong, DebrisSaleStateReport> debrisSaleStateReports = new();
        private readonly Dictionary<ulong, EventSnapshotReport> eventSnapshotReports = new();
        private readonly Dictionary<ulong, EventTerminalReport> eventTerminalReports = new();
        private readonly Dictionary<ulong, ShipPowerReport> shipPowerReports = new();
        private readonly HashSet<ulong> eventObservationReadyClients = new();

        private uint activeProbeToken;
        private int expectedClientCount;
        private bool farSelectProbeReported;
        private bool farSelectRequestIssued;
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
            public MapChoiceReport(int leftZoneId, int rightZoneId, bool ready)
            {
                LeftZoneId = leftZoneId;
                RightZoneId = rightZoneId;
                Ready = ready;
            }

            public int LeftZoneId { get; }
            public int RightZoneId { get; }
            public bool Ready { get; }
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
                MiniGameType miniGameType,
                EventId externalEventId,
                EventId chainedFailureEventId)
            {
                MiniGameType = miniGameType;
                ExternalEventId = externalEventId;
                ChainedFailureEventId = chainedFailureEventId;
            }

            public MiniGameType MiniGameType { get; }
            public EventId ExternalEventId { get; }
            public EventId ChainedFailureEventId { get; }
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
                bool powerOffActive)
            {
                StateFound = stateFound;
                PowerEnabled = powerEnabled;
                GravityEnabled = gravityEnabled;
                BatteryInstalled = batteryInstalled;
                ShipRevision = shipRevision;
                ItemRevision = itemRevision;
                HeldItemId = heldItemId;
                PowerOffActive = powerOffActive;
            }

            public bool StateFound { get; }
            public bool PowerEnabled { get; }
            public bool GravityEnabled { get; }
            public bool BatteryInstalled { get; }
            public uint ShipRevision { get; }
            public uint ItemRevision { get; }
            public string HeldItemId { get; }
            public bool PowerOffActive { get; }
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
            StartCoroutine(RunServerScenario());
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

            yield return RunEventLifecycleValidation();
            if (scenarioFinished) yield break;

            yield return RunShipPowerBatteryValidation();
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
                earlySelectionReason != "warp_ready_required")
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

            yield return RunDebrisRoundTrip();
            if (scenarioFinished) yield break;

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

            yield return ProbeMapChoices();
            if (scenarioFinished) yield break;

            yield return ProbeFarSelectRejection();
            if (scenarioFinished) yield break;

            if (!TryAcquireMapSceneReferences(out var coordinator, out _, out var safeTrigger))
            {
                Fail("post_debris_map_references_missing");
                yield break;
            }

            if (coordinator.TryActivateWarp(NetworkManager.ServerClientId, out var unsafeReason) ||
                string.IsNullOrWhiteSpace(unsafeReason) ||
                !unsafeReason.StartsWith("players_not_safe", StringComparison.Ordinal))
            {
                Fail($"unsafe_warp_not_rejected reason={unsafeReason ?? "none"}");
                yield break;
            }

            MoveConnectedPlayersIntoSafeZone(safeTrigger);
            yield return WaitFor(
                () => coordinator.RequiredSafePlayerCount == expectedClientCount &&
                    coordinator.SafePlayerCount == expectedClientCount &&
                    coordinator.IsWarpSafetySatisfied,
                15f,
                $"safe_gate_not_satisfied safe={coordinator.SafePlayerCount} required={coordinator.RequiredSafePlayerCount}");
            if (scenarioFinished) yield break;

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
                if (!TryAcquireMapSceneReferences(out coordinator, out _, out safeTrigger))
                {
                    Fail($"map_cycle_references_missing cycle={expectedClearedZones}");
                    yield break;
                }

                yield return RunAdditionalWarpCycle(coordinator, safeTrigger, expectedClearedZones);
                if (scenarioFinished) yield break;

                if (expectedClearedZones % 3 != 0)
                {
                    continue;
                }

                var expectedShopCycles = expectedClearedZones / 3;
                var expectedShopPhase = expectedClearedZones == 9
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

                if (!TryAcquireMapSceneReferences(out coordinator, out var shopConsole, out safeTrigger) ||
                    !shopConsole.CanSelectSide(TravelConsoleSide.Left) ||
                    shopConsole.CanSelectSide(TravelConsoleSide.Right) ||
                    shopConsole.TryGetCurrentMapChoices(out _, out _))
                {
                    Fail($"shop_choice_invalid cycle={expectedClearedZones}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P0_SHOP_PHASE_OK zones={expectedClearedZones} cycles={expectedShopCycles} " +
                    $"phase={expectedShopPhase}",
                    this);

                yield return RunShopRoundTrip(
                    shopConsole,
                    expectedClearedZones,
                    expectedShopCycles,
                    expectedShopPhase,
                    validatePurchaseAtomicity: expectedClearedZones == 3);
                if (scenarioFinished) yield break;

                if (!TryAcquireMapSceneReferences(out coordinator, out _, out safeTrigger))
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
                $"unsafeReject={unsafeReason} safe={safePlayerCountBeforeWarp}/{requiredSafePlayerCountBeforeWarp} " +
                $"zones={coordinator.ClearedZoneCount} shopCycles={coordinator.CompletedShopCycleCount} " +
                $"runPhase={coordinator.Phase} runPeers={runFlowReports.Count} " +
                "events=3 miniGameApiOutcomes=6 eventPeers=2 farEventReject=true");
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
                    MiniGameType.Cannon,
                    EventId.MeteorAttack,
                    EventId.OxygenLeak),
                new ExternalMiniGameValidationCase(
                    MiniGameType.WireFix,
                    EventId.EmpAttack,
                    EventId.PowerOff),
                new ExternalMiniGameValidationCase(
                    MiniGameType.PowerSync,
                    EventId.EnemyScout,
                    EventId.EnemySpawn)
            };

            Debug.Log(
                "PHS_P1_MINIGAME_API_CONTRACT_BEGIN mode=headless_api_contract uiInteraction=false authority=server",
                this);

            foreach (var validationCase in validationCases)
            {
                var terminal = FindObjectsByType<MiniGameTerminal>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(candidate =>
                        candidate != null && candidate.miniGameType == validationCase.MiniGameType);
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
            MiniGameTerminal terminal,
            ExternalMiniGameValidationCase validationCase,
            bool succeeded)
        {
            var outcomeLabel = succeeded ? "success" : "failure";
            if (eventCoordinator.IsEventActive(validationCase.ExternalEventId)
                || eventManager.IsActive(validationCase.ExternalEventId)
                || eventCoordinator.IsEventActive(validationCase.ChainedFailureEventId)
                || eventManager.IsActive(validationCase.ChainedFailureEventId))
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

            if (!eventCoordinator.TrySpawnEventServer(
                    validationCase.ExternalEventId,
                    out var externalInstanceId)
                || externalInstanceId == 0UL)
            {
                Fail(
                    $"p1_minigame_external_spawn_failed event={validationCase.ExternalEventId} " +
                    $"outcome={outcomeLabel}");
                yield break;
            }

            yield return WaitFor(
                () => eventCoordinator.TryGetSnapshot(externalInstanceId, out var snapshot)
                    && snapshot.EventId == validationCase.ExternalEventId
                    && snapshot.State == EventState.InProgress,
                5f,
                $"p1_minigame_external_not_in_progress event={validationCase.ExternalEventId} " +
                $"outcome={outcomeLabel}");
            if (scenarioFinished) yield break;

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
                    && snapshot.State == expectedTerminalState
                    && (!succeeded
                        ? TryFindActiveEventSnapshot(
                            eventCoordinator,
                            validationCase.ChainedFailureEventId,
                            out _)
                        : !eventCoordinator.IsEventActive(validationCase.ChainedFailureEventId)),
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
                Debug.Log(
                    $"PHS_P1_MINIGAME_OUTCOME_OK type={validationCase.MiniGameType} " +
                    $"event={validationCase.ExternalEventId} outcome=success distance={terminalDistance:F3} " +
                    $"peers={eventTerminalReports.Count} uiInteraction=false",
                    this);
                yield break;
            }

            if (!TryFindActiveEventSnapshot(
                    eventCoordinator,
                    validationCase.ChainedFailureEventId,
                    out var chainedSnapshot))
            {
                Fail(
                    $"p1_minigame_chained_event_missing external={validationCase.ExternalEventId} " +
                    $"chained={validationCase.ChainedFailureEventId}");
                yield break;
            }

            yield return ProbeEventSnapshot(
                validationCase.ChainedFailureEventId,
                chainedSnapshot.InstanceId,
                requireHostLocalEffect: false);
            if (scenarioFinished) yield break;

            var chainedActiveSnapshot = eventSnapshotReports.Values.First();
            yield return BeginEventTerminalObservation(
                chainedSnapshot.InstanceId,
                $"p1_minigame_chained_observation_not_ready event={validationCase.ChainedFailureEventId}");
            if (scenarioFinished) yield break;

            if (!eventCoordinator.TryTerminateAllServer())
            {
                Fail(
                    $"p1_minigame_chained_cleanup_rejected event={validationCase.ChainedFailureEventId}");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.75f);
            yield return ProbeEventTerminal(chainedSnapshot.InstanceId, chainedActiveSnapshot.Revision);
            if (scenarioFinished) yield break;

            if (eventCoordinator.TryGetSnapshot(chainedSnapshot.InstanceId, out _)
                || eventManager.IsInstanceActive(chainedSnapshot.InstanceId))
            {
                Fail(
                    $"p1_minigame_chained_cleanup_incomplete event={validationCase.ChainedFailureEventId}");
                yield break;
            }

            if (validationCase.ChainedFailureEventId == EventId.PowerOff)
            {
                var shipState = NetworkShipSystemsState.Instance;
                string restoreReason = null;
                if (shipState == null
                    || ((!shipState.IsPowerEnabled || !shipState.IsGravityEnabled)
                        && !shipState.TryRestorePowerWithBattery(out restoreReason)))
                {
                    Fail(
                        $"p1_minigame_power_cleanup_failed reason={restoreReason ?? "ship_state_missing"}");
                    yield break;
                }

                yield return WaitFor(
                    () => shipState.IsPowerEnabled && shipState.IsGravityEnabled,
                    5f,
                    "p1_minigame_power_cleanup_not_applied");
                if (scenarioFinished) yield break;
            }

            Debug.Log(
                $"PHS_P1_MINIGAME_OUTCOME_OK type={validationCase.MiniGameType} " +
                $"event={validationCase.ExternalEventId} outcome=failure " +
                $"chained={validationCase.ChainedFailureEventId} distance={terminalDistance:F3} " +
                $"peers={eventTerminalReports.Count} uiInteraction=false",
                this);
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
            var eventCoordinator = FindAnyObjectByType<NetworkEventCoordinator>(FindObjectsInactive.Include);
            var eventManager = EventManager.Peek();
            var shipState = NetworkShipSystemsState.Instance;
            var miniGameTerminal = FindObjectsByType<MiniGameTerminal>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(terminal => terminal != null && terminal.miniGameType == MiniGameType.WireFix);
            var batterySocket = FindObjectsByType<BatteryInsertPowerStationSocket>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(socket => socket != null && socket.IsSpawned);
            if (validationBatteryItem == null || !validationBatteryItem.HasHeldPrefab
                || string.IsNullOrWhiteSpace(validationBatteryItem.ItemId)
                || eventCoordinator == null || !eventCoordinator.IsSpawned || !eventCoordinator.IsServer
                || eventManager == null || !eventManager.HasRuntimeBridge() || !eventManager.IsRuntimeAuthority()
                || shipState == null || !shipState.IsSpawned || !shipState.IsServer
                || miniGameTerminal == null || batterySocket == null
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
                || eventCoordinator.IsEventActive(EventId.PowerOff)
                || eventManager.IsActive(EventId.PowerOff))
            {
                Fail("p2_ship_power_player_or_event_state_invalid");
                yield break;
            }

            if (!eventCoordinator.IsEventActive(EventId.EmpAttack)
                && !eventManager.IsActive(EventId.EmpAttack)
                && !eventCoordinator.TrySpawnEventServer(EventId.EmpAttack, out _))
            {
                Fail("p2_emp_spawn_failed");
                yield break;
            }

            yield return WaitFor(
                () => eventCoordinator.IsEventActive(EventId.EmpAttack)
                    && eventManager.IsActive(EventId.EmpAttack),
                5f,
                "p2_emp_not_active");
            if (scenarioFinished) yield break;

            SetPlayerPosition(playerObject, miniGameTerminal.transform.position);
            yield return null;
            if (!eventCoordinator.RequestMiniGameResult(EventId.EmpAttack, false))
            {
                Fail("p2_emp_failure_request_rejected");
                yield break;
            }

            yield return WaitFor(
                () => eventCoordinator.IsEventActive(EventId.PowerOff)
                    && eventManager.IsActive(EventId.PowerOff)
                    && !shipState.IsPowerEnabled
                    && !shipState.IsGravityEnabled
                    && !shipState.IsBatteryInstalled,
                5f,
                "p2_power_off_not_applied");
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
                () => !eventCoordinator.IsEventActive(EventId.PowerOff)
                    && !eventManager.IsActive(EventId.PowerOff),
                5f,
                "p2_power_off_not_resolved");
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
                $"peers={shipPowerReports.Count} duplicateReason={repeatedRestoreReason}",
                this);
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
                        && !report.PowerOffActive))
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
                        $"powerOff={report.Value.PowerOffActive}"));
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
            var requiredItemId = eventId == EventId.Fire ? "fire_extinguisher" : "wrench";
            var wrongItemId = eventId == EventId.Fire ? "wrench" : "fire_extinguisher";
            itemRecord.ReportHeldItem(wrongItemId);
            if (coordinator.RequestEffectRepair(repairTarget, itemRecord, 1U))
            {
                Fail($"event_repair_wrong_item_accepted event={eventId}");
                yield break;
            }

            itemRecord.ReportHeldItem(requiredItemId);
            SetPlayerPosition(playerObject, repairTarget is IEventRepairableEffect serverTarget
                ? serverTarget.RepairPosition + Vector3.right * 10f
                : originalPosition + Vector3.right * 10f);
            if (coordinator.RequestEffectRepair(repairTarget, itemRecord, 1U))
            {
                Fail($"event_repair_far_request_accepted event={eventId}");
                yield break;
            }

            var targetPosition = repairTarget is IEventRepairableEffect authoritativeTarget
                ? authoritativeTarget.RepairPosition
                : originalPosition;
            SetPlayerPosition(playerObject, targetPosition);
            var requestSequence = 2U;
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
                requestSequence++;
                if (!coordinator.RequestEffectRepair(repairTarget, itemRecord, requestSequence))
                {
                    Fail(
                        $"event_repair_step_rejected event={eventId} sequence={requestSequence}");
                    yield break;
                }

                yield return null;
            }

            itemRecord.ReportHeldItem(string.Empty);
            SetPlayerPosition(playerObject, originalPosition);
            if (coordinator.IsEventActive(eventId))
            {
                Fail($"event_repair_timeout event={eventId} sequence={requestSequence}");
                yield break;
            }

            Debug.Log(
                $"PHS_P1_EVENT_REPAIR_OK event={eventId} instance={instanceId} item={requiredItemId} " +
                $"steps={requestSequence - 1U} wrongItemReject=true farReject=true duplicateReject=true authority=server",
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
            var terminal = FindAnyObjectByType<ShipAccidentEventTerminal>(FindObjectsInactive.Include);
            var remoteClientId = NetworkManager.ConnectedClientsIds.FirstOrDefault(
                clientId => clientId != NetworkManager.ServerClientId);
            if (terminal == null || remoteClientId == NetworkManager.ServerClientId ||
                !NetworkManager.ConnectedClients.TryGetValue(remoteClientId, out var remoteClient) ||
                remoteClient.PlayerObject == null)
            {
                Fail("far_event_terminal_probe_setup_missing");
                yield break;
            }

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

            if (!farEventTerminalRequestIssued || coordinator.IsEventActive(EventId.Fire) ||
                manager.IsActive(EventId.Fire))
            {
                Fail(
                    $"far_event_terminal_not_rejected issued={farEventTerminalRequestIssued} " +
                    $"snapshotActive={coordinator.IsEventActive(EventId.Fire)} localActive={manager.IsActive(EventId.Fire)}");
                yield break;
            }

            Debug.Log($"PHS_P0_FAR_EVENT_TERMINAL_REJECTED client={remoteClientId}", this);
        }

        private IEnumerator RunShopRoundTrip(
            NetworkTravelConsoleController console,
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

            SetPlayerPosition(playerObject, console.transform.position);
            yield return null;
            console.RequestSelectSide(holder, TravelConsoleSide.Left);
            if (console.SelectedDestination != TravelConsoleDestination.Shop || !console.CanExecute(holder))
            {
                Fail($"shop_destination_not_selected destination={console.SelectedDestination}");
                yield break;
            }

            console.Execute(holder);
            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == ShopSceneName,
                30f,
                "shop_scene_not_loaded");
            if (scenarioFinished) yield break;

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
                if (purchaseService == null || deliveryService == null)
                {
                    Fail("shop_purchase_services_missing");
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

                var creditsBeforeFailure = purchaseService.AvailableCredits;
                var pendingBeforeFailure = deliveryService.PendingCount;
                var excessiveCount = Mathf.Clamp(creditsBeforeFailure / product.PurchasePrice + 1, 1, 16);
                var excessiveRequests = Enumerable.Range(0, excessiveCount)
                    .Select(index => new ShopPurchaseRequest($"p0_fail_{index}", product))
                    .ToArray();
                var failureAccepted = purchaseService.TryPurchase(excessiveRequests, out var failureResult);
                if (failureAccepted || failureResult.Success || failureResult.Reason != "insufficient_credits" ||
                    purchaseService.AvailableCredits != creditsBeforeFailure ||
                    deliveryService.PendingCount != pendingBeforeFailure ||
                    display.DisplayedProductCount != initialCount ||
                    GetShopOfferSignature(display) != initialSignature)
                {
                    Fail(
                        $"shop_insufficient_atomicity_failed accepted={failureAccepted} reason={failureResult.Reason ?? "none"} " +
                        $"credits={purchaseService.AvailableCredits}/{creditsBeforeFailure} " +
                        $"pending={deliveryService.PendingCount}/{pendingBeforeFailure}");
                    yield break;
                }

                Debug.Log(
                    $"PHS_P0_SHOP_INSUFFICIENT_OK credits={creditsBeforeFailure} total={failureResult.TotalPrice}",
                    this);

                var creditsBeforeSuccess = purchaseService.AvailableCredits;
                var pendingBeforeSuccess = deliveryService.PendingCount;
                var successAccepted = purchaseService.TryPurchase(
                    new[] { new ShopPurchaseRequest("p0_success", product) },
                    out var successResult);
                if (!successAccepted || !successResult.Success || successResult.PurchasedCount != 1 ||
                    purchaseService.AvailableCredits != creditsBeforeSuccess - product.PurchasePrice ||
                    deliveryService.PendingCount != pendingBeforeSuccess + 1 ||
                    display.DisplayedProductCount != initialCount - 1)
                {
                    Fail(
                        $"shop_success_atomicity_failed accepted={successAccepted} reason={successResult.Reason ?? "none"} " +
                        $"credits={purchaseService.AvailableCredits} pending={deliveryService.PendingCount} " +
                        $"displayed={display.DisplayedProductCount}/{initialCount - 1}");
                    yield break;
                }

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

            var returnPortal = FindAnyObjectByType<NetworkScenePortalInteractable>(FindObjectsInactive.Include);
            if (returnPortal == null)
            {
                Fail("shop_return_portal_missing");
                yield break;
            }

            SetPlayerPosition(playerObject, returnPortal.transform.position);
            yield return null;
            returnPortal.Interact(holder);
            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                30f,
                "shop_return_map_not_loaded");
            if (scenarioFinished) yield break;

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => NetworkRunFlowCoordinator.Instance != null &&
                    NetworkRunFlowCoordinator.Instance.ClearedZoneCount == expectedClearedZones &&
                    NetworkRunFlowCoordinator.Instance.CompletedShopCycleCount == expectedShopCycles &&
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

        private static string GetShopOfferSignature(ShopRandomDisplayController display)
        {
            return string.Join(
                "|",
                Enumerable.Range(0, display.SlotCount)
                    .Select(index => display.GetDisplayedProductAt(index)?.OfferId ?? "-"));
        }

        private static bool TryAcquireMapSceneReferences(
            out NetworkRunFlowCoordinator coordinator,
            out NetworkTravelConsoleController console,
            out BoxCollider safeTrigger)
        {
            coordinator = NetworkRunFlowCoordinator.Instance;
            console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            var safeZone = FindAnyObjectByType<NetworkWarpSafeZone>(FindObjectsInactive.Include);
            safeTrigger = safeZone == null ? null : safeZone.GetComponent<BoxCollider>();
            return SceneManager.GetActiveScene().name == MapSceneName &&
                coordinator != null &&
                console != null &&
                safeTrigger != null &&
                safeTrigger.isTrigger;
        }

        private IEnumerator RunAdditionalWarpCycle(
            NetworkRunFlowCoordinator coordinator,
            BoxCollider safeTrigger,
            int expectedClearedZones)
        {
            MoveConnectedPlayersOutsideSafeZone(safeTrigger);
            yield return WaitFor(
                () => coordinator.SafePlayerCount == 0,
                5f,
                $"safe_zone_exit_not_recorded cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => coordinator.Phase == NetworkRunPhase.Charging,
                DefaultStepTimeout,
                $"cycle_charging_not_reached cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            if (coordinator.TrySelectNextZone(expectedClearedZones, out var earlyReason) ||
                earlyReason != "warp_ready_required")
            {
                Fail($"cycle_early_selection_not_blocked cycle={expectedClearedZones} reason={earlyReason ?? "none"}");
                yield break;
            }

            yield return WaitFor(
                () => coordinator.Phase == NetworkRunPhase.WarpReady,
                DefaultStepTimeout,
                $"cycle_warp_ready_not_reached cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

            yield return ProbeMapChoices();
            if (scenarioFinished) yield break;

            if (coordinator.TryActivateWarp(NetworkManager.ServerClientId, out var unsafeReason) ||
                string.IsNullOrWhiteSpace(unsafeReason) ||
                !unsafeReason.StartsWith("players_not_safe", StringComparison.Ordinal))
            {
                Fail($"cycle_unsafe_warp_not_rejected cycle={expectedClearedZones} reason={unsafeReason ?? "none"}");
                yield break;
            }

            MoveConnectedPlayersIntoSafeZone(safeTrigger);
            yield return WaitFor(
                () => coordinator.RequiredSafePlayerCount == expectedClientCount &&
                    coordinator.SafePlayerCount == expectedClientCount &&
                    coordinator.IsWarpSafetySatisfied,
                15f,
                $"cycle_safe_gate_not_satisfied cycle={expectedClearedZones}");
            if (scenarioFinished) yield break;

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
            var console = FindAnyObjectByType<NetworkTravelConsoleController>(FindObjectsInactive.Include);
            if (console == null ||
                !NetworkManager.ConnectedClients.TryGetValue(NetworkManager.ServerClientId, out var hostClient) ||
                hostClient.PlayerObject == null)
            {
                Fail("debris_entry_setup_missing");
                yield break;
            }

            var playerObject = hostClient.PlayerObject;
            var holder = playerObject.GetComponent<TempPlayerItemHolder>();
            if (holder == null)
            {
                Fail("debris_entry_holder_missing");
                yield break;
            }

            SetPlayerPosition(playerObject, console.transform.position);
            yield return null;
            console.RequestSelectSide(holder, TravelConsoleSide.Right);
            if (console.SelectedDestination != TravelConsoleDestination.DebrisCollection || !console.CanExecute(holder))
            {
                Fail($"debris_destination_not_selected destination={console.SelectedDestination}");
                yield break;
            }

            console.Execute(holder);
            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == DebrisSceneName,
                30f,
                "debris_scene_not_loaded");
            if (scenarioFinished) yield break;

            yield return ProbeScenes(DebrisSceneName);
            if (scenarioFinished) yield break;

            var collectionZone = FindAnyObjectByType<NetworkDebrisCollectionZone>(FindObjectsInactive.Include);
            var safeVolume = FindAnyObjectByType<NetworkDebrisSafeVolume>(FindObjectsInactive.Include);
            var collectionTrigger = collectionZone == null ? null : collectionZone.GetComponent<SphereCollider>();
            var safeTrigger = safeVolume == null ? null : safeVolume.GetComponent<SphereCollider>();
            var lifeState = playerObject.GetComponent<NetworkPlayerLifeState>();
            var context = GameplaySceneContext.FindForScene(SceneManager.GetActiveScene());
            if (collectionTrigger == null || safeTrigger == null || lifeState == null || context == null ||
                !collectionTrigger.isTrigger || !safeTrigger.isTrigger ||
                safeTrigger.bounds.extents.z >= collectionTrigger.bounds.extents.z)
            {
                Fail("debris_volume_setup_invalid");
                yield break;
            }

            yield return ValidatePhysicalDebrisSale(playerObject, holder);
            if (scenarioFinished) yield break;

            var outerDistance = Mathf.Lerp(
                safeTrigger.bounds.extents.z,
                collectionTrigger.bounds.extents.z,
                0.5f);
            var outerPosition = collectionTrigger.bounds.center + Vector3.forward * outerDistance;
            var outsidePosition = collectionTrigger.bounds.center +
                Vector3.forward * (collectionTrigger.bounds.extents.z + 10f);
            MovePlayerWithCharacterController(playerObject, outsidePosition);
            yield return WaitFor(
                () => !collectionZone.IsPlayerInside(NetworkManager.ServerClientId),
                3f,
                "debris_outer_exit_not_recorded");
            if (scenarioFinished) yield break;
            MovePlayerWithCharacterController(playerObject, outerPosition);
            yield return WaitFor(
                () => lifeState.IsAlive && lifeState.DeadZoneWarningRemainingSeconds > 0f,
                3f,
                $"debris_warning_not_started player={playerObject.transform.position} outer={outerPosition} " +
                $"outside={outsidePosition} inside={collectionZone.IsPlayerInside(NetworkManager.ServerClientId)} " +
                $"warning={lifeState.DeadZoneWarningRemainingSeconds:F2}");
            if (scenarioFinished) yield break;

            MovePlayerWithCharacterController(playerObject, safeTrigger.bounds.center);
            yield return WaitFor(
                () => lifeState.DeadZoneWarningRemainingSeconds < 0f,
                3f,
                "debris_warning_not_cancelled");
            if (scenarioFinished) yield break;

            yield return new WaitForSecondsRealtime(5.5f);
            if (!lifeState.IsAlive)
            {
                Fail("debris_safe_volume_did_not_prevent_death");
                yield break;
            }

            Debug.Log("PHS_P0_DEBRIS_WARNING_CANCEL_OK", this);

            MovePlayerWithCharacterController(playerObject, outsidePosition);
            yield return WaitFor(
                () => !collectionZone.IsPlayerInside(NetworkManager.ServerClientId),
                3f,
                "debris_second_outer_exit_not_recorded");
            if (scenarioFinished) yield break;
            MovePlayerWithCharacterController(playerObject, outerPosition);
            yield return WaitFor(
                () => !lifeState.IsAlive && lifeState.IsWaitingForAutomaticRespawn,
                7f,
                "debris_dead_zone_death_not_observed");
            if (scenarioFinished) yield break;

            yield return WaitFor(
                () => lifeState.IsAlive,
                7f,
                "debris_automatic_respawn_not_observed");
            if (scenarioFinished) yield break;

            if (!context.TryGetRespawnPoint(out var respawnPoint) ||
                Vector3.Distance(playerObject.transform.position, respawnPoint.position) > 2f ||
                playerObject.GetComponent<CharacterController>() is not { enabled: true })
            {
                Fail($"debris_respawn_state_invalid position={playerObject.transform.position}");
                yield break;
            }

            Debug.Log("PHS_P0_DEBRIS_DEATH_RESPAWN_OK", this);

            var returnPortal = FindAnyObjectByType<NetworkScenePortalInteractable>(FindObjectsInactive.Include);
            if (returnPortal == null)
            {
                Fail("debris_return_portal_missing");
                yield break;
            }

            SetPlayerPosition(playerObject, returnPortal.transform.position);
            yield return null;
            returnPortal.Interact(holder);
            yield return WaitFor(
                () => SceneManager.GetActiveScene().name == MapSceneName,
                30f,
                "debris_return_map_not_loaded");
            if (scenarioFinished) yield break;

            yield return ProbeScenes(MapSceneName);
            if (scenarioFinished) yield break;

            Debug.Log("PHS_P0_DEBRIS_ROUNDTRIP_OK", this);
        }

        private IEnumerator ValidatePhysicalDebrisSale(NetworkObject playerObject, TempPlayerItemHolder holder)
        {
            var sellZone = FindAnyObjectByType<DebrisSellZone>(FindObjectsInactive.Include);
            var sellTrigger = sellZone == null ? null : sellZone.GetComponent<BoxCollider>();
            var wallet = FindAnyObjectByType<ShopEconomyWalletAdapter>(FindObjectsInactive.Include);
            var itemRecord = playerObject.GetComponent<NetworkPlayerItemRecord>();
            var debris = FindObjectsByType<DebrisItem>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.Value > 0);
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
                    if (first.Ready && first.LeftZoneId > 0 && first.RightZoneId > 0 &&
                        first.LeftZoneId != first.RightZoneId &&
                        mapChoiceReports.Values.All(report =>
                            report.Ready &&
                            report.LeftZoneId == first.LeftZoneId &&
                            report.RightZoneId == first.RightZoneId))
                    {
                        Debug.Log(
                            $"PHS_P0_CHOICES_OK peers={mapChoiceReports.Count} left={first.LeftZoneId} right={first.RightZoneId}",
                            this);
                        yield break;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            Fail("peer_map_choice_sync_timeout");
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
            var issued = coordinator != null && coordinator.RequestEventFromTerminal(EventId.Fire);
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
            ReportMapChoicesServerRpc(token, left, right, ready);
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
            ReportShipPowerStateServerRpc(
                token,
                shipState != null && shipState.IsSpawned,
                shipState != null && shipState.IsPowerEnabled,
                shipState != null && shipState.IsGravityEnabled,
                shipState != null && shipState.IsBatteryInstalled,
                shipState == null ? uint.MaxValue : shipState.Revision,
                hostRecord == null ? uint.MaxValue : hostRecord.Revision,
                hostRecord == null ? "record_missing" : hostRecord.HeldItemId,
                eventCoordinator != null && eventCoordinator.IsEventActive(EventId.PowerOff));
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
                powerOffActive);
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
            ServerRpcParams rpcParams = default)
        {
            if (!AcceptProbe(token)) return;
            mapChoiceReports[rpcParams.Receive.SenderClientId] =
                new MapChoiceReport(leftZoneId, rightZoneId, ready);
        }

        private bool AcceptProbe(uint token)
        {
            return IsServer && scenarioRunning && !scenarioFinished && token == activeProbeToken;
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
            return Debug.isDebugBuild && HasCommandLineFlag(ScenarioFlag);
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
