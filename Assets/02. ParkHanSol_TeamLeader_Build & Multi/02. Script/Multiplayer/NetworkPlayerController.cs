using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastJumpCrew.ParkHanSol.Shop;
using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(TempPlayerItemHolder))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float groundAcceleration = 18f;
        [SerializeField] private float jumpVelocity = 4.6f;
        [SerializeField] private float gravity = -18f;
        [SerializeField, Min(0.01f)] private float gravityBlendDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float maximumFallSpeed = 14f;
        [Header("Cinematic Zero Gravity Thruster")]
        [SerializeField, Min(1f)] private float thrusterFuelCapacity = 100f;
        [SerializeField, Min(0.1f)] private float thrusterFuelUsePerSecond = 7.5f;
        [SerializeField, Min(0f)] private float thrusterFuelRecoveryDelay = 1f;
        [SerializeField, Min(0.1f)] private float thrusterFuelRecoveryPerSecond = 18f;
        [SerializeField, Min(0.1f)] private float thrusterAcceleration = 7f;
        [SerializeField, Min(0.1f)] private float thrusterMaxSpeed = 7f;
        [SerializeField, Min(0f)] private float thrusterStabilizationAcceleration = 0.8f;
        [Header("Zero Gravity Weight")]
        [SerializeField, Min(0.1f)] private float spaceMass = 1f;
        [SerializeField, Min(0f)] private float heldDebrisMassInfluence = 0.25f;
        [SerializeField, Range(0f, 1f)] private float zeroGravityCollisionRestitution = 0.55f;
        [Header("Zero Gravity Camera")]
        [SerializeField, Min(0f)] private float zeroGravityCameraRotationDelay = 0.14f;
        [SerializeField, Min(0f)] private float zeroGravityCameraShakeDegrees = 0.45f;
        [SerializeField, Min(0.1f)] private float zeroGravityCameraShakeFrequency = 18f;
        [Header("Comfort Camera")]
        [SerializeField, Min(0.001f)] private float mouseDegreesPerCount = 0.05f;
        [SerializeField, Min(0.01f)] private float cameraRotationSmoothTime = 0.08f;
        [SerializeField, Min(1f)] private float cameraMaxRotationSpeed = 420f;
        [Header("Comfort Zero Gravity")]
        [SerializeField, Range(0.1f, 1f)] private float zeroGravityPrecisionMultiplier = 0.45f;
        [Header("Zero Gravity Thruster Audio")]
        [SerializeField] private NetworkPlayerThrusterAudio thrusterAudio;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [Header("Input")]
        [SerializeField] private PlayerControlInput playerControlInput;
        public const string MouseSensitivityPreferenceKey = NetworkPlayerOptionsStore.MouseSensitivityPreferenceKey;
        public const float DefaultMouseSensitivity = 0.6f;
        public const float MinimumMouseSensitivity = NetworkPlayerOptionsStore.MinimumMouseSensitivity;
        public const float MaximumMouseSensitivity = NetworkPlayerOptionsStore.MaximumMouseSensitivity;

        [SerializeField, Range(MinimumMouseSensitivity, MaximumMouseSensitivity)] private float mouseSensitivity = DefaultMouseSensitivity;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private Renderer[] localOwnerHiddenRenderers;

        private CharacterController characterController;
        private NetworkPlayerGrappleController grappleController;
        private NetworkPlayerUpgradeState upgradeState;
        private NetworkPlayerLifeState playerLifeState;
        private StatusEffectController statusEffectController;
        private NetworkPlayerSectorState playerSectorState;
        private Rigidbody attachedRigidbody;
        private IDebrisHolder debrisHolder;
        private float verticalVelocity;
        private float cameraPitch;
        private float currentCameraPitch;
        private float cameraPitchVelocity;
        private float targetYaw;
        private float yawVelocity;
        private bool isLocalThrusterActive;
        private bool thrusterAudioReferenceErrorLogged;
        private bool gameplayInputEnabled;
        private bool pauseInputBlocked;
        private bool lifeInputBlocked;
        private bool warpInputBlocked;
        private bool resultInputBlocked;
        private bool autoMoveEnabled;
        private float autoMoveSeconds;
        private float autoMoveEndTime;
        private bool autoMoveStarted;
        private float nextPositionLogTime;
        private bool inputDeviceProbeEnabled;
        private float inputDeviceProbeSeconds;
        private bool inputDeviceProbeStarted;
        private bool inputDeviceProbeReported;
        private bool inputDeviceProbeBlockedLogged;
        private bool inputDeviceProbeSampleLogged;
        private float inputDeviceProbeStartTime;
        private Vector3 inputDeviceProbeStartPosition;
        private float inputDeviceProbeStartYaw;
        private float inputDeviceProbeStartPitch;
        private float inputDeviceProbeMaximumMove;
        private float inputDeviceProbeMaximumLook;
        private bool originalRigidbodyUseGravity;
        private bool originalRigidbodyIsKinematic;
        private readonly List<NetworkPlayerGravityArea> gravityAreas = new();
        private NetworkPlayerGravityMode gravityMode = NetworkPlayerGravityMode.ShipGravity;
        private Vector3 zeroGravityVelocity;
        private float shipGravityBlend = 1f;
        private readonly NetworkVariable<float> thrusterFuel = new(
            0f,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private float standaloneThrusterFuel;
        private float lastThrusterUseTime = float.NegativeInfinity;
        private bool thrusterGaugeReferenceErrorLogged;
        private float hudBindingErrorLogTime;
        private GameplaySceneContext gameplaySceneContext;
        private ulong initializedGameplaySceneHandle = ulong.MaxValue;
        private uint gameplaySpawnRequestToken;
        private uint pendingGameplaySpawnAckToken;
        private ulong pendingGameplaySpawnSceneHandle = ulong.MaxValue;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public NetworkPlayerGravityMode GravityMode => gravityMode;
        public float EffectiveThrusterFuelCapacity =>
            thrusterFuelCapacity + upgradeState.ThrusterCapacityBonus;
        public float ThrusterFuelNormalized => Mathf.Clamp01(GetThrusterFuel() / EffectiveThrusterFuelCapacity);
        public float SpaceMass => Mathf.Max(
            0.1f,
            spaceMass + (debrisHolder == null ? 0f : debrisHolder.HeldDebrisMass * heldDebrisMassInfluence));
        public bool CanAcceptLocalInput => CanProcessLocalInput();

        public static float GetSavedMouseSensitivity(float fallback = DefaultMouseSensitivity)
        {
            return NetworkPlayerOptionsStore.Shared.GetMouseSensitivity(fallback);
        }

        public static void SaveMouseSensitivity(float value)
        {
            NetworkPlayerOptionsStore.Shared.SetMouseSensitivity(value);
        }

        public void ApplyGravityState(GravityState gravityState)
        {
            SetGravityMode(ConvertGravityMode(gravityState.Mode));
        }

        public void ApplyExternalVelocity(Vector3 velocity)
        {
            if (velocity.sqrMagnitude <= 0.001f)
            {
                return;
            }

            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_EXTERNAL_VELOCITY_FAILED reason=server_required player={name}");
                return;
            }

            if (gravityMode == NetworkPlayerGravityMode.ShipGravity)
            {
                PlanarVelocity += new Vector3(velocity.x, 0f, velocity.z);
                verticalVelocity += velocity.y;
                return;
            }

            zeroGravityVelocity += velocity;
        }

        public void ApplyGrapplePull(
            Vector3 targetPosition,
            float pullAcceleration,
            float maximumPullSpeed,
            float stopDistance,
            float deltaTime)
        {
            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_GRAPPLE_PULL_FAILED reason=server_required player={name}");
                return;
            }

            var offset = targetPosition - transform.position;
            var distance = offset.magnitude;
            if (distance <= stopDistance)
            {
                return;
            }

            var currentVelocity = gravityMode == NetworkPlayerGravityMode.ShipGravity
                ? new Vector3(PlanarVelocity.x, verticalVelocity, PlanarVelocity.z)
                : zeroGravityVelocity;
            var pullDirection = offset / distance;
            var radialSpeed = Vector3.Dot(currentVelocity, pullDirection);
            var nextRadialSpeed = Mathf.MoveTowards(
                radialSpeed,
                maximumPullSpeed,
                pullAcceleration * deltaTime);
            var tangentialVelocity = currentVelocity - pullDirection * radialSpeed;
            var nextVelocity = tangentialVelocity + pullDirection * nextRadialSpeed;

            if (gravityMode == NetworkPlayerGravityMode.ShipGravity)
            {
                PlanarVelocity = new Vector3(nextVelocity.x, 0f, nextVelocity.z);
                verticalVelocity = nextVelocity.y;
                return;
            }

            zeroGravityVelocity = nextVelocity;
        }

        public void RequestTestTeleport(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!IsSpawned)
            {
                TeleportTo(targetPosition, targetRotation);
                return;
            }

            if (!IsOwner || !IsServer)
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=server_owner_required player={name}");
                return;
            }

            TryTeleportOnServer(targetPosition, targetRotation, "test");
        }

        public void RequestLocalPortalTeleport(string portalName)
        {
            if (string.IsNullOrWhiteSpace(portalName))
            {
                Debug.LogError($"PHS_LOCAL_PORTAL_FAILED reason=portal_name_missing player={name}");
                return;
            }

            if (!IsSpawned)
            {
                Debug.LogError($"PHS_LOCAL_PORTAL_FAILED reason=player_not_spawned player={name}");
                return;
            }

            if (IsServer)
            {
                TeleportThroughLocalPortal(portalName);
                return;
            }

            if (!IsOwner)
            {
                Debug.LogError($"PHS_LOCAL_PORTAL_FAILED reason=owner_required player={name}");
                return;
            }

            RequestLocalPortalTeleportServerRpc(portalName);
        }

        public void RequestGameplaySceneTransition(
            string destinationSceneName,
            ShopSceneTransitionMode shopTransitionMode = ShopSceneTransitionMode.None)
        {
            if (string.IsNullOrWhiteSpace(destinationSceneName))
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=destination_missing player={name}");
                return;
            }

            if (!IsSpawned || !IsOwner)
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=owner_required player={name}");
                return;
            }

            if (IsServer)
            {
                LoadGameplaySceneForAll(OwnerClientId, destinationSceneName, shopTransitionMode);
                return;
            }

            RequestGameplaySceneTransitionServerRpc(destinationSceneName, shopTransitionMode);
        }

        [ServerRpc]
        private void RequestGameplaySceneTransitionServerRpc(
            string destinationSceneName,
            ShopSceneTransitionMode shopTransitionMode,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=owner_mismatch player={name}");
                return;
            }

            LoadGameplaySceneForAll(rpcParams.Receive.SenderClientId, destinationSceneName, shopTransitionMode);
        }

        private static void LoadGameplaySceneForAll(
            ulong activatorClientId,
            string destinationSceneName,
            ShopSceneTransitionMode shopTransitionMode)
        {
            var networkManager = Unity.Netcode.NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=server_unavailable scene={destinationSceneName}");
                return;
            }

            if (!networkManager.ConnectedClients.TryGetValue(activatorClientId, out var client) ||
                client.PlayerObject == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_PORTAL_FAILED reason=player_missing clientId={activatorClientId} scene={destinationSceneName}");
                return;
            }

            NetworkScenePortalInteractable matchingPortal = null;
            foreach (var portal in UnityEngine.Object.FindObjectsByType<NetworkScenePortalInteractable>(
                         UnityEngine.FindObjectsInactive.Exclude,
                         UnityEngine.FindObjectsSortMode.None))
            {
                if (portal.MatchesServerRequest(
                        client.PlayerObject.transform,
                        destinationSceneName,
                        shopTransitionMode))
                {
                    matchingPortal = portal;
                    break;
                }
            }

            if (matchingPortal == null)
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_PORTAL_FAILED reason=portal_missing_or_out_of_range clientId={activatorClientId} scene={destinationSceneName} mode={shopTransitionMode}");
                return;
            }

            if (!matchingPortal.RequiresPartyVote)
            {
                var directCoordinator = NetworkShopTransitionVoteCoordinator.Instance;
                var directReason = "vote_coordinator_missing";
                if (directCoordinator == null
                    || !directCoordinator.TryExecuteImmediate(
                        destinationSceneName,
                        shopTransitionMode,
                        out directReason))
                {
                    Debug.LogError(
                        $"PHS_NETWORK_PORTAL_FAILED reason={directReason} portal={matchingPortal.name} scene={destinationSceneName}");
                }

                return;
            }

            var voteCoordinator = NetworkShopTransitionVoteCoordinator.Instance;
            if (voteCoordinator == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_PORTAL_FAILED reason=vote_coordinator_missing scene={destinationSceneName}");
                return;
            }

            var isShopExit = SceneManager.GetActiveScene().name == "PHS_ExteriorShopScene";
            if (!voteCoordinator.TryStartVote(
                    activatorClientId,
                    destinationSceneName,
                    shopTransitionMode,
                    isShopExit,
                    out var reason))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_PORTAL_FAILED reason={reason} scene={destinationSceneName} mode={shopTransitionMode}");
            }
        }

        public void RequestWarpActivation()
        {
            if (!IsSpawned || !IsOwner)
            {
                Debug.LogError($"PHS_WARP_TERMINAL_FAILED reason=owner_required player={name}", this);
                return;
            }

            if (IsServer)
            {
                ActivateWarpOnServer(OwnerClientId);
                return;
            }

            RequestWarpActivationServerRpc();
        }

        public bool TryTeleportForWarp(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!TryRequireSectorState("warp", out var sectorState)
                || !TryRequireLifeState("warp", out var lifeState))
            {
                return false;
            }

            if (!TryTeleportOnServer(targetPosition, targetRotation, "warp"))
            {
                return false;
            }

            sectorState.ResetToInteriorServer("warp_teleport");
            lifeState.CancelDeadZoneWarning();
            Debug.Log($"PHS_WARP_TELEPORT_OK player={name} pos={targetPosition}", this);
            return true;
        }

        public bool TryTeleportForRespawn(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (!TryRequireSectorState("respawn", out var sectorState)
                || !TryRequireLifeState("respawn", out var lifeState)
                || !TryTeleportOnServer(targetPosition, targetRotation, "respawn"))
            {
                return false;
            }

            sectorState.ResetToInteriorServer("respawn");
            lifeState.CancelDeadZoneWarning();
            return true;
        }

        [ServerRpc]
        private void RequestWarpActivationServerRpc(ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_WARP_TERMINAL_FAILED reason=owner_mismatch player={name}", this);
                return;
            }

            ActivateWarpOnServer(rpcParams.Receive.SenderClientId);
        }

        private static void ActivateWarpOnServer(ulong activatorClientId)
        {
            var runFlow = NetworkRunFlowCoordinator.Instance;
            if (runFlow == null)
            {
                Debug.LogError($"PHS_WARP_TERMINAL_FAILED reason=run_flow_missing clientId={activatorClientId}");
                return;
            }

            if (!runFlow.TryActivateWarp(activatorClientId, out var reason))
            {
                Debug.LogWarning($"PHS_WARP_TERMINAL_FAILED reason={reason} clientId={activatorClientId}");
            }
        }

        [ServerRpc]
        private void RequestLocalPortalTeleportServerRpc(
            string portalName,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_LOCAL_PORTAL_FAILED reason=owner_mismatch player={name}");
                return;
            }

            TeleportThroughLocalPortal(portalName);
        }

        private void TeleportThroughLocalPortal(string portalName)
        {
            if (!IsServer)
            {
                return;
            }

            var matchingPortals = FindObjectsByType<ExteriorTestTeleportInteractable>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(portal =>
                    portal != null
                    && portal.gameObject.scene == gameObject.scene
                    && string.Equals(portal.name, portalName, StringComparison.Ordinal))
                .ToArray();
            if (matchingPortals.Length != 1)
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_FAILED reason=portal_count_invalid portal={portalName} count={matchingPortals.Length}");
                return;
            }

            if (!matchingPortals[0].TryResolveServerDestination(
                    this,
                    out var destinationPosition,
                    out var destinationRotation,
                    out var reason))
            {
                Debug.LogWarning(
                    $"PHS_LOCAL_PORTAL_FAILED reason={reason ?? "invalid"} portal={portalName} clientId={OwnerClientId}");
                return;
            }

            if (!TryRequireSectorState("local_portal", out var sectorState))
            {
                return;
            }

            var destinationSector = matchingPortals[0].DestinationSector;
            if (destinationSector == NetworkPlayerSector.Interior && playerLifeState == null)
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_FAILED reason=life_state_missing portal={portalName} clientId={OwnerClientId}",
                    this);
                return;
            }

            if (!sectorState.TryBeginTransitionServer(destinationSector, out var transitionReason))
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_FAILED reason={transitionReason} portal={portalName} clientId={OwnerClientId}",
                    this);
                return;
            }

            if (!TryTeleportOnServer(destinationPosition, destinationRotation, "local_portal"))
            {
                sectorState.CancelTransitionServer("teleport_failed");
                return;
            }

            if (!sectorState.TryCompleteTransitionServer(out transitionReason))
            {
                Debug.LogError(
                    $"PHS_LOCAL_PORTAL_FAILED reason={transitionReason} portal={portalName} clientId={OwnerClientId}",
                    this);
                sectorState.CancelTransitionServer("completion_failed");
                return;
            }

            if (destinationSector == NetworkPlayerSector.Interior)
            {
                playerLifeState.CancelDeadZoneWarning();
            }

            Debug.Log(
                $"PHS_LOCAL_PORTAL_OK portal={portalName} clientId={OwnerClientId} sector={destinationSector}");
        }

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            grappleController = GetComponent<NetworkPlayerGrappleController>();
            upgradeState = GetComponent<NetworkPlayerUpgradeState>();
            playerLifeState = GetComponent<NetworkPlayerLifeState>();
            statusEffectController = GetComponent<StatusEffectController>();
            playerSectorState = GetComponent<NetworkPlayerSectorState>();
            ConfigureNetworkRigidbody(true);
            if (playerSectorState == null)
            {
                Debug.LogError($"PHS_PLAYER_SECTOR_SETUP_FAILED reason=sector_state_missing player={name}", this);
            }
            if (IsServer)
            {
                thrusterFuel.Value = EffectiveThrusterFuelCapacity;
            }

            SetLocalOwnerVisualsVisible(!IsOwner);
            RefreshForActiveScene();
        }

        public override void OnNetworkDespawn()
        {
            initializedGameplaySceneHandle = ulong.MaxValue;
            pendingGameplaySpawnAckToken = 0U;
            pendingGameplaySpawnSceneHandle = ulong.MaxValue;
            ConfigureNetworkRigidbody(false);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            grappleController = GetComponent<NetworkPlayerGrappleController>();
            upgradeState = GetComponent<NetworkPlayerUpgradeState>();
            playerLifeState = GetComponent<NetworkPlayerLifeState>();
            statusEffectController = GetComponent<StatusEffectController>();
            playerSectorState = GetComponent<NetworkPlayerSectorState>();
            attachedRigidbody = GetComponent<Rigidbody>();
            debrisHolder = GetComponent<IDebrisHolder>();
            if (playerControlInput == null)
            {
                Debug.LogError($"PHS_PLAYER_INPUT_SETUP_FAILED reason=control_input_reference_missing player={name}", this);
            }

            if (debrisHolder == null)
            {
                Debug.LogError($"PHS_PLAYER_MASS_SETUP_FAILED reason=debris_holder_missing player={name}");
            }
            if (attachedRigidbody != null)
            {
                originalRigidbodyUseGravity = attachedRigidbody.useGravity;
                originalRigidbodyIsKinematic = attachedRigidbody.isKinematic;
            }

            ConfigureNetworkRigidbody(true);
            standaloneThrusterFuel = EffectiveThrusterFuelCapacity;
            targetYaw = transform.eulerAngles.y;
            currentCameraPitch = cameraPitch;
            CacheLocalOwnerHiddenRenderers();
            ConfigureCommandLineAutomation();
        }

        private void Start()
        {
            if (!IsSpawned)
            {
                RefreshForActiveScene();
            }
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (statusEffectController != null)
            {
                statusEffectController.StatusEffectStarted += HandleStatusEffectStarted;
            }
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (statusEffectController != null)
            {
                statusEffectController.StatusEffectStarted -= HandleStatusEffectStarted;
            }
            SetLocalOwnerVisualsVisible(true);
            StopThrusterFeedback();
        }

        private void HandleStatusEffectStarted(StatusEffectType effectType)
        {
            if (effectType != StatusEffectType.ElectricShok)
            {
                return;
            }

            // The remote owner stops submitting movement as soon as the replicated
            // status arrives. Clear the server's last accepted velocity here so that
            // stale input cannot continue moving the shocked player.
            if (IsSpawned && !IsServer)
            {
                return;
            }

            StopMovementForShock();
            Debug.Log(
                $"PHS_SHOCK_RESTRAINT_APPLIED player={name} " +
                $"ownerClientId={OwnerClientId} server={IsServer}",
                this);
        }

        private void Update()
        {
            var canProcessLocalInput = CanProcessLocalInput();
            if (inputDeviceProbeEnabled && IsOwner && !canProcessLocalInput)
            {
                var blockedMove = ReadMove();
                var blockedLook = ReadLook();
                if (!inputDeviceProbeBlockedLogged
                    && (blockedMove.sqrMagnitude > 0.01f || blockedLook.sqrMagnitude > 0.01f))
                {
                    inputDeviceProbeBlockedLogged = true;
                    Debug.Log(
                        $"PHS_INPUT_DEVICE_BLOCKED ownerClientId={OwnerClientId} " +
                        $"rawMove={blockedMove.magnitude:F2} rawLook={blockedLook.magnitude:F2} " +
                        $"gameplay={gameplayInputEnabled} pause={pauseInputBlocked} life={lifeInputBlocked} " +
                        $"warp={warpInputBlocked} result={resultInputBlocked} " +
                        $"restart={(NetworkRunRestartCoordinator.Instance != null && NetworkRunRestartCoordinator.Instance.BlocksRun)}",
                        this);
                }
            }

            if (!canProcessLocalInput)
            {
                return;
            }

            RefreshThrusterGauge();
            var move = ReadMove();
            var look = ReadLook();
            CaptureLocalInputDeviceProbe(move, look);
            if (autoMoveEnabled && Time.time < autoMoveEndTime)
            {
                move.y = 1f;
            }

            var verticalMove = ReadVerticalMove();
            if (playerControlInput == null)
            {
                return;
            }

            var jump = playerControlInput.JumpPressedThisFrame;
            var shiftPressed = playerControlInput.SprintPressed;
            var ascend = gravityMode != NetworkPlayerGravityMode.ShipGravity
                && shiftPressed;
            var thrusterFeedback = gravityMode != NetworkPlayerGravityMode.ShipGravity
                && (ascend || move.sqrMagnitude > 0.01f || Mathf.Abs(verticalMove) > 0.01f);
            var sprint = gravityMode == NetworkPlayerGravityMode.ShipGravity
                && shiftPressed;
            var deltaTime = Time.deltaTime;
            HasMoveInput = gravityMode == NetworkPlayerGravityMode.ShipGravity
                ? move.sqrMagnitude > 0.01f || Mathf.Abs(verticalMove) > 0.01f
                : thrusterFeedback;
            IsRunning = HasMoveInput && sprint;

            UpdateLocalThrusterFeedback(thrusterFeedback);
            ApplyLocalLook(look);

            if (!IsSpawned || IsServer)
            {
                MoveOnServer(
                    move,
                    verticalMove,
                    look.x * GetMouseLookDegrees(),
                    cameraPitch,
                    jump,
                    ascend,
                    sprint,
                    deltaTime);
            }
            else
            {
                if (gravityMode == NetworkPlayerGravityMode.ShipGravity)
                {
                    RotatePlayer(look.x * GetMouseLookDegrees(), deltaTime);
                }

                SubmitInputServerRpc(
                    move,
                    verticalMove,
                    look.x * GetMouseLookDegrees(),
                    cameraPitch,
                    jump,
                    ascend,
                    sprint,
                    deltaTime);
            }
        }

        public void EnterGravityArea(NetworkPlayerGravityArea gravityArea)
        {
            if (gravityArea == null)
            {
                Debug.LogError($"PHS_PLAYER_GRAVITY_AREA_FAILED reason=area_missing player={name}");
                return;
            }

            if (!gravityAreas.Contains(gravityArea))
            {
                gravityAreas.Add(gravityArea);
            }

            ApplyGravityAreaMode();
        }

        public void ExitGravityArea(NetworkPlayerGravityArea gravityArea)
        {
            if (gravityArea == null)
            {
                Debug.LogError($"PHS_PLAYER_GRAVITY_AREA_FAILED reason=area_missing player={name}");
                return;
            }

            gravityAreas.Remove(gravityArea);
            ApplyGravityAreaMode();
        }

        public void RefreshGravityArea(NetworkPlayerGravityArea gravityArea)
        {
            if (gravityArea == null)
            {
                Debug.LogError($"PHS_PLAYER_GRAVITY_REFRESH_FAILED reason=area_missing player={name}");
                return;
            }

            if (!gravityAreas.Contains(gravityArea))
            {
                return;
            }

            ApplyGravityAreaMode();
        }

        public void SetGameplayInputEnabled(bool active)
        {
            gameplayInputEnabled = IsOwner && active;
            SetLocalView(gameplayInputEnabled);

            if (!IsOwner)
            {
                return;
            }

            SetCursorLock(gameplayInputEnabled);
        }

        public void SetPauseInputBlocked(bool blocked)
        {
            if (IsSpawned && !IsOwner)
            {
                return;
            }

            pauseInputBlocked = blocked;
            if (blocked)
            {
                HasMoveInput = false;
                IsRunning = false;
                UpdateLocalThrusterFeedback(false);
            }
        }

        public void BindPlayHudPresenter(ParkHanSolPlayHudMockPresenter presenter)
        {
            playHudPresenter = presenter;
            thrusterGaugeReferenceErrorLogged = false;
            RefreshThrusterGauge();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            RefreshForActiveScene();
        }

        private void RefreshForActiveScene()
        {
            if (!IsSpawned)
            {
                SetLocalView(true);
                SetCursorLock(true);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            gameplaySceneContext = GameplaySceneContext.FindForScene(activeScene);
            var isGameplayScene = gameplaySceneContext != null && gameplaySceneContext.IsGameplayScene;
            SetGameplayInputEnabled(isGameplayScene);
            if (isGameplayScene)
            {
                ReleaseWarpInputForShop();
                hudBindingErrorLogTime = Time.time + 0.5f;
                if (IsServer && IsOwner)
                {
                    MoveToGameplaySpawnPointIfServer(activeScene);
                }
                else if (IsOwner)
                {
                    gameplaySpawnRequestToken++;
                    if (gameplaySpawnRequestToken == 0U)
                    {
                        gameplaySpawnRequestToken = 1U;
                    }

                    RequestGameplaySpawnPointServerRpc(
                        activeScene.name,
                        gameplaySpawnRequestToken);
                }
            }

            if (gameplayInputEnabled && autoMoveEnabled && !autoMoveStarted)
            {
                autoMoveStarted = true;
                autoMoveEndTime = Time.time + autoMoveSeconds;
            }

            Debug.Log($"PHS_PLAYER_SCENE_STATE scene={SceneManager.GetActiveScene().name} owner={IsOwner} input={gameplayInputEnabled}");
        }

        private void ReleaseWarpInputForShop()
        {
            if (SceneManager.GetActiveScene().name != "PHS_ExteriorShopScene")
            {
                return;
            }

            SetWarpInputBlocked(false);
        }

        private void LateUpdate()
        {
            UpdateRemoteInputDeviceProbe();

            if (!autoMoveEnabled || Time.time < nextPositionLogTime)
            {
                return;
            }

            nextPositionLogTime = Time.time + 1f;
            Debug.Log($"PHS_PLAYER_POS scene={SceneManager.GetActiveScene().name} owner={IsOwner} ownerClientId={OwnerClientId} pos={transform.position}");
        }

        private void ConfigureCommandLineAutomation()
        {
            inputDeviceProbeSeconds = GetCommandLineFloat("-phsInputDeviceProbeSeconds");
            inputDeviceProbeEnabled = inputDeviceProbeSeconds > 0f;
            autoMoveSeconds = GetCommandLineFloat("-phsAutoMoveSeconds");
            if (autoMoveSeconds <= 0f)
            {
                return;
            }

            autoMoveEnabled = true;
            nextPositionLogTime = Time.time + 1f;
        }

        private void CaptureLocalInputDeviceProbe(Vector2 move, Vector2 look)
        {
            if (!inputDeviceProbeEnabled || !IsOwner || !gameplayInputEnabled || inputDeviceProbeReported
                || SceneManager.GetActiveScene().name != "PHS_Map_ver1")
            {
                return;
            }

            inputDeviceProbeMaximumMove = Mathf.Max(inputDeviceProbeMaximumMove, move.magnitude);
            inputDeviceProbeMaximumLook = Mathf.Max(inputDeviceProbeMaximumLook, look.magnitude);
            if (!inputDeviceProbeSampleLogged
                && (move.sqrMagnitude > 0.01f || look.sqrMagnitude > 0.01f))
            {
                inputDeviceProbeSampleLogged = true;
                Debug.Log(
                    $"PHS_INPUT_DEVICE_SAMPLE ownerClientId={OwnerClientId} " +
                    $"rawMove={move.magnitude:F2} rawLook={look.magnitude:F2}",
                    this);
            }

            if (!inputDeviceProbeStarted)
            {
                if (inputDeviceProbeMaximumMove < 0.5f || inputDeviceProbeMaximumLook < 0.1f)
                {
                    return;
                }

                inputDeviceProbeStarted = true;
                inputDeviceProbeStartTime = Time.unscaledTime;
                inputDeviceProbeStartPosition = transform.position;
                inputDeviceProbeStartYaw = transform.eulerAngles.y;
                inputDeviceProbeStartPitch = cameraPitch;
                if (!IsServer)
                {
                    BeginInputDeviceRemoteProbeServerRpc();
                }
            }
            if (Time.unscaledTime < inputDeviceProbeStartTime + inputDeviceProbeSeconds)
            {
                return;
            }

            inputDeviceProbeReported = true;
            var positionDelta = Vector3.Distance(inputDeviceProbeStartPosition, transform.position);
            var yawDelta = Mathf.Abs(Mathf.DeltaAngle(inputDeviceProbeStartYaw, transform.eulerAngles.y));
            var pitchDelta = Mathf.Abs(cameraPitch - inputDeviceProbeStartPitch);
            var passed = inputDeviceProbeMaximumMove >= 0.5f
                && inputDeviceProbeMaximumLook >= 0.1f
                && positionDelta >= 0.5f
                && (yawDelta >= 1f || pitchDelta >= 1f);
            Debug.Log(
                $"PHS_INPUT_DEVICE_PROBE ownerClientId={OwnerClientId} pass={passed} " +
                $"rawMove={inputDeviceProbeMaximumMove:F2} rawLook={inputDeviceProbeMaximumLook:F2} " +
                $"positionDelta={positionDelta:F2} yawDelta={yawDelta:F2} pitchDelta={pitchDelta:F2}",
                this);
        }

        private void UpdateRemoteInputDeviceProbe()
        {
            if (!inputDeviceProbeEnabled || !IsServer || IsOwner || !IsSpawned || inputDeviceProbeReported
                || SceneManager.GetActiveScene().name != "PHS_Map_ver1")
            {
                return;
            }

            if (!inputDeviceProbeStarted || Time.unscaledTime < inputDeviceProbeStartTime + inputDeviceProbeSeconds)
            {
                return;
            }

            inputDeviceProbeReported = true;
            var positionDelta = Vector3.Distance(inputDeviceProbeStartPosition, transform.position);
            var yawDelta = Mathf.Abs(Mathf.DeltaAngle(inputDeviceProbeStartYaw, transform.eulerAngles.y));
            var passed = positionDelta >= 0.5f && yawDelta >= 1f;
            Debug.Log(
                $"PHS_INPUT_REMOTE_SYNC ownerClientId={OwnerClientId} pass={passed} " +
                $"positionDelta={positionDelta:F2} yawDelta={yawDelta:F2}",
                this);
        }

        [ServerRpc]
        private void BeginInputDeviceRemoteProbeServerRpc()
        {
            if (!inputDeviceProbeEnabled || !IsServer || IsOwner || inputDeviceProbeStarted)
            {
                return;
            }

            inputDeviceProbeStarted = true;
            inputDeviceProbeStartTime = Time.unscaledTime;
            inputDeviceProbeStartPosition = transform.position;
            inputDeviceProbeStartYaw = transform.eulerAngles.y;
        }

        private static float GetCommandLineFloat(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase)
                    && float.TryParse(args[i + 1], out var value))
                {
                    return value;
                }
            }

            return 0f;
        }

        [ServerRpc]
        private void SubmitInputServerRpc(Vector2 move, float verticalMove, float yawDegrees, float lookPitch, bool jump, bool ascend, bool sprint, float deltaTime)
        {
            MoveOnServer(move, verticalMove, yawDegrees, lookPitch, jump, ascend, sprint, Mathf.Clamp(deltaTime, 0f, 0.05f));
        }

        public void SetLifeInputBlocked(bool blocked)
        {
            if (IsSpawned && !IsOwner)
            {
                return;
            }

            lifeInputBlocked = blocked;
            if (blocked)
            {
                HasMoveInput = false;
                IsRunning = false;
                UpdateLocalThrusterFeedback(false);
            }
        }

        public void SetWarpInputBlocked(bool blocked)
        {
            if (IsSpawned && !IsOwner)
            {
                return;
            }

            warpInputBlocked = blocked;
            if (blocked)
            {
                HasMoveInput = false;
                IsRunning = false;
                UpdateLocalThrusterFeedback(false);
            }
        }

        public void ResetMovementForRespawn()
        {
            HasMoveInput = false;
            IsRunning = false;
            PlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            zeroGravityVelocity = Vector3.zero;
            UpdateLocalThrusterFeedback(false);
        }

        public void ShowRespawnCountdown(float remainingSeconds)
        {
            if (IsOwner && playHudPresenter != null)
            {
                playHudPresenter.SetRespawnCountdown(remainingSeconds);
            }
        }

        public void ShowWarpRespawnPending()
        {
            if (IsOwner && playHudPresenter != null)
            {
                playHudPresenter.SetWarpRespawnPending();
            }
        }

        public void ClearRespawnStatus()
        {
            if (IsOwner && playHudPresenter != null)
            {
                playHudPresenter.ClearRespawnStatus();
            }
        }

        public void ShowDeadZoneWarning(float remainingSeconds)
        {
            if (!IsOwner || playHudPresenter == null)
            {
                return;
            }

            playHudPresenter.SetHazardWarning(
                $"데드존 - {Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds))}초 후 사망");
        }

        public void ClearDeadZoneWarning()
        {
            if (!IsOwner || playHudPresenter == null)
            {
                return;
            }

            playHudPresenter.ClearHazardWarning();
        }

        public void ShowLifeStateMessage(string message)
        {
            if (IsOwner && playHudPresenter != null)
            {
                playHudPresenter.SetHazardWarning(message);
            }
        }

        public void ClearLifeStateMessage()
        {
            if (IsOwner && playHudPresenter != null)
            {
                playHudPresenter.ClearHazardWarning();
            }
        }

        private void MoveOnServer(Vector2 move, float verticalMove, float yawDegrees, float lookPitch, bool jump, bool ascend, bool sprint, float deltaTime)
        {
            if ((playerLifeState != null && !playerLifeState.IsAlive)
                || (statusEffectController != null && statusEffectController.IsShocked))
            {
                StopMovementForShock();
                return;
            }

            RotatePlayer(yawDegrees, deltaTime);
            RecoverThrusterFuel(deltaTime);

            if (gravityMode != NetworkPlayerGravityMode.ShipGravity)
            {
                MoveZeroGravity(move, verticalMove, lookPitch, ascend, false, deltaTime);
                return;
            }

            zeroGravityVelocity = Vector3.zero;
            var wishDirection = transform.right * move.x + transform.forward * move.y;
            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            IsGrounded = characterController.isGrounded;
            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (IsGrounded && jump)
            {
                verticalVelocity = jumpVelocity;
            }

            shipGravityBlend = Mathf.MoveTowards(
                shipGravityBlend,
                1f,
                deltaTime / gravityBlendDuration);
            verticalVelocity = Mathf.Max(
                verticalVelocity + gravity * shipGravityBlend * deltaTime,
                -maximumFallSpeed);
            var hasPlanarMoveInput = wishDirection.sqrMagnitude > 0.001f;
            if (hasPlanarMoveInput || grappleController == null || !grappleController.IsPullingPlayer)
            {
                var targetSpeed = sprint ? runSpeed : moveSpeed;
                var targetPlanar = wishDirection * targetSpeed;
                PlanarVelocity = Vector3.MoveTowards(
                    PlanarVelocity,
                    targetPlanar,
                    groundAcceleration * deltaTime);
            }
            grappleController?.ApplyServerPull(deltaTime);
            var velocity = PlanarVelocity;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * deltaTime);
        }

        private void StopMovementForShock()
        {
            HasMoveInput = false;
            IsRunning = false;
            PlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            zeroGravityVelocity = Vector3.zero;
            UpdateLocalThrusterFeedback(false);
        }

        private void MoveZeroGravity(Vector2 move, float verticalMove, float lookPitch, bool ascend, bool precision, float deltaTime)
        {
            verticalVelocity = 0f;
            IsGrounded = false;

            var moveBasis = GetZeroGravityMoveBasis(lookPitch);
            var verticalInput = Mathf.Clamp((ascend ? 1f : 0f) + verticalMove, -1f, 1f);
            var requestedDirection = moveBasis.Forward * move.y
                + moveBasis.Right * move.x
                + moveBasis.Up * verticalInput;
            var hasThrusterInput = requestedDirection.sqrMagnitude > 0.001f;
            if (hasThrusterInput)
            {
                ApplyThruster(
                    true,
                    requestedDirection.normalized,
                    precision ? zeroGravityPrecisionMultiplier : 1f,
                    deltaTime);
            }

            if (!hasThrusterInput && thrusterStabilizationAcceleration > 0f)
            {
                zeroGravityVelocity = Vector3.MoveTowards(
                    zeroGravityVelocity,
                    Vector3.zero,
                    GetEffectiveStabilizationAcceleration() * deltaTime);
            }

            grappleController?.ApplyServerPull(deltaTime);
            PlanarVelocity = new Vector3(zeroGravityVelocity.x, 0f, zeroGravityVelocity.z);

            var collisionFlags = characterController.Move(zeroGravityVelocity * deltaTime);
            grappleController?.StopGrappleForBlockedPlayerMovement(collisionFlags);
        }

        private (Vector3 Forward, Vector3 Right, Vector3 Up) GetZeroGravityMoveBasis(float lookPitch)
        {
            var clampedPitch = Mathf.Clamp(lookPitch, -80f, 80f);
            var lookRotation = Quaternion.Euler(clampedPitch, transform.eulerAngles.y, 0f);
            return (lookRotation * Vector3.forward, lookRotation * Vector3.right, lookRotation * Vector3.up);
        }

        private void ApplyThruster(bool requested, Vector3 direction, float accelerationMultiplier, float deltaTime)
        {
            var fuel = GetThrusterFuel();
            if (requested && fuel > 0f)
            {
                var fuelCost = Mathf.Min(fuel, thrusterFuelUsePerSecond * deltaTime);
                SetThrusterFuel(fuel - fuelCost);
                lastThrusterUseTime = Time.time;

                zeroGravityVelocity += direction.normalized
                    * (thrusterAcceleration * accelerationMultiplier / SpaceMass)
                    * deltaTime;
                zeroGravityVelocity = Vector3.ClampMagnitude(
                    zeroGravityVelocity,
                    thrusterMaxSpeed);
                return;
            }

        }

        private float GetEffectiveStabilizationAcceleration()
        {
            var massRatio = SpaceMass / Mathf.Max(0.1f, spaceMass);
            return thrusterStabilizationAcceleration / Mathf.Max(1f, massRatio);
        }

        private void RecoverThrusterFuel(float deltaTime)
        {
            var fuel = GetThrusterFuel();
            var capacity = EffectiveThrusterFuelCapacity;
            if (Time.time - lastThrusterUseTime < thrusterFuelRecoveryDelay || fuel >= capacity)
            {
                return;
            }

            SetThrusterFuel(Mathf.Min(
                capacity,
                fuel + thrusterFuelRecoveryPerSecond * deltaTime));
        }

        private float GetThrusterFuel()
        {
            return IsSpawned ? thrusterFuel.Value : standaloneThrusterFuel;
        }

        private void SetThrusterFuel(float value)
        {
            var clampedValue = Mathf.Clamp(value, 0f, EffectiveThrusterFuelCapacity);
            if (IsSpawned)
            {
                if (IsServer)
                {
                    thrusterFuel.Value = clampedValue;
                }

                return;
            }

            standaloneThrusterFuel = clampedValue;
        }

        private void RefreshThrusterGauge()
        {
            if (playHudPresenter == null)
            {
                if (Time.time >= hudBindingErrorLogTime && !thrusterGaugeReferenceErrorLogged)
                {
                    thrusterGaugeReferenceErrorLogged = true;
                    Debug.LogError($"PHS_THRUSTER_UI_SETUP_FAILED reason=play_hud_presenter_missing player={name}");
                }

                return;
            }

            var currentFuel = Mathf.RoundToInt(GetThrusterFuel());
            var maxFuel = Mathf.RoundToInt(EffectiveThrusterFuelCapacity);
            playHudPresenter.SetThrusterFuel(currentFuel, maxFuel);
        }

        public bool TryRestoreThrusterFuelForUpgrade(float amount, out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (amount <= 0f)
            {
                reason = "positive_amount_required";
                return false;
            }

            var capacityAfterUpgrade = EffectiveThrusterFuelCapacity + amount;
            thrusterFuel.Value = Mathf.Clamp(
                GetThrusterFuel() + amount,
                0f,
                capacityAfterUpgrade);
            reason = null;
            return true;
        }

        private void ApplyGravityAreaMode()
        {
            var nextMode = NetworkPlayerGravityMode.ShipGravity;
            var highestPriority = int.MinValue;
            foreach (var area in gravityAreas)
            {
                if (area == null || area.Priority < highestPriority)
                {
                    continue;
                }

                highestPriority = area.Priority;
                nextMode = area.EffectiveGravityMode;
            }

            SetGravityMode(nextMode);
        }

        private void SetGravityMode(NetworkPlayerGravityMode nextMode)
        {
            if (gravityMode == nextMode)
            {
                return;
            }

            var previousMode = gravityMode;
            var transitionVelocity = previousMode == NetworkPlayerGravityMode.ShipGravity
                ? new Vector3(PlanarVelocity.x, IsGrounded ? 0f : verticalVelocity, PlanarVelocity.z)
                : zeroGravityVelocity;

            gravityMode = nextMode;
            if (nextMode == NetworkPlayerGravityMode.ShipGravity)
            {
                PlanarVelocity = new Vector3(transitionVelocity.x, 0f, transitionVelocity.z);
                verticalVelocity = transitionVelocity.y;
                zeroGravityVelocity = Vector3.zero;
                shipGravityBlend = 0f;
            }
            else
            {
                zeroGravityVelocity = transitionVelocity;
                PlanarVelocity = new Vector3(transitionVelocity.x, 0f, transitionVelocity.z);
                verticalVelocity = 0f;
                shipGravityBlend = 0f;
            }

            Debug.Log($"PHS_PLAYER_GRAVITY_MODE player={name} previous={previousMode} mode={gravityMode} velocity={transitionVelocity}");
        }

        private static NetworkPlayerGravityMode ConvertGravityMode(LastJumpCrew.Common.GravityMode mode)
        {
            return mode switch
            {
                LastJumpCrew.Common.GravityMode.ShipGravity => NetworkPlayerGravityMode.ShipGravity,
                LastJumpCrew.Common.GravityMode.Spacewalk => NetworkPlayerGravityMode.Spacewalk,
                _ => NetworkPlayerGravityMode.ShipZeroGravity,
            };
        }

        private void ConfigureNetworkRigidbody(bool isNetworkSpawned)
        {
            if (attachedRigidbody == null)
            {
                return;
            }

            attachedRigidbody.useGravity = isNetworkSpawned ? false : originalRigidbodyUseGravity;
            attachedRigidbody.isKinematic = isNetworkSpawned ? true : originalRigidbodyIsKinematic;
        }

        private bool MoveToGameplaySpawnPointIfServer(Scene activeScene, bool force = false)
        {
            if (!IsSpawned || !IsServer || gameplaySceneContext == null
                || (!force && initializedGameplaySceneHandle == activeScene.handle.GetRawData()))
            {
                return false;
            }

            if (!gameplaySceneContext.TryGetSpawnPoint(OwnerClientId, out var spawnPoint))
            {
                Debug.LogWarning($"PHS_SPAWN_POINT_MISSING scene={SceneManager.GetActiveScene().name}");
                return false;
            }

            if (!TryRequireSectorState("gameplay_spawn", out var sectorState))
            {
                return false;
            }

            if (!TryTeleportOnServer(spawnPoint.position, spawnPoint.rotation, "gameplay_spawn"))
            {
                return false;
            }

            sectorState.ResetToInteriorServer("gameplay_spawn");
            initializedGameplaySceneHandle = activeScene.handle.GetRawData();

            Debug.Log($"PHS_PLAYER_SPAWN_POINT ownerClientId={OwnerClientId} pos={transform.position}");
            return true;
        }

        public void SetResultInputBlocked(bool blocked)
        {
            if (IsSpawned && !IsOwner)
            {
                return;
            }

            resultInputBlocked = blocked;
            if (blocked)
            {
                HasMoveInput = false;
                IsRunning = false;
                UpdateLocalThrusterFeedback(false);
            }
        }

        [ServerRpc]
        private void RequestGameplaySpawnPointServerRpc(
            string sceneName,
            uint requestToken,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_SPAWN_POINT_FAILED reason=owner_mismatch player={name}", this);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, sceneName, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"PHS_SPAWN_POINT_FAILED reason=scene_mismatch requested={sceneName} active={activeScene.name}",
                    this);
                return;
            }

            gameplaySceneContext = GameplaySceneContext.FindForScene(activeScene);
            if (gameplaySceneContext == null || !gameplaySceneContext.IsGameplayScene)
            {
                Debug.LogError(
                    $"PHS_SPAWN_POINT_FAILED reason=gameplay_context_missing scene={activeScene.name}",
                    this);
                return;
            }

            if (initializedGameplaySceneHandle == activeScene.handle.GetRawData())
            {
                return;
            }

            if (!MoveToGameplaySpawnPointIfServer(activeScene))
            {
                return;
            }

            pendingGameplaySpawnAckToken = requestToken;
            pendingGameplaySpawnSceneHandle = activeScene.handle.GetRawData();

            ApplyGameplaySpawnPointClientRpc(
                activeScene.name,
                requestToken,
                transform.position,
                transform.rotation,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { OwnerClientId }
                    }
                });
        }

        [ClientRpc]
        private void ApplyGameplaySpawnPointClientRpc(
            string sceneName,
            uint requestToken,
            Vector3 position,
            Quaternion rotation,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, sceneName, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"PHS_SPAWN_POINT_FAILED reason=client_scene_mismatch requested={sceneName} active={activeScene.name}",
                    this);
                return;
            }

            TeleportTo(position, rotation);
            ConfirmGameplaySpawnPointServerRpc(sceneName, requestToken);
            Debug.Log($"PHS_PLAYER_SPAWN_POINT_CLIENT ownerClientId={OwnerClientId} pos={transform.position}", this);
        }

        [ServerRpc]
        private void ConfirmGameplaySpawnPointServerRpc(
            string sceneName,
            uint requestToken,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_SPAWN_POINT_CONFIRM_FAILED reason=owner_mismatch player={name}", this);
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            var activeSceneHandle = activeScene.handle.GetRawData();
            if (!string.Equals(activeScene.name, sceneName, StringComparison.Ordinal)
                || requestToken == 0U
                || requestToken != pendingGameplaySpawnAckToken
                || activeSceneHandle != pendingGameplaySpawnSceneHandle)
            {
                Debug.LogError(
                    $"PHS_SPAWN_POINT_CONFIRM_FAILED reason=stale_ack requested={sceneName}:{requestToken} " +
                    $"active={activeScene.name}:{activeSceneHandle} " +
                    $"pending={pendingGameplaySpawnSceneHandle}:{pendingGameplaySpawnAckToken}",
                    this);
                return;
            }

            gameplaySceneContext = GameplaySceneContext.FindForScene(activeScene);
            if (gameplaySceneContext == null || !gameplaySceneContext.IsGameplayScene)
            {
                Debug.LogError(
                    $"PHS_SPAWN_POINT_CONFIRM_FAILED reason=gameplay_context_missing scene={activeScene.name}",
                    this);
                return;
            }

            if (!MoveToGameplaySpawnPointIfServer(activeScene, true))
            {
                Debug.LogError($"PHS_SPAWN_POINT_CONFIRM_FAILED reason=teleport_failed scene={activeScene.name}", this);
                return;
            }

            pendingGameplaySpawnAckToken = 0U;
            pendingGameplaySpawnSceneHandle = ulong.MaxValue;
            Debug.Log(
                $"PHS_PLAYER_SPAWN_POINT_CONFIRMED ownerClientId={OwnerClientId} " +
                $"scene={activeScene.name} pos={transform.position}",
                this);
        }

        private bool TryRequireSectorState(string operation, out NetworkPlayerSectorState sectorState)
        {
            sectorState = playerSectorState;
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError(
                    $"PHS_PLAYER_SECTOR_REQUIRED_FAILED reason=server_required operation={operation} player={name}",
                    this);
                return false;
            }

            if (sectorState != null)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_PLAYER_SECTOR_REQUIRED_FAILED reason=sector_state_missing operation={operation} player={name}",
                this);
            return false;
        }

        private bool TryRequireLifeState(string operation, out NetworkPlayerLifeState lifeState)
        {
            lifeState = playerLifeState;
            if (lifeState != null)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_PLAYER_LIFE_REQUIRED_FAILED reason=life_state_missing operation={operation} player={name}",
                this);
            return false;
        }

        private bool TryTeleportOnServer(
            Vector3 targetPosition,
            Quaternion targetRotation,
            string operation)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError(
                    $"PHS_PLAYER_TELEPORT_FAILED reason=server_required operation={operation} player={name}",
                    this);
                return false;
            }

            var networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform == null || characterController == null || grappleController == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_TELEPORT_FAILED reason=contract_missing operation={operation} player={name} " +
                    $"networkTransform={networkTransform != null} characterController={characterController != null} " +
                    $"grapple={grappleController != null}",
                    this);
                return false;
            }

            if (!grappleController.CancelForTeleport())
            {
                Debug.LogError(
                    $"PHS_PLAYER_TELEPORT_FAILED reason=grapple_cancel_failed operation={operation} player={name}",
                    this);
                return false;
            }

            ResetGravityTracking();
            var wasEnabled = characterController.enabled;
            characterController.enabled = false;
            try
            {
                networkTransform.Teleport(targetPosition, targetRotation, transform.localScale);
                ResetMovementForRespawn();
                targetYaw = targetRotation.eulerAngles.y;
            }
            finally
            {
                characterController.enabled = wasEnabled;
            }

            Debug.Log(
                $"PHS_PLAYER_TELEPORT_OK operation={operation} player={name} pos={targetPosition}",
                this);
            return true;
        }

        private void TeleportTo(Vector3 targetPosition, Quaternion targetRotation)
        {
            ResetGravityTracking();
            var wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(targetPosition, targetRotation);
            PlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            zeroGravityVelocity = Vector3.zero;

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }

            Debug.Log($"PHS_PLAYER_TELEPORT_OK player={name} pos={targetPosition}");
        }

        private void ResetGravityTracking()
        {
            gravityAreas.Clear();
            ApplyGravityAreaMode();
            if (TryGetComponent<PlayerGravityReceiver>(out var gravityReceiver))
            {
                gravityReceiver.ResetGravitySources();
            }
        }

        private void ApplyLocalLook(Vector2 look)
        {
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * GetMouseLookDegrees(), -80f, 80f);

            if (cameraRoot != null)
            {
                if (gravityMode == NetworkPlayerGravityMode.ShipGravity)
                {
                    currentCameraPitch = cameraPitch;
                    cameraPitchVelocity = 0f;
                }
                else
                {
                    currentCameraPitch = Mathf.SmoothDampAngle(
                        currentCameraPitch,
                        cameraPitch,
                        ref cameraPitchVelocity,
                        cameraRotationSmoothTime,
                        cameraMaxRotationSpeed,
                        Time.deltaTime);
                }

                var shake = GetThrusterCameraShake();
                cameraRoot.localRotation = Quaternion.Euler(
                    currentCameraPitch + shake,
                    0f,
                    isLocalThrusterActive ? shake * 0.5f : 0f);
            }
        }

        private static void SetCursorLock(bool active)
        {
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }

        public void ReflectZeroGravityVelocity(Vector3 surfaceNormal)
        {
            if (gravityMode == NetworkPlayerGravityMode.ShipGravity || surfaceNormal.sqrMagnitude <= 0.001f)
            {
                return;
            }

            var normal = surfaceNormal.normalized;
            if (Vector3.Dot(zeroGravityVelocity, normal) >= -0.1f)
            {
                return;
            }

            zeroGravityVelocity = Vector3.Reflect(zeroGravityVelocity, normal)
                * zeroGravityCollisionRestitution;
        }

        private void RotatePlayer(float yawDegrees, float deltaTime)
        {
            targetYaw += yawDegrees;
            if (gravityMode == NetworkPlayerGravityMode.ShipGravity)
            {
                yawVelocity = 0f;
                transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
                return;
            }

            var nextYaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetYaw,
                ref yawVelocity,
                cameraRotationSmoothTime,
                cameraMaxRotationSpeed,
                deltaTime);
            transform.rotation = Quaternion.Euler(0f, nextYaw, 0f);
        }

        private float GetMouseLookDegrees()
        {
            return mouseDegreesPerCount * GetSavedMouseSensitivity(mouseSensitivity);
        }

        private void UpdateLocalThrusterFeedback(bool thrusterRequested)
        {
            isLocalThrusterActive = thrusterRequested
                && gravityMode != NetworkPlayerGravityMode.ShipGravity
                && GetThrusterFuel() > 0f;

            if (thrusterAudio == null)
            {
                if (!thrusterAudioReferenceErrorLogged)
                {
                    thrusterAudioReferenceErrorLogged = true;
                    Debug.LogError(
                        $"PHS_THRUSTER_AUDIO_SETUP_FAILED reason=thruster_audio_missing player={name}",
                        this);
                }

                return;
            }

            thrusterAudio.SetThrusterActive(
                isLocalThrusterActive,
                Time.deltaTime);
        }

        private float GetThrusterCameraShake()
        {
            if (!isLocalThrusterActive)
            {
                return 0f;
            }

            return Mathf.Sin(Time.time * zeroGravityCameraShakeFrequency)
                * zeroGravityCameraShakeDegrees;
        }

        private void StopThrusterFeedback()
        {
            isLocalThrusterActive = false;
            if (thrusterAudio != null)
            {
                thrusterAudio.StopImmediate();
            }
        }

        private Vector2 ReadMove()
        {
            return playerControlInput == null
                ? Vector2.zero
                : Vector2.ClampMagnitude(playerControlInput.Move, 1f);
        }

        private bool CanProcessLocalInput()
        {
            if (NetworkRunRestartCoordinator.Instance != null
                && NetworkRunRestartCoordinator.Instance.BlocksRun)
            {
                return false;
            }

            if (!IsSpawned)
            {
                return !pauseInputBlocked
                    && !lifeInputBlocked
                    && !warpInputBlocked
                    && !resultInputBlocked
                    && (statusEffectController == null || !statusEffectController.IsShocked);
            }

            return IsOwner
                && gameplayInputEnabled
                && !pauseInputBlocked
                && !lifeInputBlocked
                && !warpInputBlocked
                && !resultInputBlocked
                && (statusEffectController == null || !statusEffectController.IsShocked);
        }

        private float ReadVerticalMove()
        {
            return playerControlInput == null
                ? 0f
                : Mathf.Clamp(playerControlInput.Descend, -1f, 1f);
        }

        private Vector2 ReadLook()
        {
            if (playerControlInput == null)
            {
                return Vector2.zero;
            }

            var look = playerControlInput.Look;
            var degreesPerCount = Mathf.Max(0.0001f, GetMouseLookDegrees());
            var maximumCountsPerFrame = 6f / degreesPerCount;
            return new Vector2(
                Mathf.Clamp(look.x, -maximumCountsPerFrame, maximumCountsPerFrame),
                Mathf.Clamp(look.y, -maximumCountsPerFrame, maximumCountsPerFrame));
        }

        private void SetLocalView(bool active)
        {
            if (playerCamera != null)
            {
                playerCamera.enabled = active;
            }

            if (audioListener != null)
            {
                audioListener.enabled = active;
            }

            SetLocalOwnerVisualsVisible(!active);
        }

        private void CacheLocalOwnerHiddenRenderers()
        {
            if (localOwnerHiddenRenderers != null
                && Array.Exists(localOwnerHiddenRenderers, targetRenderer => targetRenderer != null))
            {
                return;
            }

            localOwnerHiddenRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        }

        private void SetLocalOwnerVisualsVisible(bool isVisible)
        {
            CacheLocalOwnerHiddenRenderers();
            if (localOwnerHiddenRenderers == null)
            {
                return;
            }

            foreach (var targetRenderer in localOwnerHiddenRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.enabled = isVisible;
            }
        }
        
    }
}
