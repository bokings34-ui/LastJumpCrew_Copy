using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSRandomDebrisStream : MonoBehaviour
    {
        [SerializeField] private Transform[] debrisRoots;
        [SerializeField, Min(1)] private int minimumDebrisCount = 20;
        [SerializeField, Min(1)] private int maximumDebrisCount = 30;
        [SerializeField, Range(1f, 2f)] private float densityMultiplier = 1.35f;
        [SerializeField] private Vector3 spawnCenter = new(-330f, 6f, -15f);
        [SerializeField] private Vector3 spawnExtents = new(3f, 5f, 12f);
        [SerializeField] private Vector3[] passageLaneCenters;
        [SerializeField] private Vector3 passageLaneExtents = new(3f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float passageLaneSpawnChance = 1f;
        [SerializeField] private float recycleWorldX = -365f;
        [SerializeField] private bool distributeAcrossPathOnAwake;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2.8f;
        [SerializeField, Min(0f)] private float maximumAngularSpeed = 75f;

        private float[] speeds;
        private Vector3[] angularVelocities;
        private GameObject[] debrisSources;
        private readonly List<Transform> activeDebris = new();
        private readonly List<Transform> runtimeGeneratedDebris = new();
        private int targetDebrisCount;
        private NetworkManager boundNetworkManager;
        private NetworkSceneManager boundSceneManager;
        private bool simulationInitialized;
        private bool simulationRequested;
        private bool usesNetworkDebrisSources;
        private bool networkSceneReady;

        public void SetSimulationEnabled(bool simulationEnabled)
        {
            simulationRequested = simulationEnabled;
            enabled = simulationEnabled;

            if (simulationEnabled && usesNetworkDebrisSources)
            {
                TryBindNetworkManager();
                TryInitializeNetworkSimulation();
            }
            else if (simulationEnabled && !simulationInitialized)
            {
                InitializeSimulation();
            }

            Debug.Log($"PHS_DEBRIS_STREAM_STATE stream={name} enabled={simulationEnabled}", this);
        }

        private void Awake()
        {
            if (debrisRoots == null || debrisRoots.Length == 0)
            {
                Debug.LogError($"PHS_DEBRIS_STREAM_SETUP_FAILED reason=debris_missing stream={name}");
                enabled = false;
                return;
            }

            if (!TryCacheDebrisSources())
            {
                enabled = false;
                return;
            }

            activeDebris.AddRange(debrisRoots);
            if (usesNetworkDebrisSources)
            {
                TryBindNetworkManager();
                return;
            }

            simulationRequested = true;
            InitializeSimulation();
        }

        private void OnDestroy()
        {
            UnbindNetworkManager();
        }

        private void Update()
        {
            if (usesNetworkDebrisSources)
            {
                TryBindNetworkManager();

                if (boundNetworkManager == null
                    || !boundNetworkManager.IsListening
                    || !boundNetworkManager.IsServer
                    || !networkSceneReady)
                {
                    return;
                }

                TryInitializeNetworkSimulation();
                if (!simulationInitialized)
                {
                    return;
                }
            }

            for (var index = 0; index < activeDebris.Count; index++)
            {
                var debris = activeDebris[index];
                if (debris == null)
                {
                    debris = CreateDebris(index);
                    activeDebris[index] = debris;
                    ResetDebris(index, false);
                    if (usesNetworkDebrisSources)
                    {
                        SpawnNetworkDebris(debris);
                    }
                }

                var itemObject = debris.GetComponent<UtilityItemObject>();
                if (itemObject != null && itemObject.IsHeld)
                {
                    continue;
                }

                debris.position += Vector3.left * (speeds[index] * Time.deltaTime);
                debris.Rotate(angularVelocities[index] * Time.deltaTime, Space.Self);

                if (debris.position.x <= recycleWorldX)
                {
                    ResetDebris(index, false);
                }
            }
        }

        private void InitializeSimulation()
        {
            if (simulationInitialized)
            {
                return;
            }

            targetDebrisCount = Mathf.RoundToInt(Random.Range(
                Mathf.Min(minimumDebrisCount, maximumDebrisCount),
                Mathf.Max(minimumDebrisCount, maximumDebrisCount) + 1)
                * densityMultiplier);
            while (activeDebris.Count < targetDebrisCount)
            {
                activeDebris.Add(CreateDebris(activeDebris.Count));
            }

            speeds = new float[activeDebris.Count];
            angularVelocities = new Vector3[activeDebris.Count];
            for (var index = 0; index < activeDebris.Count; index++)
            {
                ResetDebris(index, distributeAcrossPathOnAwake);
            }

            simulationInitialized = true;
            Debug.Log(
                $"PHS_DEBRIS_STREAM_READY stream={name} mode={(usesNetworkDebrisSources ? "network" : "local")} " +
                $"total={activeDebris.Count} generated={runtimeGeneratedDebris.Count}",
                this);
        }

        private Transform CreateDebris(int index)
        {
            var debrisSource = debrisSources[index % debrisSources.Length];
            var debrisObject = usesNetworkDebrisSources
                ? Instantiate(debrisSource)
                : Instantiate(debrisSource, transform.parent);
            if (usesNetworkDebrisSources && debrisObject.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(debrisObject, gameObject.scene);
            }

            var debris = debrisObject.transform;
            runtimeGeneratedDebris.Add(debris);
            return debris;
        }

        private bool TryCacheDebrisSources()
        {
            debrisSources = new GameObject[debrisRoots.Length];
            var networkSourceCount = 0;
            for (var index = 0; index < debrisRoots.Length; index++)
            {
                var sceneSeed = debrisRoots[index];
                if (sceneSeed == null)
                {
                    Debug.LogError(
                        $"PHS_DEBRIS_STREAM_SETUP_FAILED reason=seed_missing stream={name} seed_index={index}",
                        this);
                    return false;
                }

                var utilityItemObject = sceneSeed.GetComponent<UtilityItemObject>();
                var droppedPrefab = utilityItemObject?.ItemPrefabData?.DroppedPrefab;
                if (droppedPrefab == null && sceneSeed.GetComponent<NetworkObject>() != null)
                {
                    Debug.LogError(
                        $"PHS_DEBRIS_STREAM_SETUP_FAILED reason=network_source_prefab_missing " +
                        $"stream={name} seed_index={index} seed={sceneSeed.name}",
                        this);
                    return false;
                }

                var debrisSource = droppedPrefab != null ? droppedPrefab : sceneSeed.gameObject;
                debrisSources[index] = debrisSource;
                if (debrisSource.GetComponent<NetworkObject>() != null)
                {
                    networkSourceCount++;
                }
            }

            if (networkSourceCount != 0 && networkSourceCount != debrisSources.Length)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_SETUP_FAILED reason=source_mode_mixed stream={name} " +
                    $"network={networkSourceCount} total={debrisSources.Length}",
                    this);
                return false;
            }

            usesNetworkDebrisSources = networkSourceCount == debrisSources.Length;
            return true;
        }

        private void TryInitializeNetworkSimulation()
        {
            if (simulationInitialized
                || !simulationRequested
                || !usesNetworkDebrisSources
                || !networkSceneReady
                || boundNetworkManager == null
                || !boundNetworkManager.IsListening
                || !boundNetworkManager.IsServer)
            {
                return;
            }

            InitializeSimulation();
            SpawnAllNetworkDebris();
        }

        private void SpawnAllNetworkDebris()
        {
            for (var index = 0; index < runtimeGeneratedDebris.Count; index++)
            {
                SpawnNetworkDebris(runtimeGeneratedDebris[index]);
            }
        }

        private void SpawnNetworkDebris(Transform debris)
        {
            if (boundNetworkManager == null
                || !boundNetworkManager.IsListening
                || !boundNetworkManager.IsServer
                || debris == null)
            {
                return;
            }

            var networkObject = debris.GetComponent<NetworkObject>();
            if (networkObject != null && !networkObject.IsSpawned)
            {
                networkObject.Spawn(true);
            }
        }

        private void TryBindNetworkManager()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return;
            }

            if (boundNetworkManager != networkManager)
            {
                UnbindNetworkManager();
                boundNetworkManager = networkManager;
                boundNetworkManager.OnServerStarted += HandleServerStarted;
            }

            if (boundSceneManager == null && boundNetworkManager.SceneManager != null)
            {
                boundSceneManager = boundNetworkManager.SceneManager;
                boundSceneManager.OnLoadComplete += HandleLoadComplete;
            }
        }

        private void UnbindNetworkManager()
        {
            if (boundSceneManager != null)
            {
                boundSceneManager.OnLoadComplete -= HandleLoadComplete;
                boundSceneManager = null;
            }

            if (boundNetworkManager != null)
            {
                boundNetworkManager.OnServerStarted -= HandleServerStarted;
                boundNetworkManager = null;
            }
        }

        private void HandleServerStarted()
        {
            TryBindNetworkManager();
            if (boundNetworkManager == null || !boundNetworkManager.IsServer)
            {
                return;
            }

            networkSceneReady = true;
            TryInitializeNetworkSimulation();
        }

        private void HandleLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (boundNetworkManager == null
                || !boundNetworkManager.IsServer
                || clientId != NetworkManager.ServerClientId
                || !string.Equals(sceneName, gameObject.scene.name, System.StringComparison.Ordinal))
            {
                return;
            }

            networkSceneReady = true;
            Debug.Log(
                $"PHS_DEBRIS_STREAM_SCENE_READY stream={name} scene={sceneName} mode={loadSceneMode}",
                this);
            TryInitializeNetworkSimulation();
        }

        private void ResetDebris(int index, bool distributeAcrossPath)
        {
            var debris = activeDebris[index];
            GetSpawnArea(out var laneCenter, out var laneExtents);
            var spawnX = distributeAcrossPath
                ? Random.Range(
                    Mathf.Min(recycleWorldX, laneCenter.x + laneExtents.x),
                    Mathf.Max(recycleWorldX, laneCenter.x + laneExtents.x))
                : laneCenter.x + Random.Range(-laneExtents.x, laneExtents.x);

            debris.position = new Vector3(
                spawnX,
                laneCenter.y + Random.Range(-laneExtents.y, laneExtents.y),
                laneCenter.z + Random.Range(-laneExtents.z, laneExtents.z));
            debris.rotation = Random.rotation;
            speeds[index] = Random.Range(minimumSpeed, maximumSpeed);
            angularVelocities[index] = Random.insideUnitSphere * maximumAngularSpeed;
        }

        private void GetSpawnArea(out Vector3 center, out Vector3 extents)
        {
            if (passageLaneCenters == null
                || passageLaneCenters.Length == 0
                || Random.value > passageLaneSpawnChance)
            {
                center = spawnCenter;
                extents = spawnExtents;
                return;
            }

            center = passageLaneCenters[Random.Range(0, passageLaneCenters.Length)];
            extents = passageLaneExtents;
        }
    }
}
