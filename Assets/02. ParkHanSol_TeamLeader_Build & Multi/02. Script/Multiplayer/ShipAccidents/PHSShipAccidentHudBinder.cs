using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSShipAccidentHudBinder : MonoBehaviour
    {
        [SerializeField] private PHSNetworkEventHudView eventHudView;
        [SerializeField, Min(0.05f)] private float bindRetrySeconds = 0.25f;

        private readonly List<PHSShipAccidentHudLine> lineBuffer = new();
        private PHSNetworkShipAccidentCoordinator boundCoordinator;
        private float nextBindAttemptTime;

        private void Awake()
        {
            if (eventHudView == null || !eventHudView.IsConfigured)
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_HUD_BIND_FAILED reason=view_missing_or_invalid binder={name}", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            eventHudView?.SetInternalAccidentLines(null);
            TryBindCoordinator();
        }

        private void OnDisable()
        {
            UnbindCoordinator();
            eventHudView?.SetInternalAccidentLines(null);
        }

        private void Update()
        {
            if (boundCoordinator != null && boundCoordinator.IsSpawned
                && PHSNetworkShipAccidentCoordinator.Instance == boundCoordinator)
            {
                return;
            }

            UnbindCoordinator();
            eventHudView?.SetInternalAccidentLines(null);
            if (Time.unscaledTime >= nextBindAttemptTime)
            {
                nextBindAttemptTime = Time.unscaledTime + bindRetrySeconds;
                TryBindCoordinator();
            }
        }

        private void TryBindCoordinator()
        {
            var coordinator = PHSNetworkShipAccidentCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                return;
            }

            if (boundCoordinator == coordinator)
            {
                RefreshFromCoordinator();
                return;
            }

            UnbindCoordinator();
            boundCoordinator = coordinator;
            boundCoordinator.ActiveAccidentsChanged += RefreshFromCoordinator;
            RefreshFromCoordinator();
        }

        private void UnbindCoordinator()
        {
            if (boundCoordinator != null)
            {
                boundCoordinator.ActiveAccidentsChanged -= RefreshFromCoordinator;
                boundCoordinator = null;
            }

            lineBuffer.Clear();
        }

        private void RefreshFromCoordinator()
        {
            if (boundCoordinator == null || !boundCoordinator.IsSpawned)
            {
                eventHudView.SetInternalAccidentLines(null);
                return;
            }

            lineBuffer.Clear();
            for (var index = 0; index < boundCoordinator.ActiveAccidentCount; index++)
            {
                var snapshot = boundCoordinator.GetActiveAccidentAt(index);
                if (!boundCoordinator.TryGetAccidentDefinition(snapshot.AccidentId, out var definition))
                {
                    Debug.LogError($"PHS_SHIP_ACCIDENT_HUD_REFRESH_FAILED reason=definition_missing accident={snapshot.AccidentId}", this);
                    continue;
                }

                lineBuffer.Add(new PHSShipAccidentHudLine(
                    definition.Id,
                    definition.DisplayName,
                    definition.TargetModule.ToString(),
                    snapshot.RepairProgress,
                    snapshot.RequiredRepairProgress));
            }

            eventHudView.SetInternalAccidentLines(lineBuffer);
        }
    }

    public readonly struct PHSShipAccidentHudLine
    {
        public PHSShipAccidentHudLine(
            PHSShipAccidentId accidentId,
            string displayName,
            string moduleName,
            int repairProgress,
            int requiredRepairProgress)
        {
            AccidentId = accidentId;
            DisplayName = displayName ?? string.Empty;
            ModuleName = moduleName ?? string.Empty;
            RepairProgress = repairProgress;
            RequiredRepairProgress = requiredRepairProgress;
        }

        public PHSShipAccidentId AccidentId { get; }
        public string DisplayName { get; }
        public string ModuleName { get; }
        public int RepairProgress { get; }
        public int RequiredRepairProgress { get; }
    }
}
