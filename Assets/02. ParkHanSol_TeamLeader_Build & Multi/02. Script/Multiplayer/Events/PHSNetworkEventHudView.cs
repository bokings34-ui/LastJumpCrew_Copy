using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;

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
        [SerializeField] private TMP_Text eventAlertText;

        [Header("Dedicated Ship Map")]
        [SerializeField] private GameObject shipMapRoot;
        [SerializeField] private RoomViewEntry[] roomViews = new RoomViewEntry[4];

        private readonly Dictionary<string, RoomViewEntry> roomViewsById =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> missingRoomMappingsLogged = new(StringComparer.Ordinal);

        private bool hasValidatedSetup;
        private bool isConfigured;
        private string externalAlertText = string.Empty;
        private string internalAccidentAlertText = string.Empty;

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

        public void Apply(PHSNetworkEventHudViewModel viewModel)
        {
            if (!IsConfigured || viewModel == null)
            {
                return;
            }

            externalAlertText = viewModel.AlertText;
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
        }

        public void HideOffline()
        {
            externalAlertText = string.Empty;
            internalAccidentAlertText = string.Empty;
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

            if (lines == null || lines.Count == 0)
            {
                internalAccidentAlertText = string.Empty;
                RefreshAlertText();
                return;
            }

            var builder = new System.Text.StringBuilder(128);
            builder.Append("내부 사고");
            foreach (var line in lines)
            {
                builder.Append('\n');
                builder.Append("• ");
                builder.Append(line.DisplayName);
                builder.Append(" · ");
                builder.Append(line.ModuleName);
                builder.Append(" · ");
                builder.Append(line.RepairProgress);
                builder.Append('/');
                builder.Append(line.RequiredRepairProgress);
            }

            internalAccidentAlertText = builder.ToString();
            RefreshAlertText();
        }

        private void RefreshAlertText()
        {
            if (eventAlertRoot == null || eventAlertText == null)
            {
                return;
            }

            var text = string.IsNullOrWhiteSpace(externalAlertText)
                ? internalAccidentAlertText
                : string.IsNullOrWhiteSpace(internalAccidentAlertText)
                    ? externalAlertText
                    : $"{externalAlertText}\n{internalAccidentAlertText}";
            eventAlertRoot.SetActive(!string.IsNullOrWhiteSpace(text));
            eventAlertText.text = text;
        }

        private bool ValidateSetup()
        {
            var valid = true;
            if (eventAlertRoot == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=event_alert_root_missing view={name}", this);
                valid = false;
            }

            if (eventAlertText == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=event_alert_text_missing view={name}", this);
                valid = false;
            }

            if (shipMapRoot == null)
            {
                Debug.LogError($"PHS_EVENT_HUD_VIEW_SETUP_FAILED reason=ship_map_root_missing view={name}", this);
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
    }
}
