using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSNetworkEventHudView : MonoBehaviour, INetworkEventHudView
    {
        [Serializable]
        private sealed class AccidentIconEntry
        {
            [SerializeField] private PHSShipAccidentId accidentId;
            [SerializeField] private GameObject root;
            [SerializeField] private Image iconImage;
            [SerializeField] private Image progressFill;

            public PHSShipAccidentId AccidentId => accidentId;

            public bool Validate(PHSNetworkEventHudView owner, int index)
            {
                var valid = accidentId != PHSShipAccidentId.None
                    && root != null
                    && iconImage != null
                    && iconImage.sprite != null
                    && progressFill != null;
                if (!valid)
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_ACCIDENT_ICON_SETUP_FAILED view={owner.name} index={index} accident={accidentId} root={root != null} icon={iconImage != null} sprite={iconImage != null && iconImage.sprite != null} progress={progressFill != null}",
                        owner);
                }

                return valid;
            }

            public void Apply(PHSShipAccidentHudLine line)
            {
                root.SetActive(true);
                var required = Mathf.Max(1, line.RequiredRepairProgress);
                var normalized = Mathf.Clamp01(
                    line.RepairProgress / (float)required);
                progressFill.fillAmount = normalized;
                progressFill.color = Color.Lerp(
                    new Color(1f, 0.28f, 0.08f, 1f),
                    new Color(0.15f, 1f, 0.85f, 1f),
                    normalized);
            }

            public void Clear()
            {
                if (root != null)
                {
                    root.SetActive(false);
                }

                if (progressFill != null)
                {
                    progressFill.fillAmount = 0f;
                }
            }
        }

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
                if (roomRoot != null) roomRoot.SetActive(true);
                if (activeEventIcon != null) activeEventIcon.SetActive(room.HasActiveIncident);
                if (statusLabel != null)
                {
                    statusLabel.text = RoomId;
                }
            }

            public void Clear(bool hideRoomRoot)
            {
                if (roomRoot != null && hideRoomRoot) roomRoot.SetActive(false);
                if (activeEventIcon != null) activeEventIcon.SetActive(false);
                if (statusLabel != null) statusLabel.text = string.Empty;
            }
        }

        [Header("Dedicated Event Alert (do not reuse hazard/gravity panel)")]
        [SerializeField] private GameObject eventAlertRoot;
        [SerializeField] private GameObject accidentIconRoot;
        [SerializeField] private AccidentIconEntry[] accidentIconEntries =
            new AccidentIconEntry[7];

        [Header("Dedicated Ship Map")]
        [SerializeField] private GameObject shipMapRoot;
        [SerializeField] private TMP_Text currentMapLabelText;
        [SerializeField] private RoomViewEntry[] roomViews = new RoomViewEntry[4];

        private readonly Dictionary<string, RoomViewEntry> roomViewsById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> missingRoomMappingsLogged = new(StringComparer.Ordinal);
        private readonly Dictionary<PHSShipAccidentId, AccidentIconEntry>
            accidentIconsById = new();

        private bool hasValidatedSetup;
        private bool isConfigured;
        private bool hasInternalAccidentIcons;
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
            if (!IsConfigured || string.IsNullOrWhiteSpace(displayName))
            {
                ClearCurrentMap();
                return;
            }

            currentMapText = $"현재 구역 · {displayName.Trim()}";
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

            RefreshAlertText();

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
            hasInternalAccidentIcons = false;
            ClearAccidentIcons();
            RefreshAlertText();
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

            ClearAccidentIcons();
            if (lines == null || lines.Count == 0)
            {
                hasInternalAccidentIcons = false;
                RefreshAlertText();
                return;
            }

            hasInternalAccidentIcons = false;
            foreach (var line in lines)
            {
                if (!accidentIconsById.TryGetValue(
                        line.AccidentId,
                        out var iconEntry))
                {
                    Debug.LogError(
                        $"PHS_EVENT_HUD_ACCIDENT_ICON_FAILED reason=mapping_missing view={name} accident={line.AccidentId}",
                        this);
                    continue;
                }

                iconEntry.Apply(line);
                hasInternalAccidentIcons = true;
            }

            RefreshAlertText();
        }

        private void RefreshAlertText()
        {
            if (eventAlertRoot == null)
            {
                return;
            }

            eventAlertRoot.SetActive(hasInternalAccidentIcons);
            if (accidentIconRoot != null)
            {
                accidentIconRoot.SetActive(hasInternalAccidentIcons);
            }
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

            if (accidentIconRoot == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=accident_icon_root_missing view={name}",
                    this);
                valid = false;
            }

            if (accidentIconEntries == null
                || accidentIconEntries.Length != 7)
            {
                Debug.LogError(
                    $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=accident_icon_count_invalid view={name} actual={accidentIconEntries?.Length ?? 0} expected=7",
                    this);
                valid = false;
            }
            else
            {
                accidentIconsById.Clear();
                for (var index = 0; index < accidentIconEntries.Length; index++)
                {
                    var entry = accidentIconEntries[index];
                    if (entry == null || !entry.Validate(this, index))
                    {
                        valid = false;
                        continue;
                    }

                    if (!accidentIconsById.TryAdd(entry.AccidentId, entry))
                    {
                        Debug.LogError(
                            $"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=accident_icon_duplicate view={name} accident={entry.AccidentId}",
                            this);
                        valid = false;
                    }
                }
            }

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

        private void EnsureSetupValidated()
        {
            if (hasValidatedSetup)
            {
                return;
            }

            hasValidatedSetup = true;
            isConfigured = ValidateSetup();
        }

        private void ClearAccidentIcons()
        {
            if (accidentIconEntries == null)
            {
                return;
            }

            foreach (var entry in accidentIconEntries)
            {
                entry?.Clear();
            }
        }
    }
}
