using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;
using LastJumpCrew.ParkHanSol.PlayerPrefab;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour, IPlayerMovementAnimationSource
    {
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private string gameplaySceneName = "ParkHanSol_PlayScene";
        [SerializeField] private string spawnPointsRootName = "Spawn Points";
        [SerializeField] private bool allowSceneInputWithoutNetwork = true;

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;
        private bool hasMoveInput;
        private bool isRunning;
        private bool isGrounded = true;
        private bool gameplayInputEnabled;
        private bool autoMoveEnabled;
        private float autoMoveSeconds;
        private float autoMoveEndTime;
        private bool autoMoveStarted;
        private float nextPositionLogTime;

        public bool IsGrounded => isGrounded;
        public bool HasMoveInput => hasMoveInput;
        public bool IsRunning => isRunning;
        public float VerticalVelocity => verticalVelocity;

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            MoveToGameplaySpawnPointIfServer();
            ApplySceneInputState();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (!enabled)
            {
                return;
            }

            SetLocalView(false);
            ConfigureCommandLineAutomation();
        }

        private void Start()
        {
            ApplySceneInputState();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void Update()
        {
            if (UseSceneInputWithoutNetwork())
            {
                ReadAndApplyLocalInput(true);
                return;
            }

            if (!IsOwner || !gameplayInputEnabled)
            {
                ClearMovementState();
                return;
            }

            ReadAndApplyLocalInput(false);
        }

        private void ReadAndApplyLocalInput(bool moveLocally)
        {
            var move = ReadMove();
            if (autoMoveEnabled && Time.time < autoMoveEndTime)
            {
                move.y = 1f;
            }

            var look = ReadLook();
            var jump = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            var sprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            var deltaTime = Time.deltaTime;

            ApplyLocalLook(look);

            if (moveLocally || IsServer)
            {
                MoveCharacter(move, look.x, jump, sprint, deltaTime);
            }
            else
            {
                SubmitInputServerRpc(move, look.x, jump, sprint, deltaTime);
            }

            UpdateMovementState(move, sprint);
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
            if (UseSceneInputWithoutNetwork())
            {
                gameplayInputEnabled = true;
                SetLocalView(true);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            if (!IsSpawned)
            {
                SetLocalView(false);
                ClearMovementState();
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
        private void SubmitInputServerRpc(Vector2 move, float yawInput, bool jump, bool sprint, float deltaTime)
        {
            MoveCharacter(move, yawInput, jump, sprint, Mathf.Clamp(deltaTime, 0f, 0.05f));
        }

        private void MoveCharacter(Vector2 move, float yawInput, bool jump, bool sprint, float deltaTime)
        {
            transform.Rotate(Vector3.up, yawInput * mouseSensitivity);

            var wishDirection = transform.right * move.x + transform.forward * move.y;
            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            isGrounded = characterController.isGrounded;
            if (isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (isGrounded && jump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * deltaTime;
            var speed = sprint ? moveSpeed * sprintMultiplier : moveSpeed;
            var velocity = wishDirection * speed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * deltaTime);
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

        private static Vector2 ReadLook()
        {
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
        }

        private bool UseSceneInputWithoutNetwork()
        {
            return allowSceneInputWithoutNetwork
                && !IsSpawned
                && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);
        }

        private void UpdateMovementState(Vector2 move, bool sprint)
        {
            hasMoveInput = move.sqrMagnitude > 0.01f;
            isRunning = hasMoveInput && sprint;
        }

        private void ClearMovementState()
        {
            hasMoveInput = false;
            isRunning = false;
            isGrounded = characterController == null || characterController.isGrounded;
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
        }
    }
}
