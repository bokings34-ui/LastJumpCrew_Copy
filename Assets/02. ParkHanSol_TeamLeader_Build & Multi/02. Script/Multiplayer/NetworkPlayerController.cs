using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour
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

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;
        private bool gameplayInputEnabled;
        private bool autoMoveEnabled;
        private float autoMoveSeconds;
        private float autoMoveEndTime;
        private bool autoMoveStarted;
        private float nextPositionLogTime;

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            ApplySceneInputState();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            SetLocalView(false);
            ConfigureCommandLineAutomation();
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
            if (!IsOwner || !gameplayInputEnabled)
            {
                return;
            }

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

            if (IsServer)
            {
                MoveOnServer(move, look.x, jump, sprint, deltaTime);
            }
            else
            {
                SubmitInputServerRpc(move, look.x, jump, sprint, deltaTime);
            }
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
            ApplySceneInputState();
        }

        private void ApplySceneInputState()
        {
            if (!IsSpawned)
            {
                SetLocalView(false);
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
            MoveOnServer(move, yawInput, jump, sprint, Mathf.Clamp(deltaTime, 0f, 0.05f));
        }

        private void MoveOnServer(Vector2 move, float yawInput, bool jump, bool sprint, float deltaTime)
        {
            transform.Rotate(Vector3.up, yawInput * mouseSensitivity);

            var wishDirection = transform.right * move.x + transform.forward * move.y;
            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            var grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (grounded && jump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * deltaTime;
            var speed = sprint ? moveSpeed * sprintMultiplier : moveSpeed;
            var velocity = wishDirection * speed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * deltaTime);
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
