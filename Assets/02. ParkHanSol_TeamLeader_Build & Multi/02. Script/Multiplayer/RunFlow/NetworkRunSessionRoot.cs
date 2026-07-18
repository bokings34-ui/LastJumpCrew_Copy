using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Server-owned network root for state that must survive map and shop scene changes.
    /// The root is spawned once by <see cref="NetworkRunSessionRootBootstrap"/> with
    /// destroyWithScene disabled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkRunFlowCoordinator))]
    [RequireComponent(typeof(NetworkRunStageClock))]
    [RequireComponent(typeof(NetworkShipSystemsState))]
    [RequireComponent(typeof(PHSShipEventImpactAdapter))]
    [RequireComponent(typeof(NetworkRunEconomyLedger))]
    [RequireComponent(typeof(NetworkRunRandomLedger))]
    public sealed class NetworkRunSessionRoot : NetworkBehaviour
    {
        public static NetworkRunSessionRoot Instance { get; private set; }
        public static event Action<NetworkRunSessionRoot> InstanceAvailable;

        public NetworkRunFlowCoordinator RunFlow { get; private set; }
        public NetworkRunStageClock StageClock { get; private set; }
        public NetworkShipSystemsState ShipSystems { get; private set; }
        public NetworkRunEconomyLedger Economy { get; private set; }
        public NetworkRunRandomLedger Rng { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            InstanceAvailable = null;
        }

        private void Awake()
        {
            RunFlow = GetComponent<NetworkRunFlowCoordinator>();
            StageClock = GetComponent<NetworkRunStageClock>();
            ShipSystems = GetComponent<NetworkShipSystemsState>();
            Economy = GetComponent<NetworkRunEconomyLedger>();
            Rng = GetComponent<NetworkRunRandomLedger>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (OwnerClientId != NetworkManager.ServerClientId)
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SETUP_FAILED reason=server_owner_required owner={OwnerClientId}",
                    this);
                enabled = false;
                return;
            }

            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SETUP_FAILED reason=duplicate_root current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            if (NetworkObject.DestroyWithScene)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_SETUP_FAILED reason=destroy_with_scene_enabled",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            NotifyInstanceAvailable();
            Debug.Log(
                $"PHS_RUN_SESSION_ROOT_READY server={IsServer} objectId={NetworkObjectId}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        private void NotifyInstanceAvailable()
        {
            var handlers = InstanceAvailable;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<NetworkRunSessionRoot> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_RUN_SESSION_ROOT_OBSERVER_FAILED observer={handler.Method.Name} exception={exception.GetType().Name}",
                        this);
                }
            }
        }
    }
}
