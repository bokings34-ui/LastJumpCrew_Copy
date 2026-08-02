using UnityEngine;
using UnityEngine.InputSystem;

namespace SM
{
    [RequireComponent(typeof(CharacterController))]
    public class TestPlayerController : MonoBehaviour
    {
        [Header("이동 설정")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintMultiplier = 1.8f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float jumpHeight = 1.5f;

        [Header("카메라 설정")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float verticalLookLimit = 85f;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _verticalRotation;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (playerCamera == null)
            {
                // 카메라가 인스펙터에 안 정해져 있으면 자식에서 자동으로 찾음
                playerCamera = GetComponentInChildren<Camera>();
            }

            if (playerCamera == null)
            {
                // 그래도 없으면 새로 만들어서 눈높이에 배치
                var camObj = new GameObject("PlayerCamera");
                camObj.transform.SetParent(transform);
                camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                camObj.transform.localRotation = Quaternion.identity;
                playerCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null) return;

            HandleLook();
            HandleMove();

            // ESC로 마우스 잠금 풀기 (테스트 편의용)
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                    ? CursorLockMode.None
                    : CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState == CursorLockMode.None;
            }
        }

        private void HandleLook()
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            // 좌우 회전은 몸체(transform) 자체를 돌림
            transform.Rotate(Vector3.up * mouseDelta.x * lookSensitivity * Time.deltaTime);

            // 상하 회전은 카메라만 돌림 (고개 숙이기/들기)
            _verticalRotation -= mouseDelta.y * lookSensitivity * Time.deltaTime;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -verticalLookLimit, verticalLookLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
        }

        private void HandleMove()
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.wKey.isPressed) vertical += 1f;
            if (Keyboard.current.sKey.isPressed) vertical -= 1f;
            if (Keyboard.current.dKey.isPressed) horizontal += 1f;
            if (Keyboard.current.aKey.isPressed) horizontal -= 1f;

            Vector3 moveDirection = (transform.right * horizontal + transform.forward * vertical).normalized;

            float currentSpeed = moveSpeed;
            if (Keyboard.current.leftShiftKey.isPressed)
            {
                currentSpeed *= sprintMultiplier;
            }

            // 중력 처리
            if (_controller.isGrounded && _velocity.y < 0f)
            {
                _velocity.y = -2f; // 바닥에 붙어있게 살짝 음수 유지
            }

            if (_controller.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _velocity.y += gravity * Time.deltaTime;

            Vector3 motion = moveDirection * currentSpeed + Vector3.up * _velocity.y;
            _controller.Move(motion * Time.deltaTime);
        }
    }
}