using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastJumpCrew.ParkHanSol.Shop;
using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Interaction;

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
        [SerializeField, Min(0.1f)] private float thrusterFuelUsePerSecond = 30f;
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
        [SerializeField] private AudioSource thrusterAudioSource;
        [SerializeField] private AudioClip thrusterAudioClip;
        [SerializeField, Range(0f, 1f)] private float thrusterAudioVolume = 0.65f;
        [SerializeField, Min(0.1f)] private float thrusterAudioAttackSpeed = 12f;
        [SerializeField, Min(0.1f)] private float thrusterAudioReleaseSpeed = 2.5f;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        public const string MouseSensitivityPreferenceKey = "PHS_MouseSensitivity";
        public const float DefaultMouseSensitivity = 0.6f;
        public const float MinimumMouseSensitivity = 0.05f;
        public const float MaximumMouseSensitivity = 5f;

        [SerializeField, Range(MinimumMouseSensitivity, MaximumMouseSensitivity)] private float mouseSensitivity = DefaultMouseSensitivity;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private Renderer[] localOwnerHiddenRenderers;

        private CharacterController characterController;
        private NetworkPlayerGrappleController grappleController;
        private NetworkPlayerLifeState playerLifeState;
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
        private bool autoMoveEnabled;
        private float autoMoveSeconds;
        private float autoMoveEndTime;
        private bool autoMoveStarted;
        private float nextPositionLogTime;
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

        private Vector3 externalVelocity; //필드 추가 
        [SerializeField, Min(0f)]
        private float groundedKnockbackRecovery = 10f;

        [SerializeField, Min(0f)]
        private float zeroGravityKnockbackRecovery = 2.5f;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public NetworkPlayerGravityMode GravityMode => gravityMode;
        public float ThrusterFuelNormalized => Mathf.Clamp01(GetThrusterFuel() / thrusterFuelCapacity);
        public float SpaceMass => Mathf.Max(
            0.1f,
            spaceMass + (debrisHolder == null ? 0f : debrisHolder.HeldDebrisMass * heldDebrisMassInfluence));
        public bool CanAcceptLocalInput => CanProcessLocalInput();

        public static float GetSavedMouseSensitivity(float fallback = DefaultMouseSensitivity)
        {
            return Mathf.Clamp(
                PlayerPrefs.GetFloat(MouseSensitivityPreferenceKey, fallback),
                MinimumMouseSensitivity,
                MaximumMouseSensitivity);
        }

        public static void SaveMouseSensitivity(float value)
        {
            PlayerPrefs.SetFloat(
                MouseSensitivityPreferenceKey,
                Mathf.Clamp(value, MinimumMouseSensitivity, MaximumMouseSensitivity));
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

            if (!IsOwner)
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=owner_required player={name}");
                return;
            }

            if (IsServer)
            {
                TeleportTo(targetPosition, targetRotation);
                return;
            }

            RequestTestTeleportServerRpc(targetPosition, targetRotation);
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

            var matchingPortal = false;
            foreach (var portal in UnityEngine.Object.FindObjectsByType<NetworkScenePortalInteractable>(
                         UnityEngine.FindObjectsInactive.Exclude,
                         UnityEngine.FindObjectsSortMode.None))
            {
                if (portal.MatchesServerRequest(
                        client.PlayerObject.transform,
                        destinationSceneName,
                        shopTransitionMode))
                {
                    matchingPortal = true;
                    break;
                }
            }

            if (!matchingPortal)
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_PORTAL_FAILED reason=portal_missing_or_out_of_range clientId={activatorClientId} scene={destinationSceneName} mode={shopTransitionMode}");
                return;
            }

            if (shopTransitionMode != ShopSceneTransitionMode.None)
            {
                var runFlowAdapter = UnityEngine.Object.FindAnyObjectByType<ShopRunFlowAdapter>(
                    UnityEngine.FindObjectsInactive.Include);
                IShopRunFlowService runFlow = runFlowAdapter;
                if (runFlow == null || !runFlow.IsReady)
                {
                    Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=run_flow_missing scene={destinationSceneName}");
                    return;
                }

                var allowed = shopTransitionMode == ShopSceneTransitionMode.CompleteShop
                    ? runFlow.TryCompleteShop(out var reason)
                    : runFlow.CanEnterShop(out reason);
                if (!allowed)
                {
                    Debug.LogWarning($"PHS_NETWORK_PORTAL_FAILED reason={reason} scene={destinationSceneName} mode={shopTransitionMode}");
                    return;
                }
            }

            if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
            {
                Debug.LogError($"PHS_NETWORK_PORTAL_FAILED reason=scene_not_in_build scene={destinationSceneName}");
                return;
            }

            networkManager.SceneManager.LoadScene(destinationSceneName, LoadSceneMode.Single);
            Debug.Log($"PHS_NETWORK_PORTAL_LOAD scene={destinationSceneName}");
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
        private void RequestTestTeleportServerRpc(Vector3 targetPosition, Quaternion targetRotation, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_TEST_TELEPORT_FAILED reason=owner_mismatch player={name}");
                return;
            }

            TeleportTo(targetPosition, targetRotation);
        }

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            grappleController = GetComponent<NetworkPlayerGrappleController>();
            playerLifeState = GetComponent<NetworkPlayerLifeState>();
            ConfigureNetworkRigidbody(true);
            if (IsServer)
            {
                thrusterFuel.Value = thrusterFuelCapacity;
            }

            RefreshForActiveScene();
        }

        public override void OnNetworkDespawn()
        {
            initializedGameplaySceneHandle = ulong.MaxValue;
            ConfigureNetworkRigidbody(false);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            grappleController = GetComponent<NetworkPlayerGrappleController>();
            playerLifeState = GetComponent<NetworkPlayerLifeState>();
            attachedRigidbody = GetComponent<Rigidbody>();
            debrisHolder = GetComponent<IDebrisHolder>();
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
            standaloneThrusterFuel = thrusterFuelCapacity;
            targetYaw = transform.eulerAngles.y;
            currentCameraPitch = cameraPitch;
            CacheLocalOwnerHiddenRenderers();
            ConfigureCommandLineAutomation();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SetLocalOwnerVisualsVisible(true);
            StopThrusterAudio();
        }

        private void Update()
        {
            if (!CanProcessLocalInput())
            {
                return;
            }

            RefreshThrusterGauge();
            var move = ReadMove();
            if (autoMoveEnabled && Time.time < autoMoveEndTime)
            {
                move.y = 1f;
            }

            var verticalMove = ReadVerticalMove();
            var look = ReadLook();
            var spacebarPressedThisFrame = Keyboard.current != null
                && Keyboard.current.spaceKey.wasPressedThisFrame;
            var jump = spacebarPressedThisFrame;
            var shiftPressed = Keyboard.current != null
                && Keyboard.current.leftShiftKey.isPressed;
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
                MoveOnServer(move, verticalMove, look.x, cameraPitch, jump, ascend, sprint, deltaTime);
            }
            else
            {
                SubmitInputServerRpc(move, verticalMove, look.x, cameraPitch, jump, ascend, sprint, deltaTime);
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
                hudBindingErrorLogTime = Time.time + 0.5f;
                MoveToGameplaySpawnPointIfServer(activeScene);
            }

            if (gameplayInputEnabled && autoMoveEnabled && !autoMoveStarted)
            {
                autoMoveStarted = true;
                autoMoveEndTime = Time.time + autoMoveSeconds;
            }

            Debug.Log($"PHS_PLAYER_SCENE_STATE scene={SceneManager.GetActiveScene().name} owner={IsOwner} input={gameplayInputEnabled}");
        }

        private void LateUpdate()
        {
            if (!autoMoveEnabled || Time.time < nextPositionLogTime)
            {
                return;
            }

            nextPositionLogTime = Time.time + 1f;
            Debug.Log($"PHS_PLAYER_POS scene={SceneManager.GetActiveScene().name} owner={IsOwner} ownerClientId={OwnerClientId} pos={transform.position}");
        }

        private void ConfigureCommandLineAutomation()
        {
            autoMoveSeconds = GetCommandLineFloat("-phsAutoMoveSeconds");
            if (autoMoveSeconds <= 0f)
            {
                return;
            }

            autoMoveEnabled = true;
            nextPositionLogTime = Time.time + 1f;
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
        private void SubmitInputServerRpc(Vector2 move, float verticalMove, float yawInput, float lookPitch, bool jump, bool ascend, bool sprint, float deltaTime)
        {
            MoveOnServer(move, verticalMove, yawInput, lookPitch, jump, ascend, sprint, Mathf.Clamp(deltaTime, 0f, 0.05f));
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
                $"?곕뱶議?- {Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds))}珥????щ쭩");
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

        private void MoveOnServer(Vector2 move, float verticalMove, float yawInput, float lookPitch, bool jump, bool ascend, bool sprint, float deltaTime)
        {
            if (playerLifeState != null && !playerLifeState.IsAlive)
            {
                HasMoveInput = false;
                IsRunning = false;
                PlanarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                zeroGravityVelocity = Vector3.zero;
                return;
            }

            RotatePlayer(yawInput, deltaTime);
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

           
            var finalVelocity = velocity + externalVelocity; //걷기, 달리기, 점프 속도에 외부 넉백 속도 +


            characterController.Move(finalVelocity * deltaTime);

            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, groundedKnockbackRecovery * deltaTime);
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

            var finalVelocity = zeroGravityVelocity + externalVelocity; //무중력 이동 속도와 외부 넉백 속도 + 
            characterController.Move(zeroGravityVelocity * deltaTime);

            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, zeroGravityKnockbackRecovery * deltaTime); //일반 보다 중력 상태보다 넉백이 천천이 줄어듬

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
            if (Time.time - lastThrusterUseTime < thrusterFuelRecoveryDelay || fuel >= thrusterFuelCapacity)
            {
                return;
            }

            SetThrusterFuel(Mathf.Min(
                thrusterFuelCapacity,
                fuel + thrusterFuelRecoveryPerSecond * deltaTime));
        }

        private float GetThrusterFuel()
        {
            return IsSpawned ? thrusterFuel.Value : standaloneThrusterFuel;
        }

        private void SetThrusterFuel(float value)
        {
            var clampedValue = Mathf.Clamp(value, 0f, thrusterFuelCapacity);
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
            var maxFuel = Mathf.RoundToInt(thrusterFuelCapacity);
            playHudPresenter.SetThrusterFuel(currentFuel, maxFuel);
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

        private void MoveToGameplaySpawnPointIfServer(Scene activeScene)
        {
            if (!IsSpawned || !IsServer || gameplaySceneContext == null
                || initializedGameplaySceneHandle == activeScene.handle.GetRawData())
            {
                return;
            }

            if (!gameplaySceneContext.TryGetSpawnPoint(OwnerClientId, out var spawnPoint))
            {
                Debug.LogWarning($"PHS_SPAWN_POINT_MISSING scene={SceneManager.GetActiveScene().name}");
                return;
            }

            var wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            verticalVelocity = 0f;
            initializedGameplaySceneHandle = activeScene.handle.GetRawData();

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }

            Debug.Log($"PHS_PLAYER_SPAWN_POINT ownerClientId={OwnerClientId} pos={transform.position}");
        }

        private void TeleportTo(Vector3 targetPosition, Quaternion targetRotation)
        {
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

            Debug.Log($"PHS_TEST_TELEPORT_OK player={name} pos={targetPosition}");
        }

        private void ApplyLocalLook(Vector2 look)
        {
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * GetMouseLookDegrees(), -80f, 80f);

            if (cameraRoot != null)
            {
                currentCameraPitch = Mathf.SmoothDampAngle(
                    currentCameraPitch,
                    cameraPitch,
                    ref cameraPitchVelocity,
                    cameraRotationSmoothTime,
                    cameraMaxRotationSpeed,
                    Time.deltaTime);

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

        private void RotatePlayer(float yawInput, float deltaTime)
        {
            targetYaw += yawInput * GetMouseLookDegrees();
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

            if (thrusterAudioSource == null || thrusterAudioClip == null)
            {
                if (!thrusterAudioReferenceErrorLogged)
                {
                    thrusterAudioReferenceErrorLogged = true;
                    Debug.LogError($"PHS_THRUSTER_AUDIO_SETUP_FAILED reason=audio_reference_missing player={name}");
                }

                return;
            }

            thrusterAudioSource.clip = thrusterAudioClip;
            thrusterAudioSource.loop = true;
            thrusterAudioSource.spatialBlend = 0f;
            if (isLocalThrusterActive && !thrusterAudioSource.isPlaying)
            {
                thrusterAudioSource.volume = 0f;
                thrusterAudioSource.Play();
            }

            var targetVolume = isLocalThrusterActive ? thrusterAudioVolume : 0f;
            var fadeSpeed = isLocalThrusterActive
                ? thrusterAudioAttackSpeed
                : thrusterAudioReleaseSpeed;
            thrusterAudioSource.volume = Mathf.MoveTowards(
                thrusterAudioSource.volume,
                targetVolume,
                fadeSpeed * Time.deltaTime);

            if (!isLocalThrusterActive
                && thrusterAudioSource.isPlaying
                && thrusterAudioSource.volume <= 0.001f)
            {
                thrusterAudioSource.Stop();
            }
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

        private void StopThrusterAudio()
        {
            isLocalThrusterActive = false;
            if (thrusterAudioSource != null && thrusterAudioSource.isPlaying)
            {
                thrusterAudioSource.Stop();
            }
        }

        private static Vector2 ReadMove()
        {
            if (Keyboard.current == null)
            {
                return Vector2.zero;
            }

            var move = Vector2.zero;
            if (Keyboard.current.aKey.isPressed) move.x -= 1f;
            if (Keyboard.current.dKey.isPressed) move.x += 1f;
            if (Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.wKey.isPressed) move.y += 1f;
            return Vector2.ClampMagnitude(move, 1f);
        }

        private bool CanProcessLocalInput()
        {
            if (!IsSpawned)
            {
                return !pauseInputBlocked && !lifeInputBlocked;
            }

            return IsOwner && gameplayInputEnabled && !pauseInputBlocked && !lifeInputBlocked;
        }

        private static float ReadVerticalMove()
        {
            if (Keyboard.current == null)
            {
                return 0f;
            }

            var verticalMove = 0f;
            if (Keyboard.current.leftCtrlKey.isPressed) verticalMove -= 1f;
            return Mathf.Clamp(verticalMove, -1f, 1f);
        }

        private Vector2 ReadLook()
        {
            if (Mouse.current == null)
            {
                return Vector2.zero;
            }

            var look = Mouse.current.delta.ReadValue();
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
            if (localOwnerHiddenRenderers != null && localOwnerHiddenRenderers.Length > 0)
            {
                return;
            }

            localOwnerHiddenRenderers = GetComponentsInChildren<Renderer>(true);
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
