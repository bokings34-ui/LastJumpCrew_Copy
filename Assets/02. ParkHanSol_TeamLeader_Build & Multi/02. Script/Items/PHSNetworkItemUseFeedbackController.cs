using System;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum PHSItemUseFeedbackShape : byte
    {
        Sphere = 1,
        Cast = 2
    }

    public enum PHSItemUseFeedbackKind : byte
    {
        Generic = 0,
        Wrench = 1,
        FireExtinguisher = 2,
        Battery = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkItemUseFeedbackController : NetworkBehaviour
    {
        [SerializeField] private GameObject sphereRangePrefab;
        [SerializeField] private GameObject castRangePrefab;
        [SerializeField] private GameObject targetFeedbackPrefab;
        [SerializeField, Min(0.05f)] private float rangeLifetimeSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float targetLifetimeSeconds = 0.75f;
        [SerializeField, Min(1f)] private float maximumFeedbackDistance = 8f;

        [Header("Item Feedback Colors")]
        [SerializeField] private Color genericRangeColor = new(0.2f, 0.9f, 1f, 0.18f);
        [SerializeField] private Color genericTargetColor = new(1f, 0.2f, 0.65f, 0.95f);
        [SerializeField] private Color wrenchRangeColor = new(1f, 0.58f, 0.08f, 0.22f);
        [SerializeField] private Color wrenchTargetColor = new(1f, 0.78f, 0.12f, 1f);
        [SerializeField] private Color extinguisherRangeColor = new(0.82f, 0.96f, 1f, 0.2f);
        [SerializeField] private Color extinguisherTargetColor = new(0.9f, 1f, 1f, 1f);
        [SerializeField] private Color batteryRangeColor = new(0.1f, 0.8f, 1f, 0.22f);
        [SerializeField] private Color batteryTargetColor = new(0.2f, 0.95f, 1f, 1f);

        private void Awake()
        {
            if (sphereRangePrefab == null || castRangePrefab == null || targetFeedbackPrefab == null)
            {
                Debug.LogError(
                    $"PHS_ITEM_FEEDBACK_SETUP_FAILED player={name} sphere={sphereRangePrefab != null} cast={castRangePrefab != null} target={targetFeedbackPrefab != null}",
                    this);
                enabled = false;
            }
        }

        public void PublishServerFeedback(
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            PublishServerFeedback(
                PHSItemUseFeedbackKind.Generic,
                shape,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        public void PublishServerFeedback(
            PHSItemUseFeedbackKind kind,
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            if (IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_ITEM_FEEDBACK_FAILED reason=server_required player={name}", this);
                    return;
                }

                ShowFeedbackClientRpc(
                    (byte)kind,
                    (byte)shape,
                    origin,
                    direction,
                    radius,
                    distance,
                    targetPositions);
                return;
            }

            ShowFeedbackLocal(
                kind,
                shape,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        public void PublishConfirmedTargetImpactServer(
            UtilityItemActionKind actionKind,
            Vector3 targetPosition)
        {
            PublishServerFeedback(
                ResolveFeedbackKind(actionKind),
                PHSItemUseFeedbackShape.Sphere,
                targetPosition,
                Vector3.up,
                0.28f,
                0f,
                new[] { targetPosition });
        }

        public void ShowOwnerLocalTelegraph(
            PHSItemUseFeedbackKind kind,
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance)
        {
            if ((IsSpawned && !IsOwner)
                || !Enum.IsDefined(typeof(PHSItemUseFeedbackKind), kind)
                || !Enum.IsDefined(typeof(PHSItemUseFeedbackShape), shape)
                || radius <= 0f
                || radius > maximumFeedbackDistance
                || distance < 0f
                || distance > maximumFeedbackDistance
                || Vector3.Distance(transform.position, origin)
                    > maximumFeedbackDistance)
            {
                return;
            }

            ShowFeedbackLocal(
                kind,
                shape,
                origin,
                direction,
                radius,
                distance,
                Array.Empty<Vector3>());
        }

        public void RequestOwnerFeedback(
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            RequestOwnerFeedback(
                PHSItemUseFeedbackKind.Generic,
                shape,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        public void RequestOwnerFeedback(
            PHSItemUseFeedbackKind kind,
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            if (IsSpawned)
            {
                if (!IsOwner)
                {
                    Debug.LogError($"PHS_ITEM_FEEDBACK_FAILED reason=owner_required player={name}", this);
                    return;
                }

                RequestFeedbackServerRpc(
                    (byte)kind,
                    (byte)shape,
                    origin,
                    direction,
                    radius,
                    distance,
                    targetPositions ?? Array.Empty<Vector3>());
                return;
            }

            ShowFeedbackLocal(
                kind,
                shape,
                origin,
                direction,
                radius,
                distance,
                targetPositions ?? Array.Empty<Vector3>());
        }

        [ServerRpc]
        private void RequestFeedbackServerRpc(
            byte kindValue,
            byte shapeValue,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId
                || !Enum.IsDefined(typeof(PHSItemUseFeedbackKind), kindValue)
                || !Enum.IsDefined(typeof(PHSItemUseFeedbackShape), shapeValue)
                || radius <= 0f
                || radius > maximumFeedbackDistance
                || distance < 0f
                || distance > maximumFeedbackDistance
                || Vector3.Distance(transform.position, origin) > maximumFeedbackDistance
                || targetPositions == null
                || targetPositions.Length > 16)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_FEEDBACK_REJECTED player={name} kind={kindValue} shape={shapeValue} radius={radius:F2} distance={distance:F2}",
                    this);
                return;
            }

            for (var index = 0; index < targetPositions.Length; index++)
            {
                if (Vector3.Distance(origin, targetPositions[index]) > maximumFeedbackDistance)
                {
                    Debug.LogWarning(
                        $"PHS_ITEM_FEEDBACK_REJECTED reason=target_distance index={index} player={name}",
                        this);
                    return;
                }
            }

            ShowFeedbackClientRpc(
                kindValue,
                shapeValue,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        [ClientRpc]
        private void ShowFeedbackClientRpc(
            byte kindValue,
            byte shapeValue,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            ShowFeedbackLocal(
                (PHSItemUseFeedbackKind)kindValue,
                (PHSItemUseFeedbackShape)shapeValue,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        private void ShowFeedbackLocal(
            PHSItemUseFeedbackKind kind,
            PHSItemUseFeedbackShape shape,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            if (!enabled)
            {
                return;
            }

            var rangePrefab = shape == PHSItemUseFeedbackShape.Sphere
                ? sphereRangePrefab
                : castRangePrefab;
            var normalizedDirection = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : transform.forward;
            var rangeInstance = Instantiate(rangePrefab);
            if (shape == PHSItemUseFeedbackShape.Sphere)
            {
                rangeInstance.transform.position = origin;
                rangeInstance.transform.localScale = Vector3.one * (radius * 2f);
            }
            else
            {
                rangeInstance.transform.position = origin + normalizedDirection * (distance * 0.5f);
                rangeInstance.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalizedDirection);
                rangeInstance.transform.localScale = new Vector3(radius * 2f, distance * 0.5f, radius * 2f);
            }

            ApplyFeedbackColor(rangeInstance, ResolveRangeColor(kind));

            Destroy(rangeInstance, rangeLifetimeSeconds);

            if (targetPositions == null)
            {
                return;
            }

            for (var index = 0; index < targetPositions.Length; index++)
            {
                var targetInstance = Instantiate(
                    targetFeedbackPrefab,
                    targetPositions[index],
                    Quaternion.identity);
                ApplyFeedbackColor(targetInstance, ResolveTargetColor(kind));
                Destroy(targetInstance, targetLifetimeSeconds);
            }

            Debug.Log(
                $"PHS_ITEM_FEEDBACK_SHOWN kind={kind} shape={shape} radius={radius:F2} distance={distance:F2} acceptedTargets={targetPositions.Length}",
                this);
        }

        private Color ResolveRangeColor(PHSItemUseFeedbackKind kind)
        {
            return kind switch
            {
                PHSItemUseFeedbackKind.Wrench => wrenchRangeColor,
                PHSItemUseFeedbackKind.FireExtinguisher => extinguisherRangeColor,
                PHSItemUseFeedbackKind.Battery => batteryRangeColor,
                _ => genericRangeColor
            };
        }

        private Color ResolveTargetColor(PHSItemUseFeedbackKind kind)
        {
            return kind switch
            {
                PHSItemUseFeedbackKind.Wrench => wrenchTargetColor,
                PHSItemUseFeedbackKind.FireExtinguisher => extinguisherTargetColor,
                PHSItemUseFeedbackKind.Battery => batteryTargetColor,
                _ => genericTargetColor
            };
        }

        private static PHSItemUseFeedbackKind ResolveFeedbackKind(
            UtilityItemActionKind actionKind)
        {
            return actionKind switch
            {
                UtilityItemActionKind.FireSuppression =>
                    PHSItemUseFeedbackKind.FireExtinguisher,
                UtilityItemActionKind.PowerRestore or
                UtilityItemActionKind.BatteryDischarge =>
                    PHSItemUseFeedbackKind.Battery,
                UtilityItemActionKind.DeviceRepair or
                UtilityItemActionKind.HullBreachRepair or
                UtilityItemActionKind.SteamLeakRepair or
                UtilityItemActionKind.OxygenLeakRepair or
                UtilityItemActionKind.OxygenGeneratorRepair or
                UtilityItemActionKind.GravityGeneratorRepair =>
                    PHSItemUseFeedbackKind.Wrench,
                _ => PHSItemUseFeedbackKind.Generic
            };
        }

        private static void ApplyFeedbackColor(GameObject root, Color color)
        {
            if (root == null)
            {
                return;
            }

            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.startColor = color;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                propertyBlock.SetColor("_EmissionColor", color * 2f);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
