using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    // Prefab-driven tactical map presentation.
    [DisallowMultipleComponent]
    public sealed class PHSHandheldShipMapView : MonoBehaviour, IShipMapView
    {
        [SerializeField] private GameObject deviceRoot;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Image markerTemplate;
        [SerializeField] private TMP_Text markerLabelTemplate;
        [Header("Tactical Status")]
        [SerializeField] private TMP_Text currentMapText;
        [SerializeField] private TMP_Text mapDetailText;
        [SerializeField] private TMP_Text runPhaseText;
        [SerializeField] private Image warpFill;
        [SerializeField] private TMP_Text warpValueText;
        [SerializeField] private Image shipHpFill;
        [SerializeField] private TMP_Text shipHpValueText;
        [SerializeField] private TMP_Text[] eventRows = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] eventIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text eventOverflowText;
        [Header("Existing HUD Icons")]
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite powerFailureIcon;
        [SerializeField] private Sprite deviceFailureIcon;
        [SerializeField] private Sprite hullBreachIcon;
        [SerializeField] private Sprite steamLeakIcon;
        [SerializeField] private Sprite oxygenFailureIcon;
        [SerializeField] private Sprite gravityFailureIcon;
        [SerializeField] private Sprite powerSyncIcon;
        [SerializeField] private Sprite cannonIcon;
        [SerializeField] private Sprite wireFixIcon;
        [SerializeField] private Sprite warpIcon;
        [SerializeField] private Sprite batteryIcon;
        [SerializeField] private Sprite wrenchIcon;
        [SerializeField] private Sprite fireExtinguisherIcon;
        [SerializeField] private Color selfColor = new(0.1f, 1f, 0.35f, 1f);
        [SerializeField] private Color teammateColor = new(0.1f, 0.75f, 1f, 1f);
        [SerializeField] private Color incidentColor = new(1f, 0.15f, 0.08f, 1f);
        [SerializeField] private Color objectColor = new(1f, 0.72f, 0.12f, 1f);

        private readonly List<Image> markerPool = new();
        private readonly List<TMP_Text> markerLabelPool = new();

        private void Awake()
        {
            if (deviceRoot == null
                || markerRoot == null
                || markerTemplate == null
                || markerLabelTemplate == null
                || currentMapText == null
                || mapDetailText == null
                || runPhaseText == null
                || warpFill == null
                || warpValueText == null
                || shipHpFill == null
                || shipHpValueText == null
                || eventRows == null
                || eventRows.Length == 0
                || Array.Exists(eventRows, row => row == null)
                || eventIcons == null
                || eventIcons.Length != eventRows.Length
                || Array.Exists(eventIcons, icon => icon == null)
                || eventOverflowText == null)
            {
                Debug.LogError(
                    $"PHS_HANDHELD_MAP_VIEW_SETUP_FAILED view={name} " +
                    $"device={deviceRoot != null} root={markerRoot != null} " +
                    $"template={markerTemplate != null && markerLabelTemplate != null} " +
                    $"map={currentMapText != null} detail={mapDetailText != null} phase={runPhaseText != null} " +
                    $"warp={warpFill != null && warpValueText != null} " +
                    $"ship={shipHpFill != null && shipHpValueText != null} " +
                    $"events={eventRows?.Length ?? 0}/{eventIcons?.Length ?? 0} " +
                    $"overflow={eventOverflowText != null}",
                    this);
                enabled = false;
                return;
            }

            ValidateIconReferences();
            markerTemplate.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (deviceRoot == null)
            {
                Debug.LogError($"PHS_HANDHELD_MAP_VIEW_FAILED reason=device_missing view={name}", this);
                return;
            }

            deviceRoot.SetActive(visible);
        }

        public void Render(in ShipMapPresentation presentation)
        {
            if (!enabled || markerRoot == null || markerTemplate == null)
            {
                return;
            }

            var markers = presentation.Markers;
            if (markers == null || presentation.Events == null)
            {
                Debug.LogError($"PHS_HANDHELD_MAP_RENDER_FAILED reason=presentation_collection_missing view={name}", this);
                return;
            }

            currentMapText.text = presentation.MapName;
            mapDetailText.text =
                $"구역 {presentation.MapId}  ·  난이도 {presentation.Difficulty}";
            runPhaseText.text = presentation.RunPhase;
            var warpCharge = Mathf.Clamp01(presentation.WarpChargeNormalized);
            warpFill.fillAmount = warpCharge;
            warpValueText.text = $"{Mathf.RoundToInt(warpCharge * 100f)}%";
            var shipHp = Mathf.Clamp01(presentation.ShipHpNormalized);
            shipHpFill.fillAmount = shipHp;
            shipHpValueText.text = $"{Mathf.RoundToInt(shipHp * 100f)}%";
            RenderEvents(presentation.Events);

            EnsurePoolSize(markers.Count);
            var size = markerRoot.rect.size;
            for (var index = 0; index < markerPool.Count; index++)
            {
                var markerImage = markerPool[index];
                var active = index < markers.Count;
                markerImage.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                var marker = markers[index];
                markerImage.rectTransform.anchoredPosition = new Vector2(
                    (marker.NormalizedPosition.x - 0.5f) * size.x,
                    (marker.NormalizedPosition.y - 0.5f) * size.y);
                var hasIcon = marker.IconId != ShipMapIconId.None;
                markerImage.sprite = hasIcon
                    ? ResolveIcon(marker.IconId)
                    : markerTemplate.sprite;
                markerImage.preserveAspect = hasIcon;
                markerImage.color = hasIcon ? Color.white : ResolveColor(marker.Kind);
                markerImage.rectTransform.sizeDelta = marker.Kind switch
                {
                    ShipMapMarkerKind.Incident => new Vector2(26f, 26f),
                    ShipMapMarkerKind.Object => new Vector2(17f, 17f),
                    _ => new Vector2(22f, 22f)
                };
                var markerLabel = markerLabelPool[index];
                markerLabel.text = marker.Symbol;
                markerLabel.gameObject.SetActive(
                    !hasIcon && !string.IsNullOrWhiteSpace(marker.Symbol));
            }
        }

        private void RenderEvents(IReadOnlyList<ShipMapEventDetail> events)
        {
            var visibleCount = Mathf.Min(events.Count, eventRows.Length);
            for (var index = 0; index < eventRows.Length; index++)
            {
                var active = index < visibleCount;
                eventRows[index].gameObject.SetActive(active);
                if (active)
                {
                    var detail = events[index];
                    var hasIcon = detail.IconId != ShipMapIconId.None;
                    eventIcons[index].gameObject.SetActive(hasIcon);
                    if (hasIcon)
                    {
                        eventIcons[index].sprite = ResolveIcon(detail.IconId);
                        eventIcons[index].preserveAspect = true;
                        eventIcons[index].color = Color.white;
                    }

                    eventRows[index].rectTransform.anchoredPosition = new Vector2(
                        hasIcon ? 11f : 0f,
                        eventRows[index].rectTransform.anchoredPosition.y);
                    eventRows[index].rectTransform.sizeDelta = new Vector2(
                        hasIcon ? 128f : 154f,
                        eventRows[index].rectTransform.sizeDelta.y);
                    var prefix = hasIcon || string.IsNullOrWhiteSpace(detail.Symbol)
                        ? string.Empty
                        : $"[{detail.Symbol}] ";
                    eventRows[index].text =
                        $"{prefix}{detail.Title}\n<color=#A9C7D0>{detail.Status}</color>";
                }
                else
                {
                    eventIcons[index].gameObject.SetActive(false);
                }
            }

            var overflow = events.Count - visibleCount;
            eventOverflowText.gameObject.SetActive(overflow > 0);
            eventOverflowText.text = overflow > 0 ? $"+{overflow}개 더 있음" : string.Empty;
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (markerPool.Count < requiredCount)
            {
                var instance = Instantiate(markerTemplate, markerRoot);
                instance.name = $"RuntimeMarker_{markerPool.Count}";
                instance.gameObject.SetActive(false);
                var label = instance.transform.Find("MarkerLabel")?.GetComponent<TMP_Text>();
                if (label == null)
                {
                    Debug.LogError(
                        $"PHS_HANDHELD_MAP_MARKER_FAILED reason=label_missing marker={instance.name}",
                        this);
                    enabled = false;
                    return;
                }

                markerPool.Add(instance);
                markerLabelPool.Add(label);
            }
        }

        private Color ResolveColor(ShipMapMarkerKind kind)
        {
            return kind switch
            {
                ShipMapMarkerKind.Self => selfColor,
                ShipMapMarkerKind.Teammate => teammateColor,
                ShipMapMarkerKind.Incident => incidentColor,
                ShipMapMarkerKind.Object => objectColor,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private Sprite ResolveIcon(ShipMapIconId iconId)
        {
            return iconId switch
            {
                ShipMapIconId.Fire => fireIcon,
                ShipMapIconId.PowerFailure => powerFailureIcon,
                ShipMapIconId.DeviceFailure => deviceFailureIcon,
                ShipMapIconId.HullBreach => hullBreachIcon,
                ShipMapIconId.SteamLeak => steamLeakIcon,
                ShipMapIconId.OxygenFailure => oxygenFailureIcon,
                ShipMapIconId.GravityFailure => gravityFailureIcon,
                ShipMapIconId.PowerSync => powerSyncIcon,
                ShipMapIconId.Cannon => cannonIcon,
                ShipMapIconId.WireFix => wireFixIcon,
                ShipMapIconId.Warp => warpIcon,
                ShipMapIconId.Battery => batteryIcon,
                ShipMapIconId.Wrench => wrenchIcon,
                ShipMapIconId.FireExtinguisher => fireExtinguisherIcon,
                _ => throw new ArgumentOutOfRangeException(nameof(iconId), iconId, null)
            };
        }

        private void ValidateIconReferences()
        {
            foreach (ShipMapIconId iconId in Enum.GetValues(typeof(ShipMapIconId)))
            {
                if (iconId == ShipMapIconId.None)
                {
                    continue;
                }

                if (ResolveIcon(iconId) == null)
                {
                    Debug.LogError(
                        $"PHS_HANDHELD_MAP_VIEW_SETUP_FAILED reason=icon_missing icon={iconId}",
                        this);
                    enabled = false;
                }
            }
        }
    }
}
