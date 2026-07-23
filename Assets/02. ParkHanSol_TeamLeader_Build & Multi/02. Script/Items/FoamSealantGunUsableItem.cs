namespace LastJumpCrew.ParkHanSol.Items
{
    using LastJumpCrew.Common;
    using UnityEngine;

    public sealed class FoamSealantGunUsableItem :
        MonoBehaviour,
        IUsableItem,
        IContinuousUsableItem
    {
        public bool CanUse(IItemHolder user, IInteractable target)
        {
            return user != null
                && user.HasItem
                && user.CurrentItem != null
                && user.CurrentItem.ItemId
                    == PHSNetworkFoamCoordinator.FoamItemId
                && TryGetController(user, out var controller)
                && controller.CanRequestFire;
        }

        public void Use(IItemHolder user, IInteractable target)
        {
            if (!CanUse(user, target))
            {
                return;
            }

            TryGetController(user, out var controller);
            controller.TryRequestFire();
        }

        private static bool TryGetController(
            IItemHolder user,
            out PHSNetworkFoamGunController controller)
        {
            var userComponent = user as Component;
            controller = userComponent == null
                ? null
                : userComponent.GetComponent<PHSNetworkFoamGunController>();
            return controller != null;
        }
    }
}
