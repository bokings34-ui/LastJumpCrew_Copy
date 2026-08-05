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
            TryCompleteFromItem(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryCompleteFromItem(other);
        }

        private void TryCompleteFromItem(Collider other)
        {
            if (!CanComplete)
            {
                return;
            }

            var debrisItem = other.GetComponentInParent<
                LastJumpCrew.ParkHanSol.Interaction.DebrisItem>();
            if (debrisItem == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(expectedItemId))
            {
                CompleteObjective();
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
