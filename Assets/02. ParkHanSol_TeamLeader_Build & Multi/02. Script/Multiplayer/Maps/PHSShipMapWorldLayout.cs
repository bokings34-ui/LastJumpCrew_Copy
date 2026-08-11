using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSShipMapWorldLayout : MonoBehaviour
    {
        [Header("Schematic Projection")]
        [SerializeField] private PHSShipMapRenderRig mapRenderRig;
        [SerializeField] private PHSShipAccidentAnchor[] accidentAnchors = Array.Empty<PHSShipAccidentAnchor>();
        [SerializeField] private PHSShipMapObjectAnchor[] objectAnchors = Array.Empty<PHSShipMapObjectAnchor>();

        private readonly Dictionary<string, PHSShipAccidentAnchor> anchorsById = new(StringComparer.Ordinal);
        private bool projectionRigErrorLogged;

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
            normalizedPosition = default;
            if (mapRenderRig == null)
            {
                if (!projectionRigErrorLogged)
                {
                    projectionRigErrorLogged = true;
                    Debug.LogError(
                        "PHS_SHIP_MAP_LAYOUT_PROJECT_FAILED reason=render_rig_missing",
                        this);
                }

                return false;
            }

            projectionRigErrorLogged = false;
            return mapRenderRig.TryProjectWorldPosition(
                worldPosition,
                "ship_map_marker",
                out normalizedPosition);
        }

        public void SetMapRenderVisible(bool visible)
        {
            if (mapRenderRig == null)
            {
                if (!projectionRigErrorLogged)
                {
                    projectionRigErrorLogged = true;
                    Debug.LogError(
                        "PHS_SHIP_MAP_LAYOUT_RENDER_FAILED reason=render_rig_missing",
                        this);
                }

                return;
            }

            projectionRigErrorLogged = false;
            mapRenderRig.SetMapVisible(visible);
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
