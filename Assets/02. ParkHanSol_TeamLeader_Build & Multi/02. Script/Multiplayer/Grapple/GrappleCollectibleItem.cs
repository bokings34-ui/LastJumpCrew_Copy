using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class GrappleCollectibleItem : MonoBehaviour, IGrappleCollectible
    {
        [SerializeField] private UtilityItemObject itemObject;
        [SerializeField] private Transform collectionPoint;

        private bool setupErrorLogged;

        public Transform CollectionPoint => collectionPoint;

        private void Awake()
        {
            ValidateSetup();
        }

        public bool TryCollect(IItemHolder itemHolder)
        {
            if (!ValidateSetup())
            {
                return false;
            }

            if (itemHolder == null)
            {
                Debug.LogError($"PHS_GRAPPLE_COLLECT_FAILED reason=item_holder_missing item={name}");
                return false;
            }

            if (!itemObject.CanInteract(itemHolder))
            {
                return false;
            }

            itemObject.Interact(itemHolder);
            return true;
        }

        private bool ValidateSetup()
        {
            if (itemObject != null && collectionPoint != null)
            {
                return true;
            }

            if (!setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_GRAPPLE_COLLECTIBLE_SETUP_FAILED item={name} itemObject={itemObject != null} collectionPoint={collectionPoint != null}");
            }

            return false;
        }
    }
}
