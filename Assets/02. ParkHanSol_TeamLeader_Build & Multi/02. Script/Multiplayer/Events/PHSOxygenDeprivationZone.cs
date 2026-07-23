using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using SM;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PHSOxygenDeprivationZone :
        MonoBehaviour,
        IOxygenLeakZone
    {
        private static readonly Dictionary<ulong, float>
            NextSharedDamageTimeByClient = new();

        [Header("Zone Contract")]
        [SerializeField] private string zoneId = "oxygen_zone";
        [SerializeField] private BoxCollider zoneBounds;
        [SerializeField] private Transform repairPoint;
        [SerializeField] private bool activeOnEnable;

        [Header("Server Suffocation")]
        [SerializeField, Min(0f)] private float graceSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float damageIntervalSeconds = 1.25f;
        [SerializeField, Min(1)] private int initialDamage = 4;
        [SerializeField, Min(0)] private int damageIncreasePerTick = 2;
        [SerializeField, Min(1)] private int maximumDamage = 12;
        [SerializeField] private LayerMask playerLayers = 1;

        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly HashSet<NetworkPlayerLifeState> currentTargets =
            new();
        private readonly Dictionary<NetworkPlayerLifeState, int> exposureTicks =
            new();
        private readonly List<NetworkPlayerLifeState> staleTargets = new();

        private bool runtimeActive;
        private bool networkSetupErrorLogged;
        private float nextDamageTime;

        public string ZoneId => zoneId?.Trim() ?? string.Empty;
        public Vector3 RepairPosition => repairPoint != null
            ? repairPoint.position
            : transform.position;
        public bool IsAvailable => !runtimeActive;

        private void OnEnable()
        {
            if (!activeOnEnable)
            {
                ResetRuntime();
                return;
            }

            if (!TryValidate(out var reason))
            {
                ResetRuntime();
                Debug.LogError(
                    $"PHS_OXYGEN_ZONE_FAILED reason={reason} " +
                    $"zone={ZoneId}",
                    this);
                return;
            }

            ActivateRuntime();
        }

        private void OnDisable()
        {
            ResetRuntime();
        }

        private void Update()
        {
            if (!runtimeActive || Time.time < nextDamageTime)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                if (!networkSetupErrorLogged)
                {
                    networkSetupErrorLogged = true;
                    Debug.LogError(
                        $"PHS_OXYGEN_ZONE_FAILED " +
                        $"reason=network_not_listening zone={ZoneId}",
                        this);
                }

                return;
            }

            if (!networkManager.IsServer)
            {
                return;
            }

            nextDamageTime = Time.time + damageIntervalSeconds;
            ApplySuffocationDamage();
        }

        public bool TryActivate(out string reason)
        {
            if (!TryValidate(out reason))
            {
                return false;
            }

            if (runtimeActive)
            {
                reason = "zone_already_active";
                return false;
            }

            ActivateRuntime();
            Debug.Log(
                $"PHS_OXYGEN_ZONE_ACTIVATED zone={ZoneId} " +
                $"grace={graceSeconds:0.00} interval={damageIntervalSeconds:0.00}",
                this);
            reason = null;
            return true;
        }

        public void Deactivate()
        {
            if (!runtimeActive)
            {
                return;
            }

            ResetRuntime();
            Debug.Log($"PHS_OXYGEN_ZONE_DEACTIVATED zone={ZoneId}", this);
        }

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                reason = "zone_id_missing";
                return false;
            }

            if (zoneBounds == null)
            {
                reason = "zone_bounds_missing";
                return false;
            }

            if (!zoneBounds.isTrigger)
            {
                reason = "zone_bounds_must_be_trigger";
                return false;
            }

            if (repairPoint == null)
            {
                reason = "repair_point_missing";
                return false;
            }

            if (damageIntervalSeconds <= 0f
                || initialDamage <= 0
                || maximumDamage < initialDamage
                || playerLayers.value == 0)
            {
                reason = "damage_contract_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private void ActivateRuntime()
        {
            runtimeActive = true;
            networkSetupErrorLogged = false;
            nextDamageTime = Time.time + graceSeconds;
            currentTargets.Clear();
            exposureTicks.Clear();
            staleTargets.Clear();
        }

        private void ResetRuntime()
        {
            runtimeActive = false;
            networkSetupErrorLogged = false;
            currentTargets.Clear();
            exposureTicks.Clear();
            staleTargets.Clear();
        }

        private void ApplySuffocationDamage()
        {
            var zoneTransform = zoneBounds.transform;
            var lossyScale = zoneTransform.lossyScale;
            var halfExtents = Vector3.Scale(
                zoneBounds.size * 0.5f,
                new Vector3(
                    Mathf.Abs(lossyScale.x),
                    Mathf.Abs(lossyScale.y),
                    Mathf.Abs(lossyScale.z)));
            var center = zoneTransform.TransformPoint(zoneBounds.center);
            var hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                overlapBuffer,
                zoneTransform.rotation,
                playerLayers,
                QueryTriggerInteraction.Collide);
            currentTargets.Clear();

            for (var index = 0; index < hitCount; index++)
            {
                var overlap = overlapBuffer[index];
                overlapBuffer[index] = null;
                if (overlap == null)
                {
                    continue;
                }

                var lifeState = overlap.GetComponentInParent<
                    NetworkPlayerLifeState>();
                if (lifeState == null
                    || !lifeState.IsAlive
                    || !currentTargets.Add(lifeState))
                {
                    continue;
                }

                exposureTicks.TryGetValue(lifeState, out var exposureTick);
                var damage = Mathf.Min(
                    maximumDamage,
                    initialDamage + (exposureTick * damageIncreasePerTick));
                if (!TryReserveSharedDamageWindow(
                        lifeState.OwnerClientId,
                        damageIntervalSeconds))
                {
                    continue;
                }

                lifeState.ApplyDamage(damage, gameObject);
                exposureTicks[lifeState] = exposureTick + 1;
                Debug.Log(
                    $"PHS_OXYGEN_SUFFOCATION_APPLIED zone={ZoneId} " +
                    $"client={lifeState.OwnerClientId} damage={damage} " +
                    $"exposureTick={exposureTick + 1}",
                    this);
            }

            if (hitCount == overlapBuffer.Length)
            {
                Debug.LogError(
                    $"PHS_OXYGEN_ZONE_FAILED " +
                    $"reason=overlap_capacity_exceeded zone={ZoneId} " +
                    $"capacity={overlapBuffer.Length}",
                    this);
            }

            staleTargets.Clear();
            foreach (var target in exposureTicks.Keys)
            {
                if (target == null || !currentTargets.Contains(target))
                {
                    staleTargets.Add(target);
                }
            }

            foreach (var target in staleTargets)
            {
                exposureTicks.Remove(target);
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedDamageWindows()
        {
            NextSharedDamageTimeByClient.Clear();
        }

        private static bool TryReserveSharedDamageWindow(
            ulong clientId,
            float intervalSeconds)
        {
            if (NextSharedDamageTimeByClient.TryGetValue(
                    clientId,
                    out var nextDamageTime)
                && Time.time < nextDamageTime)
            {
                return false;
            }

            NextSharedDamageTimeByClient[clientId] =
                Time.time + intervalSeconds;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (zoneBounds == null)
            {
                return;
            }

            var previousMatrix = Gizmos.matrix;
            var zoneTransform = zoneBounds.transform;
            Gizmos.matrix = Matrix4x4.TRS(
                zoneTransform.position,
                zoneTransform.rotation,
                zoneTransform.lossyScale);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireCube(zoneBounds.center, zoneBounds.size);
            Gizmos.matrix = previousMatrix;
        }
    }
}
