using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class TempPlayerInteractionScanner : MonoBehaviour
    {
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float interactDistance = 2.5f;
        [SerializeField] private LayerMask interactableLayers = ~0;

        private IItemHolder itemHolder;
        private NetworkObject networkObject;
        private UtilityToolBoxStorageSlotInteractable focusedToolBoxSlot;
        private InteractableFocusGlow focusedGlow;

        private void Awake()
        {
            itemHolder = GetComponent<IItemHolder>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            {
                ClearToolBoxSlotFocus();
                return;
            }

            RefreshToolBoxSlotFocus();
            RefreshInteractableFocusGlow();

            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.fKey.wasPressedThisFrame)
            {
                TryInteract();
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                PlaceHeldItem();
            }
        }

        private void OnDisable()
        {
            ClearToolBoxSlotFocus();
            ClearInteractableFocusGlow();
        }

        private void PlaceHeldItem()
        {
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TEMP_PLACE_FAILED reason=itemHolder_missing player={name}");
                return;
            }

            itemHolder.PlaceHeldItem();
        }

        private void TryInteract()
        {
            if (interactionCamera == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=camera_missing player={name}");
                return;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=itemHolder_missing player={name}");
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_TARGET_MISSING player={name}");
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=interactable_missing target={hit.collider.name}");
                return;
            }

            if (!interactable.CanInteract(itemHolder))
            {
                Debug.LogWarning($"PHS_TEMP_INTERACT_FAILED reason=canInteract_false target={hit.collider.name}");
                return;
            }

            interactable.Interact(itemHolder);
        }

        private void RefreshToolBoxSlotFocus()
        {
            if (interactionCamera == null || itemHolder == null)
            {
                ClearToolBoxSlotFocus();
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                ClearToolBoxSlotFocus();
                return;
            }

            var toolBoxSlot = hit.collider.GetComponentInParent<UtilityToolBoxStorageSlotInteractable>();
            if (toolBoxSlot == focusedToolBoxSlot)
            {
                focusedToolBoxSlot?.SetInteractionFocus(itemHolder, true);
                return;
            }

            ClearToolBoxSlotFocus();
            focusedToolBoxSlot = toolBoxSlot;
            focusedToolBoxSlot?.SetInteractionFocus(itemHolder, true);
        }

        private void ClearToolBoxSlotFocus()
        {
            if (focusedToolBoxSlot == null)
            {
                return;
            }

            focusedToolBoxSlot.ClearInteractionFocus(itemHolder);
            focusedToolBoxSlot = null;
        }

        private void RefreshInteractableFocusGlow()
        {
            if (interactionCamera == null || itemHolder == null)
            {
                ClearInteractableFocusGlow();
                return;
            }

            var ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                ClearInteractableFocusGlow();
                return;
            }

            var glow = hit.collider.GetComponentInParent<InteractableFocusGlow>();
            if (glow == null)
            {
                ClearInteractableFocusGlow();
                return;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(itemHolder))
            {
                ClearInteractableFocusGlow();
                return;
            }

            if (glow == focusedGlow)
            {
                focusedGlow?.SetFocused(true);
                return;
            }

            ClearInteractableFocusGlow();
            focusedGlow = glow;
            focusedGlow?.SetFocused(true);
        }

        private void ClearInteractableFocusGlow()
        {
            if (focusedGlow == null)
            {
                return;
            }

            focusedGlow.SetFocused(false);
            focusedGlow = null;
        }
    }
}
