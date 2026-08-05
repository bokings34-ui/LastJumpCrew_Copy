using System;
using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    internal struct NetworkShopPurchaseRequest : INetworkSerializable
    {
        public FixedString128Bytes PurchaseId;
        public FixedString64Bytes OfferId;

        public NetworkShopPurchaseRequest(string purchaseId, string offerId)
        {
            PurchaseId = new FixedString128Bytes(purchaseId);
            OfferId = new FixedString64Bytes(offerId);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PurchaseId);
            serializer.SerializeValue(ref OfferId);
        }
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopPurchaseService :
        NetworkBehaviour,
        IShopPurchaseService,
        INetworkShopPurchaseReceiptService
    {
        private const int MaximumItemsPerPurchase = 16;

        private readonly struct CompletedPurchaseReceipt
        {
            public CompletedPurchaseReceipt(
                ulong purchaserClientId,
                ShopProductData product)
            {
                PurchaserClientId = purchaserClientId;
                Product = product;
            }

            public ulong PurchaserClientId { get; }
            public ShopProductData Product { get; }
        }

        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private MonoBehaviour walletSource;
        [SerializeField] private MonoBehaviour deliverySource;

        private readonly HashSet<string> completedPurchaseIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CompletedPurchaseReceipt> completedPurchaseReceipts =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> claimedPurchaseReceiptIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> soldOnePerVisitOfferIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Action<ShopPurchaseResult>> pendingClientRequests =
            new(StringComparer.Ordinal);

        private IShopWallet wallet;
        private IShopDeliveryService deliveryService;
        private IShopPurchaseTransactionService purchaseTransactionService;
        private uint nextClientRequestSequence;

        public int AvailableCredits => wallet != null && wallet.IsReady ? wallet.Credits : 0;

        public event Action<ShopProductData> ProductPurchased;

        private void Awake()
        {
            wallet = walletSource as IShopWallet;
            deliveryService = deliverySource as IShopDeliveryService;
            purchaseTransactionService = deliverySource as IShopPurchaseTransactionService;
            ValidateSetup();
        }

        public override void OnNetworkDespawn()
        {
            pendingClientRequests.Clear();
            base.OnNetworkDespawn();
        }

        public bool RequestPurchase(
            IReadOnlyList<ShopPurchaseRequest> requests,
            Action<ShopPurchaseResult> onCompleted)
        {
            if (onCompleted == null)
            {
                Debug.LogError($"PHS_SHOP_PURCHASE_REQUEST_FAILED reason=callback_missing service={name}", this);
                return false;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening || IsServer)
            {
                TryPurchase(requests, out var localResult);
                onCompleted(localResult);
                return true;
            }

            if (!IsSpawned)
            {
                onCompleted(CreateFailure("service_not_spawned", 0));
                return false;
            }

            if (requests == null || requests.Count == 0 || requests.Count > MaximumItemsPerPurchase)
            {
                onCompleted(CreateFailure("request_count_invalid", 0));
                return false;
            }

            var networkRequests = new NetworkShopPurchaseRequest[requests.Count];
            try
            {
                for (var index = 0; index < requests.Count; index++)
                {
                    var request = requests[index];
                    if (string.IsNullOrWhiteSpace(request.PurchaseId) || request.Product == null)
                    {
                        onCompleted(CreateFailure("request_invalid", 0));
                        return false;
                    }

                    networkRequests[index] = new NetworkShopPurchaseRequest(
                        request.PurchaseId,
                        request.Product.OfferId);
                }
            }
            catch (ArgumentException)
            {
                onCompleted(CreateFailure("request_id_too_long", 0));
                return false;
            }

            var requestToken = new FixedString64Bytes(
                $"{networkManager.LocalClientId}:{++nextClientRequestSequence}");
            var requestTokenKey = requestToken.ToString();
            pendingClientRequests.Add(requestTokenKey, onCompleted);
            SubmitPurchaseServerRpc(networkRequests, requestToken);
            return true;
        }

        public bool TryPurchase(IReadOnlyList<ShopPurchaseRequest> requests, out ShopPurchaseResult result)
        {
            return TryPurchaseForClient(
                requests,
                NetworkManager.ServerClientId,
                out result);
        }

        private bool TryPurchaseForClient(
            IReadOnlyList<ShopPurchaseRequest> requests,
            ulong purchaserClientId,
            out ShopPurchaseResult result)
        {
            result = default;
            if (!ValidateSetup())
            {
                return Fail("service_not_ready", 0, out result);
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !IsServer)
            {
                return Fail("server_required", 0, out result);
            }

            if (!wallet.IsReady)
            {
                return Fail("wallet_not_ready", 0, out result);
            }

            if (requests == null || requests.Count == 0 || requests.Count > MaximumItemsPerPurchase)
            {
                return Fail("request_count_invalid", 0, out result);
            }

            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            var onePerVisitOfferIds = new HashSet<string>(StringComparer.Ordinal);
            var deliveryItems = new List<UtilityItemDataSO>(requests.Count);
            var transactionDeliveries =
                new List<ShopPurchaseDeliveryRequest>(requests.Count);
            var totalPrice = 0;
            foreach (var request in requests)
            {
                if (string.IsNullOrWhiteSpace(request.PurchaseId) || request.Product == null)
                {
                    return Fail("request_invalid", totalPrice, out result);
                }

                if (!requestIds.Add(request.PurchaseId) || completedPurchaseIds.Contains(request.PurchaseId))
                {
                    return Fail("purchase_duplicate", totalPrice, out result);
                }

                if (!catalog.TryGetByOfferId(request.Product.OfferId, out var catalogProduct) ||
                    catalogProduct != request.Product)
                {
                    return Fail("product_not_in_catalog", totalPrice, out result);
                }

                if (request.Product.StockPolicy == ShopStockPolicy.OnePerVisit &&
                    (!onePerVisitOfferIds.Add(request.Product.OfferId) ||
                     soldOnePerVisitOfferIds.Contains(request.Product.OfferId)))
                {
                    return Fail("out_of_stock", totalPrice, out result);
                }

                try
                {
                    totalPrice = checked(totalPrice + request.Product.PurchasePrice);
                }
                catch (OverflowException)
                {
                    return Fail("price_overflow", totalPrice, out result);
                }

                deliveryItems.Add(request.Product.ItemPrefabData);
                transactionDeliveries.Add(
                    new ShopPurchaseDeliveryRequest(
                        request.PurchaseId,
                        request.Product.ItemPrefabData));
            }

            if (!deliveryService.CanQueueDeliveries(deliveryItems))
            {
                return Fail("delivery_rejected", totalPrice, out result);
            }

            if (IsNetworkSessionActive())
            {
                var transactionId = requests[0].PurchaseId;
                if (!purchaseTransactionService.TryCommitPurchase(
                        transactionId,
                        totalPrice,
                        transactionDeliveries,
                        purchaserClientId,
                        out var transactionReason))
                {
                    return Fail(
                        NormalizeTransactionFailure(transactionReason),
                        totalPrice,
                        out result);
                }
            }
            else
            {
                if (!wallet.TrySpendCredits(totalPrice))
                {
                    return Fail("insufficient_credits", totalPrice, out result);
                }

                if (!deliveryService.TryQueueDeliveries(deliveryItems))
                {
                    if (!wallet.TryAddCredits(totalPrice))
                    {
                        Debug.LogError(
                            $"PHS_SHOP_PURCHASE_INVARIANT_FAILED reason=payment_rollback_failed service={name} totalPrice={totalPrice}",
                            this);
                    }

                    return Fail("delivery_rejected_after_payment", totalPrice, out result);
                }
            }

            foreach (var request in requests)
            {
                completedPurchaseIds.Add(request.PurchaseId);
                completedPurchaseReceipts.Add(
                    request.PurchaseId,
                    new CompletedPurchaseReceipt(
                        purchaserClientId,
                        request.Product));
                if (request.Product.StockPolicy == ShopStockPolicy.OnePerVisit)
                {
                    soldOnePerVisitOfferIds.Add(request.Product.OfferId);
                }

                NotifyProductPurchased(request.Product);
            }

            result = new ShopPurchaseResult(true, null, totalPrice, requests.Count);
            Debug.Log(
                $"PHS_SHOP_PURCHASE_COMPLETED service={name} totalPrice={totalPrice} itemCount={requests.Count} pendingDelivery={deliveryService.PendingCount}");
            return true;
        }

        public bool TryCommitCheckoutPurchaseServer(
            ulong purchaserClientId,
            IReadOnlyList<string> purchaseIds,
            IReadOnlyList<ShopProductData> products,
            out ShopPurchaseResult result)
        {
            result = default;
            if (!IsSpawned || !IsServer)
            {
                return Fail("server_required", 0, out result);
            }

            if (purchaseIds == null
                || products == null
                || purchaseIds.Count == 0
                || purchaseIds.Count > MaximumItemsPerPurchase
                || purchaseIds.Count != products.Count)
            {
                return Fail("receipt_count_invalid", 0, out result);
            }

            var requests = new List<ShopPurchaseRequest>(purchaseIds.Count);
            var receiptKeys = new string[purchaseIds.Count];
            var requestReceiptKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < purchaseIds.Count; index++)
            {
                var purchaseId = purchaseIds[index];
                var product = products[index];
                if (string.IsNullOrWhiteSpace(purchaseId) || product == null)
                {
                    return Fail("receipt_request_invalid", 0, out result);
                }

                var receiptKey = GetReceiptKey(purchaserClientId, purchaseId);
                if (!requestReceiptKeys.Add(receiptKey))
                {
                    return Fail(
                        "receipt_duplicate_in_request",
                        0,
                        out result);
                }

                if (claimedPurchaseReceiptIds.Contains(receiptKey))
                {
                    return Fail("receipt_already_claimed", 0, out result);
                }

                receiptKeys[index] = receiptKey;
                requests.Add(new ShopPurchaseRequest(receiptKey, product));
            }

            if (!TryPurchaseForClient(
                    requests,
                    purchaserClientId,
                    out result))
            {
                return false;
            }

            for (var index = 0; index < receiptKeys.Length; index++)
            {
                var receiptKey = receiptKeys[index];
                var product = products[index];
                if (!completedPurchaseIds.Contains(receiptKey)
                    || !completedPurchaseReceipts.TryGetValue(
                        receiptKey,
                        out var receipt)
                    || receipt.PurchaserClientId != purchaserClientId
                    || receipt.Product != product)
                {
                    Debug.LogError(
                        $"PHS_SHOP_PURCHASE_INVARIANT_FAILED reason=receipt_commit_mismatch service={name} purchaser={purchaserClientId} receipt={receiptKey}",
                        this);
                    result = CreateFailure(
                        "receipt_commit_mismatch",
                        result.TotalPrice);
                    return false;
                }

            }

            foreach (var receiptKey in receiptKeys)
            {
                claimedPurchaseReceiptIds.Add(receiptKey);
            }

            Debug.Log(
                $"PHS_SHOP_PURCHASE_RECEIPTS_CLAIMED service={name} purchaser={purchaserClientId} count={receiptKeys.Length}",
                this);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitPurchaseServerRpc(
            NetworkShopPurchaseRequest[] networkRequests,
            FixedString64Bytes requestToken,
            ServerRpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            ShopPurchaseResult result;

            if (networkRequests == null ||
                networkRequests.Length == 0 ||
                networkRequests.Length > MaximumItemsPerPurchase)
            {
                result = CreateFailure("request_count_invalid", 0);
            }
            else
            {
                var requests = new List<ShopPurchaseRequest>(networkRequests.Length);
                foreach (var networkRequest in networkRequests)
                {
                    var offerId = networkRequest.OfferId.ToString();
                    if (string.IsNullOrWhiteSpace(offerId) ||
                        !catalog.TryGetByOfferId(offerId, out var product))
                    {
                        requests.Clear();
                        result = CreateFailure("product_not_in_catalog", 0);
                        SendPurchaseResult(senderClientId, requestToken, result);
                        return;
                    }

                    var purchaseId = $"client:{senderClientId}:{networkRequest.PurchaseId}";
                    requests.Add(new ShopPurchaseRequest(purchaseId, product));
                }

                TryPurchaseForClient(requests, senderClientId, out result);
            }

            SendPurchaseResult(senderClientId, requestToken, result);
        }

        private void SendPurchaseResult(
            ulong targetClientId,
            FixedString64Bytes requestToken,
            ShopPurchaseResult result)
        {
            var reason = new FixedString64Bytes(result.Reason ?? string.Empty);
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };

            CompletePurchaseClientRpc(
                requestToken,
                result.Success,
                reason,
                result.TotalPrice,
                result.PurchasedCount,
                clientRpcParams);
        }

        [ClientRpc]
        private void CompletePurchaseClientRpc(
            FixedString64Bytes requestToken,
            bool success,
            FixedString64Bytes reason,
            int totalPrice,
            int purchasedCount,
            ClientRpcParams clientRpcParams = default)
        {
            var requestTokenKey = requestToken.ToString();
            if (!pendingClientRequests.Remove(requestTokenKey, out var onCompleted))
            {
                Debug.LogWarning(
                    $"PHS_SHOP_PURCHASE_RESULT_IGNORED reason=request_missing service={name} token={requestTokenKey}",
                    this);
                return;
            }

            onCompleted(new ShopPurchaseResult(
                success,
                reason.IsEmpty ? null : reason.ToString(),
                totalPrice,
                purchasedCount));
        }

        private bool ValidateSetup()
        {
            var valid = true;
            if (catalog == null)
            {
                Debug.LogError($"PHS_SHOP_PURCHASE_SETUP_FAILED reason=catalog_missing service={name}", this);
                valid = false;
            }

            if (wallet == null)
            {
                Debug.LogError($"PHS_SHOP_PURCHASE_SETUP_FAILED reason=wallet_adapter_missing service={name}", this);
                valid = false;
            }

            if (deliveryService == null)
            {
                Debug.LogError($"PHS_SHOP_PURCHASE_SETUP_FAILED reason=delivery_service_missing service={name}", this);
                valid = false;
            }

            if (purchaseTransactionService == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_PURCHASE_SETUP_FAILED reason=purchase_transaction_service_missing service={name}",
                    this);
                valid = false;
            }

            return valid;
        }

        private static string NormalizeTransactionFailure(string reason)
        {
            return reason == "transaction_already_committed"
                || reason == "purchase_already_committed"
                ? "purchase_duplicate"
                : string.IsNullOrWhiteSpace(reason)
                    ? "purchase_transaction_rejected"
                    : reason;
        }

        private static string GetReceiptKey(
            ulong purchaserClientId,
            string purchaseId)
        {
            return purchaserClientId == NetworkManager.ServerClientId
                ? purchaseId
                : $"client:{purchaserClientId}:{purchaseId}";
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }

        private void NotifyProductPurchased(ShopProductData product)
        {
            var handlers = ProductPurchased;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<ShopProductData> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(product);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"PHS_SHOP_PURCHASE_OBSERVER_FAILED observer={handler.Method.Name} exception={exception.GetType().Name} offer={product?.OfferId ?? "none"}",
                        this);
                }
            }
        }

        private bool Fail(string reason, int totalPrice, out ShopPurchaseResult result)
        {
            result = CreateFailure(reason, totalPrice);
            Debug.LogWarning(
                $"PHS_SHOP_PURCHASE_FAILED reason={reason} service={name} totalPrice={totalPrice}",
                this);
            return false;
        }

        private static ShopPurchaseResult CreateFailure(string reason, int totalPrice)
        {
            return new ShopPurchaseResult(false, reason, totalPrice, 0);
        }
    }
}
