using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PHS_CuteWhiteGhostFirstPersonController : MonoBehaviour, IPlayerMovementAnimationSource
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4.2f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float mouseSensitivity = 2.2f;

        private CharacterController characterController;
        private NetworkObject networkObject;
        private NetworkPlayerController networkPlayerController;
        private float pitch;
        private float verticalVelocity;
        private bool jumpRequested;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            networkObject = GetComponent<NetworkObject>();
            networkPlayerController = GetComponent<NetworkPlayerController>();

            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            if (ShouldSkipLegacyMovement())
            {
                ClearMovementState();
                return;
            }

            ApplyLookInput();
            ReadJumpInput();
            MoveCharacter(Time.deltaTime);
        }

        private void ApplyLookInput()
        {
            Vector2 look = ReadLook() * mouseSensitivity;
            transform.Rotate(Vector3.up, look.x, Space.World);

            pitch = Mathf.Clamp(pitch - look.y, -70f, 75f);
            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void ReadJumpInput()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        private void MoveCharacter(float deltaTime)
        {
            IsGrounded = characterController.isGrounded;
            if (IsGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            Vector2 input = ReadMove();
            input = Vector2.ClampMagnitude(input, 1f);
            HasMoveInput = input.sqrMagnitude > 0.01f;
            IsRunning = HasMoveInput && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            Vector3 move = (transform.right * input.x) + (transform.forward * input.y);
            float targetSpeed = IsRunning ? runSpeed : walkSpeed;
            Vector3 targetPlanar = move * targetSpeed;

            Vector3 nextPlanar = Vector3.MoveTowards(PlanarVelocity, targetPlanar, acceleration * deltaTime);

            if (jumpRequested && IsGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * deltaTime;
            Vector3 velocity = new Vector3(nextPlanar.x, verticalVelocity, nextPlanar.z);
            characterController.Move(velocity * deltaTime);

            PlanarVelocity = nextPlanar;
            jumpRequested = false;
        }

        private bool ShouldSkipLegacyMovement()
        {
            return networkObject != null && networkObject.IsSpawned
                || networkPlayerController != null && networkPlayerController.enabled;
        }

        private void ClearMovementState()
        {
            IsGrounded = characterController == null || characterController.isGrounded;
            HasMoveInput = false;
            IsRunning = false;
            PlanarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            jumpRequested = false;
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
            return move;
        }

        private static Vector2 ReadLook()
        {
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
        }
    }
}
