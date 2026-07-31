using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSShipMapWorldLayout : MonoBehaviour
    {
        [SerializeField] private Vector2 worldCenterXZ = new(2f, 55f);
        [SerializeField] private Vector2 worldSizeXZ = new(82f, 114f);
        [SerializeField] private PHSShipAccidentAnchor[] accidentAnchors = Array.Empty<PHSShipAccidentAnchor>();
        [SerializeField] private PHSShipMapObjectAnchor[] objectAnchors = Array.Empty<PHSShipMapObjectAnchor>();

        private readonly Dictionary<string, PHSShipAccidentAnchor> anchorsById = new(StringComparer.Ordinal);

        public static PHSShipMapWorldLayout Instance { get; private set; }
        public int ObjectAnchorCount => objectAnchors?.Length ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=duplicate first={Instance.name} duplicate={name}",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            RebuildAnchorLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool TryProject(Vector3 worldPosition, out Vector2 normalizedPosition)
        {
            if (worldSizeXZ.x <= 0f || worldSizeXZ.y <= 0f)
            {
                Debug.LogError(
                    $"PHS_SHIP_MAP_LAYOUT_PROJECT_FAILED reason=invalid_size size={worldSizeXZ}",
                    this);
                normalizedPosition = default;
                return false;
            }

            normalizedPosition = new Vector2(
                Mathf.Clamp01((worldPosition.x - worldCenterXZ.x) / worldSizeXZ.x + 0.5f),
                Mathf.Clamp01((worldPosition.z - worldCenterXZ.y) / worldSizeXZ.y + 0.5f));
            return true;
        }

        public bool TryGetAnchorWorldPosition(string anchorId, out Vector3 worldPosition)
        {
            if (!string.IsNullOrWhiteSpace(anchorId)
                && anchorsById.TryGetValue(anchorId, out var anchor)
                && anchor != null)
            {
                worldPosition = anchor.RepairPosition;
                return true;
            }

            Debug.LogError(
                $"PHS_SHIP_MAP_ANCHOR_FAILED reason=anchor_not_registered anchor={anchorId}",
                this);
            worldPosition = default;
            return false;
        }

        public PHSShipMapObjectAnchor GetObjectAnchorAt(int index)
        {
            if (index < 0 || index >= ObjectAnchorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return objectAnchors[index];
        }

        private void RebuildAnchorLookup()
        {
            anchorsById.Clear();
            if (accidentAnchors == null || accidentAnchors.Length == 0)
            {
                Debug.LogError("PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=accident_anchors_missing", this);
                enabled = false;
                return;
            }

            for (var index = 0; index < accidentAnchors.Length; index++)
            {
                var anchor = accidentAnchors[index];
                if (anchor == null || string.IsNullOrWhiteSpace(anchor.AnchorId))
                {
                    Debug.LogError(
                        $"PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=anchor_reference_invalid index={index}",
                        this);
                    enabled = false;
                    continue;
                }

                if (!anchorsById.TryAdd(anchor.AnchorId, anchor))
                {
                    Debug.LogError(
                        $"PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=anchor_duplicate anchor={anchor.AnchorId}",
                        this);
                    enabled = false;
                }
            }

            if (objectAnchors == null || objectAnchors.Length == 0)
            {
                Debug.LogError("PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=object_anchors_missing", this);
                enabled = false;
                return;
            }

            for (var index = 0; index < objectAnchors.Length; index++)
            {
                var anchor = objectAnchors[index];
                var reason = anchor == null ? "reference_missing" : null;
                if (anchor == null || !anchor.TryValidate(out reason))
                {
                    Debug.LogError(
                        $"PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=object_anchor_invalid " +
                        $"index={index} detail={reason}",
                        this);
                    enabled = false;
                }
            }
        }
    }
}
