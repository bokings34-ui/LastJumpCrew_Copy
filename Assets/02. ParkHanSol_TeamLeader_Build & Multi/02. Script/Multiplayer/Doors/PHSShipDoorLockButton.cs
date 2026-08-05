using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Doors
{
    [DisallowMultipleComponent]
    public sealed class PHSShipDoorLockButton : MonoBehaviour, IInteractable
    {
        [SerializeField] private PHSNetworkShipDoorCoordinator coordinator;
        [SerializeField] private int doorIndex = -1;
        [SerializeField] private Renderer stateRenderer;
        [SerializeField] private Color unlockedColor = new(0.1f, 0.9f, 0.25f);
        [SerializeField] private Color lockedColor = new(1f, 0.15f, 0.05f);
        [SerializeField] private Color brokenColor = new(0.2f, 0.2f, 0.2f);

        private MaterialPropertyBlock propertyBlock;

        public string InteractionPrompt => coordinator != null
            && coordinator.GetState(doorIndex).Locked
                ? "문 잠금 해제"
                : "문 잠금";

        public bool CanInteract(IItemHolder itemHolder)
        {
            return coordinator != null
                && !coordinator.GetState(doorIndex).Destroyed;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (CanInteract(itemHolder))
            {
                coordinator.RequestToggleLock(doorIndex);
            }
        }

        public void Initialize(PHSNetworkShipDoorCoordinator owner, int index)
        {
            coordinator = owner;
            doorIndex = index;
            if (stateRenderer == null)
            {
                stateRenderer = GetComponent<Renderer>();
            }
        }

        public void SetState(bool locked, bool destroyed)
        {
            if (stateRenderer == null)
            {
                return;
            }
            propertyBlock ??= new MaterialPropertyBlock();
            stateRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor",
                destroyed ? brokenColor : locked ? lockedColor : unlockedColor);
            propertyBlock.SetColor("_Color",
                destroyed ? brokenColor : locked ? lockedColor : unlockedColor);
            stateRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
