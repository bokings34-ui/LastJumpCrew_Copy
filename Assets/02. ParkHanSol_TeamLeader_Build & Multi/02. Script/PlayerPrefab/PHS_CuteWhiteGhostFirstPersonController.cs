using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using LastJumpCrew.ParkHanSol.Multiplayer;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PHS_CuteWhiteGhostFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float runSpeed = 4.2f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float jumpVelocity = 4.6f;
        [SerializeField] private float mouseSensitivity = 2.2f;
        [SerializeField] private float groundCheckDistance = 0.18f;
        [SerializeField] private LayerMask groundMask = ~0;

        private Rigidbody body;
        private NetworkObject networkObject;
        private NetworkPlayerController networkPlayerController;
        private float pitch;
        private bool jumpRequested;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => body == null ? 0f : body.linearVelocity.y;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            networkObject = GetComponent<NetworkObject>();
            networkPlayerController = GetComponent<NetworkPlayerController>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(true);
            }
        }

        private void Update()
        {
            if (ShouldSkipLegacyMovement())
            {
                return;
            }

            var look = ReadLook() * mouseSensitivity;
            transform.Rotate(Vector3.up, look.x, Space.World);

            pitch = Mathf.Clamp(pitch - look.y, -70f, 75f);
            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            if (ShouldSkipLegacyMovement())
            {
                return;
            }

            IsGrounded = CheckGrounded();

            Vector2 input = ReadMove();
            input = Vector2.ClampMagnitude(input, 1f);
            HasMoveInput = input.sqrMagnitude > 0.01f;
            IsRunning = HasMoveInput && Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            Vector3 move = (transform.right * input.x) + (transform.forward * input.y);
            float targetSpeed = IsRunning ? runSpeed : walkSpeed;
            Vector3 targetPlanar = move * targetSpeed;

            Vector3 velocity = body.linearVelocity;
            Vector3 currentPlanar = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 nextPlanar = Vector3.MoveTowards(currentPlanar, targetPlanar, acceleration * Time.fixedDeltaTime);

            if (jumpRequested && IsGrounded)
            {
                velocity.y = jumpVelocity;
            }

            body.linearVelocity = new Vector3(nextPlanar.x, velocity.y, nextPlanar.z);
            PlanarVelocity = nextPlanar;
            jumpRequested = false;
        }

        private bool CheckGrounded()
        {
            Vector3 origin = transform.position + Vector3.up * 0.08f;
            float radius = 0.22f;
            foreach (RaycastHit hit in Physics.SphereCastAll(origin, radius, Vector3.down, groundCheckDistance + 0.1f, groundMask, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsNetworkSpawned()
        {
            return networkObject != null && networkObject.IsSpawned;
        }

        private bool ShouldSkipLegacyMovement()
        {
            return IsNetworkSpawned()
                || (networkPlayerController != null && networkPlayerController.enabled);
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
