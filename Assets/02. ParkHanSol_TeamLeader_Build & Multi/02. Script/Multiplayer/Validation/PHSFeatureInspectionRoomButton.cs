using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;
using LocalInteraction = LastJumpCrew.ParkHanSol.Interaction;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionRoomButton :
        MonoBehaviour,
        LocalInteraction.IInteractable,
        IInteractable
    {
        [SerializeField] private PHSFeatureInspectionRoomController roomController;
        [SerializeField] private int roomIndex = -1;
        [SerializeField] private bool returnToHub;
        [SerializeField] private string interactionPrompt = "Open inspection room";
        [SerializeField] private LocalInteraction.ShopCheckoutButtonPressVisual pressVisual;

        public string InteractionPrompt => interactionPrompt;

        public bool CanInteract(LocalInteraction.IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        public void Interact(LocalInteraction.IItemHolder itemHolder)
        {
            Trigger();
        }

        bool IInteractable.CanInteract(IItemHolder itemHolder)
        {
            return CanInteractCore();
        }

        void IInteractable.Interact(IItemHolder itemHolder)
        {
            Trigger();
        }

        private bool CanInteractCore()
        {
            var networkManager = NetworkManager.Singleton;
            return roomController != null
                && networkManager != null
                && networkManager.IsListening
                && networkManager.IsServer;
        }

        private void Trigger()
        {
            var accepted = returnToHub
                ? roomController.TryReturnToHub()
                : roomController.TryOpenRoom(roomIndex);
            pressVisual?.Play(accepted);
        }
    }
}
