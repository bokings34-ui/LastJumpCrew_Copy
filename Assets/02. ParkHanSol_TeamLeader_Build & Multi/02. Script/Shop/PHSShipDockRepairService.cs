using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSShipDockRepairService :
        NetworkBehaviour,
        IShipDockRepairService
    {
        [SerializeField] private MonoBehaviour walletSource;
        [SerializeField] private NetworkShipSystemsState shipSystemsState;
        [SerializeField] private NetworkRunFlowCoordinator runFlowCoordinator;
        [SerializeField] private PHSShipDockRepairOfferSO[] offers = Array.Empty<PHSShipDockRepairOfferSO>();

        private readonly Dictionary<string, PHSShipDockRepairOfferSO> offersById = new(StringComparer.Ordinal);
        private readonly HashSet<string> completedPurchaseIds = new(StringComparer.Ordinal);
        private IShopWallet wallet;
        private bool setupValid;

        private void Awake()
        {
            wallet = walletSource as IShopWallet;
            setupValid = ValidateSetup();
            enabled = setupValid;
        }

        public bool TryPurchaseRepairServer(
            string offerId,
            string purchaseId,
            out string reason)
        {
            if (!setupValid || !IsSpawned || !IsServer)
            {
                reason = "server_service_not_ready";
                return false;
            }

            if (runFlowCoordinator.Phase != NetworkRunPhase.Shop
                && runFlowCoordinator.Phase != NetworkRunPhase.FinalShop)
            {
                reason = $"shop_phase_required:current={runFlowCoordinator.Phase}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(purchaseId))
            {
                reason = "purchase_id_missing";
                return false;
            }

            if (completedPurchaseIds.Contains(purchaseId))
            {
                reason = $"purchase_duplicate:{purchaseId}";
                return false;
            }

            if (!offersById.TryGetValue(offerId ?? string.Empty, out var offer))
            {
                reason = $"offer_missing:{offerId}";
                return false;
            }

            if (!wallet.IsReady)
            {
                reason = "wallet_not_ready";
                return false;
            }

            if (shipSystemsState.CurrentShipHp <= 0)
            {
                reason = "ship_destroyed";
                return false;
            }

            if (shipSystemsState.CurrentShipHp >= shipSystemsState.MaximumShipHp)
            {
                reason = "ship_durability_full";
                return false;
            }

            if (!wallet.TrySpendCredits(offer.Price))
            {
                reason = $"credits_insufficient:price={offer.Price}:balance={wallet.Credits}";
                return false;
            }

            if (!shipSystemsState.TryRestoreShipDurabilityAtDock(offer.RepairAmount, out var repairReason))
            {
                if (!wallet.TryAddCredits(offer.Price))
                {
                    Debug.LogError(
                        $"PHS_SHIP_DOCK_REPAIR_TRANSACTION_FAILED reason=rollback_failed purchase={purchaseId} offer={offerId} repairReason={repairReason}",
                        this);
                    reason = "rollback_failed";
                    return false;
                }

                reason = $"repair_failed:{repairReason}";
                return false;
            }

            completedPurchaseIds.Add(purchaseId);
            reason = null;
            Debug.Log(
                $"PHS_SHIP_DOCK_REPAIR_PURCHASED purchase={purchaseId} offer={offerId} price={offer.Price} hp={shipSystemsState.CurrentShipHp}/{shipSystemsState.MaximumShipHp} credits={wallet.Credits}",
                this);
            return true;
        }

        private bool ValidateSetup()
        {
            if (wallet == null)
            {
                Debug.LogError("PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=wallet_contract_missing", this);
                return false;
            }

            if (shipSystemsState == null)
            {
                Debug.LogError("PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=ship_systems_missing", this);
                return false;
            }

            if (runFlowCoordinator == null)
            {
                Debug.LogError("PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=run_flow_missing", this);
                return false;
            }

            if (offers == null || offers.Length == 0)
            {
                Debug.LogError("PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=offers_missing", this);
                return false;
            }

            offersById.Clear();
            foreach (var offer in offers)
            {
                if (offer == null)
                {
                    Debug.LogError("PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=offer_missing", this);
                    return false;
                }

                if (!offer.TryValidate(out var offerReason))
                {
                    Debug.LogError($"PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=offer_invalid detail={offerReason}", this);
                    return false;
                }

                if (!offersById.TryAdd(offer.OfferId, offer))
                {
                    Debug.LogError($"PHS_SHIP_DOCK_REPAIR_SETUP_FAILED reason=offer_duplicate id={offer.OfferId}", this);
                    return false;
                }
            }

            return true;
        }
    }
}
