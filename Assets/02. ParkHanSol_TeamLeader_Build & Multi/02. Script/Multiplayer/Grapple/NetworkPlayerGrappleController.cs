using System;
using LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class NetworkPlayerGrappleController : NetworkBehaviour
    {
        private enum GrappleMotionState
        {
            Idle,
            Flying,
            Latched
        }

        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform ropeOrigin;
        [SerializeField] private LineRenderer ropeRenderer;
        [SerializeField] private Transform hookVisual;
        [SerializeField] private Transform aimMarker;
        [Header("Item Collection")]
        [SerializeField] private MonoBehaviour itemHolderBehaviour;
        [SerializeField] private Transform itemCollectionPoint;
        [SerializeField, Min(0.1f)] private float itemCollectionDistance = 1.5f;
        [SerializeField, Min(0.005f)] private float ropeWidth = 0.045f;
        [SerializeField] private LayerMask grappleLayers = ~0;
        [SerializeField, Min(1f)] private float maximumDistance = 24f;
        [SerializeField, Min(1f)] private float hookLaunchSpeed = 50f;
        [SerializeField, Min(0.01f)] private float hookCollisionRadius = 0.16f;
        [SerializeField, Min(0.1f)] private float pullAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float maximumPullSpeed = 10f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.25f;
        [Header("Input")]
        [SerializeField] private Key hookKey = Key.Q;
        [SerializeField, Min(0f)] private float refireCooldown = 0.15f;

        private readonly NetworkVariable<bool> grappleActive = new(false);
        private readonly NetworkVariable<Vector3> grapplePosition = new(Vector3.zero);
        private NetworkPlayerController playerController;
        private IItemHolder itemHolder;
        private bool standaloneActive;
        private Vector3 standalonePosition;
        private GrappleMotionState motionState;
        private Vector3 flightPosition;
        private Vector3 flightDirection;
        private float flightDistance;
        private Transform latchedTransform;
        private Vector3 latchedLocalPoint;
        private IGrappleTarget activeTarget;
        private IGrappleCollectible activeCollectible;
        private bool pullRequested;
        private float lastLaunchTime = float.NegativeInfinity;
        private bool setupErrorLogged;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            itemHolder = itemHolderBehaviour as IItemHolder;
            ValidateSetup();
            if (ropeRenderer != null)
            {
                ropeRenderer.startWidth = ropeWidth;
                ropeRenderer.endWidth = ropeWidth;
                ropeRenderer.useWorldSpace = true;
                ropeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ropeRenderer.receiveShadows = false;
            }

            SetRopeVisible(false);
            SetHookVisible(false);
            SetAimMarkerVisible(false);
        }

        private void Update()
        {
            if (!ValidateSetup())
            {
                return;
            }

            if (playerController.CanAcceptLocalInput)
            {
                HandleLocalInput();
            }

            RefreshAimMarker();

            if ((!IsSpawned || IsServer) && motionState == GrappleMotionState.Flying)
            {
                AdvanceHookFlight(Time.deltaTime);
            }

            RefreshRope();
        }

        public void ApplyServerPull(float deltaTime)
        {
            if (motionState != GrappleMotionState.Latched)
            {
                return;
            }

            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=server_required player={name}");
                return;
            }

            if (latchedTransform == null)
            {
                Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=latch_missing player={name}");
                StopGrapple();
                return;
            }

            var latchPosition = latchedTransform.TransformPoint(latchedLocalPoint);
            UpdateGrapplePosition(latchPosition);
            if (TryFinishCollectibleArrival())
            {
                return;
            }

            if (TryAutoDetachAtStopDistance(latchPosition, "distance_reached"))
            {
                return;
            }

            if (!pullRequested)
            {
                return;
            }

            ApplyMassBalancedPull(latchPosition, deltaTime);
        }

        private void OnDisable()
        {
            lastLaunchTime = float.NegativeInfinity;
            SetRopeVisible(false);
            SetHookVisible(false);
            SetAimMarkerVisible(false);
        }

        private void HandleLocalInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[hookKey].wasPressedThisFrame)
            {
                HandleHookPressed();
            }

            if (Keyboard.current[hookKey].wasReleasedThisFrame)
            {
                RequestStopGrapple();
            }
        }

        private void HandleHookPressed()
        {
            if (IsGrappleActive())
            {
                RequestSetPull(true);
                return;
            }

            RequestStartGrapple(ropeOrigin.position, aimCamera.transform.forward);
        }

        private void RequestStartGrapple(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                Debug.LogError($"PHS_GRAPPLE_START_FAILED reason=direction_invalid player={name}");
                return;
            }

            if (IsSpawned && !IsServer)
            {
                RequestStartGrappleServerRpc(origin, direction);
                return;
            }

            LaunchGrapple(origin, direction);
        }

        private void RequestStopGrapple()
        {
            if (IsSpawned && !IsServer)
            {
                RequestStopGrappleServerRpc();
                return;
            }

            StopGrapple();
        }

        private void RequestSetPull(bool requested)
        {
            if (IsSpawned && !IsServer)
            {
                RequestSetPullServerRpc(requested);
                return;
            }

            SetPullRequested(requested);
        }

        [ServerRpc]
        private void RequestStartGrappleServerRpc(
            Vector3 origin,
            Vector3 direction,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_GRAPPLE_START_FAILED reason=owner_mismatch player={name}");
                return;
            }

            if (Vector3.Distance(origin, transform.position) > 3f)
            {
                Debug.LogError($"PHS_GRAPPLE_START_FAILED reason=origin_out_of_range player={name}");
                return;
            }

            if (direction.sqrMagnitude <= 0.001f)
            {
                Debug.LogError($"PHS_GRAPPLE_START_FAILED reason=direction_invalid player={name}");
                return;
            }

            LaunchGrapple(origin, direction);
        }

        [ServerRpc]
        private void RequestStopGrappleServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_GRAPPLE_STOP_FAILED reason=owner_mismatch player={name}");
                return;
            }

            StopGrapple();
        }

        [ServerRpc]
        private void RequestSetPullServerRpc(
            bool requested,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=owner_mismatch player={name}");
                return;
            }

            SetPullRequested(requested);
        }

        private void LaunchGrapple(Vector3 origin, Vector3 direction)
        {
            if (Time.unscaledTime - lastLaunchTime < refireCooldown)
            {
                return;
            }

            lastLaunchTime = Time.unscaledTime;
            StopGrapple();
            motionState = GrappleMotionState.Flying;
            flightPosition = origin;
            flightDirection = direction.normalized;
            flightDistance = 0f;
            pullRequested = true;
            SetGrappleState(true, flightPosition);
            Debug.Log($"PHS_GRAPPLE_LAUNCHED player={name} origin={origin} direction={flightDirection}");
        }

        private void AdvanceHookFlight(float deltaTime)
        {
            var remainingDistance = maximumDistance - flightDistance;
            if (remainingDistance <= 0f)
            {
                Debug.LogWarning($"PHS_GRAPPLE_FLIGHT_MISSED player={name}");
                StopGrapple();
                return;
            }

            var stepDistance = Mathf.Min(hookLaunchSpeed * deltaTime, remainingDistance);
            if (TryFindFirstGrappleHit(
                flightPosition,
                flightDirection,
                stepDistance,
                out var hitCollider,
                out var hitPoint,
                out _))
            {
                LatchHook(hitCollider, hitPoint);
                return;
            }

            flightPosition += flightDirection * stepDistance;
            flightDistance += stepDistance;
            UpdateGrapplePosition(flightPosition);

            if (flightDistance >= maximumDistance)
            {
                Debug.LogWarning($"PHS_GRAPPLE_FLIGHT_MISSED player={name}");
                StopGrapple();
            }
        }

        private bool TryFindFirstGrappleHit(
            Vector3 origin,
            Vector3 direction,
            float distance,
            out Collider hitCollider,
            out Vector3 hitPoint,
            out Vector3 hitNormal)
        {
            hitCollider = null;
            hitPoint = Vector3.zero;
            hitNormal = Vector3.zero;
            if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
            {
                return false;
            }

            var overlaps = Physics.OverlapSphere(
                origin,
                hookCollisionRadius,
                grappleLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(
                overlaps,
                (left, right) => left.bounds.SqrDistance(origin)
                    .CompareTo(right.bounds.SqrDistance(origin)));
            foreach (var overlap in overlaps)
            {
                if (overlap.transform.root == transform.root || overlap.isTrigger)
                {
                    continue;
                }

                hitCollider = overlap;
                hitPoint = GetSafeClosestPoint(overlap, origin);
                hitNormal = (origin - overlap.bounds.center).normalized;
                if (hitNormal.sqrMagnitude <= 0.001f)
                {
                    hitNormal = -direction.normalized;
                }

                return true;
            }

            var hits = Physics.SphereCastAll(
                origin,
                hookCollisionRadius,
                direction.normalized,
                distance,
                grappleLayers,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.root == transform.root || hit.collider.isTrigger)
                {
                    continue;
                }

                hitCollider = hit.collider;
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }

            return false;
        }

        private void LatchHook(Collider collider, Vector3 point)
        {
            motionState = GrappleMotionState.Latched;
            activeTarget = collider.GetComponentInParent<IGrappleTarget>();
            activeCollectible = collider.GetComponentInParent<IGrappleCollectible>();
            if (activeTarget?.GrapplePoint != null)
            {
                latchedTransform = activeTarget.GrapplePoint;
                latchedLocalPoint = Vector3.zero;
                point = latchedTransform.position;
            }
            else
            {
                latchedTransform = collider.transform;
                latchedLocalPoint = latchedTransform.InverseTransformPoint(point);
            }

            UpdateGrapplePosition(point);
            if (TryFinishCollectibleArrival())
            {
                return;
            }

            if (TryAutoDetachAtStopDistance(point, "latch_too_close"))
            {
                return;
            }

            Debug.Log($"PHS_GRAPPLE_LATCHED player={name} collider={collider.name} movable={activeTarget?.CanMoveByGrapple == true}");
        }

        private bool TryFinishCollectibleArrival()
        {
            if (activeCollectible == null)
            {
                return false;
            }

            if (activeCollectible.CollectionPoint == null)
            {
                Debug.LogError($"PHS_GRAPPLE_COLLECT_FAILED reason=collection_point_missing player={name}");
                StopGrapple();
                return true;
            }

            var distance = Vector3.Distance(
                itemCollectionPoint.position,
                activeCollectible.CollectionPoint.position);
            if (distance > itemCollectionDistance)
            {
                return false;
            }

            var itemName = activeCollectible.CollectionPoint.name;
            if (activeCollectible.TryCollect(itemHolder))
            {
                Debug.Log($"PHS_GRAPPLE_COLLECTED player={name} item={itemName} distance={distance:F2}");
            }
            else
            {
                Debug.LogWarning($"PHS_GRAPPLE_COLLECT_FAILED reason=holder_rejected player={name} item={itemName}");
            }

            StopGrapple();
            return true;
        }

        private bool TryAutoDetachAtStopDistance(Vector3 latchPosition, string reason)
        {
            var distance = Vector3.Distance(transform.position, latchPosition);
            if (distance > stopDistance)
            {
                return false;
            }

            Debug.Log($"PHS_GRAPPLE_AUTO_DETACH player={name} reason={reason} distance={distance:F2} stopDistance={stopDistance:F2}");
            StopGrapple();
            return true;
        }

        private void StopGrapple()
        {
            pullRequested = false;
            motionState = GrappleMotionState.Idle;
            flightPosition = Vector3.zero;
            flightDirection = Vector3.zero;
            flightDistance = 0f;
            latchedTransform = null;
            latchedLocalPoint = Vector3.zero;
            activeTarget = null;
            activeCollectible = null;
            SetGrappleState(false, Vector3.zero);
        }

        private void SetPullRequested(bool requested)
        {
            if (requested && !IsGrappleActive())
            {
                Debug.LogWarning($"PHS_GRAPPLE_PULL_IGNORED reason=not_latched player={name}");
                return;
            }

            pullRequested = requested;
        }

        private void ApplyMassBalancedPull(Vector3 targetPosition, float deltaTime)
        {
            if (activeTarget == null || !activeTarget.CanMoveByGrapple)
            {
                playerController.ApplyGrapplePull(
                    targetPosition,
                    pullAcceleration,
                    maximumPullSpeed,
                    stopDistance,
                    deltaTime);
                return;
            }

            if (activeTarget.GrapplePoint == null)
            {
                Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=target_point_missing player={name}");
                StopGrapple();
                return;
            }

            var playerMass = Mathf.Max(0.1f, playerController.SpaceMass);
            var targetMass = Mathf.Max(0.1f, activeTarget.GrappleMass);
            var totalMass = playerMass + targetMass;
            var playerAcceleration = pullAcceleration * (targetMass / totalMass);
            var targetAcceleration = pullAcceleration * (playerMass / totalMass);

            playerController.ApplyGrapplePull(
                targetPosition,
                playerAcceleration,
                maximumPullSpeed,
                stopDistance,
                deltaTime);
            activeTarget.ApplyGrapplePull(
                transform.position,
                targetAcceleration,
                maximumPullSpeed,
                stopDistance,
                deltaTime);
        }

        private void UpdateGrapplePosition(Vector3 position)
        {
            if (IsSpawned)
            {
                grapplePosition.Value = position;
                return;
            }

            standalonePosition = position;
        }

        private void SetGrappleState(bool active, Vector3 position)
        {
            if (IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_GRAPPLE_STATE_FAILED reason=server_required player={name}");
                    return;
                }

                grappleActive.Value = active;
                grapplePosition.Value = position;
                return;
            }

            standaloneActive = active;
            standalonePosition = position;
        }

        private bool IsGrappleActive()
        {
            return IsSpawned ? grappleActive.Value : standaloneActive;
        }

        private Vector3 GetGrapplePosition()
        {
            return IsSpawned ? grapplePosition.Value : standalonePosition;
        }

        private void RefreshRope()
        {
            var active = IsGrappleActive();
            SetRopeVisible(active);
            SetHookVisible(active);
            if (!active)
            {
                return;
            }

            var grapplePoint = GetGrapplePosition();
            ropeRenderer.SetPosition(0, ropeOrigin.position);
            ropeRenderer.SetPosition(1, grapplePoint);
            hookVisual.position = grapplePoint;
            var hookDirection = grapplePoint - ropeOrigin.position;
            if (hookDirection.sqrMagnitude > 0.001f)
            {
                hookVisual.rotation = Quaternion.LookRotation(hookDirection.normalized, transform.up);
            }
        }

        private void RefreshAimMarker()
        {
            if (!playerController.CanAcceptLocalInput || IsGrappleActive())
            {
                SetAimMarkerVisible(false);
                return;
            }

            if (!TryFindFirstGrappleHit(
                    ropeOrigin.position,
                    aimCamera.transform.forward,
                    maximumDistance,
                    out _,
                    out var point,
                    out var normal)
                || Vector3.Distance(transform.position, point) <= stopDistance)
            {
                SetAimMarkerVisible(false);
                return;
            }

            aimMarker.position = point + normal * 0.025f;
            aimMarker.rotation = Quaternion.LookRotation(normal, transform.up);
            SetAimMarkerVisible(true);
        }

        private void SetRopeVisible(bool visible)
        {
            if (ropeRenderer == null)
            {
                return;
            }

            ropeRenderer.positionCount = 2;
            ropeRenderer.enabled = visible;
        }

        private void SetHookVisible(bool visible)
        {
            if (hookVisual == null)
            {
                return;
            }

            hookVisual.gameObject.SetActive(visible);
        }

        private void SetAimMarkerVisible(bool visible)
        {
            if (aimMarker == null)
            {
                return;
            }

            aimMarker.gameObject.SetActive(visible);
        }

        private bool ValidateSetup()
        {
            if (playerController != null
                && aimCamera != null
                && ropeOrigin != null
                && ropeRenderer != null
                && hookVisual != null
                && aimMarker != null
                && itemHolder != null
                && itemCollectionPoint != null)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_GRAPPLE_SETUP_FAILED player={name} controller={playerController != null} camera={aimCamera != null} ropeOrigin={ropeOrigin != null} ropeRenderer={ropeRenderer != null} hookVisual={hookVisual != null} aimMarker={aimMarker != null} itemHolder={itemHolder != null} itemCollectionPoint={itemCollectionPoint != null}");
            }

            return false;
        }

        private static Vector3 GetSafeClosestPoint(Collider targetCollider, Vector3 point)
        {
            if (targetCollider is MeshCollider meshCollider && !meshCollider.convex)
            {
                return targetCollider.bounds.ClosestPoint(point);
            }

            return targetCollider.ClosestPoint(point);
        }
    }
}
