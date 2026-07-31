using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    [DisallowMultipleComponent]
    public sealed class PHSHandheldMapHudVisibilityBridge : MonoBehaviour
    {
        [SerializeField] private CanvasGroup hudContent;

        private bool defaultInteractable;
        private bool defaultBlocksRaycasts;

        public static PHSHandheldMapHudVisibilityBridge Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_HANDHELD_MAP_HUD_BRIDGE_FAILED reason=duplicate current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            if (hudContent == null)
            {
                Debug.LogError("PHS_HANDHELD_MAP_HUD_BRIDGE_FAILED reason=hud_content_missing", this);
                enabled = false;
                return;
            }

            Instance = this;
            defaultInteractable = hudContent.interactable;
            defaultBlocksRaycasts = hudContent.blocksRaycasts;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetMapVisible(bool visible)
        {
            if (!enabled || hudContent == null)
            {
                Debug.LogError("PHS_HANDHELD_MAP_HUD_BRIDGE_FAILED reason=bridge_not_ready", this);
                return;
            }

            hudContent.alpha = visible ? 0f : 1f;
            hudContent.interactable = visible ? false : defaultInteractable;
            hudContent.blocksRaycasts = visible ? false : defaultBlocksRaycasts;
        }
    }
}
