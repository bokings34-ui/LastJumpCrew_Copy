using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSNetworkEventHudView : MonoBehaviour, INetworkEventHudView
    {
        [Serializable]
        private sealed class RoomViewEntry
        {
            [Tooltip("Network event snapshot RoomId. Configure Room A, Room B, Room C, and center corridor exactly once each.")]
            [SerializeField] private string roomId;
            [SerializeField] private GameObject roomRoot;
            [SerializeField] private GameObject activeEventIcon;
            [SerializeField] private TMP_Text statusLabel;

            public string RoomId => roomId?.Trim() ?? string.Empty;

            public bool Validate(PHSNetworkEventHudView owner, int index)
            {
                var valid = true;
                if (string.IsNullOrWhiteSpace(RoomId))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_id_missing view={owner.name} index={index}",
                        owner);
                    valid = false;
                }

                if (roomRoot == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_root_missing view={owner.name} room={RoomId} index={index}",
                        owner);
                    valid = false;
                }

                if (activeEventIcon == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_icon_missing view={owner.name} room={RoomId} index={index}",
                        owner);
                    valid = false;
                }

                if (statusLabel == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_label_missing view={owner.name} room={RoomId} index={index}",
                        owner);
                    valid = false;
                }

                return valid;
            }

            public void Apply(PHSNetworkEventRoomViewModel room)
            {
                // A room location is an incident indicator, not a permanent map label.
                // Keeping this root enabled made every configured room look active.
                if (roomRoot != null) roomRoot.SetActive(room.HasActiveIncident);
                if (activeEventIcon != null) activeEventIcon.SetActive(room.HasActiveIncident);
                if (statusLabel != null)
                {
                    statusLabel.text = room.HasActiveIncident ? RoomId : string.Empty;
                }
            }

            public void Clear(bool hideRoomRoot)
            {
                if (roomRoot != null && hideRoomRoot) roomRoot.SetActive(false);
                if (activeEventIcon != null) activeEventIcon.SetActive(false);
                if (statusLabel != null) statusLabel.text = string.Empty;
            }
        }

        [Serializable]
        private sealed class AccidentIconEntry
        {
            [SerializeField] private PHSShipAccidentId accidentId;
            [SerializeField] private GameObject root;

            public PHSShipAccidentId AccidentId => accidentId;

            public bool Validate(PHSNetworkEventHudView owner, int index, HashSet<PHSShipAccidentId> configuredIds)
            {
                var valid = true;
                if (accidentId == PHSShipAccidentId.None || !configuredIds.Add(accidentId))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=accident_icon_id_invalid_or_duplicate " +
                        $"view={owner.name} accident={accidentId} index={index}",
                        owner);
                    valid = false;
                }

                if (root == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=accident_icon_root_missing " +
                        $"view={owner.name} accident={accidentId} index={index}",
                        owner);
                    valid = false;
                }

                return valid;
            }

            public void SetVisible(bool visible)
            {
                if (root != null)
                {
                    root.SetActive(visible);
                }
            }
        }

        [Serializable]
        private sealed class LifecycleEventIconEntry
        {
            [SerializeField] private EventId eventId;
            [SerializeField] private GameObject root;

            public EventId EventId => eventId;

            public bool Validate(PHSNetworkEventHudView owner, int index, HashSet<EventId> configuredIds)
            {
                var valid = true;
                if (!IsHudLifecycleEvent(eventId) || !configuredIds.Add(eventId))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=lifecycle_icon_id_invalid_or_duplicate " +
                        $"view={owner.name} event={eventId} index={index}",
                        owner);
                    valid = false;
                }

                if (root == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=lifecycle_icon_root_missing " +
                        $"view={owner.name} event={eventId} index={index}",
                        owner);
                    valid = false;
                }

                return valid;
            }

            public void SetVisible(bool visible)
            {
                if (root != null)
                {
                    root.SetActive(visible);
                }
            }
        }

        [Header("Dedicated Event Alert (do not reuse hazard/gravity panel)")]
        [SerializeField] private GameObject eventAlertRoot;
        [SerializeField] private GameObject eventAlertIcon;
        [SerializeField] private TMP_Text eventAlertLabelText;
        [SerializeField] private GameObject iconLineupRoot;
        [SerializeField] private LifecycleEventIconEntry[] lifecycleIconEntries = new LifecycleEventIconEntry[13];

        [Header("Dedicated Ship Map")]
        [SerializeField] private GameObject shipMapRoot;
        [SerializeField] private TMP_Text currentMapLabelText;
        [SerializeField] private RoomViewEntry[] roomViews = new RoomViewEntry[4];

        private readonly Dictionary<string, RoomViewEntry> roomViewsById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> missingRoomMappingsLogged = new(StringComparer.Ordinal);
        private readonly HashSet<EventId> activeLifecycleEventIds = new();

        private bool hasValidatedSetup;
        private bool isConfigured;
        private string externalAlertText = string.Empty;
        private bool hasActiveLifecycleIcons;
        private string currentMapText = string.Empty;

        public bool IsConfigured
        {
            get
            {
                EnsureSetupValidated();
                return isConfigured;
            }
        }

        private void Awake()
        {
            EnsureSetupValidated();
            HideOffline();
        }

        public void ShowCurrentMap(string displayName, float visibleSeconds)
        {
            ShowCurrentMap(displayName, 0, string.Empty, string.Empty, visibleSeconds);
        }

        public void ShowCurrentMap(
            string displayName,
            int debrisAmount,
            string debrisAmountLabel,
            string difficultyLabel,
            float visibleSeconds)
        {
            if (!IsConfigured || string.IsNullOrWhiteSpace(displayName))
            {
                ClearCurrentMap();
                return;
            }

            var resolvedDebrisAmount = string.IsNullOrWhiteSpace(debrisAmountLabel)
                ? debrisAmount.ToString()
                : debrisAmountLabel.Trim();
            currentMapText = string.IsNullOrWhiteSpace(difficultyLabel)
                ? $"현재 구역 · {displayName.Trim()}"
                : $"현재 구역 · {displayName.Trim()}\n" +
                    $"잔해량 {resolvedDebrisAmount} · 난이도 {difficultyLabel}";
            RefreshCurrentMapText();
        }

        public void ClearCurrentMap()
        {
            currentMapText = string.Empty;
            RefreshCurrentMapText();
        }

        public void Apply(PHSNetworkEventHudViewModel viewModel)
        {
            if (!IsConfigured || viewModel == null)
            {
                return;
            }

            externalAlertText = viewModel.AlertText;
            SetLifecycleEvents(viewModel.ActiveEventIds);
            RefreshAlertPresentation();

            foreach (var roomView in roomViews)
            {
                roomView?.Apply(new PHSNetworkEventRoomViewModel(roomView.RoomId, string.Empty, 0));
            }

            if (viewModel.Rooms == null)
            {
                return;
            }

            foreach (var room in viewModel.Rooms)
            {
                if (roomViewsById.TryGetValue(room.RoomId, out var roomView))
                {
                    roomView.Apply(room);
                    continue;
                }

                if (room.HasActiveIncident && missingRoomMappingsLogged.Add(room.RoomId))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_MAPPING_FAILED reason=room_not_configured view={name} room={room.RoomId}",
                        this);
                }
            }
        }

        public void SetShipMapVisible(bool isVisible)
        {
            if (shipMapRoot != null)
            {
                shipMapRoot.SetActive(IsConfigured && isVisible);
            }

            RefreshCurrentMapText();
        }

        public void HideOffline()
        {
            externalAlertText = string.Empty;
            SetLifecycleEvents(null);
            RefreshAlertPresentation();
            if (shipMapRoot != null) shipMapRoot.SetActive(false);

            if (roomViews == null)
            {
                return;
            }

            foreach (var roomView in roomViews)
            {
                roomView?.Clear(true);
            }
        }

        public void SetInternalAccidentLines(IReadOnlyList<PHSShipAccidentHudLine> lines)
        {
            if (!IsConfigured)
            {
                return;
            }

            // Ship accidents were removed from the active event source.  The scheduler
            // lifecycle snapshot is now the single HUD icon authority.
            RefreshAlertPresentation();
        }

        private void SetLifecycleEvents(IReadOnlyList<EventId> eventIds)
        {
            activeLifecycleEventIds.Clear();
            if (eventIds != null)
            {
                foreach (var eventId in eventIds)
                {
                    if (IsHudLifecycleEvent(eventId))
                    {
                        activeLifecycleEventIds.Add(eventId);
                    }
                }
            }

            hasActiveLifecycleIcons = false;
            if (lifecycleIconEntries == null)
            {
                return;
            }

            foreach (var entry in lifecycleIconEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                var visible = activeLifecycleEventIds.Contains(entry.EventId);
                entry.SetVisible(visible);
                hasActiveLifecycleIcons |= visible;
            }
        }

        private void RefreshAlertPresentation()
        {
            if (eventAlertRoot == null
                || eventAlertIcon == null
                || eventAlertLabelText == null
                || iconLineupRoot == null)
            {
                return;
            }

            var hasLineupIcons = hasActiveLifecycleIcons;
            var hasGenericExternalAlert = !string.IsNullOrWhiteSpace(externalAlertText);
            // Scheduler events always have a typed FlatSF icon.  The old text-only
            // alert is reserved for a genuinely unmapped external message; otherwise
            // it overflows the user-authored alert position beside the icon grid.
            var showGenericAlert = hasGenericExternalAlert && !hasLineupIcons;
            eventAlertRoot.SetActive(hasLineupIcons || showGenericAlert);
            iconLineupRoot.SetActive(hasLineupIcons);
            eventAlertIcon.SetActive(false);
            eventAlertLabelText.text = showGenericAlert
                ? externalAlertText
                : string.Empty;
            eventAlertLabelText.gameObject.SetActive(showGenericAlert);
        }

        private static bool IsHudLifecycleEvent(EventId eventId)
        {
            return eventId == EventId.Fire
                || eventId == EventId.EnemySpawn
                || eventId == EventId.PowerOff
                || eventId == EventId.OxygenLeak
                || eventId == EventId.EngineBreak
                || eventId == EventId.MicDestroy
                || eventId == EventId.HullBreach
                || eventId == EventId.SteamLeak
                || eventId == EventId.OxygenGeneratorFailure
                || eventId == EventId.GravityGeneratorFailure
                || eventId == EventId.EnemyScout
                || eventId == EventId.MeteorAttack
                || eventId == EventId.EmpAttack;
        }

        private void RefreshCurrentMapText()
        {
            if (currentMapLabelText == null)
            {
                return;
            }

            currentMapLabelText.text = currentMapText;
            currentMapLabelText.gameObject.SetActive(
                shipMapRoot != null &&
                shipMapRoot.activeSelf &&
                !string.IsNullOrWhiteSpace(currentMapText));
        }

        private bool ValidateSetup()
        {
            var valid = true;
            if (eventAlertRoot == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=event_alert_root_missing view={name}", this);
                valid = false;
            }

            if (eventAlertIcon == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=event_alert_icon_missing view={name}", this);
                valid = false;
            }

            if (eventAlertLabelText == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=event_alert_label_missing view={name}", this);
                valid = false;
            }

            if (iconLineupRoot == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=icon_lineup_root_missing view={name}", this);
                valid = false;
            }

            valid &= ValidateLifecycleIcons();

            if (shipMapRoot == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=ship_map_root_missing view={name}", this);
                valid = false;
            }

            if (currentMapLabelText == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=current_map_label_missing view={name}", this);
                valid = false;
            }

            if (roomViews == null || roomViews.Length != 4)
            {
                Debug.LogError(
                    $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_view_count_invalid view={name} " +
                    $"actual={roomViews?.Length ?? 0} expected=4",
                    this);
                return false;
            }

            roomViewsById.Clear();
            for (var index = 0; index < roomViews.Length; index++)
            {
                var roomView = roomViews[index];
                if (roomView == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_view_missing view={name} index={index}",
                        this);
                    valid = false;
                    continue;
                }

                valid &= roomView.Validate(this, index);
                if (!string.IsNullOrWhiteSpace(roomView.RoomId) &&
                    !roomViewsById.TryAdd(roomView.RoomId, roomView))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=room_id_duplicate view={name} room={roomView.RoomId}",
                        this);
                    valid = false;
                }
            }

            return valid;
        }

        private bool ValidateLifecycleIcons()
        {
            if (lifecycleIconEntries == null || lifecycleIconEntries.Length != 13)
            {
                Debug.LogError(
                    $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=lifecycle_icon_count_invalid view={name} " +
                    $"actual={lifecycleIconEntries?.Length ?? 0} expected=13",
                    this);
                return false;
            }

            var valid = true;
            var configuredIds = new HashSet<EventId>();
            for (var index = 0; index < lifecycleIconEntries.Length; index++)
            {
                var entry = lifecycleIconEntries[index];
                if (entry == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=lifecycle_icon_entry_missing view={name} index={index}",
                        this);
                    valid = false;
                    continue;
                }

                valid &= entry.Validate(this, index, configuredIds);
            }

            return valid;
        }

        private void EnsureSetupValidated()
        {
            if (hasValidatedSetup)
            {
                return;
            }

            hasValidatedSetup = true;
            isConfigured = ValidateSetup();
        }
    }
}
