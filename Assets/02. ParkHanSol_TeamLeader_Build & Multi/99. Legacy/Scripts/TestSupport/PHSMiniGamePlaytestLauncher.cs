using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PHSMiniGamePlaytestLauncher : MonoBehaviour, IMiniGameTarget, IPHSFinalMiniGameSessionOwner
    {
        [Header("Runtime Reference")]
        [SerializeField] private PHSMiniGameManager miniGameManager;

        [Header("Playtest Menu")]
        [SerializeField] private GameObject launcherPanel;
        [SerializeField] private Button doorKeypadButton;
        [SerializeField] private Button wireFixButton;
        [SerializeField] private Button powerSyncButton;
        [SerializeField] private Button cannonButton;

        public string MiniGameTargetId => "MiniGamePlaytest";

        private void Awake()
        {
            if (!ValidateSetup())
            {
                return;
            }

            doorKeypadButton.onClick.AddListener(() => Open(PHSMiniGameType.DoorKeypad));
            wireFixButton.onClick.AddListener(() => Open(PHSMiniGameType.WireFix));
            powerSyncButton.onClick.AddListener(() => Open(PHSMiniGameType.PowerSync));
            cannonButton.onClick.AddListener(() => Open(PHSMiniGameType.Cannon));
        }

        public void OnMiniGameSucceeded()
        {
            Debug.Log("PHS_MINIGAME_PLAYTEST_RESULT result=success", this);
        }

        public void OnMiniGameFailed()
        {
            Debug.Log("PHS_MINIGAME_PLAYTEST_RESULT result=failed", this);
        }

        public void OnMiniGameSessionClosed()
        {
            launcherPanel.SetActive(true);
        }

        private void Open(PHSMiniGameType gameType)
        {
            if (miniGameManager == null)
            {
                Debug.LogError("PHS_MINIGAME_PLAYTEST_OPEN_REJECTED reason=manager_missing", this);
                return;
            }

            if (!miniGameManager.OpenMiniGame(gameType, this))
            {
                Debug.LogError($"PHS_MINIGAME_PLAYTEST_OPEN_REJECTED reason=open_failed type={gameType}", this);
                return;
            }

            launcherPanel.SetActive(false);
        }

        private bool ValidateSetup()
        {
            bool valid = miniGameManager != null
                && launcherPanel != null
                && doorKeypadButton != null
                && wireFixButton != null
                && powerSyncButton != null
                && cannonButton != null;

            if (!valid)
            {
                Debug.LogError("PHS_MINIGAME_PLAYTEST_SETUP_INVALID reason=inspector_reference_missing", this);
            }

            return valid;
        }
    }
}
