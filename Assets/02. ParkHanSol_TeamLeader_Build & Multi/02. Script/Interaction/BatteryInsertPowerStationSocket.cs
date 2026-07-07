using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class BatteryInsertPowerStationSocket : MonoBehaviour, IInteractable
    {
        [SerializeField] private string requiredItemId = "battery_pack";
        [SerializeField] private string interactionPrompt = "Insert Battery";
        [SerializeField] private GameObject installedBatteryVisual;
        [SerializeField] private bool destroyInsertedBattery = true;

        private bool isBatteryInstalled;

        public string InteractionPrompt => interactionPrompt;
        public bool IsBatteryInstalled => isBatteryInstalled;

        private void Awake()
        {
            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_SOCKET_SETUP_FAILED reason=installedBatteryVisual_missing target={name}");
                return;
            }

            installedBatteryVisual.SetActive(isBatteryInstalled);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isBatteryInstalled)
            {
                return;
            }

            if (other == null)
            {
                Debug.LogError($"PHS_BATTERY_INSERT_FAILED reason=collider_missing target={name}");
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null)
            {
                return;
            }

            if (itemObject.IsHeld)
            {
                return;
            }

            if (itemObject.ItemId != requiredItemId)
            {
                Debug.LogWarning($"PHS_BATTERY_INSERT_FAILED reason=wrong_item target={name} item={itemObject.ItemId}");
                return;
            }

            InsertBattery(itemObject);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (isBatteryInstalled)
            {
                return false;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (itemHolder.CurrentItemPrefabData == null)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=heldItem_missing target={name}");
                return false;
            }

            if (itemHolder.CurrentItemPrefabData.ItemId != requiredItemId)
            {
                Debug.LogWarning($"PHS_BATTERY_INTERACT_FAILED reason=wrong_item target={name} expected={requiredItemId} actual={itemHolder.CurrentItemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                return;
            }

            if (!itemHolder.TryConsumeHeldItem(requiredItemId))
            {
                Debug.LogError($"PHS_BATTERY_INTERACT_FAILED reason=consume_failed target={name} item={requiredItemId}");
                return;
            }

            InstallBattery();
            Debug.Log($"PHS_BATTERY_INSERTED_BY_INTERACT target={name} item={requiredItemId}");
        }

        private void InsertBattery(UtilityItemObject itemObject)
        {
            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_INSERT_FAILED reason=installedBatteryVisual_missing target={name}");
                return;
            }

            InstallBattery();

            if (destroyInsertedBattery && itemObject != null)
            {
                Destroy(itemObject.gameObject);
            }

            Debug.Log($"PHS_BATTERY_INSERTED target={name} item={requiredItemId}");
        }

        private void InstallBattery()
        {
            isBatteryInstalled = true;
            installedBatteryVisual.SetActive(true);
        }
    }
}
