using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSShipMapWorldLayout : MonoBehaviour
    {
        [Header("Schematic Projection")]
        [SerializeField] private PHSShipMapRenderRig mapRenderRig;
        [SerializeField] private PHSShipMapObjectAnchor[] objectAnchors = Array.Empty<PHSShipMapObjectAnchor>();

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
            ValidateSetup();
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

        public PHSShipMapObjectAnchor GetObjectAnchorAt(int index)
        {
            if (index < 0 || index >= ObjectAnchorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return objectAnchors[index];
        }

        private void ValidateSetup()
        {
            if (mapRenderRig == null)
            {
                Debug.LogError("PHS_SHIP_MAP_LAYOUT_SETUP_FAILED reason=render_rig_missing", this);
                enabled = false;
                return;
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
