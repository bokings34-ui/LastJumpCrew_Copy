using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class ParkHanSolLobbyCreditsPopupController : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (openButton == null || popupRoot == null || closeButton == null)
            {
                Debug.LogError($"PHS_CREDITS_POPUP_SETUP_FAILED controller={name}", this);
                enabled = false;
                return;
            }

            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            Close();
        }

        private void OnDestroy()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(Open);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
            }
        }

        public void Open()
        {
            popupRoot.SetActive(true);
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
