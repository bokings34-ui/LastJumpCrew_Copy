using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSRandomDebrisStream :
        MonoBehaviour,
        IDebrisPopulationRuntime
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
        [SerializeField, Range(0f, 1f)] private float oppositeFlowChance = 0.45f;
        [SerializeField] private bool distributeAcrossPathOnAwake;
        [SerializeField] private bool allowOfflineLocalSimulation;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2.8f;
        [SerializeField, Min(0f)] private float maximumAngularSpeed = 75f;
        [SerializeField, Min(0f)] private float maximumFlowAcceleration = 0.35f;

        private float[] speeds;
        private Vector3[] angularVelocities;
        private Vector3[] flowDirections;
        private Rigidbody[] debrisRigidbodies;
        private NetworkTransform[] debrisNetworkTransforms;
        private GameObject[] debrisSources;
        private readonly List<Transform> activeDebris = new();
        private readonly List<Transform> runtimeGeneratedDebris = new();
        private int targetDebrisCount;
        private int configuredDebrisAmount;
        private NetworkManager boundNetworkManager;
        private NetworkSceneManager boundSceneManager;
        private bool simulationInitialized;
        private bool simulationRequested;
        private bool usesNetworkDebrisSources;
        private bool networkSceneReady;
        private bool offlineLocalSimulationActive;

        public int ConfiguredDebrisAmount => configuredDebrisAmount;
        public int ActiveDebrisAmount => simulationRequested
            ? activeDebris.Count
            : 0;

        public bool ConfigureTargetDebrisCount(int debrisAmount)
        {
            if (debrisAmount < 0)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_CONFIG_FAILED reason=amount_negative amount={debrisAmount}",
                    this);
                return false;
            }

            if (debrisAmount > 0
                && debrisRoots != null
                && debrisAmount < debrisRoots.Length)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_CONFIG_FAILED reason=amount_below_seed_count " +
                    $"amount={debrisAmount} seeds={debrisRoots.Length}",
                    this);
                return false;
            }

            if (configuredDebrisAmount == debrisAmount)
            {
                return true;
            }

            configuredDebrisAmount = debrisAmount;
            if (simulationInitialized)
            {
                RebuildSimulationForConfiguredAmount();
            }

            return true;
        }

        public void SetSimulationEnabled(bool simulationEnabled)
        {
            simulationRequested = simulationEnabled;
            enabled = simulationEnabled;

            if (simulationEnabled && usesNetworkDebrisSources)
            {
                TryBindNetworkManager();
                if (!TryStartOfflineLocalSimulation("simulation_enabled"))
                {
                    TryInitializeNetworkSimulation();
                }
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
                if (allowOfflineLocalSimulation && !IsNetworkListening())
                {
                    simulationRequested = true;
                    TryStartOfflineLocalSimulation("awake");
                }

                return;
            }

            simulationRequested = true;
            InitializeSimulation();
        }

        private void OnEnable()
        {
            if (!simulationRequested || !usesNetworkDebrisSources)
            {
                return;
            }

            TryBindNetworkManager();
            TryStartOfflineLocalSimulation("component_enabled");
        }

        private void OnDisable()
        {
            StopOfflineLocalSimulation("component_disabled");
        }

        private void OnDestroy()
        {
            UnbindNetworkManager();
        }

        private void FixedUpdate()
        {
            if (usesNetworkDebrisSources)
            {
                TryBindNetworkManager();

                if (offlineLocalSimulationActive)
                {
                    if (IsNetworkListening())
                    {
                        StopOfflineLocalSimulation("network_started");
                        Debug.LogError(
                            $"PHS_DEBRIS_STREAM_OFFLINE_FAILED reason=network_started_after_offline " +
                            $"stream={name}",
                            this);

                        enabled = false;
                        return;
                    }
                }
                else if (boundNetworkManager == null
                         || !boundNetworkManager.IsListening
                         || !boundNetworkManager.IsServer
                         || !networkSceneReady)
                {
                    return;
                }

                if (!offlineLocalSimulationActive)
                {
                    TryInitializeNetworkSimulation();
                    if (!simulationInitialized)
                    {
                        return;
                    }
                }
            }

            for (var index = 0; index < activeDebris.Count; index++)
            {
                var debris = activeDebris[index];
                if (debris == null)
                {
                    debris = CreateDebris(index);
                    activeDebris[index] = debris;
                    if (!TryCacheDebrisPhysics(index, debris))
                    {
                        enabled = false;
                        return;
                    }

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

                var rigidbody = debrisRigidbodies[index];
                var flowDirection = flowDirections[index];
                var currentFlowSpeed = Vector3.Dot(rigidbody.linearVelocity, flowDirection);
                var targetFlowAcceleration = (speeds[index] - currentFlowSpeed) / Time.fixedDeltaTime;
                var clampedFlowAcceleration = Mathf.Clamp(
                    targetFlowAcceleration,
                    -maximumFlowAcceleration,
                    maximumFlowAcceleration);
                rigidbody.AddForce(
                    flowDirection * clampedFlowAcceleration,
                    ForceMode.Acceleration);

                var movingRight = flowDirections[index].x > 0f;
                var oppositeRecycleWorldX = spawnCenter.x + (spawnCenter.x - recycleWorldX);
                if ((!movingRight && rigidbody.position.x <= recycleWorldX)
                    || (movingRight && rigidbody.position.x >= oppositeRecycleWorldX))
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

            targetDebrisCount = configuredDebrisAmount > 0
                ? configuredDebrisAmount
                : Mathf.RoundToInt(Random.Range(
                    Mathf.Min(minimumDebrisCount, maximumDebrisCount),
                    Mathf.Max(minimumDebrisCount, maximumDebrisCount) + 1)
                    * densityMultiplier);
            while (activeDebris.Count < targetDebrisCount)
            {
                activeDebris.Add(CreateDebris(activeDebris.Count));
            }

            speeds = new float[activeDebris.Count];
            angularVelocities = new Vector3[activeDebris.Count];
            flowDirections = new Vector3[activeDebris.Count];
            debrisRigidbodies = new Rigidbody[activeDebris.Count];
            debrisNetworkTransforms = new NetworkTransform[activeDebris.Count];
            for (var index = 0; index < activeDebris.Count; index++)
            {
                if (!TryCacheDebrisPhysics(index, activeDebris[index]))
                {
                    enabled = false;
                    return;
                }

                ResetDebris(index, distributeAcrossPathOnAwake);
            }

            simulationInitialized = true;
            Debug.Log(
                $"PHS_DEBRIS_STREAM_READY stream={name} " +
                $"mode={(offlineLocalSimulationActive ? "offline_local" : usesNetworkDebrisSources ? "network" : "local")} " +
                $"total={activeDebris.Count} generated={runtimeGeneratedDebris.Count}",
                this);
        }

        private void RebuildSimulationForConfiguredAmount()
        {
            foreach (var debris in runtimeGeneratedDebris)
            {
                if (debris == null)
                {
                    continue;
                }

                var networkObject = debris.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsSpawned)
                {
                    if (boundNetworkManager != null
                        && boundNetworkManager.IsServer)
                    {
                        networkObject.Despawn(true);
                    }

                    continue;
                }

                Destroy(debris.gameObject);
            }

            runtimeGeneratedDebris.Clear();
            activeDebris.Clear();
            activeDebris.AddRange(debrisRoots);
            simulationInitialized = false;
            if (configuredDebrisAmount == 0)
            {
                return;
            }

            if (offlineLocalSimulationActive)
            {
                InitializeSimulation();
            }
            else if (usesNetworkDebrisSources)
            {
                TryInitializeNetworkSimulation();
            }
            else if (simulationRequested)
            {
                InitializeSimulation();
            }
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
                if (debrisSource.GetComponent<Rigidbody>() == null)
                {
                    Debug.LogError(
                        $"PHS_DEBRIS_STREAM_SETUP_FAILED reason=source_rigidbody_missing " +
                        $"stream={name} seed_index={index} source={debrisSource.name}",
                        this);
                    return false;
                }

                var sourceNetworkObject = debrisSource.GetComponent<NetworkObject>();
                if (sourceNetworkObject != null
                    && debrisSource.GetComponent<NetworkTransform>() == null)
                {
                    Debug.LogError(
                        $"PHS_DEBRIS_STREAM_SETUP_FAILED reason=source_network_transform_missing " +
                        $"stream={name} seed_index={index} source={debrisSource.name}",
                        this);
                    return false;
                }

                if (sourceNetworkObject != null)
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

        private bool TryCacheDebrisPhysics(int index, Transform debris)
        {
            if (debris == null)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_RUNTIME_FAILED reason=debris_missing " +
                    $"stream={name} debris_index={index}",
                    this);
                return false;
            }

            if (!debris.TryGetComponent(out Rigidbody rigidbody))
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_RUNTIME_FAILED reason=rigidbody_missing " +
                    $"stream={name} debris_index={index} debris={debris.name}",
                    debris);
                return false;
            }

            var networkObject = debris.GetComponent<NetworkObject>();
            var networkTransform = debris.GetComponent<NetworkTransform>();
            if (networkObject != null && networkTransform == null)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_STREAM_RUNTIME_FAILED reason=network_transform_missing " +
                    $"stream={name} debris_index={index} debris={debris.name}",
                    debris);
                return false;
            }

            debrisRigidbodies[index] = rigidbody;
            debrisNetworkTransforms[index] = networkTransform;
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

        private bool TryStartOfflineLocalSimulation(string cause)
        {
            if (!ShouldUseOfflineLocalSimulation(
                    allowOfflineLocalSimulation,
                    simulationRequested,
                    usesNetworkDebrisSources,
                    IsNetworkListening()))
            {
                return false;
            }

            if (!offlineLocalSimulationActive)
            {
                offlineLocalSimulationActive = true;
                InitializeSimulation();
                if (!simulationInitialized)
                {
                    offlineLocalSimulationActive = false;
                    return false;
                }

                Debug.Log(
                    $"PHS_DEBRIS_STREAM_OFFLINE_STARTED stream={name} cause={cause} " +
                    $"total={activeDebris.Count}",
                    this);
            }

            return true;
        }

        private void StopOfflineLocalSimulation(string cause)
        {
            if (!offlineLocalSimulationActive)
            {
                return;
            }

            offlineLocalSimulationActive = false;
            Debug.Log(
                $"PHS_DEBRIS_STREAM_OFFLINE_STOPPED stream={name} cause={cause}",
                this);
        }

        private bool IsNetworkListening()
        {
            return boundNetworkManager != null && boundNetworkManager.IsListening;
        }

        private static bool ShouldUseOfflineLocalSimulation(
            bool allowOffline,
            bool simulationEnabled,
            bool usesNetworkSources,
            bool networkListening)
        {
            return allowOffline
                && simulationEnabled
                && usesNetworkSources
                && !networkListening;
        }

        [ContextMenu("Validate Offline Local Simulation Contract")]
        private void ValidateOfflineLocalSimulationContract()
        {
            var valid = ShouldUseOfflineLocalSimulation(true, true, true, false)
                && !ShouldUseOfflineLocalSimulation(false, true, true, false)
                && !ShouldUseOfflineLocalSimulation(true, false, true, false)
                && !ShouldUseOfflineLocalSimulation(true, true, false, false)
                && !ShouldUseOfflineLocalSimulation(true, true, true, true);
            if (!valid)
            {
                Debug.LogError(
                    "PHS_DEBRIS_STREAM_OFFLINE_CONTRACT_FAILED",
                    this);
                return;
            }

            Debug.Log("PHS_DEBRIS_STREAM_OFFLINE_CONTRACT_OK", this);
        }

        private void ResetDebris(int index, bool distributeAcrossPath)
        {
            var debris = activeDebris[index];
            var rigidbody = debrisRigidbodies[index];
            var networkTransform = debrisNetworkTransforms[index];
            GetSpawnArea(out var laneCenter, out var laneExtents);
            var movingRight = Random.value < oppositeFlowChance;
            var targetRecycleWorldX = movingRight
                ? spawnCenter.x + (spawnCenter.x - recycleWorldX)
                : recycleWorldX;
            var spawnX = distributeAcrossPath
                ? Random.Range(
                    Mathf.Min(targetRecycleWorldX, laneCenter.x + laneExtents.x),
                    Mathf.Max(targetRecycleWorldX, laneCenter.x + laneExtents.x))
                : laneCenter.x + Random.Range(-laneExtents.x, laneExtents.x);

            var spawnPosition = new Vector3(
                spawnX,
                laneCenter.y + Random.Range(-laneExtents.y, laneExtents.y),
                laneCenter.z + Random.Range(-laneExtents.z, laneExtents.z));
            var spawnRotation = Random.rotation;
            speeds[index] = Random.Range(minimumSpeed, maximumSpeed);
            flowDirections[index] = movingRight ? Vector3.right : Vector3.left;
            angularVelocities[index] = Random.insideUnitSphere * maximumAngularSpeed;

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = spawnPosition;
            rigidbody.rotation = spawnRotation;

            var networkObject = debris.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkTransform.Teleport(spawnPosition, spawnRotation, debris.localScale);
            }

            rigidbody.linearVelocity = flowDirections[index] * speeds[index];
            rigidbody.angularVelocity = angularVelocities[index] * Mathf.Deg2Rad;
            rigidbody.WakeUp();
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
