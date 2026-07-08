using UnityEngine;

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
        private float pitch;
        private bool jumpRequested;

        public bool IsGrounded { get; private set; }
        public bool HasMoveInput { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            transform.Rotate(Vector3.up, mouseX, Space.World);

            pitch = Mathf.Clamp(pitch - mouseY, -70f, 75f);
            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            HasMoveInput = input.sqrMagnitude > 0.01f;
            IsRunning = HasMoveInput && Input.GetKey(KeyCode.LeftShift);

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
    }
}
