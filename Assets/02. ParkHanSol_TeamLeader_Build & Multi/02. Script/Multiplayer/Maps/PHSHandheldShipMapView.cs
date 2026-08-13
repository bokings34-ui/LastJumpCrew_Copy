using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    // Prefab-driven tactical map presentation.
    [DisallowMultipleComponent]
    public sealed class PHSHandheldShipMapView : MonoBehaviour, IShipMapView
    {
        private static readonly Quaternion MarkerGlyphUprightRotation =
            Quaternion.Euler(0f, 0f, -90f);

        [SerializeField] private GameObject deviceRoot;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Image markerTemplate;
        [SerializeField] private Image markerGlyphTemplate;
        [SerializeField] private TMP_Text markerLabelTemplate;
        [Header("Player Marker Priority")]
        [SerializeField] private Vector2 playerMarkerSize = new(18f, 18f);
        [SerializeField] private Color playerMarkerColor = new(0.9f, 0.96f, 1f, 1f);
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
        [Header("Event List Layout")]
        [SerializeField] private float eventRowWithIconX = 25f;
        [SerializeField] private float eventRowWithoutIconX;
        [SerializeField] private float eventRowWithIconWidth = 160f;
        [SerializeField] private float eventRowWithoutIconWidth = 185f;
        [Header("Existing HUD Icons")]
        [SerializeField] private Sprite fireIcon;
        [SerializeField] private Sprite powerFailureIcon;
        [SerializeField] private Sprite deviceFailureIcon;
        [SerializeField] private Sprite hullBreachIcon;
        [SerializeField] private Sprite steamLeakIcon;
        [SerializeField] private Sprite oxygenFailureIcon;
        [SerializeField] private Sprite gravityFailureIcon;
        [SerializeField] private Sprite enemySpawnIcon;
        [SerializeField] private Sprite patrolZoneIcon;
        [SerializeField] private Sprite meteorZoneIcon;
        [SerializeField] private Sprite nebulaZoneIcon;
        [SerializeField] private Sprite planetZoneIcon;
        [SerializeField] private Sprite powerSyncIcon;
        [SerializeField] private Sprite cannonIcon;
        [SerializeField] private Sprite wireFixIcon;
        [SerializeField] private Sprite warpIcon;
        [SerializeField] private Sprite batteryIcon;
        [SerializeField] private Sprite wrenchIcon;
        [SerializeField] private Sprite fireExtinguisherIcon;
        [SerializeField] private Sprite playerIcon;

        private readonly List<Image> markerPool = new();
        private readonly List<Image> markerGlyphPool = new();
        private readonly List<TMP_Text> markerLabelPool = new();
        private readonly List<TMP_Text> eventRowPool = new();
        private readonly List<Image> eventIconPool = new();
        private bool mapTextureErrorLogged;

        private void Awake()
        {
            if (deviceRoot == null
                || mapImage == null
                || markerRoot == null
                || markerTemplate == null
                || markerGlyphTemplate == null
                || markerLabelTemplate == null
                || markerGlyphTemplate.transform.parent != markerTemplate.transform
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
                    $"device={deviceRoot != null} map_image={mapImage != null} root={markerRoot != null} " +
                    $"template={markerTemplate != null && markerGlyphTemplate != null && markerLabelTemplate != null} " +
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
            eventRowPool.AddRange(eventRows);
            eventIconPool.AddRange(eventIcons);
            ConfigureEventRows();
            markerTemplate.gameObject.SetActive(false);
            markerGlyphTemplate.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (!enabled || deviceRoot == null || mapImage == null)
            {
                Debug.LogError(
                    $"PHS_HANDHELD_MAP_VIEW_FAILED reason=setup_invalid view={name} " +
                    $"device={deviceRoot != null} map_image={mapImage != null}",
                    this);
                return;
            }

            deviceRoot.SetActive(visible);
            if (visible)
            {
                TryBindMapTexture();
            }
        }

        public void Render(in ShipMapPresentation presentation)
        {
            if (!enabled || markerRoot == null || markerTemplate == null)
            {
                return;
            }

            if (!TryBindMapTexture())
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
            shipHpValueText.text =
                $"{presentation.CurrentShipHp}/{presentation.MaximumShipHp}";
            RenderEvents(presentation.Events);

            EnsurePoolSize(markers.Count);
            // MarkerRoot owns map coordinates. Rotating it rotates marker
            // positions around the map centre, so only glyph visuals compensate.
            markerRoot.localRotation = Quaternion.identity;
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
                // The device is world-space UI. Runtime markers must not inherit
                // any authored tilt from the map panel hierarchy.
                markerImage.rectTransform.localRotation = Quaternion.identity;
                markerImage.rectTransform.localScale = Vector3.one;
                markerImage.rectTransform.anchoredPosition = new Vector2(
                    (marker.NormalizedPosition.x - 0.5f) * size.x,
                    (marker.NormalizedPosition.y - 0.5f) * size.y);
                var hasIcon = marker.IconId != ShipMapIconId.None;
                markerImage.enabled = false;
                markerImage.rectTransform.sizeDelta = marker.Kind switch
                {
                    ShipMapMarkerKind.Incident => new Vector2(26f, 26f),
                    ShipMapMarkerKind.Object => new Vector2(20f, 20f),
                    ShipMapMarkerKind.Self or ShipMapMarkerKind.Teammate => playerMarkerSize,
                    _ => new Vector2(22f, 22f)
                };
                var markerGlyph = markerGlyphPool[index];
                markerGlyph.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    markerGlyph.rectTransform.localRotation = MarkerGlyphUprightRotation;
                    markerGlyph.rectTransform.localScale = Vector3.one;
                    markerGlyph.sprite = ResolveIcon(marker.IconId);
                    markerGlyph.preserveAspect = true;
                    markerGlyph.color = ResolveMarkerIconColor(marker.IconId);
                }

                var markerLabel = markerLabelPool[index];
                var isPlayerMarker = marker.Kind is ShipMapMarkerKind.Self
                    or ShipMapMarkerKind.Teammate;
                markerLabel.gameObject.SetActive(
                    isPlayerMarker && !string.IsNullOrWhiteSpace(marker.Symbol));
                if (markerLabel.gameObject.activeSelf)
                {
                    markerLabel.rectTransform.localRotation = Quaternion.identity;
                    markerLabel.rectTransform.localScale = Vector3.one;
                    markerLabel.text = marker.Symbol;
                    markerLabel.alignment = TextAlignmentOptions.Center;
                    markerLabel.color = Color.white;
                }
            }

            // Player markers must stay readable above every incident/tool marker.
            // This is explicit ordering, not dependent on incoming marker order.
            for (var index = 0; index < markers.Count; index++)
            {
                if (markers[index].Kind is ShipMapMarkerKind.Self
                    or ShipMapMarkerKind.Teammate)
                {
                    markerPool[index].transform.SetAsLastSibling();
                }
            }
        }

        private void RenderEvents(IReadOnlyList<ShipMapEventDetail> events)
        {
            EnsureEventRowPoolSize(events.Count);
            var visibleCount = events.Count;
            for (var index = 0; index < eventRowPool.Count; index++)
            {
                var active = index < visibleCount;
                var eventRow = eventRowPool[index];
                var eventIcon = eventIconPool[index];
                eventRow.gameObject.SetActive(active);
                if (active)
                {
                    var detail = events[index];
                    var hasIcon = detail.IconId != ShipMapIconId.None;
                    eventIcon.gameObject.SetActive(hasIcon);
                    if (hasIcon)
                    {
                        eventIcon.sprite = ResolveIcon(detail.IconId);
                        eventIcon.preserveAspect = true;
                        eventIcon.color = Color.white;
                    }

                    eventRow.rectTransform.anchoredPosition = new Vector2(
                        hasIcon ? eventRowWithIconX : eventRowWithoutIconX,
                        eventRow.rectTransform.anchoredPosition.y);
                    eventRow.rectTransform.sizeDelta = new Vector2(
                        hasIcon ? eventRowWithIconWidth : eventRowWithoutIconWidth,
                        eventRow.rectTransform.sizeDelta.y);
                    var prefix = hasIcon || string.IsNullOrWhiteSpace(detail.Symbol)
                        ? string.Empty
                        : $"[{detail.Symbol}] ";
                    eventRow.text =
                        $"{prefix}{detail.Title} <color=#A9C7D0>· {detail.Status}</color>";
                }
                else
                {
                    eventIcon.gameObject.SetActive(false);
                }
            }

            eventOverflowText.gameObject.SetActive(false);
            eventOverflowText.text = string.Empty;
        }

        private void EnsureEventRowPoolSize(int requiredCount)
        {
            while (eventRowPool.Count < requiredCount)
            {
                var sourceRow = eventRowPool[eventRowPool.Count - 1];
                var sourceIcon = eventIconPool[eventIconPool.Count - 1];
                var previousRow = eventRowPool.Count > 1
                    ? eventRowPool[eventRowPool.Count - 2]
                    : null;
                var step = previousRow == null
                    ? -(sourceRow.rectTransform.rect.height + 4f)
                    : sourceRow.rectTransform.anchoredPosition.y
                        - previousRow.rectTransform.anchoredPosition.y;
                if (Mathf.Approximately(step, 0f))
                {
                    step = -(sourceRow.rectTransform.rect.height + 4f);
                }

                var row = Instantiate(sourceRow, sourceRow.transform.parent);
                row.name = $"Runtime Event Row {eventRowPool.Count + 1}";
                row.rectTransform.anchoredPosition = new Vector2(
                    sourceRow.rectTransform.anchoredPosition.x,
                    sourceRow.rectTransform.anchoredPosition.y + step);
                row.gameObject.SetActive(false);

                var icon = Instantiate(sourceIcon, sourceIcon.transform.parent);
                icon.name = $"Runtime Event Icon {eventIconPool.Count + 1}";
                icon.rectTransform.anchoredPosition = new Vector2(
                    sourceIcon.rectTransform.anchoredPosition.x,
                    sourceIcon.rectTransform.anchoredPosition.y + step);
                icon.gameObject.SetActive(false);

                eventRowPool.Add(row);
                eventIconPool.Add(icon);
            }
        }

        private bool TryBindMapTexture()
        {
            var renderRig = PHSShipMapRenderRig.Instance;
            if (renderRig == null || renderRig.MapTexture == null)
            {
                mapImage.texture = null;
                if (!mapTextureErrorLogged)
                {
                    mapTextureErrorLogged = true;
                    Debug.LogError(
                        $"PHS_HANDHELD_MAP_VIEW_RENDER_FAILED reason=map_texture_missing view={name}",
                        this);
                }

                return false;
            }

            mapTextureErrorLogged = false;
            if (mapImage.texture != renderRig.MapTexture)
            {
                mapImage.texture = renderRig.MapTexture;
            }

            return true;
        }

        private void ConfigureEventRows()
        {
            for (var index = 0; index < eventRows.Length; index++)
            {
                var eventRow = eventRows[index];
                eventRow.enableWordWrapping = false;
                eventRow.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (markerPool.Count < requiredCount)
            {
                var instance = Instantiate(markerTemplate, markerRoot);
                instance.name = $"RuntimeMarker_{markerPool.Count}";
                instance.gameObject.SetActive(false);
                var label = instance.transform.Find("MarkerLabel")?.GetComponent<TMP_Text>();
                var glyph = instance.transform.Find(markerGlyphTemplate.name)?.GetComponent<Image>();
                if (label == null || glyph == null)
                {
                    Debug.LogError(
                        $"PHS_HANDHELD_MAP_MARKER_FAILED reason=child_missing marker={instance.name} " +
                        $"label={label != null} glyph={glyph != null}",
                        this);
                    enabled = false;
                    return;
                }

                markerPool.Add(instance);
                markerGlyphPool.Add(glyph);
                markerLabelPool.Add(label);
            }
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
                ShipMapIconId.EnemySpawn => enemySpawnIcon,
                ShipMapIconId.PatrolZone => patrolZoneIcon,
                ShipMapIconId.MeteorZone => meteorZoneIcon,
                ShipMapIconId.NebulaZone => nebulaZoneIcon,
                ShipMapIconId.PlanetZone => planetZoneIcon,
                ShipMapIconId.PowerSync => powerSyncIcon,
                ShipMapIconId.Cannon => cannonIcon,
                ShipMapIconId.WireFix => wireFixIcon,
                ShipMapIconId.Warp => warpIcon,
                ShipMapIconId.Battery => batteryIcon,
                ShipMapIconId.Wrench => wrenchIcon,
                ShipMapIconId.FireExtinguisher => fireExtinguisherIcon,
                ShipMapIconId.Vending => throw new InvalidOperationException(
                    "PHS_HANDHELD_MAP_ICON_FAILED reason=generic_vending_icon_forbidden"),
                ShipMapIconId.Player => playerIcon,
                _ => throw new ArgumentOutOfRangeException(nameof(iconId), iconId, null)
            };
        }

        private Color ResolveMarkerIconColor(ShipMapIconId iconId)
        {
            return iconId switch
            {
                ShipMapIconId.Wrench => TeamRepairToolVisualPalette.Wrench,
                ShipMapIconId.FireExtinguisher => TeamRepairToolVisualPalette.FireExtinguisher,
                ShipMapIconId.Battery => TeamRepairToolVisualPalette.Battery,
                ShipMapIconId.Player => playerMarkerColor,
                _ => Color.white
            };
        }

        private void ValidateIconReferences()
        {
            foreach (ShipMapIconId iconId in Enum.GetValues(typeof(ShipMapIconId)))
            {
                if (iconId == ShipMapIconId.None || iconId == ShipMapIconId.Vending)
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
