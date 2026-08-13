using LastJumpCrew.ParkHanSol.Interaction;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Reuses the tutorial TargetIndicator UI contract while the local owner is
    /// in the exterior sector, pointing only at the ship return portal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PHSExteriorReturnNavigation : MonoBehaviour
    {
        [Header("Tutorial TargetIndicator Contract")]
        [SerializeField] private GameObject targetIndicatorRoot;
        [SerializeField] private TMP_Text targetIndicatorText;

        [Header("Map Scene References")]
        [SerializeField] private ExteriorTestTeleportInteractable returnPortal;
        [SerializeField, Min(0.1f)] private float arrivedDistance = 8f;

        private NetworkPlayerController localPlayer;
        private NetworkPlayerSectorState localSectorState;
        private Camera guidanceCamera;
        private bool setupFailureLogged;

        private void OnEnable()
        {
            SetVisible(false);
        }

        private void Update()
        {
            ResolveLocalOwner();
            if (!HasRequiredReferences())
            {
                SetVisible(false);
                return;
            }

            var shouldShow = localSectorState.CurrentSector
                == NetworkPlayerSector.AuthorizedExterior;
            if (!shouldShow)
            {
                SetVisible(false);
                return;
            }

            var targetPosition = returnPortal.transform.position;
            var distance = Vector3.Distance(localPlayer.transform.position, targetPosition);
            if (distance <= arrivedDistance)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            RefreshIndicator(targetPosition, distance);
        }

        public bool IsConfigured => targetIndicatorRoot != null
            && targetIndicatorText != null
            && returnPortal != null
            && arrivedDistance > 0f;

        private void ResolveLocalOwner()
        {
            if (localPlayer != null && localPlayer.IsSpawned && localPlayer.IsOwner)
            {
                if (guidanceCamera == null)
                {
                    guidanceCamera = localPlayer.GetComponentInChildren<Camera>(true);
                }

                return;
            }

            localPlayer = null;
            localSectorState = null;
            guidanceCamera = null;
            foreach (var candidate in FindObjectsByType<NetworkPlayerController>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (!candidate.IsSpawned || !candidate.IsOwner)
                {
                    continue;
                }

                localPlayer = candidate;
                localSectorState = candidate.GetComponent<NetworkPlayerSectorState>();
                guidanceCamera = candidate.GetComponentInChildren<Camera>(true);
                return;
            }
        }

        private bool HasRequiredReferences()
        {
            var valid = IsConfigured
                && localPlayer != null
                && localSectorState != null
                && guidanceCamera != null;
            if (!valid && !setupFailureLogged && localPlayer != null)
            {
                setupFailureLogged = true;
                Debug.LogError(
                    "PHS_EXTERIOR_RETURN_NAVIGATION_SETUP_FAILED " +
                    $"root={targetIndicatorRoot != null} text={targetIndicatorText != null} " +
                    $"portal={returnPortal != null} sector={localSectorState != null} " +
                    $"camera={guidanceCamera != null}", this);
            }

            return valid;
        }

        private void RefreshIndicator(Vector3 targetPosition, float distance)
        {
            var viewport = guidanceCamera.WorldToViewportPoint(targetPosition);
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            var arrow = viewport.x < 0.08f
                ? "<  "
                : viewport.x > 0.92f
                    ? ">  "
                    : viewport.y < 0.08f
                        ? "V  "
                        : viewport.y > 0.92f
                            ? "^  "
                            : string.Empty;
            targetIndicatorText.text = $"{arrow}SHIP ENTRY  {distance:0}m";
        }

        private void SetVisible(bool visible)
        {
            if (targetIndicatorRoot != null
                && targetIndicatorRoot.activeSelf != visible)
            {
                targetIndicatorRoot.SetActive(visible);
            }
        }
    }
}
