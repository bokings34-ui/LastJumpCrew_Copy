using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Calculates physical shop items from ShopProductData and queues paid items for ship delivery.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopCheckoutZone : NetworkBehaviour
    {
        private const int MaximumItemsPerCheckout = 16;

        private readonly struct CheckoutEntry
        {
            public CheckoutEntry(UtilityItemObject itemObject, ShopProductData productData)
            {
                ItemObject = itemObject;
                ProductData = productData;
            }

            public UtilityItemObject ItemObject { get; }
            public ShopProductData ProductData { get; }
        }

        [SerializeField] private BoxCollider checkoutTrigger;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private TMP_Text purchaseUnavailableText;
        [SerializeField] private string pricePrefix = "TOTAL";
        [SerializeField] private ShopCatalogSO catalog;
        [SerializeField] private MonoBehaviour purchaseServiceSource;
        [SerializeField] private MonoBehaviour audioCuePlayerSource;
        [SerializeField, Min(0.1f)] private float statusDuration = 2f;
        [SerializeField] private GameObject teleportEffectPrefab;
        [SerializeField] private Transform teleportEffectAnchor;
        [SerializeField, Min(0.01f)] private float teleportEffectScale = 0.1f;
        [SerializeField, Min(0.1f)] private float teleportEffectDuration = 3.2f;
        [SerializeField, Min(0.1f)] private float maximumServerCheckoutDistance = 4f;

        private readonly HashSet<UtilityItemObject> checkoutItems = new();
        private IShopPurchaseService purchaseService;
        private INetworkShopPurchaseReceiptService networkPurchaseReceiptService;
        private INetworkAudioCuePlayer audioCuePlayer;
        private bool checkoutPending;
        private int lastDisplayedPrice = -1;
        private int lastDisplayedCredits = -1;
        private string temporaryStatus;
        private float temporaryStatusUntil;
        private bool temporaryPurchaseUnavailable;

        public int CurrentTotalPrice => CalculateTotalPrice();

        private void Awake()
        {
            purchaseService = purchaseServiceSource as IShopPurchaseService;
            networkPurchaseReceiptService =
                purchaseServiceSource as INetworkShopPurchaseReceiptService;
            audioCuePlayer = audioCuePlayerSource as INetworkAudioCuePlayer;
            if (audioCuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_AUDIO_SETUP_FAILED reason=cue_player_missing zone={name}",
                    this);
            }
            ValidateSetup();
            RefreshPriceText(true);
        }

        private void Update()
        {
            RefreshCheckoutItemsFromZone();
            RefreshPriceText(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject != null)
            {
                checkoutItems.Add(itemObject);
                RefreshPriceText(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null)
            {
                return;
            }

            var itemObject = other.GetComponentInParent<UtilityItemObject>();
            if (itemObject != null)
            {
                checkoutItems.Remove(itemObject);
                RefreshPriceText(true);
            }
        }

        public bool CanCheckout()
        {
            if (checkoutPending || !IsTriggerConfigured())
            {
                return false;
            }

            RefreshCheckoutItemsFromZone();
            return BuildCheckoutSnapshot(null, out var totalPrice, false) &&
                totalPrice > 0 &&
                purchaseService != null &&
                totalPrice <= purchaseService.AvailableCredits;
        }

        public bool TryCheckout()
        {
            if (checkoutPending)
            {
                ShowTemporaryStatus("PURCHASE PENDING");
                return false;
            }

            if (!ValidateSetup())
            {
                ShowTemporaryStatus("CHECKOUT ERROR", true);
                return false;
            }

            RefreshCheckoutItemsFromZone();
            var entries = new List<CheckoutEntry>();
            if (!BuildCheckoutSnapshot(entries, out var totalPrice, true) || totalPrice <= 0)
            {
                ShowTemporaryStatus("NO SHOP ITEMS", true);
                return false;
            }

            if (entries.Count > MaximumItemsPerCheckout)
            {
                Debug.LogError(
                    $"PHS_SHOP_CHECKOUT_FAILED reason=item_count_invalid zone={name} count={entries.Count}",
                    this);
                ShowTemporaryStatus("TOO MANY ITEMS", true);
                return false;
            }

            var availableCredits = purchaseService.AvailableCredits;
            if (totalPrice > availableCredits)
            {
                ShowTemporaryStatus($"NOT ENOUGH CR\nNEED {totalPrice} / HAVE {availableCredits}", true);
                return false;
            }

            if (IsNetworkSessionActive())
            {
                return RequestNetworkCheckout(entries);
            }

            var requests = new List<ShopPurchaseRequest>(entries.Count);
            foreach (var entry in entries)
            {
                requests.Add(new ShopPurchaseRequest(
                    entry.ItemObject.GetEntityId().ToString(),
                    entry.ProductData));
            }

            checkoutPending = true;
            ShowTemporaryStatus("PURCHASE PENDING");
            if (!purchaseService.RequestPurchase(
                    requests,
                    result => HandleStandalonePurchaseCompleted(
                        entries,
                        totalPrice,
                        result)))
            {
                checkoutPending = false;
                return false;
            }

            return true;
        }

        private void HandleStandalonePurchaseCompleted(
            IReadOnlyList<CheckoutEntry> entries,
            int requestedTotalPrice,
            ShopPurchaseResult result)
        {
            checkoutPending = false;
            if (!result.Success)
            {
                var status = result.Reason switch
                {
                    "insufficient_credits" => $"NEED {requestedTotalPrice} CR",
                    "out_of_stock" => "ITEM SOLD OUT",
                    _ => "PURCHASE FAILED"
                };
                ShowTemporaryStatus(status, true);
                PlayAudioCue(NetworkAudioCue.ShopFailure);
                return;
            }

            foreach (var entry in entries)
            {
                checkoutItems.Remove(entry.ItemObject);
            }

            PlayCheckoutTeleportEffect();
            foreach (var entry in entries)
            {
                if (entry.ItemObject != null)
                {
                    Destroy(entry.ItemObject.gameObject);
                }
            }

            Debug.Log($"PHS_SHOP_CHECKOUT_COMPLETED zone={name} totalPrice={result.TotalPrice} itemCount={result.PurchasedCount}");
            ShowTemporaryStatus($"PAID {result.TotalPrice} CR\nSHIP DELIVERY");
            PlayAudioCue(NetworkAudioCue.ShopSuccess);
        }

        private bool RequestNetworkCheckout(
            IReadOnlyList<CheckoutEntry> entries)
        {
            if (entries == null
                || entries.Count == 0
                || entries.Count > MaximumItemsPerCheckout)
            {
                Debug.LogError(
                    $"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=request_count_invalid zone={name} entries={entries?.Count ?? 0}",
                    this);
                return false;
            }

            var itemReferences = new NetworkObjectReference[entries.Count];
            for (var index = 0; index < entries.Count; index++)
            {
                var itemObject = entries[index].ItemObject;
                if (itemObject == null ||
                    !itemObject.TryGetComponent<NetworkObject>(out var itemNetworkObject) ||
                    !itemNetworkObject.IsSpawned)
                {
                    Debug.LogError(
                        $"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=item_network_object_missing zone={name} item={itemObject?.name}",
                        this);
                    return false;
                }

                itemReferences[index] = new NetworkObjectReference(itemNetworkObject);
            }

            if (!IsSpawned)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_NETWORK_FAILED reason=checkout_zone_not_spawned zone={name}", this);
                return false;
            }

            checkoutPending = true;
            ShowTemporaryStatus("PURCHASE PENDING");

            if (IsServer)
            {
                var result = TryCompleteNetworkCheckoutServer(
                    itemReferences,
                    NetworkManager.ServerClientId);
                HandleNetworkCheckoutResult(result);
                return true;
            }

            RequestNetworkCheckoutServerRpc(itemReferences);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestNetworkCheckoutServerRpc(
            NetworkObjectReference[] itemReferences,
            ServerRpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            var result = TryCompleteNetworkCheckoutServer(
                itemReferences,
                senderClientId);
            SendNetworkCheckoutResult(senderClientId, result);
        }

        private ShopPurchaseResult TryCompleteNetworkCheckoutServer(
            NetworkObjectReference[] itemReferences,
            ulong senderClientId)
        {
            if (!IsSpawned
                || !IsServer
                || !IsTriggerConfigured()
                || networkPurchaseReceiptService == null)
            {
                return RejectNetworkCompletion(
                    "server_contract",
                    senderClientId);
            }

            if (itemReferences == null
                || itemReferences.Length == 0
                || itemReferences.Length > MaximumItemsPerCheckout)
            {
                return RejectNetworkCompletion(
                    "request_count_invalid",
                    senderClientId);
            }

            var networkManager = NetworkManager;
            if (networkManager == null
                || !networkManager.ConnectedClients.TryGetValue(
                    senderClientId,
                    out var senderClient)
                || senderClient.PlayerObject == null
                || senderClient.PlayerObject.gameObject.scene != gameObject.scene)
            {
                return RejectNetworkCompletion(
                    "sender_player_invalid",
                    senderClientId);
            }

            var senderPosition = senderClient.PlayerObject.transform.position;
            var closestCheckoutPoint = checkoutTrigger.ClosestPoint(senderPosition);
            if ((senderPosition - closestCheckoutPoint).sqrMagnitude
                > maximumServerCheckoutDistance * maximumServerCheckoutDistance)
            {
                return RejectNetworkCompletion(
                    "sender_too_far",
                    senderClientId);
            }

            RefreshCheckoutItemsFromZone();
            var validatedNetworkObjects =
                new NetworkObject[itemReferences.Length];
            var validatedProducts =
                new ShopProductData[itemReferences.Length];
            var validatedPurchaseIds = new string[itemReferences.Length];
            var uniqueNetworkObjectIds = new HashSet<ulong>();
            var uniquePurchaseIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < itemReferences.Length; index++)
            {
                if (!itemReferences[index].TryGet(out var itemNetworkObject) ||
                    itemNetworkObject == null ||
                    !itemNetworkObject.IsSpawned)
                {
                    return RejectNetworkCompletion(
                        "item_reference_invalid",
                        senderClientId,
                        index);
                }

                if (itemNetworkObject == NetworkObject
                    || itemNetworkObject.gameObject.scene != gameObject.scene
                    || !uniqueNetworkObjectIds.Add(
                        itemNetworkObject.NetworkObjectId))
                {
                    return RejectNetworkCompletion(
                        "item_network_identity_invalid",
                        senderClientId,
                        index);
                }

                var itemObject =
                    itemNetworkObject.GetComponent<UtilityItemObject>();
                if (itemObject == null
                    || !checkoutItems.Contains(itemObject)
                    || !TryResolveProduct(
                        itemObject,
                        out var productData,
                        true))
                {
                    return RejectNetworkCompletion(
                        "item_not_in_checkout_snapshot",
                        senderClientId,
                        index);
                }

                var purchaseId =
                    CreateNetworkPurchaseId(
                        itemNetworkObject.NetworkObjectId);
                if (!uniquePurchaseIds.Add(purchaseId))
                {
                    return RejectNetworkCompletion(
                        "purchase_id_duplicate",
                        senderClientId,
                        index);
                }

                validatedNetworkObjects[index] = itemNetworkObject;
                validatedProducts[index] = productData;
                validatedPurchaseIds[index] = purchaseId;
            }

            if (!networkPurchaseReceiptService.TryCommitCheckoutPurchaseServer(
                    senderClientId,
                    validatedPurchaseIds,
                    validatedProducts,
                    out var purchaseResult))
            {
                return RejectNetworkCompletion(
                    purchaseResult.Reason ?? "purchase_rejected",
                    senderClientId,
                    result: purchaseResult);
            }

            PlayCheckoutTeleportEffectClientRpc();
            foreach (var itemNetworkObject in validatedNetworkObjects)
            {
                var itemObject =
                    itemNetworkObject.GetComponent<UtilityItemObject>();
                if (itemObject != null)
                {
                    checkoutItems.Remove(itemObject);
                }

                itemNetworkObject.Despawn(true);
            }

            Debug.Log(
                $"PHS_SHOP_CHECKOUT_NETWORK_COMPLETED zone={name} purchaser={senderClientId} itemCount={validatedNetworkObjects.Length}",
                this);
            return purchaseResult;
        }

        private void SendNetworkCheckoutResult(
            ulong targetClientId,
            ShopPurchaseResult result)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
            CompleteNetworkCheckoutClientRpc(
                result.Success,
                new FixedString64Bytes(result.Reason ?? string.Empty),
                result.TotalPrice,
                result.PurchasedCount,
                clientRpcParams);
        }

        [ClientRpc]
        private void CompleteNetworkCheckoutClientRpc(
            bool success,
            FixedString64Bytes reason,
            int totalPrice,
            int purchasedCount,
            ClientRpcParams clientRpcParams = default)
        {
            HandleNetworkCheckoutResult(new ShopPurchaseResult(
                success,
                reason.IsEmpty ? null : reason.ToString(),
                totalPrice,
                purchasedCount));
        }

        private void HandleNetworkCheckoutResult(ShopPurchaseResult result)
        {
            checkoutPending = false;
            if (!result.Success)
            {
                var status = result.Reason switch
                {
                    "insufficient_credits" => "NOT ENOUGH CR",
                    "out_of_stock" => "ITEM SOLD OUT",
                    "sender_too_far" => "MOVE CLOSER",
                    _ => "PURCHASE FAILED"
                };
                ShowTemporaryStatus(status, true);
                PlayAudioCue(NetworkAudioCue.ShopFailure);
                return;
            }

            Debug.Log(
                $"PHS_SHOP_CHECKOUT_COMPLETED zone={name} totalPrice={result.TotalPrice} itemCount={result.PurchasedCount}",
                this);
            ShowTemporaryStatus(
                $"PAID {result.TotalPrice} CR\nSHIP DELIVERY");
        }

        [ClientRpc]
        private void PlayCheckoutTeleportEffectClientRpc()
        {
            PlayCheckoutTeleportEffect();
            PlayAudioCue(NetworkAudioCue.ShopSuccess);
        }

        private void PlayAudioCue(NetworkAudioCue cue)
        {
            if (audioCuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_AUDIO_PLAY_FAILED reason=cue_player_missing zone={name} cue={cue}",
                    this);
                return;
            }

            if (!audioCuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_SHOP_AUDIO_PLAY_FAILED reason={reason} zone={name} cue={cue}",
                    this);
            }
        }

        private ShopPurchaseResult RejectNetworkCompletion(
            string reason,
            ulong senderClientId,
            int itemIndex = -1,
            ShopPurchaseResult result = default)
        {
            Debug.LogWarning(
                $"PHS_SHOP_CHECKOUT_NETWORK_REJECTED reason={reason} zone={name} sender={senderClientId} index={itemIndex}",
                this);
            return string.IsNullOrWhiteSpace(result.Reason)
                ? new ShopPurchaseResult(false, reason, 0, 0)
                : result;
        }

        private static string CreateNetworkPurchaseId(
            ulong networkObjectId)
        {
            return $"checkout:{networkObjectId}";
        }

        private void PlayCheckoutTeleportEffect()
        {
            if (teleportEffectAnchor == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_EFFECT_FAILED reason=teleport_effect_anchor_missing zone={name}", this);
                return;
            }

            PlayTeleportEffect(teleportEffectAnchor.position);
        }

        private void PlayTeleportEffect(Vector3 position)
        {
            if (teleportEffectPrefab == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_EFFECT_FAILED reason=teleport_effect_missing zone={name}", this);
                return;
            }

            var effectInstance = Instantiate(teleportEffectPrefab, position, Quaternion.identity);
            effectInstance.transform.localScale = Vector3.one * teleportEffectScale;
            foreach (var particleSystem in effectInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particleSystem.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                particleSystem.Play(true);
            }

            Destroy(effectInstance, teleportEffectDuration);
        }

        private bool BuildCheckoutSnapshot(List<CheckoutEntry> entries, out int totalPrice, bool shouldLog)
        {
            totalPrice = 0;
            RemoveMissingItems();

            foreach (var itemObject in checkoutItems)
            {
                if (!TryResolveProduct(itemObject, out var productData, shouldLog))
                {
                    continue;
                }

                entries?.Add(new CheckoutEntry(itemObject, productData));
                totalPrice += productData.PurchasePrice;
            }

            return totalPrice > 0;
        }

        private bool TryResolveProduct(UtilityItemObject itemObject, out ShopProductData productData, bool shouldLog)
        {
            productData = null;
            if (itemObject == null || itemObject.IsHeld)
            {
                return false;
            }

            var itemPrefabData = itemObject.ItemPrefabData;
            if (itemPrefabData == null)
            {
                if (shouldLog)
                {
                    Debug.LogError($"PHS_SHOP_CHECKOUT_ITEM_FAILED reason=item_data_missing zone={name} item={itemObject.name}");
                }

                return false;
            }

            if (catalog == null || !catalog.TryGetByItemData(itemPrefabData, out productData))
            {
                if (shouldLog)
                {
                    Debug.LogWarning($"PHS_SHOP_CHECKOUT_ITEM_IGNORED reason=product_missing zone={name} item={itemPrefabData.ItemId}");
                }

                return false;
            }

            return productData.IsConfigured;
        }

        private int CalculateTotalPrice()
        {
            BuildCheckoutSnapshot(null, out var totalPrice, false);
            return totalPrice;
        }

        private void RefreshPriceText(bool force)
        {
            if (priceText == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(temporaryStatus) && Time.unscaledTime < temporaryStatusUntil)
            {
                SetPurchaseUnavailable(temporaryPurchaseUnavailable);
                priceText.text = temporaryPurchaseUnavailable
                    ? $"{pricePrefix} ${CalculateTotalPrice()}"
                    : temporaryStatus;
                return;
            }

            temporaryStatus = string.Empty;
            temporaryPurchaseUnavailable = false;
            var totalPrice = CalculateTotalPrice();
            var availableCredits = purchaseService?.AvailableCredits ?? -1;
            SetPurchaseUnavailable(totalPrice > 0 &&
                availableCredits >= 0 &&
                totalPrice > availableCredits);
            if (!force &&
                totalPrice == lastDisplayedPrice &&
                availableCredits == lastDisplayedCredits)
            {
                return;
            }

            lastDisplayedPrice = totalPrice;
            lastDisplayedCredits = availableCredits;
            priceText.text = $"{pricePrefix} ${totalPrice}";
        }

        private void ShowTemporaryStatus(string message, bool showPurchaseUnavailable = false)
        {
            temporaryStatus = message;
            temporaryStatusUntil = Time.unscaledTime + statusDuration;
            temporaryPurchaseUnavailable = showPurchaseUnavailable;
            lastDisplayedPrice = -1;
            lastDisplayedCredits = -1;
            RefreshPriceText(true);
        }

        private void SetPurchaseUnavailable(bool isVisible)
        {
            if (purchaseUnavailableText == null)
            {
                return;
            }

            purchaseUnavailableText.gameObject.SetActive(isVisible);
        }

        private bool ValidateSetup()
        {
            var isValid = IsTriggerConfigured();
            if (!isValid)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=checkout_trigger_invalid zone={name}");
            }

            if (catalog == null || catalog.Products.Count == 0)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=catalog_missing zone={name}");
                isValid = false;
            }

            if (purchaseService == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=purchase_service_missing zone={name}");
                isValid = false;
            }

            if (networkPurchaseReceiptService == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=network_receipt_service_missing zone={name}",
                    this);
                isValid = false;
            }

            if (purchaseUnavailableText == null)
            {
                Debug.LogError($"PHS_SHOP_CHECKOUT_SETUP_FAILED reason=purchase_unavailable_text_missing zone={name}");
                isValid = false;
            }

            return isValid;
        }

        private bool IsTriggerConfigured()
        {
            return checkoutTrigger != null && checkoutTrigger.isTrigger;
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }

        private void RemoveMissingItems()
        {
            checkoutItems.RemoveWhere(itemObject => itemObject == null);
        }

        private void RefreshCheckoutItemsFromZone()
        {
            if (checkoutTrigger == null)
            {
                return;
            }

            checkoutItems.Clear();
            var center = checkoutTrigger.transform.TransformPoint(checkoutTrigger.center);
            var halfExtents = Vector3.Scale(checkoutTrigger.size, checkoutTrigger.transform.lossyScale) * 0.5f;
            var colliders = Physics.OverlapBox(
                center,
                halfExtents,
                checkoutTrigger.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (var itemCollider in colliders)
            {
                var itemObject = itemCollider.GetComponentInParent<UtilityItemObject>();
                if (itemObject != null)
                {
                    checkoutItems.Add(itemObject);
                }
            }
        }
    }
}
