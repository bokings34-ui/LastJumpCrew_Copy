using System;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
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
    [RequireComponent(typeof(NetworkShopTransitionVoteCoordinator))]
    [RequireComponent(typeof(NetworkRunRestartCoordinator))]
    [RequireComponent(typeof(NetworkGameOverSequenceCoordinator))]
    [RequireComponent(typeof(NetworkEventCoordinator))]
    [RequireComponent(typeof(RoomRegistry))]
    [RequireComponent(typeof(NetworkPersistentToolBoxStorage))]
    public sealed class NetworkRunSessionRoot : NetworkBehaviour
    {
        public static NetworkRunSessionRoot Instance { get; private set; }
        public static event Action<NetworkRunSessionRoot> InstanceAvailable;

        [Header("Persistent Team Event Authority")]
        [SerializeField] private NetworkEventCoordinator eventCoordinator;
        [SerializeField] private PHSNetworkEventScheduler eventScheduler;

        [Header("Persistent ToolBox State")]
        [SerializeField] private NetworkPersistentToolBoxStorage toolBoxStorage;

        public NetworkRunFlowCoordinator RunFlow { get; private set; }
        public NetworkRunStageClock StageClock { get; private set; }
        public NetworkShipSystemsState ShipSystems { get; private set; }
        public NetworkRunEconomyLedger Economy { get; private set; }
        public NetworkRunRandomLedger Rng { get; private set; }
        public NetworkShopTransitionVoteCoordinator ShopTransitionVotes { get; private set; }
        public NetworkRunRestartCoordinator Restart { get; private set; }
        public NetworkGameOverSequenceCoordinator GameOverSequence { get; private set; }
        public NetworkEventCoordinator EventCoordinator => eventCoordinator;
        public PHSNetworkEventScheduler EventScheduler => eventScheduler;
        public NetworkPersistentToolBoxStorage ToolBoxStorage => toolBoxStorage;

        /// <summary>
        /// The team event NetworkBehaviour must share this root NetworkObject.  A nested
        /// NetworkObject is deliberately forbidden: its NetworkVariable messages can arrive
        /// before its spawn during an additive/single scene transition.
        /// </summary>
        public bool TryValidatePersistentEventAuthority(out string reason)
        {
            if (eventCoordinator == null || eventScheduler == null)
            {
                reason = "event_authority_reference_missing";
                return false;
            }

            if (GetComponent<RoomRegistry>() == null)
            {
                reason = "event_room_registry_missing_on_session_root";
                return false;
            }

            if (eventCoordinator.NetworkObject != NetworkObject)
            {
                reason = "event_authority_not_on_session_root";
                return false;
            }

            if (!IsSpawned || !eventCoordinator.IsSpawned)
            {
                reason = "event_authority_not_spawned";
                return false;
            }

            if (NetworkObject.DestroyWithScene)
            {
                reason = "session_root_destroy_with_scene";
                return false;
            }

            reason = null;
            return true;
        }

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
            ShopTransitionVotes = GetComponent<NetworkShopTransitionVoteCoordinator>();
            Restart = GetComponent<NetworkRunRestartCoordinator>();
            GameOverSequence = GetComponent<NetworkGameOverSequenceCoordinator>();
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

            if (!TryValidatePersistentEventAuthority(out var authorityReason))
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SETUP_FAILED reason={authorityReason}",
                    this);
                enabled = false;
                return;
            }

            if (!RegisterPersistentEventRooms())
            {
                enabled = false;
                return;
            }

            Instance = this;
            NotifyInstanceAvailable();
            Debug.Log(
                $"PHS_P0_EVENT_AUTHORITY_READY role={(IsServer ? "host" : "client")} " +
                $"objectId={NetworkObjectId} coordinatorObjectId={eventCoordinator.NetworkObjectId} " +
                "shared_root=true",
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

        private bool RegisterPersistentEventRooms()
        {
            var roomRegistry = GetComponent<RoomRegistry>();
            var rooms = GetComponentsInChildren<ShipRoom>(true);
            if (roomRegistry == null || rooms.Length == 0)
            {
                Debug.LogError(
                    $"PHS_RUN_SESSION_ROOT_SETUP_FAILED reason=persistent_rooms_missing " +
                    $"registry={roomRegistry != null} rooms={rooms.Length}",
                    this);
                return false;
            }

            foreach (var room in rooms)
            {
                roomRegistry.Register(room);
            }

            Debug.Log(
                $"PHS_PERSISTENT_EVENT_ROOMS_READY registry=root rooms={rooms.Length}",
                this);
            return true;
        }
    }
}
