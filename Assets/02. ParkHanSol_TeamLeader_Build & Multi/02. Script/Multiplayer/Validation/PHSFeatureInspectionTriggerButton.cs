using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using Unity.Netcode;
using UnityEngine;
using LocalInteraction = LastJumpCrew.ParkHanSol.Interaction;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    public enum PHSFeatureInspectionTriggerKind
    {
        NetworkEvent = 0,
        ShipAccident = 1,
        ShipDamage = 2,
        ShipReset = 3
    }

    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionTriggerButton :
        MonoBehaviour,
        LocalInteraction.IInteractable,
        IInteractable
    {
        [Header("Trigger")]
        [SerializeField] private PHSFeatureInspectionTriggerKind triggerKind;
        [SerializeField] private EventId networkEventId = EventId.Fire;
        [SerializeField] private ShipRoom networkEventRoom;
        [SerializeField] private PHSShipAccidentId shipAccidentId = PHSShipAccidentId.Fire;
        [SerializeField] private string shipAccidentAnchorId;
        [SerializeField, Min(1)] private int shipDamageAmount = 40;

        [Header("Incident Command")]
        [SerializeField] private PHSIncidentRequestGateway incidentGateway;
        [SerializeField] private PHSShipIncidentLayout incidentLayout;

        [Header("Presentation")]
        [SerializeField] private string interactionPrompt = "Activate inspection event";
        [SerializeField] private LocalInteraction.ShopCheckoutButtonPressVisual pressVisual;

        private readonly List<string> availableAnchorIds = new();

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(LocalInteraction.IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        public void Interact(LocalInteraction.IItemHolder itemHolder)
        {
            Trigger();
        }

        bool IInteractable.CanInteract(IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        void IInteractable.Interact(IItemHolder itemHolder)
        {
            Trigger();
        }

        private bool CanInteractCore()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return false;
            }

            if (triggerKind == PHSFeatureInspectionTriggerKind.NetworkEvent)
            {
                var coordinator = NetworkEventCoordinator.Instance;
                return coordinator != null
                    && coordinator.IsAuthoritative
                    && (!IsTerminalExternalEvent(networkEventId)
                        || (incidentGateway != null
                            && incidentGateway.IsReady
                            && (networkEventRoom == null
                                || incidentLayout != null)))
                    && !coordinator.IsEventActive(networkEventId);
            }

            if (triggerKind == PHSFeatureInspectionTriggerKind.ShipDamage)
            {
                var shipSystems = NetworkRunSessionRoot.Instance?.ShipSystems;
                return shipSystems != null && shipSystems.CurrentShipHp > 1;
            }

            if (triggerKind == PHSFeatureInspectionTriggerKind.ShipReset)
            {
                return NetworkRunSessionRoot.Instance?.ShipSystems != null;
            }

            var accidentCoordinator = PHSNetworkShipAccidentCoordinator.Instance;
            if (accidentCoordinator == null)
            {
                return false;
            }

            if (!accidentCoordinator.TryCopyAvailableCompatibleAnchorIdsServer(
                    shipAccidentId,
                    availableAnchorIds,
                    out _))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(shipAccidentAnchorId)
                || availableAnchorIds.Contains(shipAccidentAnchorId);
        }

        private void Trigger()
        {
            var accepted = triggerKind switch
            {
                PHSFeatureInspectionTriggerKind.NetworkEvent => TryTriggerNetworkEvent(),
                PHSFeatureInspectionTriggerKind.ShipAccident => TryTriggerShipAccident(),
                PHSFeatureInspectionTriggerKind.ShipDamage => TryDamageShip(),
                PHSFeatureInspectionTriggerKind.ShipReset => TryResetShip(),
                _ => false
            };
            pressVisual?.Play(accepted);
        }

        private bool TryDamageShip()
        {
            var shipSystems = NetworkRunSessionRoot.Instance?.ShipSystems;
            if (shipSystems == null || shipSystems.CurrentShipHp <= 1)
            {
                Debug.LogWarning(
                    "PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=ship_damage reason=ship_unavailable",
                    this);
                return false;
            }

            var appliedDamage = Mathf.Min(shipDamageAmount, shipSystems.CurrentShipHp - 1);
            if (!shipSystems.TryApplyShipDamage(
                    appliedDamage,
                    "feature_inspection",
                    out var reason))
            {
                Debug.LogWarning(
                    $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=ship_damage reason={reason}",
                    this);
                return false;
            }

            Debug.Log(
                $"PHS_FEATURE_INSPECTION_TRIGGERED kind=ship_damage amount={appliedDamage} hp={shipSystems.CurrentShipHp}/{shipSystems.MaximumShipHp}",
                this);
            return true;
        }

        private bool TryResetShip()
        {
            var shipSystems = NetworkRunSessionRoot.Instance?.ShipSystems;
            if (shipSystems == null)
            {
                Debug.LogWarning(
                    "PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=ship_reset reason=ship_systems_missing",
                    this);
                return false;
            }

            foreach (var moduleId in new[]
                     {
                         NetworkShipModuleId.Power,
                         NetworkShipModuleId.Gravity,
                         NetworkShipModuleId.LifeSupport,
                         NetworkShipModuleId.Engine
                     })
            {
                shipSystems.TryRepairModule(moduleId, 1000, out _);
            }

            if (!shipSystems.IsPowerEnabled)
            {
                shipSystems.TryRestorePowerWithBattery(out _);
            }

            if (!shipSystems.IsGravityEnabled)
            {
                shipSystems.TryRestoreGravityAfterRepair(out _);
            }

            if (shipSystems.CurrentShipHp < shipSystems.MaximumShipHp)
            {
                shipSystems.TryRestoreShipDurabilityAtDock(
                    shipSystems.MaximumShipHp - shipSystems.CurrentShipHp,
                    out _);
            }

            Debug.Log(
                $"PHS_FEATURE_INSPECTION_TRIGGERED kind=ship_reset hp={shipSystems.CurrentShipHp}/{shipSystems.MaximumShipHp} power={shipSystems.IsPowerEnabled} gravity={shipSystems.IsGravityEnabled}",
                this);
            return shipSystems.IsShipAlive
                && shipSystems.IsPowerEnabled
                && shipSystems.IsGravityEnabled;
        }

        private bool TryTriggerNetworkEvent()
        {
            if (!IsTerminalExternalEvent(networkEventId))
            {
                var coordinator = NetworkEventCoordinator.Instance;
                ulong instanceId = 0UL;
                var accepted = coordinator != null
                    && (networkEventRoom != null
                        ? coordinator.TrySpawnEventServer(
                            networkEventId,
                            networkEventRoom,
                            out instanceId)
                        : coordinator.TrySpawnEventServer(
                            networkEventId,
                            out instanceId));
                if (!accepted)
                {
                    Debug.LogWarning(
                        $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED " +
                        $"kind=event event={networkEventId}",
                        this);
                    return false;
                }

                Debug.Log(
                    $"PHS_FEATURE_INSPECTION_TRIGGERED kind=event " +
                    $"event={networkEventId} instance={instanceId} " +
                    $"room={networkEventRoom?.RoomId}",
                    this);
                return true;
            }

            if (incidentGateway == null)
            {
                Debug.LogWarning(
                    $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=event " +
                    $"event={networkEventId} reason=incident_gateway_missing",
                    this);
                return false;
            }

            if (!TryResolveNetworkEventTarget(
                    out var targetId,
                    out var targetReason))
            {
                Debug.LogWarning(
                    $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=event " +
                    $"event={networkEventId} " +
                    $"reason={targetReason}",
                    this);
                return false;
            }

            if (!incidentGateway.TrySubmitTerminalEventServer(
                    networkEventId,
                    targetId,
                    out var command,
                    out var submitReason))
            {
                Debug.LogWarning(
                    $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=event " +
                    $"event={networkEventId} reason={submitReason}",
                    this);
                return false;
            }

            Debug.Log(
                $"PHS_FEATURE_INSPECTION_TRIGGERED kind=event " +
                $"event={networkEventId} command={command.CommandId} " +
                $"target={targetId ?? "automatic"}",
                this);
            return true;
        }

        private bool TryResolveNetworkEventTarget(
            out string targetId,
            out string reason)
        {
            targetId = null;
            if (networkEventRoom == null)
            {
                reason = null;
                return true;
            }

            if (incidentLayout == null)
            {
                reason = "incident_layout_missing";
                return false;
            }

            foreach (var location in incidentLayout.Locations)
            {
                if (location != null
                    && location.RuntimeTarget == networkEventRoom)
                {
                    targetId = location.LocationId;
                    reason = null;
                    return true;
                }
            }

            reason = $"network_event_room_location_missing:{networkEventRoom.RoomId}";
            return false;
        }

        private static bool IsTerminalExternalEvent(EventId eventId)
        {
            return eventId == EventId.EmpAttack
                || eventId == EventId.MeteorAttack
                || eventId == EventId.EnemyScout;
        }

        private bool TryTriggerShipAccident()
        {
            var coordinator = PHSNetworkShipAccidentCoordinator.Instance;
            var reason = "coordinator_missing";
            if (coordinator == null
                || !coordinator.TrySpawnAccidentServer(
                    shipAccidentId,
                    shipAccidentAnchorId,
                    out var instanceId,
                    out reason))
            {
                Debug.LogWarning(
                    $"PHS_FEATURE_INSPECTION_TRIGGER_REJECTED kind=accident accident={shipAccidentId} reason={reason}",
                    this);
                return false;
            }

            Debug.Log(
                $"PHS_FEATURE_INSPECTION_TRIGGERED kind=accident accident={shipAccidentId} instance={instanceId} anchor={shipAccidentAnchorId}",
                this);
            return true;
        }
    }
}
