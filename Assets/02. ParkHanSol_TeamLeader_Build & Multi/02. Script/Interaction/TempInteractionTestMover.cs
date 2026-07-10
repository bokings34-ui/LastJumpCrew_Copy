using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TempInteractionTestMover : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gravity = -18f;

        private CharacterController characterController;
        private float cameraPitch;
        private float verticalVelocity;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            ApplyLook();
            ApplyMove();
        }

        private void ApplyLook()
        {
            if (Mouse.current == null)
            {
                return;
            }

            var look = Mouse.current.delta.ReadValue();
            transform.Rotate(Vector3.up, look.x * mouseSensitivity);
            cameraPitch = Mathf.Clamp(cameraPitch - look.y * mouseSensitivity, -80f, 80f);

            if (cameraRoot != null)
            {
                cameraRoot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        private void ApplyMove()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            var move = Vector2.zero;
            if (Keyboard.current.aKey.isPressed) move.x -= 1f;
            if (Keyboard.current.dKey.isPressed) move.x += 1f;
            if (Keyboard.current.sKey.isPressed) move.y -= 1f;
            if (Keyboard.current.wKey.isPressed) move.y += 1f;
            move = Vector2.ClampMagnitude(move, 1f);

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;

            var velocity = (transform.right * move.x + transform.forward * move.y) * moveSpeed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
