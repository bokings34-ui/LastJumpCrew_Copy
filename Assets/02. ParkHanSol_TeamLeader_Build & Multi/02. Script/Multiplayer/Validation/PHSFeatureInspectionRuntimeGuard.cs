using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionRuntimeGuard : MonoBehaviour
    {
        [SerializeField] private PHSMapRuntimeContext mapRuntimeContext;
        [SerializeField] private PHSNetworkEventScheduler eventScheduler;
        [SerializeField] private PHSNetworkShipAccidentCoordinator accidentCoordinator;

        private bool setupValid;
        private bool readyLogged;

        private void Awake()
        {
            setupValid = mapRuntimeContext != null
                && eventScheduler != null
                && accidentCoordinator != null;
            if (!setupValid)
            {
                Debug.LogError(
                    $"PHS_FEATURE_INSPECTION_GUARD_FAILED reason=reference_missing guard={name}",
                    this);
                enabled = false;
                return;
            }

            DisableAutomaticSchedulingComponents();
        }

        private void Update()
        {
            if (!setupValid)
            {
                return;
            }

            DisableAutomaticSchedulingComponents();

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer
                || !accidentCoordinator.IsSpawned)
            {
                return;
            }

            eventScheduler.TryStopServer(out _);
            accidentCoordinator.TryStopServer(out _);
            if (readyLogged)
            {
                return;
            }

            readyLogged = true;
            Debug.Log(
                "PHS_FEATURE_INSPECTION_GUARD_READY automatic_schedules=false",
                this);
        }

        private void DisableAutomaticSchedulingComponents()
        {
            if (mapRuntimeContext.enabled)
            {
                mapRuntimeContext.enabled = false;
            }

            if (eventScheduler.enabled)
            {
                eventScheduler.enabled = false;
            }
        }
    }
}
