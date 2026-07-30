using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class NetworkPlayerGrappleController : NetworkBehaviour
    {
        public bool IsGrappleActive => IsGrappleActiveInternal();
        public event Action<Collider> GrappleLatched;

        private enum GrappleMotionState
        {
            Idle,
            Flying,
            Latched
        }

        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform ropeOrigin;
        [SerializeField] private Transform hookVisual;
        [SerializeField] private GrappleClawVisual clawVisual;
        [SerializeField] private Transform ropeAttachPoint;
        [SerializeField] private Transform clawTipPoint;
        [Header("Robotic Arm Visual")]
        [SerializeField] private Transform armVisual;
        [SerializeField] private Transform armSegment;
        [SerializeField] private Transform armEndJoint;
        [SerializeField] private GrappleTelescopicArmVisual telescopicArmVisual;
        [SerializeField, Range(0.02f, 0.12f)]
        private float armThickness = 0.05f;
        [SerializeField] private Transform aimMarker;
        [SerializeField] private Transform aimReticle;
        [SerializeField] private Renderer aimReticleRenderer;
        [SerializeField] private Color aimReticleIdleColor = Color.white;
        [SerializeField] private Color aimReticleValidColor = new(0.18f, 1f, 0.65f, 1f);
        [SerializeField, Min(1f)] private float aimReticleValidScaleMultiplier = 1.35f;
        [Header("Item Collection")]
        [SerializeField] private MonoBehaviour itemHolderBehaviour;
        [SerializeField] private Transform itemCollectionPoint;
        [SerializeField, Min(0.1f)] private float itemCollectionDistance = 1.5f;
        [SerializeField] private LayerMask grappleLayers = ~0;
        [SerializeField, Min(1f)] private float maximumDistance = 24f;
        [SerializeField, Min(1f)] private float hookLaunchSpeed = 50f;
        [SerializeField, Min(0.01f)] private float hookCollisionRadius = 0.16f;
        [SerializeField, Min(0.1f)] private float pullAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float maximumPullSpeed = 10f;
        [SerializeField, Min(0.1f)] private float stopDistance = 1.25f;
        [Header("Input")]
        [SerializeField] private PlayerControlInput playerControlInput;
        [SerializeField, Min(0f)] private float refireCooldown = 0.15f;

        private readonly NetworkVariable<bool> grappleActive = new(false);
        private readonly NetworkVariable<Vector3> grapplePosition = new(Vector3.zero);
        private readonly NetworkVariable<GrappleClawPhase> grappleVisualPhase = new(GrappleClawPhase.Hidden);
        private NetworkPlayerController playerController;
        private NetworkPlayerUpgradeState upgradeState;
        private IItemHolder itemHolder;
        private bool standaloneActive;
        private Vector3 standalonePosition;
        private GrappleClawPhase standaloneVisualPhase = GrappleClawPhase.Hidden;
        private GrappleMotionState motionState;
        private Vector3 flightPosition;
        private Vector3 flightDirection;
        private float flightDistance;
        private Transform latchedTransform;
        private Vector3 latchedLocalPoint;
        private IGrappleTarget activeTarget;
        private IGrappleCollectible activeCollectible;
        private bool activeCollectibleIsDebris;
        private bool pullRequested;
        private float lastLaunchTime = float.NegativeInfinity;
        private bool setupErrorLogged;
        private Vector3 aimReticleInitialScale;
        private MaterialPropertyBlock aimReticlePropertyBlock;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            upgradeState = GetComponent<NetworkPlayerUpgradeState>();
            itemHolder = itemHolderBehaviour as IItemHolder;
            ValidateSetup();
            SetHookVisible(false);
            SetArmVisible(false);
            if (clawVisual != null)
            {
                clawVisual.SetPhase(GrappleClawPhase.Hidden);
            }
            SetAimMarkerVisible(false);
            if (aimReticle != null)
            {
                aimReticleInitialScale = aimReticle.localScale;
            }
            aimReticlePropertyBlock = new MaterialPropertyBlock();
            SetAimReticleState(false, false);
        }

        private void Update()
        {
            if (!ValidateSetup())
            {
                return;
            }

            // Release must be observed even while a tutorial popup blocks gameplay input.
            if (playerControlInput != null
                && playerControlInput.GrappleReleasedThisFrame)
            {
                RequestStopGrapple();
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

            RefreshGrappleVisual();
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

        public bool IsPullingPlayer
        {
            get
            {
                return pullRequested
                    && motionState == GrappleMotionState.Latched
                    && (activeCollectible == null || activeCollectibleIsDebris)
                    && (activeTarget == null || activeTarget.PullMode != GrapplePullMode.PullTarget);
            }
        }

        public void StopGrappleForBlockedPlayerMovement(CollisionFlags collisionFlags)
        {
            if (!pullRequested || motionState != GrappleMotionState.Latched)
            {
                return;
            }

            var blockingFlags = CollisionFlags.Above | CollisionFlags.Sides;
            if ((collisionFlags & blockingFlags) == 0)
            {
                return;
            }

            Debug.Log($"PHS_GRAPPLE_AUTO_DETACH player={name} reason=player_blocked flags={collisionFlags}");
            StopGrapple();
        }

        public bool CancelForTeleport()
        {
            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_GRAPPLE_TELEPORT_CANCEL_FAILED reason=server_required player={name}", this);
                return false;
            }

            var wasActive = IsGrappleActiveInternal();
            StopGrapple();
            if (wasActive)
            {
                Debug.Log($"PHS_GRAPPLE_TELEPORT_CANCELLED player={name}", this);
            }

            return true;
        }

        private void OnDisable()
        {
            lastLaunchTime = float.NegativeInfinity;
            SetHookVisible(false);
            SetArmVisible(false);
            if (clawVisual != null)
            {
                clawVisual.SetPhase(GrappleClawPhase.Hidden);
            }
            SetAimMarkerVisible(false);
            SetAimReticleState(false, false);
        }

        private void HandleLocalInput()
        {
            if (playerControlInput == null)
            {
                return;
            }

            if (playerControlInput.GrapplePressedThisFrame)
            {
                HandleHookPressed();
            }

        }

        private void HandleHookPressed()
        {
            if (IsGrappleActiveInternal())
            {
                RequestStopGrapple();
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
            SetGrappleState(true, flightPosition, GrappleClawPhase.Flying);
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
                if (ShouldIgnoreHookCollider(overlap))
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
                if (ShouldIgnoreHookCollider(hit.collider))
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
            SetGrappleVisualPhase(GrappleClawPhase.Latched);
            GrappleLatched?.Invoke(collider);
            activeTarget = collider.GetComponentInParent<IGrappleTarget>();
            activeCollectible = collider.GetComponentInParent<IGrappleCollectible>();
            activeCollectibleIsDebris = activeCollectible != null && HasDebrisTag(collider.transform);
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

            var pullMode = activeTarget == null
                ? GrapplePullMode.PullOwner
                : activeTarget.PullMode;
            Debug.Log($"PHS_GRAPPLE_LATCHED player={name} collider={collider.name} movable={activeTarget?.CanMoveByGrapple == true} pullMode={pullMode}");
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
            activeCollectibleIsDebris = false;
            SetGrappleState(false, Vector3.zero, GrappleClawPhase.Hidden);
        }

        private void SetPullRequested(bool requested)
        {
            if (requested && !IsGrappleActiveInternal())
            {
                Debug.LogWarning($"PHS_GRAPPLE_PULL_IGNORED reason=not_latched player={name}");
                return;
            }

            pullRequested = requested;
        }

        private void ApplyMassBalancedPull(Vector3 targetPosition, float deltaTime)
        {
            var effectivePullAcceleration = pullAcceleration
                * upgradeState.HookPowerMultiplier;
            if (activeCollectible != null && !activeCollectibleIsDebris)
            {
                if (activeTarget == null || !activeTarget.CanMoveByGrapple)
                {
                    Debug.LogError($"PHS_GRAPPLE_COLLECT_FAILED reason=collectible_not_movable player={name}");
                    StopGrapple();
                    return;
                }

                activeTarget.ApplyGrapplePull(
                    itemCollectionPoint.position,
                    effectivePullAcceleration,
                    maximumPullSpeed,
                    itemCollectionDistance,
                    deltaTime);
                return;
            }

            if (activeTarget?.PullMode == GrapplePullMode.PullTarget)
            {
                if (!activeTarget.CanMoveByGrapple)
                {
                    Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=player_target_not_movable player={name}");
                    StopGrapple();
                    return;
                }

                activeTarget.ApplyGrapplePull(
                    transform.position,
                    effectivePullAcceleration,
                    maximumPullSpeed,
                    stopDistance,
                    deltaTime);
                return;
            }

            if (activeTarget == null || !activeTarget.CanMoveByGrapple)
            {
                playerController.ApplyGrapplePull(
                    targetPosition,
                    effectivePullAcceleration,
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
            var playerAcceleration = effectivePullAcceleration * (targetMass / totalMass);
            var targetAcceleration = effectivePullAcceleration * (playerMass / totalMass);

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

        private void SetGrappleState(
            bool active,
            Vector3 position,
            GrappleClawPhase visualPhase)
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
                grappleVisualPhase.Value = visualPhase;
                return;
            }

            standaloneActive = active;
            standalonePosition = position;
            standaloneVisualPhase = visualPhase;
        }

        private void SetGrappleVisualPhase(GrappleClawPhase visualPhase)
        {
            if (IsSpawned)
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_GRAPPLE_VISUAL_PHASE_FAILED reason=server_required player={name}");
                    return;
                }

                grappleVisualPhase.Value = visualPhase;
                return;
            }

            standaloneVisualPhase = visualPhase;
        }

        private bool IsGrappleActiveInternal()
        {
            return IsSpawned ? grappleActive.Value : standaloneActive;
        }

        private Vector3 GetGrapplePosition()
        {
            return IsSpawned ? grapplePosition.Value : standalonePosition;
        }

        private GrappleClawPhase GetGrappleVisualPhase()
        {
            return IsSpawned ? grappleVisualPhase.Value : standaloneVisualPhase;
        }

        private void RefreshGrappleVisual()
        {
            var active = IsGrappleActiveInternal();
            SetHookVisible(active);
            SetArmVisible(active);
            clawVisual.SetPhase(GetGrappleVisualPhase());
            if (!active)
            {
                return;
            }

            var grapplePoint = GetGrapplePosition();
            hookVisual.position = grapplePoint;
            var hookDirection = grapplePoint - ropeOrigin.position;
            if (hookDirection.sqrMagnitude > 0.001f)
            {
                hookVisual.rotation = Quaternion.LookRotation(hookDirection.normalized, transform.up);
            }

            hookVisual.position += grapplePoint - clawTipPoint.position;
            RefreshRoboticArmVisual(ropeAttachPoint.position);
        }

        private void RefreshAimMarker()
        {
            if (!playerController.CanAcceptLocalInput || IsGrappleActiveInternal())
            {
                SetAimMarkerVisible(false);
                SetAimReticleState(false, false);
                return;
            }

            SetAimReticleState(true, false);
            if (!TryFindFirstGrappleHit(
                    ropeOrigin.position,
                    aimCamera.transform.forward,
                    maximumDistance,
                    out _,
                    out var point,
                    out _)
                || Vector3.Distance(transform.position, point) <= stopDistance)
            {
                SetAimMarkerVisible(false);
                return;
            }

            SetAimMarkerVisible(false);
            SetAimReticleState(true, true);
        }

        private void SetHookVisible(bool visible)
        {
            if (hookVisual == null)
            {
                return;
            }

            hookVisual.gameObject.SetActive(visible);
        }

        private void SetArmVisible(bool visible)
        {
            if (armVisual == null)
            {
                return;
            }

            armVisual.gameObject.SetActive(visible);
        }

        private void RefreshRoboticArmVisual(Vector3 grapplePoint)
        {
            if (armVisual == null
                || armSegment == null
                || armEndJoint == null
                || telescopicArmVisual == null
                || !telescopicArmVisual.IsConfigured)
            {
                return;
            }

            var direction = grapplePoint - ropeOrigin.position;
            var length = direction.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            armVisual.SetPositionAndRotation(
                ropeOrigin.position,
                Quaternion.FromToRotation(Vector3.up, direction));
            telescopicArmVisual.SetLength(length, armThickness);
        }

        private void SetAimMarkerVisible(bool visible)
        {
            if (aimMarker == null)
            {
                return;
            }

            aimMarker.gameObject.SetActive(visible);
        }

        private void SetAimReticleState(bool visible, bool hasValidTarget)
        {
            if (aimReticle == null || aimReticleRenderer == null)
            {
                return;
            }

            aimReticle.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            aimReticle.localScale = aimReticleInitialScale
                * (hasValidTarget ? aimReticleValidScaleMultiplier : 1f);
            var color = hasValidTarget ? aimReticleValidColor : aimReticleIdleColor;
            aimReticleRenderer.GetPropertyBlock(aimReticlePropertyBlock);
            aimReticlePropertyBlock.SetColor("_BaseColor", color);
            aimReticlePropertyBlock.SetColor("_Color", color);
            aimReticleRenderer.SetPropertyBlock(aimReticlePropertyBlock);
        }

        private bool ValidateSetup()
        {
            if (playerController != null
                && playerControlInput != null
                && aimCamera != null
                && ropeOrigin != null
                && hookVisual != null
                && clawVisual != null
                && ropeAttachPoint != null
                && clawTipPoint != null
                && armVisual != null
                && armSegment != null
                && armEndJoint != null
                && telescopicArmVisual != null
                && telescopicArmVisual.IsConfigured
                && aimMarker != null
                && aimReticle != null
                && aimReticleRenderer != null
                && itemHolder != null
                && itemCollectionPoint != null)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_GRAPPLE_SETUP_FAILED player={name} controller={playerController != null} input={playerControlInput != null} camera={aimCamera != null} ropeOrigin={ropeOrigin != null} hookVisual={hookVisual != null} clawVisual={clawVisual != null} ropeAttachPoint={ropeAttachPoint != null} clawTipPoint={clawTipPoint != null} armVisual={armVisual != null} armSegment={armSegment != null} armEndJoint={armEndJoint != null} telescopicArmVisual={telescopicArmVisual != null} telescopicArmConfigured={telescopicArmVisual != null && telescopicArmVisual.IsConfigured} aimMarker={aimMarker != null} aimReticle={aimReticle != null} itemHolder={itemHolder != null} itemCollectionPoint={itemCollectionPoint != null}");
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

        private static bool HasDebrisTag(Transform target)
        {
            while (target != null)
            {
                if (target.CompareTag("Debris"))
                {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private bool ShouldIgnoreHookCollider(Collider candidate)
        {
            if (candidate == null)
            {
                Debug.LogError($"PHS_GRAPPLE_HIT_FILTER_FAILED reason=collider_missing player={name}");
                return true;
            }

            if (candidate.isTrigger || candidate.transform.root == transform.root)
            {
                return true;
            }

            var utilityItem = candidate.GetComponentInParent<UtilityItemObject>();
            return utilityItem != null && utilityItem.IsHeld;
        }
    }
}
