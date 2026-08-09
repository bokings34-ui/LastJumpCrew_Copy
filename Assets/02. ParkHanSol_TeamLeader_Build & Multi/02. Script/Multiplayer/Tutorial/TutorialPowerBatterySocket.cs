using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class TutorialPowerBatterySocket : MonoBehaviour, IInteractable
    {
        [SerializeField] private string requiredItemId = "battery_pack";
        [SerializeField] private string interactionPrompt = "배터리 장착";
        [SerializeField] private GameObject installedBatteryVisual;
        [SerializeField] private Light statusLight;

        public string InteractionPrompt => interactionPrompt;
        public bool IsRestored { get; private set; }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return !IsRestored
                && itemHolder?.CurrentItemPrefabData != null
                && itemHolder.CurrentItemPrefabData.ItemId == requiredItemId;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder)
                || !itemHolder.TryConsumeHeldItem(requiredItemId))
            {
                return;
            }

            IsRestored = true;
            if (installedBatteryVisual != null)
            {
                installedBatteryVisual.SetActive(true);
            }

            if (statusLight != null)
            {
                statusLight.color = Color.cyan;
            }
        }
    }
}
