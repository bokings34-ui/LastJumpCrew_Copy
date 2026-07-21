using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSHandheldShipMapController : NetworkBehaviour
    {
        [SerializeField] private PHSHandheldShipMapView firstPersonView;
        [SerializeField] private PHSHandheldShipMapView worldView;
        [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;

        private readonly NetworkVariable<bool> mapVisible = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly List<ShipMapMarker> markers = new();
        private readonly List<NetworkEventEffectSnapshot> effectSnapshots = new();
        private bool requestedVisible;
        private float nextRefreshTime;
        private bool layoutErrorLogged;
        private bool eventCoordinatorErrorLogged;
        private bool accidentCoordinatorErrorLogged;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (firstPersonView == null || worldView == null)
            {
                Debug.LogError(
                    $"PHS_HANDHELD_MAP_SETUP_FAILED player={name} first_person={firstPersonView != null} world={worldView != null}",
                    this);
                enabled = false;
                return;
            }

            mapVisible.OnValueChanged += HandleVisibilityChanged;
            requestedVisible = mapVisible.Value;
            ApplyVisibility(mapVisible.Value);
        }

        public override void OnNetworkDespawn()
        {
            mapVisible.OnValueChanged -= HandleVisibilityChanged;
            firstPersonView?.SetVisible(false);
            worldView?.SetVisible(false);
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !enabled)
            {
                return;
            }

            if (IsOwner)
            {
                var keyboard = Keyboard.current;
                var desiredVisible = keyboard != null && keyboard.tabKey.isPressed;
                if (desiredVisible != requestedVisible)
                {
                    requestedVisible = desiredVisible;
                    firstPersonView.SetVisible(desiredVisible);
                    RequestVisibilityServerRpc(desiredVisible);
                }
            }

            var shouldRefresh = IsOwner ? requestedVisible : mapVisible.Value;
            if (shouldRefresh && Time.unscaledTime >= nextRefreshTime)
            {
                nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
                RefreshVisibleMap();
            }
        }

        [ServerRpc]
        private void RequestVisibilityServerRpc(bool visible, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogWarning(
                    $"PHS_HANDHELD_MAP_VISIBILITY_REJECTED owner={OwnerClientId} sender={rpcParams.Receive.SenderClientId}",
                    this);
                return;
            }

            mapVisible.Value = visible;
        }

        private void HandleVisibilityChanged(bool previousValue, bool currentValue)
        {
            ApplyVisibility(currentValue);
        }

        private void ApplyVisibility(bool visible)
        {
            firstPersonView.SetVisible(IsOwner && visible);
            worldView.SetVisible(!IsOwner && visible);
        }

        private void RefreshVisibleMap()
        {
            var layout = PHSShipMapWorldLayout.Instance;
            if (layout == null || !layout.isActiveAndEnabled)
            {
                if (!layoutErrorLogged)
                {
                    layoutErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_REFRESH_FAILED reason=layout_missing", this);
                }

                return;
            }

            layoutErrorLogged = false;
            markers.Clear();
            AppendPlayerMarkers(layout);
            AppendEventMarkers(layout);
            AppendAccidentMarkers(layout);

            if (IsOwner)
            {
                firstPersonView.Render(markers);
            }
            else
            {
                worldView.Render(markers);
            }
        }

        private void AppendPlayerMarkers(PHSShipMapWorldLayout layout)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.SpawnManager == null)
            {
                Debug.LogError("PHS_HANDHELD_MAP_PLAYERS_FAILED reason=spawn_manager_missing", this);
                return;
            }

            foreach (var pair in networkManager.SpawnManager.SpawnedObjects)
            {
                var playerObject = pair.Value;
                if (playerObject == null
                    || playerObject.GetComponent<NetworkPlayerController>() == null
                    || !layout.TryProject(playerObject.transform.position, out var position))
                {
                    continue;
                }

                markers.Add(new ShipMapMarker(
                    playerObject.OwnerClientId == OwnerClientId
                        ? ShipMapMarkerKind.Self
                        : ShipMapMarkerKind.Teammate,
                    position));
            }
        }

        private void AppendEventMarkers(PHSShipMapWorldLayout layout)
        {
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                if (!eventCoordinatorErrorLogged)
                {
                    eventCoordinatorErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_EVENTS_FAILED reason=coordinator_missing", this);
                }

                return;
            }

            eventCoordinatorErrorLogged = false;
            effectSnapshots.Clear();
            coordinator.CopyEffectSnapshotsTo(effectSnapshots);
            for (var index = 0; index < effectSnapshots.Count; index++)
            {
                var snapshot = effectSnapshots[index];
                if (snapshot.IsActive && layout.TryProject(snapshot.WorldPosition, out var position))
                {
                    markers.Add(new ShipMapMarker(ShipMapMarkerKind.Incident, position));
                }
            }
        }

        private void AppendAccidentMarkers(PHSShipMapWorldLayout layout)
        {
            var coordinator = PHSNetworkShipAccidentCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                if (!accidentCoordinatorErrorLogged)
                {
                    accidentCoordinatorErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_ACCIDENTS_FAILED reason=coordinator_missing", this);
                }

                return;
            }

            accidentCoordinatorErrorLogged = false;
            for (var index = 0; index < coordinator.ActiveAccidentCount; index++)
            {
                var snapshot = coordinator.GetActiveAccidentAt(index);
                if (layout.TryGetAnchorWorldPosition(snapshot.AnchorId.ToString(), out var worldPosition)
                    && layout.TryProject(worldPosition, out var position))
                {
                    markers.Add(new ShipMapMarker(ShipMapMarkerKind.Incident, position));
                }
            }
        }
    }
}
