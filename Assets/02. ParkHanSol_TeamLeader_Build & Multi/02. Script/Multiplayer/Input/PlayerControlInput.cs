using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public sealed class PlayerControlInput : NetworkBehaviour, IPlayerControlInput
    {
        public const string BindingOverridesPreferenceKey = "PHS_InputBindingOverrides_v1";

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
            playerInput.SwitchCurrentActionMap(playerActionMapName);
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
            if (!PlayerPrefs.HasKey(BindingOverridesPreferenceKey))
            {
                return;
            }

            var json = PlayerPrefs.GetString(BindingOverridesPreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    $"PHS_PLAYER_INPUT_OVERRIDE_FAILED reason=saved_json_empty key={BindingOverridesPreferenceKey}",
                    this);
                return;
            }

            try
            {
                playerInput.actions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"PHS_PLAYER_INPUT_OVERRIDE_FAILED reason=invalid_json exception={exception.GetType().Name} message={exception.Message}",
                    this);
            }
        }
    }
}
