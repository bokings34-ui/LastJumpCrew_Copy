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
            if (IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_ITEM_FEEDBACK_FAILED reason=server_required player={name}", this);
                    return;
                }

                ShowFeedbackClientRpc((byte)shape, origin, direction, radius, distance, targetPositions);
                return;
            }

            ShowFeedbackLocal(shape, origin, direction, radius, distance, targetPositions);
        }

        public void RequestOwnerFeedback(
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
                    (byte)shape,
                    origin,
                    direction,
                    radius,
                    distance,
                    targetPositions ?? Array.Empty<Vector3>());
                return;
            }

            ShowFeedbackLocal(
                shape,
                origin,
                direction,
                radius,
                distance,
                targetPositions ?? Array.Empty<Vector3>());
        }

        [ServerRpc]
        private void RequestFeedbackServerRpc(
            byte shapeValue,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId
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
                    $"PHS_ITEM_FEEDBACK_REJECTED player={name} shape={shapeValue} radius={radius:F2} distance={distance:F2}",
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

            ShowFeedbackClientRpc(shapeValue, origin, direction, radius, distance, targetPositions);
        }

        [ClientRpc]
        private void ShowFeedbackClientRpc(
            byte shapeValue,
            Vector3 origin,
            Vector3 direction,
            float radius,
            float distance,
            Vector3[] targetPositions)
        {
            ShowFeedbackLocal(
                (PHSItemUseFeedbackShape)shapeValue,
                origin,
                direction,
                radius,
                distance,
                targetPositions);
        }

        private void ShowFeedbackLocal(
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
                Destroy(targetInstance, targetLifetimeSeconds);
            }
        }
    }
}
