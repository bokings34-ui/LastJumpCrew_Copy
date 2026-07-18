using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Inspector-wired lobby bootstrap that spawns the persistent run root once on the server.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public sealed class NetworkRunSessionRootBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkRunSessionRoot runSessionRootPrefab;

        private NetworkManager networkManager;
        private bool setupValid;

        private void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            setupValid = ValidateSetup();
            enabled = setupValid;
        }

        private void OnEnable()
        {
            if (!setupValid)
            {
                return;
            }

            networkManager.OnServerStarted += HandleServerStarted;
            TrySpawnServerRoot();
        }

        private void OnDisable()
        {
            if (networkManager != null)
            {
                networkManager.OnServerStarted -= HandleServerStarted;
            }
        }

        private void HandleServerStarted()
        {
            TrySpawnServerRoot();
        }

        private void TrySpawnServerRoot()
        {
            if (!setupValid || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }

            if (NetworkRunSessionRoot.Instance != null)
            {
                return;
            }

            foreach (var spawnedObject in networkManager.SpawnManager.SpawnedObjectsList)
            {
                if (spawnedObject != null
                    && spawnedObject.TryGetComponent<NetworkRunSessionRoot>(out _))
                {
                    return;
                }
            }

            var prefabNetworkObject = runSessionRootPrefab.GetComponent<NetworkObject>();
            var networkObject = networkManager.SpawnManager.InstantiateAndSpawn(
                prefabNetworkObject,
                NetworkManager.ServerClientId,
                false,
                false);
            if (networkObject == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_SPAWN_FAILED reason=instantiate_and_spawn_failed",
                    this);
                return;
            }

            networkObject.name = runSessionRootPrefab.name;
            Debug.Log(
                $"PHS_RUN_SESSION_ROOT_SPAWNED prefab={runSessionRootPrefab.name} objectId={networkObject.NetworkObjectId}",
                this);
        }

        private bool ValidateSetup()
        {
            if (runSessionRootPrefab == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_BOOTSTRAP_FAILED reason=prefab_missing",
                    this);
                return false;
            }

            if (runSessionRootPrefab.GetComponent<NetworkObject>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunFlowCoordinator>() == null
                || runSessionRootPrefab.GetComponent<NetworkRunStageClock>() == null
                || runSessionRootPrefab.GetComponent<NetworkShipSystemsState>() == null)
            {
                Debug.LogError(
                    "PHS_RUN_SESSION_ROOT_BOOTSTRAP_FAILED reason=prefab_contract_invalid",
                    this);
                return false;
            }

            return true;
        }
    }
}
