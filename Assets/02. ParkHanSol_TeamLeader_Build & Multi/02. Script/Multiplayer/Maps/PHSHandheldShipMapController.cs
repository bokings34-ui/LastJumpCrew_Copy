using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using SM;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSHandheldShipMapController : NetworkBehaviour
    {
        [SerializeField] private PHSHandheldShipMapView firstPersonView;
        [SerializeField] private PHSHandheldShipMapView worldView;
        [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;
        [SerializeField, Min(0.1f)] private float coordinatorBindTimeoutSeconds = 5f;

        private readonly NetworkVariable<bool> mapVisible = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly List<ShipMapMarker> markers = new();
        private readonly List<ShipMapEventDetail> eventDetails = new();
        private readonly List<NetworkEventEffectSnapshot> effectSnapshots = new();
        private readonly List<NetworkEventLifecycleSnapshot> lifecycleSnapshots = new();
        private readonly HashSet<ulong> itemMarkerSetupErrors = new();
        private readonly HashSet<PHSShipAccidentId> accidentDefinitionErrors = new();
        private PHSMapRuntimeContext mapRuntimeContext;
        private bool requestedVisible;
        private float nextRefreshTime;
        private bool layoutErrorLogged;
        private bool eventCoordinatorErrorLogged;
        private bool accidentCoordinatorErrorLogged;
        private bool runFlowErrorLogged;
        private bool mapProfileErrorLogged;
        private bool shipSystemsErrorLogged;
        private bool hudBridgeErrorLogged;
        private bool itemSpawnManagerErrorLogged;
        private string observedScenePath;
        private float coordinatorBindDeadline;

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
            ResetCoordinatorBindWindow();
            ApplyVisibility(mapVisible.Value);
        }

        public override void OnNetworkDespawn()
        {
            mapVisible.OnValueChanged -= HandleVisibilityChanged;
            if (IsOwner && PHSHandheldMapHudVisibilityBridge.Instance != null)
            {
                PHSHandheldMapHudVisibilityBridge.Instance.SetMapVisible(false);
            }

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
            if (!IsOwner)
            {
                return;
            }

            var bridge = PHSHandheldMapHudVisibilityBridge.Instance;
            if (bridge == null)
            {
                if (visible && !hudBridgeErrorLogged)
                {
                    hudBridgeErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_HUD_FAILED reason=visibility_bridge_missing", this);
                }

                return;
            }

            hudBridgeErrorLogged = false;
            bridge.SetMapVisible(visible);
        }

        private void RefreshVisibleMap()
        {
            var activeScenePath = SceneManager.GetActiveScene().path;
            if (activeScenePath != observedScenePath)
            {
                ResetCoordinatorBindWindow();
            }

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
            eventDetails.Clear();
            AppendPlayerMarkers(layout);
            AppendObjectMarkers(layout);
            AppendFloorItemMarkers(layout);
            AppendEventMarkers(layout);
            AppendAccidentMarkers(layout);

            if (!TryBuildPresentation(out var presentation))
            {
                return;
            }

            if (IsOwner)
            {
                firstPersonView.Render(in presentation);
            }
            else
            {
                worldView.Render(in presentation);
            }
        }

        private void ResetCoordinatorBindWindow()
        {
            observedScenePath = SceneManager.GetActiveScene().path;
            coordinatorBindDeadline = Time.unscaledTime + coordinatorBindTimeoutSeconds;
            eventCoordinatorErrorLogged = false;
            accidentCoordinatorErrorLogged = false;
            accidentDefinitionErrors.Clear();
            itemMarkerSetupErrors.Clear();
            itemSpawnManagerErrorLogged = false;
            runFlowErrorLogged = false;
            mapProfileErrorLogged = false;
            shipSystemsErrorLogged = false;
            hudBridgeErrorLogged = false;
        }

        private bool TryBuildPresentation(out ShipMapPresentation presentation)
        {
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null || !runFlow.IsSpawned)
            {
                if (!IsCoordinatorBindPending() && !runFlowErrorLogged)
                {
                    runFlowErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_STATUS_FAILED reason=run_flow_missing", this);
                }

                presentation = default;
                return false;
            }

            runFlowErrorLogged = false;
            if (mapRuntimeContext == null || !mapRuntimeContext.isActiveAndEnabled)
            {
                mapRuntimeContext = FindAnyObjectByType<PHSMapRuntimeContext>();
            }

            var profile = mapRuntimeContext != null ? mapRuntimeContext.CurrentProfile : null;
            if (profile == null)
            {
                if (!IsCoordinatorBindPending() && !mapProfileErrorLogged)
                {
                    mapProfileErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_STATUS_FAILED reason=map_profile_missing", this);
                }

                presentation = default;
                return false;
            }

            mapProfileErrorLogged = false;
            var shipSystems = NetworkShipSystemsState.Instance;
            if (shipSystems == null || !shipSystems.IsSpawned || shipSystems.MaximumShipHp <= 0)
            {
                if (!IsCoordinatorBindPending() && !shipSystemsErrorLogged)
                {
                    shipSystemsErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_STATUS_FAILED reason=ship_systems_missing", this);
                }

                presentation = default;
                return false;
            }

            shipSystemsErrorLogged = false;
            presentation = new ShipMapPresentation(
                markers,
                profile.DisplayName,
                runFlow.ActiveMapId,
                profile.Difficulty,
                ResolveRunPhase(runFlow.Phase),
                runFlow.WarpChargeNormalized,
                (float)shipSystems.CurrentShipHp / shipSystems.MaximumShipHp,
                eventDetails);
            return true;
        }

        private bool IsCoordinatorBindPending()
        {
            return Time.unscaledTime < coordinatorBindDeadline;
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
                    position,
                    playerObject.OwnerClientId == OwnerClientId ? "YOU" : "P"));
            }
        }

        private void AppendEventMarkers(PHSShipMapWorldLayout layout)
        {
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                if (IsCoordinatorBindPending())
                {
                    return;
                }

                if (!eventCoordinatorErrorLogged)
                {
                    eventCoordinatorErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_EVENTS_FAILED reason=coordinator_missing", this);
                }

                return;
            }

            eventCoordinatorErrorLogged = false;
            effectSnapshots.Clear();
            lifecycleSnapshots.Clear();
            coordinator.CopyEffectSnapshotsTo(effectSnapshots);
            coordinator.CopySnapshotsTo(lifecycleSnapshots);
            for (var index = 0; index < lifecycleSnapshots.Count; index++)
            {
                var lifecycle = lifecycleSnapshots[index];
                if (lifecycle.IsTerminal)
                {
                    continue;
                }

                var symbol = TryResolveEventSymbol(lifecycle.InstanceId, out var resolvedSymbol)
                    ? resolvedSymbol
                    : ResolveLifecycleEventSymbol(lifecycle.EventId);
                eventDetails.Add(new ShipMapEventDetail(
                    ResolveLifecycleEventIcon(lifecycle.EventId),
                    symbol,
                    ResolveLifecycleEventTitle(lifecycle.EventId),
                    $"{ResolveRoomName(lifecycle.RoomId.ToString())} · " +
                    ResolveEventState(lifecycle.State)));
            }

            for (var index = 0; index < effectSnapshots.Count; index++)
            {
                var snapshot = effectSnapshots[index];
                if (snapshot.IsActive && layout.TryProject(snapshot.WorldPosition, out var position))
                {
                    markers.Add(new ShipMapMarker(
                        ShipMapMarkerKind.Incident,
                        position,
                        string.Empty,
                        ResolveEventIcon(snapshot.Kind)));
                }
            }
        }

        private void AppendAccidentMarkers(PHSShipMapWorldLayout layout)
        {
            var coordinator = PHSNetworkShipAccidentCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                if (IsCoordinatorBindPending())
                {
                    return;
                }

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
                    markers.Add(new ShipMapMarker(
                        ShipMapMarkerKind.Incident,
                        position,
                        string.Empty,
                        ResolveAccidentIcon(snapshot.AccidentId)));
                }

                if (!coordinator.TryGetAccidentDefinition(snapshot.AccidentId, out var definition)
                    || definition == null
                    || string.IsNullOrWhiteSpace(definition.RequiredItemId))
                {
                    if (accidentDefinitionErrors.Add(snapshot.AccidentId))
                    {
                        Debug.LogError(
                            $"PHS_HANDHELD_MAP_ACCIDENTS_FAILED reason=required_item_missing accident={snapshot.AccidentId}",
                            this);
                    }

                    continue;
                }

                accidentDefinitionErrors.Remove(snapshot.AccidentId);
                eventDetails.Add(new ShipMapEventDetail(
                    ResolveAccidentIcon(snapshot.AccidentId),
                    ResolveAccidentSymbol(snapshot.AccidentId),
                    ResolveAccidentTitle(snapshot.AccidentId),
                    $"발생 중 · 필요: {definition.RequiredItemId}"));
            }
        }

        private void AppendObjectMarkers(PHSShipMapWorldLayout layout)
        {
            for (var index = 0; index < layout.ObjectAnchorCount; index++)
            {
                var anchor = layout.GetObjectAnchorAt(index);
                if (anchor != null
                    && (anchor.Kind == ShipMapObjectKind.SellStation || anchor.gameObject.activeInHierarchy)
                    && layout.TryProject(anchor.transform.position, out var position))
                {
                    markers.Add(new ShipMapMarker(
                        ShipMapMarkerKind.Object,
                        position,
                        anchor.Symbol,
                        anchor.IconId));
                }
            }
        }

        private void AppendFloorItemMarkers(PHSShipMapWorldLayout layout)
        {
            var spawnManager = NetworkManager.Singleton == null
                ? null
                : NetworkManager.Singleton.SpawnManager;
            if (spawnManager == null)
            {
                if (!itemSpawnManagerErrorLogged)
                {
                    itemSpawnManagerErrorLogged = true;
                    Debug.LogError("PHS_HANDHELD_MAP_ITEMS_FAILED reason=spawn_manager_missing", this);
                }

                return;
            }

            itemSpawnManagerErrorLogged = false;
            foreach (var pair in spawnManager.SpawnedObjects)
            {
                var networkObject = pair.Value;
                var itemObject = networkObject == null
                    ? null
                    : networkObject.GetComponent<UtilityItemObject>();
                if (itemObject == null)
                {
                    continue;
                }

                if (itemObject.ItemPrefabData == null)
                {
                    if (itemMarkerSetupErrors.Add(pair.Key))
                    {
                        Debug.LogError(
                            $"PHS_HANDHELD_MAP_ITEMS_FAILED reason=item_data_missing networkObjectId={pair.Key}",
                            networkObject);
                    }

                    continue;
                }

                if (!TryResolveTrackedItemIcon(itemObject.ItemId, out var iconId))
                {
                    itemMarkerSetupErrors.Remove(pair.Key);
                    continue;
                }

                var networkTransform = networkObject.GetComponent<NetworkTransform>();
                if (networkTransform == null)
                {
                    if (itemMarkerSetupErrors.Add(pair.Key))
                    {
                        Debug.LogError(
                            $"PHS_HANDHELD_MAP_ITEMS_FAILED reason=network_transform_missing networkObjectId={pair.Key} item={itemObject.ItemId}",
                            networkObject);
                    }

                    continue;
                }

                itemMarkerSetupErrors.Remove(pair.Key);
                if (layout.TryProject(networkTransform.transform.position, out var position))
                {
                    markers.Add(new ShipMapMarker(
                        ShipMapMarkerKind.Object,
                        position,
                        string.Empty,
                        iconId));
                }
            }
        }

        private static bool TryResolveTrackedItemIcon(
            string itemId,
            out ShipMapIconId iconId)
        {
            iconId = itemId switch
            {
                "battery_pack" => ShipMapIconId.Battery,
                "wrench" => ShipMapIconId.Wrench,
                "fire_extinguisher" => ShipMapIconId.FireExtinguisher,
                _ => ShipMapIconId.None
            };
            return iconId != ShipMapIconId.None;
        }

        private bool TryResolveEventSymbol(ulong eventInstanceId, out string symbol)
        {
            for (var index = 0; index < effectSnapshots.Count; index++)
            {
                var effect = effectSnapshots[index];
                if (effect.EventInstanceId == eventInstanceId && effect.IsActive)
                {
                    symbol = ResolveEventSymbol(effect.Kind);
                    return true;
                }
            }

            symbol = null;
            return false;
        }

        private static string ResolveEventSymbol(EventEffectKind kind)
        {
            return kind switch
            {
                EventEffectKind.Fire => "F",
                EventEffectKind.OxygenLeak => "O2",
                EventEffectKind.Enemy => "EN",
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static ShipMapIconId ResolveEventIcon(EventEffectKind kind)
        {
            return kind switch
            {
                EventEffectKind.Fire => ShipMapIconId.Fire,
                EventEffectKind.OxygenLeak => ShipMapIconId.OxygenFailure,
                EventEffectKind.Enemy => ShipMapIconId.None,
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static string ResolveLifecycleEventSymbol(EventId eventId)
        {
            return eventId switch
            {
                EventId.Fire => "FIR",
                EventId.EnemySpawn => "EN",
                EventId.PowerOff => "PWR",
                EventId.OxygenLeak => "O2",
                EventId.EngineBreak => "ENG",
                EventId.MicDestroy => "MIC",
                EventId.EnemyScout => "SCT",
                EventId.MeteorAttack => "MET",
                EventId.EmpAttack => "EMP",
                EventId.PatrolZone => "PAT",
                EventId.MeteorZone => "MET",
                EventId.NebulaZone => "NEB",
                EventId.PlanetZone => "PLN",
                _ => throw new System.ArgumentOutOfRangeException(nameof(eventId), eventId, null)
            };
        }

        private static ShipMapIconId ResolveLifecycleEventIcon(EventId eventId)
        {
            return eventId switch
            {
                EventId.Fire => ShipMapIconId.Fire,
                EventId.PowerOff => ShipMapIconId.PowerFailure,
                EventId.OxygenLeak => ShipMapIconId.OxygenFailure,
                EventId.EngineBreak => ShipMapIconId.DeviceFailure,
                EventId.MicDestroy => ShipMapIconId.DeviceFailure,
                EventId.EnemySpawn
                    or EventId.EnemyScout
                    or EventId.MeteorAttack
                    or EventId.EmpAttack
                    or EventId.PatrolZone
                    or EventId.MeteorZone
                    or EventId.NebulaZone
                    or EventId.PlanetZone => ShipMapIconId.None,
                _ => throw new System.ArgumentOutOfRangeException(nameof(eventId), eventId, null)
            };
        }

        private static string ResolveLifecycleEventTitle(EventId eventId)
        {
            return eventId switch
            {
                EventId.Fire => "화재",
                EventId.EnemySpawn => "적 침입",
                EventId.PowerOff => "전력 차단",
                EventId.OxygenLeak => "산소 누출",
                EventId.EngineBreak => "엔진 고장",
                EventId.MicDestroy => "통신 장치 파손",
                EventId.EnemyScout => "적 정찰",
                EventId.MeteorAttack => "운석 공격",
                EventId.EmpAttack => "EMP 공격",
                EventId.PatrolZone => "적 순찰 구역",
                EventId.MeteorZone => "운석 지대",
                EventId.NebulaZone => "성운 지대",
                EventId.PlanetZone => "행성 구역",
                _ => throw new System.ArgumentOutOfRangeException(nameof(eventId), eventId, null)
            };
        }

        private static string ResolveAccidentSymbol(PHSShipAccidentId accidentId)
        {
            return accidentId switch
            {
                PHSShipAccidentId.Fire => "F",
                PHSShipAccidentId.PowerFailure => "PWR",
                PHSShipAccidentId.DeviceFailure => "DEV",
                PHSShipAccidentId.HullBreach => "HULL",
                PHSShipAccidentId.SteamLeak => "STM",
                PHSShipAccidentId.OxygenFailure => "O2",
                PHSShipAccidentId.GravityGeneratorFailure => "GRAV",
                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(accidentId), accidentId, null)
            };
        }

        private static ShipMapIconId ResolveAccidentIcon(PHSShipAccidentId accidentId)
        {
            return accidentId switch
            {
                PHSShipAccidentId.Fire => ShipMapIconId.Fire,
                PHSShipAccidentId.PowerFailure => ShipMapIconId.PowerFailure,
                PHSShipAccidentId.DeviceFailure => ShipMapIconId.DeviceFailure,
                PHSShipAccidentId.HullBreach => ShipMapIconId.HullBreach,
                PHSShipAccidentId.SteamLeak => ShipMapIconId.SteamLeak,
                PHSShipAccidentId.OxygenFailure => ShipMapIconId.OxygenFailure,
                PHSShipAccidentId.GravityGeneratorFailure => ShipMapIconId.GravityFailure,
                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(accidentId), accidentId, null)
            };
        }

        private static string ResolveAccidentTitle(PHSShipAccidentId accidentId)
        {
            return accidentId switch
            {
                PHSShipAccidentId.Fire => "함선 화재",
                PHSShipAccidentId.PowerFailure => "전력 고장",
                PHSShipAccidentId.DeviceFailure => "장치 고장",
                PHSShipAccidentId.HullBreach => "선체 파손",
                PHSShipAccidentId.SteamLeak => "증기 누출",
                PHSShipAccidentId.OxygenFailure => "산소 장치 고장",
                PHSShipAccidentId.GravityGeneratorFailure => "중력 장치 고장",
                _ => throw new System.ArgumentOutOfRangeException(
                    nameof(accidentId), accidentId, null)
            };
        }

        private static string ResolveRoomName(string roomId)
        {
            return roomId switch
            {
                "Room A" or "room_a" => "중앙 홀 · 좌현",
                "Room B" or "room_b" => "중앙 홀 · 우현",
                "Room C" or "room_c" => "후미 통로",
                "Room D" or "room_d" or "중앙 복도" or "central_corridor" => "중앙 홀 · 중앙",
                _ => roomId
            };
        }

        private static string ResolveEventState(EventState state)
        {
            return state switch
            {
                EventState.Ready => "대기",
                EventState.Trigger => "발생",
                EventState.InProgress => "진행 중",
                EventState.Resolve => "해결",
                EventState.Fail => "실패",
                _ => throw new System.ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }

        private static string ResolveRunPhase(NetworkRunPhase phase)
        {
            return phase switch
            {
                NetworkRunPhase.Waiting => "대기",
                NetworkRunPhase.Charging => "워프 충전 중",
                NetworkRunPhase.WarpReady => "워프 준비 완료",
                NetworkRunPhase.Warping => "워프 중",
                NetworkRunPhase.WarpArrival => "워프 도착",
                NetworkRunPhase.Rearming => "재정비",
                NetworkRunPhase.Shop => "상점",
                NetworkRunPhase.FinalShop => "최종 상점",
                NetworkRunPhase.Clear => "항해 완료",
                NetworkRunPhase.GameOver => "함선 손실",
                NetworkRunPhase.WarpSafe => "워프 안전 구역",
                _ => throw new System.ArgumentOutOfRangeException(nameof(phase), phase, null)
            };
        }
    }
}
