using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.UI.Prototypes
{
    // Audit scene 전용: NGO spawn 없이 기존 PlayerInput 액션 맵만 활성화한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PHSAuditOfflinePlayerInputActivator : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string actionMapName = "Player";

        private void Start()
        {
            if (NetworkManager.Singleton is { IsListening: true })
            {
                Debug.LogError("PHS_AUDIT_OFFLINE_INPUT_FAILED reason=network_session_active", this);
                enabled = false;
                return;
            }

            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogError("PHS_AUDIT_OFFLINE_INPUT_FAILED reason=player_input_missing", this);
                enabled = false;
                return;
            }

            if (playerInput.actions.FindActionMap(actionMapName, false) == null)
            {
                Debug.LogError(
                    $"PHS_AUDIT_OFFLINE_INPUT_FAILED reason=action_map_missing map={actionMapName}",
                    this);
                enabled = false;
                return;
            }

            playerInput.ActivateInput();
            playerInput.SwitchCurrentActionMap(actionMapName);
            if (!playerInput.inputIsActive || playerInput.currentActionMap?.name != actionMapName)
            {
                Debug.LogError(
                    $"PHS_AUDIT_OFFLINE_INPUT_FAILED reason=activation_failed map={actionMapName}",
                    this);
                enabled = false;
                return;
            }

            Debug.Log($"PHS_AUDIT_OFFLINE_INPUT_READY map={actionMapName}", this);
        }

        private void OnDisable()
        {
            if (playerInput != null && playerInput.inputIsActive)
            {
                playerInput.DeactivateInput();
            }
        }
    }
}
