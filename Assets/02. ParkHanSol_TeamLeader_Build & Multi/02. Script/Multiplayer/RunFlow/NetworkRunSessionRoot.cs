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
    public sealed class NetworkRunSessionRoot : NetworkBehaviour
    {
        public static NetworkRunSessionRoot Instance { get; private set; }

        public NetworkRunFlowCoordinator RunFlow { get; private set; }
        public NetworkRunStageClock StageClock { get; private set; }
        public NetworkShipSystemsState ShipSystems { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            RunFlow = GetComponent<NetworkRunFlowCoordinator>();
            StageClock = GetComponent<NetworkRunStageClock>();
            ShipSystems = GetComponent<NetworkShipSystemsState>();
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
    }
}
