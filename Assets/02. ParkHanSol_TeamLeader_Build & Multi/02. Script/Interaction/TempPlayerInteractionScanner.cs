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

        private void Awake()
        {
            itemHolder = GetComponent<IItemHolder>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
            {
                return;
            }

            if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            {
                return;
            }

            TryInteract();
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
    }
}
