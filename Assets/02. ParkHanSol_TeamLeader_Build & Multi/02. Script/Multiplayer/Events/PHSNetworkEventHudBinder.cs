using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SM;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    public enum PHSShipMapInputMode
    {
        Hold,
        Toggle
    }

    [DisallowMultipleComponent]
    public sealed class PHSNetworkEventHudBinder : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour eventHudViewSource;
        [SerializeField] private PHSShipMapInputMode shipMapInputMode = PHSShipMapInputMode.Hold;
        [SerializeField, Min(0.25f)] private float terminalMessageSeconds = 2f;
        [SerializeField, Min(0.05f)] private float bindRetrySeconds = 0.25f;

        private readonly List<NetworkEventLifecycleSnapshot> snapshotBuffer = new();
        private readonly List<NetworkEventLifecycleSnapshot> activeSnapshots = new();
        private readonly List<PHSNetworkEventRoomViewModel> roomViewModels = new();

        private INetworkEventHudView eventHudView;
        private NetworkEventCoordinator boundCoordinator;
        private NetworkEventLifecycleSnapshot terminalSnapshot;
        private bool hasTerminalSnapshot;
        private bool toggleMapVisible;
        private float terminalVisibleUntil;
        private float nextBindAttemptTime;

        private void Awake()
        {
            eventHudView = eventHudViewSource as INetworkEventHudView;
            if (eventHudViewSource == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_BIND_FAILED reason=view_source_missing binder={name}", this);
                enabled = false;
                return;
            }

            if (eventHudView == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_HUD_BIND_FAILED reason=view_interface_missing binder={name} " +
                    $"source={eventHudViewSource.GetType().Name}",
                    this);
                enabled = false;
                return;
            }

            if (!eventHudView.IsConfigured)
            {
                Debug.LogError($"PHS_EVENT_HUD_BIND_FAILED reason=view_not_configured binder={name}", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (eventHudView == null || !eventHudView.IsConfigured)
            {
                return;
            }

            eventHudView.HideOffline();
            TryBindCoordinator();
        }

        private void OnDisable()
        {
            UnbindCoordinator();
            eventHudView?.HideOffline();
        }

        private void Update()
        {
            if (eventHudView == null || !eventHudView.IsConfigured)
            {
                return;
            }

            if (boundCoordinator == null || !boundCoordinator.IsSpawned ||
                NetworkEventCoordinator.Instance != boundCoordinator)
            {
                UnbindCoordinator();
                eventHudView.HideOffline();
                if (Time.unscaledTime >= nextBindAttemptTime)
                {
                    nextBindAttemptTime = Time.unscaledTime + bindRetrySeconds;
                    TryBindCoordinator();
                }

                return;
            }

            if (hasTerminalSnapshot && Time.unscaledTime >= terminalVisibleUntil)
            {
                hasTerminalSnapshot = false;
                RefreshFromCoordinator();
            }

            UpdateMapInput();
        }

        private void TryBindCoordinator()
        {
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                return;
            }

            if (boundCoordinator == coordinator)
            {
                RefreshFromCoordinator();
                return;
            }

            UnbindCoordinator();
            boundCoordinator = coordinator;
            boundCoordinator.LifecycleSnapshotsChanged += RefreshFromCoordinator;
            toggleMapVisible = false;
            eventHudView.SetShipMapVisible(false);
            RefreshFromCoordinator();
            Debug.Log($"PHS_EVENT_HUD_BOUND snapshots={boundCoordinator.SnapshotCount}", this);
        }

        private void UnbindCoordinator()
        {
            if (boundCoordinator != null)
            {
                boundCoordinator.LifecycleSnapshotsChanged -= RefreshFromCoordinator;
                boundCoordinator = null;
            }

            snapshotBuffer.Clear();
            activeSnapshots.Clear();
            roomViewModels.Clear();
            hasTerminalSnapshot = false;
            toggleMapVisible = false;
        }

        private void RefreshFromCoordinator()
        {
            if (boundCoordinator == null || !boundCoordinator.IsSpawned)
            {
                eventHudView.HideOffline();
                return;
            }

            boundCoordinator.CopySnapshotsTo(snapshotBuffer);
            snapshotBuffer.Sort(CompareSnapshots);

            activeSnapshots.Clear();
            NetworkEventLifecycleSnapshot? newestTerminal = null;
            foreach (var snapshot in snapshotBuffer)
            {
                if (!snapshot.IsTerminal)
                {
                    activeSnapshots.Add(snapshot);
                    continue;
                }

                if (!newestTerminal.HasValue || CompareTerminalRecency(snapshot, newestTerminal.Value) > 0)
                {
                    newestTerminal = snapshot;
                }
            }

            if (newestTerminal.HasValue && IsNewTerminalSnapshot(newestTerminal.Value))
            {
                terminalSnapshot = newestTerminal.Value;
                hasTerminalSnapshot = true;
                terminalVisibleUntil = Time.unscaledTime + terminalMessageSeconds;
            }

            BuildRoomViewModels();
            var alertText = BuildAlertText();
            eventHudView.Apply(new PHSNetworkEventHudViewModel(
                alertText,
                activeSnapshots.Count,
                roomViewModels));
        }

        private void BuildRoomViewModels()
        {
            roomViewModels.Clear();
            foreach (var roomGroup in activeSnapshots.GroupBy(
                         snapshot => snapshot.RoomId.ToString(),
                         StringComparer.Ordinal))
            {
                var roomSnapshots = roomGroup.ToArray();
                var builder = new StringBuilder(96);
                builder.Append(roomSnapshots.Length);
                builder.Append(" ACTIVE");
                foreach (var snapshot in roomSnapshots)
                {
                    builder.AppendLine();
                    builder.Append(snapshot.EventId);
                    builder.Append(" · ");
                    builder.Append(GetStateLabel(snapshot.State));
                }

                roomViewModels.Add(new PHSNetworkEventRoomViewModel(
                    roomGroup.Key,
                    builder.ToString(),
                    roomSnapshots.Length));
            }
        }

        private string BuildAlertText()
        {
            if (hasTerminalSnapshot)
            {
                return FormatAlert(terminalSnapshot);
            }

            return activeSnapshots.Count == 0
                ? string.Empty
                : FormatAlert(activeSnapshots[0]);
        }

        private static string FormatAlert(NetworkEventLifecycleSnapshot snapshot)
        {
            return GetEventDisplayName(snapshot.EventId);
        }

        private static string GetEventDisplayName(EventId eventId)
        {
            return eventId switch
            {
                EventId.Fire => "화재",
                EventId.EnemySpawn => "적 침입",
                EventId.PowerOff => "정전",
                EventId.OxygenLeak => "산소 누출",
                EventId.EngineBreak => "엔진 고장",
                EventId.MicDestroy => "통신 장치 파손",
                EventId.EnemyScout => "적 정찰",
                EventId.MeteorAttack => "운석 충돌",
                EventId.EmpAttack => "EMP 공격",
                EventId.PatrolZone => "순찰 구역",
                EventId.MeteorZone => "운석 구역",
                EventId.NebulaZone => "성운 구역",
                EventId.PlanetZone => "행성 구역",
                _ => eventId.ToString()
            };
        }

        private void UpdateMapInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                eventHudView.SetShipMapVisible(false);
                return;
            }

            if (shipMapInputMode == PHSShipMapInputMode.Hold)
            {
                eventHudView.SetShipMapVisible(keyboard.tabKey.isPressed);
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                toggleMapVisible = !toggleMapVisible;
            }

            eventHudView.SetShipMapVisible(toggleMapVisible);
        }

        private bool IsNewTerminalSnapshot(NetworkEventLifecycleSnapshot candidate)
        {
            return !hasTerminalSnapshot || terminalSnapshot.InstanceId != candidate.InstanceId ||
                terminalSnapshot.Revision != candidate.Revision ||
                terminalSnapshot.StateValue != candidate.StateValue;
        }

        private static int CompareSnapshots(
            NetworkEventLifecycleSnapshot left,
            NetworkEventLifecycleSnapshot right)
        {
            var roomComparison = string.CompareOrdinal(left.RoomId.ToString(), right.RoomId.ToString());
            if (roomComparison != 0) return roomComparison;

            var eventComparison = left.EventIdValue.CompareTo(right.EventIdValue);
            if (eventComparison != 0) return eventComparison;

            var instanceComparison = left.InstanceId.CompareTo(right.InstanceId);
            if (instanceComparison != 0) return instanceComparison;

            return left.Revision.CompareTo(right.Revision);
        }

        private static int CompareTerminalRecency(
            NetworkEventLifecycleSnapshot left,
            NetworkEventLifecycleSnapshot right)
        {
            var timeComparison = left.ChangedAtServerTime.CompareTo(right.ChangedAtServerTime);
            if (timeComparison != 0) return timeComparison;

            var revisionComparison = left.Revision.CompareTo(right.Revision);
            if (revisionComparison != 0) return revisionComparison;

            return left.InstanceId.CompareTo(right.InstanceId);
        }

        private static string GetStateLabel(EventState state)
        {
            return state switch
            {
                EventState.Resolve => "SUCCESS",
                EventState.Fail => "FAILED",
                _ => state.ToString().ToUpperInvariant()
            };
        }
    }
}
