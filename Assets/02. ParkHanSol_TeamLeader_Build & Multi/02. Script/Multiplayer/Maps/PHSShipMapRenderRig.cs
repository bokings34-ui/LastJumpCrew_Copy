using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSShipMapRenderRig : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera;
        [SerializeField] private RenderTexture mapTexture;
        [SerializeField] private Transform schematicRoot;
        [SerializeField, Min(0.1f)] private float renderInterval = 0.1f;

        private readonly HashSet<string> projectionErrors = new(StringComparer.Ordinal);
        private bool mapVisible;
        private float nextRenderTime;

        public static PHSShipMapRenderRig Instance { get; private set; }
        public RenderTexture MapTexture => mapTexture;
        public Camera MapCamera => mapCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=duplicate current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            if (!TryValidateSetup())
            {
                enabled = false;
                return;
            }

            Instance = this;
            mapCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (!mapVisible || Time.unscaledTime < nextRenderTime)
            {
                return;
            }

            RenderNow();
        }

        public void SetMapVisible(bool visible)
        {
            if (!enabled)
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_VISIBILITY_FAILED reason=rig_not_ready", this);
                return;
            }

            if (mapVisible == visible)
            {
                return;
            }

            mapVisible = visible;
            if (mapVisible)
            {
                nextRenderTime = 0f;
                RenderNow();
            }
        }

        public void RenderNow()
        {
            if (!enabled || !TryValidateSetup())
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_RENDER_FAILED reason=setup_invalid", this);
                return;
            }

            var wasEnabled = mapCamera.enabled;
            mapCamera.enabled = true;
            mapCamera.Render();
            mapCamera.enabled = wasEnabled;
            nextRenderTime = Time.unscaledTime + renderInterval;
        }

        public bool TryProjectWorldPosition(
            Vector3 worldPosition,
            string sourceId,
            out Vector2 normalizedPosition)
        {
            normalizedPosition = default;
            if (!enabled || !TryValidateSetup())
            {
                LogProjectionFailure(sourceId, "setup_invalid", worldPosition);
                return false;
            }

            var viewportPosition = mapCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0f)
            {
                LogProjectionFailure(sourceId, "behind_camera", worldPosition);
                return false;
            }

            if (viewportPosition.x < 0f || viewportPosition.x > 1f
                || viewportPosition.y < 0f || viewportPosition.y > 1f)
            {
                LogProjectionFailure(sourceId, "outside_viewport", worldPosition);
                return false;
            }

            normalizedPosition = new Vector2(viewportPosition.x, viewportPosition.y);
            return true;
        }

        private bool TryValidateSetup()
        {
            if (mapCamera == null)
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=map_camera_missing", this);
                return false;
            }

            if (mapTexture == null)
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=render_texture_missing", this);
                return false;
            }

            if (schematicRoot == null)
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=schematic_root_missing", this);
                return false;
            }

            if (!mapCamera.orthographic)
            {
                Debug.LogError("PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=camera_not_orthographic", this);
                return false;
            }

            if (mapCamera.targetTexture != mapTexture)
            {
                Debug.LogError(
                    "PHS_SHIP_MAP_RENDER_RIG_SETUP_FAILED reason=camera_render_texture_mismatch",
                    this);
                return false;
            }

            return true;
        }

        private void LogProjectionFailure(string sourceId, string reason, Vector3 worldPosition)
        {
            var key = $"{sourceId ?? string.Empty}:{reason}";
            if (!projectionErrors.Add(key))
            {
                return;
            }

            Debug.LogError(
                $"PHS_SHIP_MAP_RENDER_RIG_PROJECT_FAILED reason={reason} source={sourceId} world={worldPosition}",
                this);
        }
    }
}
