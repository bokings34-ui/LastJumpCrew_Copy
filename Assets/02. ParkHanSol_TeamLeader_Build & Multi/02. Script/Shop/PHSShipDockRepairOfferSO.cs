using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [CreateAssetMenu(
        fileName = "PHS_ShipDockRepairOffer_New",
        menuName = "LastJumpCrew/ParkHanSol/Ship Dock Repair Offer")]
    public sealed class PHSShipDockRepairOfferSO : ScriptableObject
    {
        [SerializeField] private string offerId;
        [SerializeField, Min(1)] private int repairAmount = 20;
        [SerializeField, Min(0)] private int price = 100;

        public string OfferId => offerId;
        public int RepairAmount => repairAmount;
        public int Price => price;

        public bool TryValidate(out string reason)
        {
            if (string.IsNullOrWhiteSpace(offerId))
            {
                reason = "offer_id_missing";
                return false;
            }

            if (repairAmount <= 0)
            {
                reason = "repair_amount_not_positive";
                return false;
            }

            if (price < 0)
            {
                reason = "price_negative";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
