namespace LastJumpCrew.ParkHanSol.Items
{
    using LastJumpCrew.Common;
    using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;

    public sealed class FoamSealantGunUsableItem : UtilityItemUseBehaviour
    {
        protected override bool CanUseItem(IItemHolder user, IInteractable target)
        {
            return user != null
                && user.HasItem
                && user.CurrentItem != null
                && user.CurrentItem.ItemId == "foam_sealant_gun"
                && TryGetTarget<IShipAccidentRepairTarget>(target, out var repairTarget)
                && repairTarget.RequiredItemId == "foam_sealant_gun"
                && repairTarget.CanInteract(user);
        }

        protected override void OnUseFinished(IItemHolder user, IInteractable target)
        {
            if (!TryGetTarget<PHSShipAccidentAnchor>(target, out var anchor)
                || !anchor.RequestRepair(user))
            {
                UnityEngine.Debug.LogWarning(
                    $"PHS_FOAM_SEALANT_REPAIR_FAILED reason=target_or_request item={name}",
                    this);
                return;
            }

            UnityEngine.Debug.Log($"PHS_FOAM_SEALANT_REPAIR_SENT item={name}", this);
        }
    }
}
