using Unity.Netcode;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public sealed class PlayerControlInput : NetworkBehaviour, IPlayerControlInput
    {
        public const string BindingOverridesPreferenceKey = NetworkPlayerOptionsStore.BindingOverridesPreferenceKey;

        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private string playerActionMapName = "Player";

        private InputActionMap playerActionMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction interactAction;
        private InputAction useAction;
        private InputAction dropAction;
        private InputAction grappleAction;
        private InputAction descendAction;
        private bool setupValid;

        public Vector2 Move => setupValid ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 Look => setupValid ? lookAction.ReadValue<Vector2>() : Vector2.zero;
        public float Descend => setupValid && descendAction.IsPressed() ? -1f : 0f;
        public bool JumpPressedThisFrame => setupValid && jumpAction.WasPressedThisFrame();
        public bool SprintPressed => setupValid && sprintAction.IsPressed();
        public bool InteractPressedThisFrame => setupValid && interactAction.WasPressedThisFrame();
        public bool UsePressedThisFrame => setupValid && useAction.WasPressedThisFrame();
        public bool UsePressed => setupValid && useAction.IsPressed();
        public bool DropPressedThisFrame => setupValid && dropAction.WasPressedThisFrame();
        public bool DropReleasedThisFrame => setupValid && dropAction.WasReleasedThisFrame();
        public bool GrapplePressedThisFrame => setupValid && grappleAction.WasPressedThisFrame();
        public bool GrappleReleasedThisFrame => setupValid && grappleAction.WasReleasedThisFrame();

        private void Awake()
        {
            setupValid = TryCacheActions();
            if (!setupValid)
            {
                enabled = false;
                return;
            }

            playerInput.DeactivateInput();
            LoadBindingOverrides();
        }

        public override void OnNetworkSpawn()
        {
            if (!setupValid)
            {
                return;
            }

            if (!IsOwner)
            {
                playerInput.DeactivateInput();
                return;
            }

            playerInput.ActivateInput();
            PreferKeyboardAndMouseScheme();
            playerInput.SwitchCurrentActionMap(playerActionMapName);
            Debug.Log(
                $"PHS_PLAYER_INPUT_STATE ownerClientId={OwnerClientId} " +
                $"active={playerInput.inputIsActive} map={playerInput.currentActionMap?.name ?? "none"} " +
                $"scheme={playerInput.currentControlScheme ?? "none"} " +
                $"devices={string.Join(",", playerInput.user.pairedDevices.Select(device => device.displayName))}",
                this);
        }

        private void PreferKeyboardAndMouseScheme()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                Debug.LogWarning(
                    $"PHS_PLAYER_INPUT_SCHEME_FALLBACK ownerClientId={OwnerClientId} " +
                    $"keyboard={keyboard != null} mouse={mouse != null}",
                    this);
                return;
            }

            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            if (playerInput.currentControlScheme != "Keyboard&Mouse")
            {
                Debug.LogWarning(
                    $"PHS_PLAYER_INPUT_SCHEME_FAILED ownerClientId={OwnerClientId} scheme=Keyboard&Mouse",
                    this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (playerInput != null)
            {
                playerInput.DeactivateInput();
            }
        }

        public void ReloadBindingOverridesFromPreferences()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_INPUT_OVERRIDE_FAILED reason=actions_asset_missing player={name}",
                    this);
                return;
            }

            playerInput.actions.RemoveAllBindingOverrides();
            LoadBindingOverrides();
        }

        private bool TryCacheActions()
        {
            if (playerInput == null)
            {
                Debug.LogError($"PHS_PLAYER_INPUT_SETUP_FAILED reason=player_input_reference_missing player={name}", this);
                return false;
            }

            if (playerInput.actions == null)
            {
                Debug.LogError($"PHS_PLAYER_INPUT_SETUP_FAILED reason=actions_asset_missing player={name}", this);
                return false;
            }

            playerActionMap = playerInput.actions.FindActionMap(playerActionMapName, false);
            if (playerActionMap == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_INPUT_SETUP_FAILED reason=action_map_missing map={playerActionMapName} player={name}",
                    this);
                return false;
            }

            moveAction = FindRequiredAction("Move");
            lookAction = FindRequiredAction("Look");
            jumpAction = FindRequiredAction("Jump");
            sprintAction = FindRequiredAction("Sprint");
            interactAction = FindRequiredAction("Interact");
            useAction = FindRequiredAction("Use");
            dropAction = FindRequiredAction("Drop");
            grappleAction = FindRequiredAction("Grapple");
            descendAction = FindRequiredAction("Descend");

            return moveAction != null
                && lookAction != null
                && jumpAction != null
                && sprintAction != null
                && interactAction != null
                && useAction != null
                && dropAction != null
                && grappleAction != null
                && descendAction != null;
        }

        private InputAction FindRequiredAction(string actionName)
        {
            var action = playerActionMap.FindAction(actionName, false);
            if (action == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_INPUT_SETUP_FAILED reason=action_missing action={actionName} map={playerActionMapName} player={name}",
                    this);
            }

            return action;
        }

        private void LoadBindingOverrides()
        {
            NetworkPlayerOptionsStore.Shared.LoadBindingOverrides(playerInput.actions);
        }
    }
}
