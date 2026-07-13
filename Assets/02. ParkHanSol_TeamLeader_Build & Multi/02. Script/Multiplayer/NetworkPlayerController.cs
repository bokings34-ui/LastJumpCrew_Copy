using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using LastJumpCrew.Common;
using System;
using System.Collections.Generic;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public enum ZeroGravityControlPreset
    {
        Direct = 0,
        Inertia = 1,
        Hybrid = 2,
        Thruster = 3,
        ThrusterOnly = 4
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private float moveSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4.2f;
        [SerializeField, Min(0.1f)] private float groundAcceleration = 18f;
        [SerializeField] private float jumpVelocity = 4.6f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float gravity = -18f;
        [SerializeField, Min(0.1f)] private float zeroGravityMoveSpeed = 3.5f;
        [SerializeField, Min(0.1f)] private float zeroGravityAcceleration = 8f;
        [SerializeField, Min(0.1f)] private float spacewalkMoveSpeed = 5f;
        [SerializeField, Min(0.1f)] private float spacewalkAcceleration = 5f;
        [SerializeField, Min(0f)] private float zeroGravityDamping = 2.5f;
        [SerializeField] private ZeroGravityControlPreset zeroGravityControlPreset = ZeroGravityControlPreset.Hybrid;
        [Header("Zero Gravity Thruster")]
        [SerializeField, Min(1f)] private float thrusterFuelCapacity = 100f;
        [SerializeField, Min(0.1f)] private float thrusterFuelUsePerSecond = 30f;
        [SerializeField, Min(0f)] private float thrusterFuelRecoveryDelay = 1f;
        [SerializeField, Min(0.1f)] private float thrusterFuelRecoveryPerSecond = 18f;
        [SerializeField, Min(0.1f)] private float thrusterAcceleration = 14f;
        [SerializeField, Min(0.1f)] private float thrusterMaxSpeed = 9f;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private Renderer[] localOwnerHiddenRenderers;
        [SerializeField] private string gameplaySceneName = "ParkHanSol_PlayScene";
        [SerializeField] private string spawnPointsRootName = "Spawn Points";

        private CharacterController characterController;
        private Rigidbody attachedRigidbody;
        private float verticalVelocity;
        private float cameraPitch;
        private bool gameplayInputEnabled;
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
        private readonly NetworkVariable<float> thrusterFuel = new(
            0f,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);
        private float standaloneThrusterFuel;
        private float lastThrusterUseTime = float.NegativeInfinity;
        private bool thrusterGaugeReferenceErrorLogged;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public NetworkPlayerGravityMode GravityMode => gravityMode;
        public ZeroGravityControlPreset ZeroGravityControlPreset => zeroGravityControlPreset;
        public float ThrusterFuelNormalized => Mathf.Clamp01(GetThrusterFuel() / thrusterFuelCapacity);

        public void SetZeroGravityControlPreset(ZeroGravityControlPreset preset)
        {
            if (zeroGravityControlPreset == preset)
            {
                return;
            }

            zeroGravityControlPreset = preset;
            zeroGravityVelocity = Vector3.zero;
            Debug.Log($"PHS_ZERO_GRAVITY_CONTROL_PRESET player={name} preset={zeroGravityControlPreset}");
        }

        public void ApplyGravityState(GravityState gravityState)
        {
            var nextMode = ConvertGravityMode(gravityState.Mode);
            if (gravityMode == nextMode)
            {
                return;
            }

            gravityMode = nextMode;
            verticalVelocity = 0f;
            zeroGravityVelocity = Vector3.zero;
            Debug.Log($"PHS_PLAYER_GRAVITY_MODE player={name} mode={gravityMode}");
        }

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            ConfigureNetworkRigidbody(true);
            if (IsServer)
            {
                thrusterFuel.Value = thrusterFuelCapacity;
            }

            MoveToGameplaySpawnPointIfServer();
            ApplySceneInputState();
        }

        public override void OnNetworkDespawn()
        {
            ConfigureNetworkRigidbody(false);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            attachedRigidbody = GetComponent<Rigidbody>();
            if (attachedRigidbody != null)
            {
                originalRigidbodyUseGravity = attachedRigidbody.useGravity;
                originalRigidbodyIsKinematic = attachedRigidbody.isKinematic;
            }

            ConfigureNetworkRigidbody(true);
            standaloneThrusterFuel = thrusterFuelCapacity;
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
            var jump = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            var thruster = Keyboard.current != null
                && Keyboard.current.spaceKey.isPressed
                && zeroGravityControlPreset == ZeroGravityControlPreset.Thruster;
            var sprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            var deltaTime = Time.deltaTime;
            HasMoveInput = move.sqrMagnitude > 0.01f || Mathf.Abs(verticalMove) > 0.01f || thruster;
            IsRunning = HasMoveInput && sprint;

            ApplyLocalLook(look);

            if (!IsSpawned || IsServer)
            {
                MoveOnServer(move, verticalMove, look.x, jump, thruster, sprint, deltaTime);
            }
            else
            {
                SubmitInputServerRpc(move, verticalMove, look.x, jump, thruster, sprint, deltaTime);
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

            Cursor.lockState = gameplayInputEnabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplayInputEnabled;
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            MoveToGameplaySpawnPointIfServer();
            ApplySceneInputState();
        }

        private void ApplySceneInputState()
        {
            if (!IsSpawned)
            {
                SetLocalView(true);
                return;
            }

            var isGameplayScene = SceneManager.GetActiveScene().name == gameplaySceneName;
            SetGameplayInputEnabled(isGameplayScene);
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
        private void SubmitInputServerRpc(Vector2 move, float verticalMove, float yawInput, bool jump, bool thruster, bool sprint, float deltaTime)
        {
            MoveOnServer(move, verticalMove, yawInput, jump, thruster, sprint, Mathf.Clamp(deltaTime, 0f, 0.05f));
        }

        private void MoveOnServer(Vector2 move, float verticalMove, float yawInput, bool jump, bool thruster, bool sprint, float deltaTime)
        {
            transform.Rotate(Vector3.up, yawInput * mouseSensitivity);
            RecoverThrusterFuel(deltaTime);

            if (gravityMode != NetworkPlayerGravityMode.ShipGravity)
            {
                MoveZeroGravity(move, verticalMove, thruster, sprint, deltaTime);
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

            verticalVelocity += gravity * deltaTime;
            var targetSpeed = sprint ? runSpeed : moveSpeed;
            var targetPlanar = wishDirection * targetSpeed;
            var nextPlanar = Vector3.MoveTowards(
                PlanarVelocity,
                targetPlanar,
                groundAcceleration * deltaTime);
            var velocity = nextPlanar;
            velocity.y = verticalVelocity;
            PlanarVelocity = nextPlanar;
            characterController.Move(velocity * deltaTime);
        }

        private void MoveZeroGravity(Vector2 move, float verticalMove, bool thruster, bool sprint, float deltaTime)
        {
            verticalVelocity = 0f;
            IsGrounded = false;

            var moveBasis = GetZeroGravityMoveBasis();
            var wishDirection = moveBasis.Right * move.x
                + moveBasis.Forward * move.y
                + transform.up * verticalMove;
            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            var baseSpeed = gravityMode == NetworkPlayerGravityMode.Spacewalk
                ? spacewalkMoveSpeed
                : zeroGravityMoveSpeed;
            var acceleration = gravityMode == NetworkPlayerGravityMode.Spacewalk
                ? spacewalkAcceleration
                : zeroGravityAcceleration;
            var speed = sprint ? baseSpeed * sprintMultiplier : baseSpeed;
            if (zeroGravityControlPreset == ZeroGravityControlPreset.Thruster)
            {
                ApplyThruster(thruster, moveBasis.Forward, speed, deltaTime);
            }
            else if (zeroGravityControlPreset == ZeroGravityControlPreset.ThrusterOnly)
            {
                ApplyThruster(wishDirection.sqrMagnitude > 0.001f, wishDirection, speed, deltaTime);
            }
            else
            {
                var targetVelocity = wishDirection * speed;
                MoveZeroGravityByPreset(wishDirection, targetVelocity, speed, acceleration, deltaTime);
            }

            PlanarVelocity = new Vector3(zeroGravityVelocity.x, 0f, zeroGravityVelocity.z);
            
            characterController.Move(zeroGravityVelocity * deltaTime);
        }

        private (Vector3 Forward, Vector3 Right) GetZeroGravityMoveBasis()
        {
            var basisTransform = cameraRoot != null ? cameraRoot : transform;
            var forward = basisTransform.forward;
            var right = basisTransform.right;

            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = transform.forward;
            }

            if (right.sqrMagnitude <= 0.001f)
            {
                right = transform.right;
            }

            return (forward.normalized, right.normalized);
        }

        private void MoveZeroGravityByPreset(
            Vector3 wishDirection,
            Vector3 targetVelocity,
            float speed,
            float acceleration,
            float deltaTime)
        {
            switch (zeroGravityControlPreset)
            {
                case ZeroGravityControlPreset.Direct:
                    zeroGravityVelocity = targetVelocity;
                    return;
                case ZeroGravityControlPreset.Inertia:
                    if (wishDirection.sqrMagnitude > 0.001f)
                    {
                        zeroGravityVelocity += wishDirection * acceleration * deltaTime;
                        zeroGravityVelocity = Vector3.ClampMagnitude(zeroGravityVelocity, speed);
                    }
                    return;
                case ZeroGravityControlPreset.Hybrid:
                case ZeroGravityControlPreset.Thruster:
                case ZeroGravityControlPreset.ThrusterOnly:
                default:
                    zeroGravityVelocity = Vector3.MoveTowards(
                        zeroGravityVelocity,
                        targetVelocity,
                        acceleration * deltaTime);

                    if (wishDirection.sqrMagnitude <= 0.001f && zeroGravityDamping > 0f)
                    {
                        zeroGravityVelocity = Vector3.MoveTowards(
                            zeroGravityVelocity,
                            Vector3.zero,
                            zeroGravityDamping * deltaTime);
                    }

                    return;
            }
        }

        private void ApplyThruster(bool requested, Vector3 direction, float baseSpeed, float deltaTime)
        {
            var fuel = GetThrusterFuel();
            if (requested && fuel > 0f)
            {
                var fuelCost = Mathf.Min(fuel, thrusterFuelUsePerSecond * deltaTime);
                SetThrusterFuel(fuel - fuelCost);
                lastThrusterUseTime = Time.time;

                zeroGravityVelocity += direction.normalized * thrusterAcceleration * deltaTime;
                zeroGravityVelocity = Vector3.ClampMagnitude(
                    zeroGravityVelocity,
                    Mathf.Max(baseSpeed, thrusterMaxSpeed));
                return;
            }

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
                if (!thrusterGaugeReferenceErrorLogged)
                {
                    thrusterGaugeReferenceErrorLogged = true;
                    Debug.LogError($"PHS_THRUSTER_UI_SETUP_FAILED reason=play_hud_presenter_missing player={name}");
                }

                return;
            }

            var currentFuel = Mathf.RoundToInt(GetThrusterFuel());
            var maxFuel = Mathf.RoundToInt(thrusterFuelCapacity);
            playHudPresenter.SetWarpGauge(ThrusterFuelNormalized);
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

            if (gravityMode == nextMode)
            {
                return;
            }

            gravityMode = nextMode;
            verticalVelocity = 0f;
            zeroGravityVelocity = Vector3.zero;
            Debug.Log($"PHS_PLAYER_GRAVITY_MODE player={name} mode={gravityMode}");
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

        private void MoveToGameplaySpawnPointIfServer()
        {
            if (!IsSpawned || !IsServer || SceneManager.GetActiveScene().name != gameplaySceneName)
            {
                return;
            }

            if (!TryGetSpawnPoint(out var spawnPoint))
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

            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }

            Debug.Log($"PHS_PLAYER_SPAWN_POINT ownerClientId={OwnerClientId} pos={transform.position}");
        }

        private bool TryGetSpawnPoint(out Transform spawnPoint)
        {
            spawnPoint = null;

            var root = GameObject.Find(spawnPointsRootName);
            if (root == null || root.transform.childCount == 0)
            {
                return false;
            }

            var index = (int)(OwnerClientId % (ulong)root.transform.childCount);
            spawnPoint = root.transform.GetChild(index);
            return spawnPoint != null;
        }

        private void ApplyLocalLook(Vector2 look)
        {
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * mouseSensitivity, -80f, 80f);

            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
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
                return true;
            }

            return IsOwner && gameplayInputEnabled;
        }

        private static float ReadVerticalMove()
        {
            if (Keyboard.current == null)
            {
                return 0f;
            }

            var verticalMove = 0f;
            if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed) verticalMove -= 1f;
            return Mathf.Clamp(verticalMove, -1f, 1f);
        }

        private static Vector2 ReadLook()
        {
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
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
