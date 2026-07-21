using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSHandheldShipMapView : MonoBehaviour, IShipMapView
    {
        [SerializeField] private GameObject deviceRoot;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Image markerTemplate;
        [SerializeField] private Color selfColor = new(0.1f, 1f, 0.35f, 1f);
        [SerializeField] private Color teammateColor = new(0.1f, 0.75f, 1f, 1f);
        [SerializeField] private Color incidentColor = new(1f, 0.15f, 0.08f, 1f);

        private readonly List<Image> markerPool = new();

        private void Awake()
        {
            if (deviceRoot == null || markerRoot == null || markerTemplate == null)
            {
                Debug.LogError(
                    $"PHS_HANDHELD_MAP_VIEW_SETUP_FAILED view={name} device={deviceRoot != null} root={markerRoot != null} template={markerTemplate != null}",
                    this);
                enabled = false;
                return;
            }

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

        public void Render(IReadOnlyList<ShipMapMarker> markers)
        {
            if (!enabled || markerRoot == null || markerTemplate == null)
            {
                return;
            }

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
                markerImage.color = ResolveColor(marker.Kind);
                markerImage.rectTransform.sizeDelta = marker.Kind == ShipMapMarkerKind.Incident
                    ? new Vector2(20f, 20f)
                    : new Vector2(15f, 15f);
            }
        }

        private void EnsurePoolSize(int requiredCount)
        {
            while (markerPool.Count < requiredCount)
            {
                var instance = Instantiate(markerTemplate, markerRoot);
                instance.name = $"RuntimeMarker_{markerPool.Count}";
                instance.gameObject.SetActive(false);
                markerPool.Add(instance);
            }
        }

        private Color ResolveColor(ShipMapMarkerKind kind)
        {
            return kind switch
            {
                ShipMapMarkerKind.Self => selfColor,
                ShipMapMarkerKind.Teammate => teammateColor,
                ShipMapMarkerKind.Incident => incidentColor,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }
    }
}
