using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [RequireComponent(typeof(Collider))]
    public sealed class NetworkTutorialItemDropZoneObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private string expectedItemId;

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanComplete)
            {
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            var itemData = itemObject == null
                ? null
                : itemObject.ItemPrefabData;
            if (itemData == null || itemData.ItemId != expectedItemId)
            {
                return;
            }

            CompleteObjective();
        }
    }
}
