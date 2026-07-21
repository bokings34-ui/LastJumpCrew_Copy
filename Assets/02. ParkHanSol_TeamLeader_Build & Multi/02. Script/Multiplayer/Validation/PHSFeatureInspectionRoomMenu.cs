using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionRoomMenu : MonoBehaviour
    {
        [SerializeField] private PHSFeatureInspectionRoomController roomController;
        [SerializeField] private CanvasGroup menuGroup;
        [SerializeField] private bool openOnStart = true;

        private NetworkPlayerController localPlayer;

        public bool IsOpen => menuGroup != null
            && menuGroup.alpha > 0.5f
            && menuGroup.interactable;

        private void Start()
        {
            if (openOnStart)
            {
                OpenMenu();
            }
            else
            {
                ApplyMenuState(false);
            }
        }

        private void Update()
        {
            if (Keyboard.current?.tabKey.wasPressedThisFrame == true)
            {
                if (IsOpen)
                {
                    CloseMenu();
                }
                else
                {
                    OpenMenu();
                }
            }

            if (IsOpen)
            {
                SetLocalGameplayInput(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void OnDisable()
        {
            SetLocalGameplayInput(true);
        }

        public void SelectRoom(int roomIndex)
        {
            if (roomController != null && roomController.TryOpenRoom(roomIndex))
            {
                CloseMenu();
            }
        }

        public void OpenMenu()
        {
            ApplyMenuState(true);
            SetLocalGameplayInput(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseMenu()
        {
            ApplyMenuState(false);
            SetLocalGameplayInput(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ApplyMenuState(bool open)
        {
            if (menuGroup == null)
            {
                return;
            }

            menuGroup.alpha = open ? 1f : 0f;
            menuGroup.interactable = open;
            menuGroup.blocksRaycasts = open;
        }

        private void SetLocalGameplayInput(bool enabled)
        {
            if (localPlayer == null)
            {
                var networkManager = NetworkManager.Singleton;
                if (networkManager != null
                    && networkManager.IsListening
                    && networkManager.ConnectedClients.TryGetValue(
                        networkManager.LocalClientId,
                        out var client)
                    && client.PlayerObject != null)
                {
                    localPlayer = client.PlayerObject.GetComponent<NetworkPlayerController>();
                }
            }

            localPlayer?.SetGameplayInputEnabled(enabled);
        }
    }
}
