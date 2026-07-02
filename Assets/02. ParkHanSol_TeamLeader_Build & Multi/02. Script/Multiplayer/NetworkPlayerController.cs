using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private CharacterController characterController;
        private float verticalVelocity;
        private float cameraPitch;

        public override void OnNetworkSpawn()
        {
            characterController = GetComponent<CharacterController>();
            SetLocalView(IsOwner);

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            SetLocalView(false);
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            var move = ReadMove();
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
