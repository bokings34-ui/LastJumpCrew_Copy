using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class LocalShipInteriorVisibilityController : MonoBehaviour
    {
        [SerializeField] private Transform interiorVisualRoot;
        [SerializeField, Min(0.1f)] private float localPlayerSearchInterval = 0.5f;

        private Renderer[] interiorRenderers = Array.Empty<Renderer>();
        private bool[] initialRendererStates = Array.Empty<bool>();
        private NetworkPlayerSectorState localSectorState;
        private NetworkPlayerSector appliedSector;
        private bool hasAppliedSector;
        private bool appliedVisibility;
        private bool hasAppliedVisibility;
        private float nextLocalPlayerSearchTime;

        private void Awake()
        {
            CacheInteriorRenderers();
        }

        private void OnEnable()
        {
            nextLocalPlayerSearchTime = 0f;
            hasAppliedSector = false;
            hasAppliedVisibility = false;
        }

        private void Update()
        {
            if (!HasUsableLocalSectorState())
            {
                TryFindLocalSectorState();
            }

            if (localSectorState == null)
            {
                ApplyInteriorVisibility(true);
                return;
            }

            var sector = localSectorState.CurrentSector;
            if (hasAppliedSector && appliedSector == sector)
            {
                return;
            }

            appliedSector = sector;
            hasAppliedSector = true;
            ApplyInteriorVisibility(sector != NetworkPlayerSector.AuthorizedExterior);
        }

        private void OnDisable()
        {
            RestoreInitialRendererStates();
            localSectorState = null;
            hasAppliedSector = false;
            hasAppliedVisibility = false;
        }

        private void CacheInteriorRenderers()
        {
            if (interiorVisualRoot == null)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_VISIBILITY_SETUP_FAILED reason=visual_root_missing controller={name}",
                    this);
                return;
            }

            interiorRenderers = interiorVisualRoot.GetComponentsInChildren<Renderer>(true);
            initialRendererStates = new bool[interiorRenderers.Length];
            for (var index = 0; index < interiorRenderers.Length; index++)
            {
                initialRendererStates[index] = interiorRenderers[index] != null
                    && interiorRenderers[index].enabled;
            }

            if (interiorRenderers.Length == 0)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_VISIBILITY_SETUP_FAILED reason=renderers_missing controller={name}",
                    this);
            }
        }

        private bool HasUsableLocalSectorState()
        {
            return localSectorState != null
                && localSectorState.IsSpawned
                && localSectorState.IsOwner;
        }

        private void TryFindLocalSectorState()
        {
            localSectorState = null;
            if (Time.unscaledTime < nextLocalPlayerSearchTime)
            {
                return;
            }

            nextLocalPlayerSearchTime = Time.unscaledTime + localPlayerSearchInterval;
            foreach (var sectorState in FindObjectsByType<NetworkPlayerSectorState>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (sectorState != null && sectorState.IsSpawned && sectorState.IsOwner)
                {
                    localSectorState = sectorState;
                    hasAppliedSector = false;
                    return;
                }
            }
        }

        private void ApplyInteriorVisibility(bool isVisible)
        {
            if (hasAppliedVisibility && appliedVisibility == isVisible)
            {
                return;
            }

            for (var index = 0; index < interiorRenderers.Length; index++)
            {
                var interiorRenderer = interiorRenderers[index];
                if (interiorRenderer == null)
                {
                    continue;
                }

                interiorRenderer.enabled = isVisible && initialRendererStates[index];
            }

            appliedVisibility = isVisible;
            hasAppliedVisibility = true;
        }

        private void RestoreInitialRendererStates()
        {
            for (var index = 0; index < interiorRenderers.Length; index++)
            {
                if (interiorRenderers[index] != null)
                {
                    interiorRenderers[index].enabled = initialRendererStates[index];
                }
            }
        }
    }
}
