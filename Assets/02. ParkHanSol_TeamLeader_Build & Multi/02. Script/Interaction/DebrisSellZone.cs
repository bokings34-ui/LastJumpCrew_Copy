using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Shop;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class DebrisSellZone : NetworkBehaviour
    {
        [SerializeField] private BoxCollider sellTrigger;
        [SerializeField] private MonoBehaviour shopWalletSource;
        [SerializeField] private UtilityItemDataSO[] sellableDebris;
        [SerializeField] private string debrisTag = "Debris";
        [SerializeField, Min(0.1f)] private float maximumSaleDistance = 6f;
        [SerializeField, Min(0.05f)] private float retrySeconds = 0.25f;

        private readonly HashSet<DebrisItem> pendingItems = new();
        private readonly HashSet<string> soldItemIds = new();
        private readonly HashSet<string> completedNetworkSales = new();
        private IShopWallet shopWallet;
        private bool networkSalePending;
        private float nextNetworkSaleTime;

        private void Awake()
        {
            shopWallet = shopWalletSource as IShopWallet;
            ValidateSetup();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryResolveDebrisForSale(other, out var debrisItem))
            {
                return;
            }

            if (IsNetworkSessionActive())
            {
                TryRequestNetworkSale(debrisItem);
                return;
            }

            pendingItems.Add(debrisItem);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!IsNetworkSessionActive() || networkSalePending || Time.unscaledTime < nextNetworkSaleTime)
            {
                return;
            }

            if (TryResolveDebrisForSale(other, out var debrisItem))
            {
                TryRequestNetworkSale(debrisItem);
            }
        }

        private void FixedUpdate()
        {
            if (IsNetworkSessionActive())
            {
                pendingItems.Clear();
                return;
            }

            if (pendingItems.Count == 0)
            {
                return;
            }

            foreach (var debrisItem in pendingItems)
            {
                if (debrisItem == null)
                {
                    continue;
                }

                var debrisInstanceId = debrisItem.GetEntityId().ToString();
                if (soldItemIds.Contains(debrisInstanceId))
                {
                    Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=duplicate_sale zone={name} debris={debrisItem.name}");
                    continue;
                }

                var consumedHeldDebris = TryConsumeHeldDebris(debrisItem, out var isHeldDebris);
                if (isHeldDebris && !consumedHeldDebris)
                {
                    // 손에 든 아이템은 Holder 상태까지 함께 비워져야 한다.
                    // 소비에 실패한 상태에서 모델만 제거하면 다음 획득 때 이전 아이템이 다시 생성된다.
                    continue;
                }

                if (!shopWallet.TryAddCredits(debrisItem.Value))
                {
                    Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=wallet_rejected zone={name} debris={debrisItem.name} value={debrisItem.Value}");
                    continue;
                }

                soldItemIds.Add(debrisInstanceId);
                Debug.Log($"PHS_DEBRIS_SOLD zone={name} debris={debrisItem.name} value={debrisItem.Value}");

                // 월드 데브리만 여기서 직접 제거한다. 손에 든 데브리는 Holder가
                // 모델, 보유 데이터, HUD를 한 번에 정리한다.
                if (!isHeldDebris)
                {
                    Destroy(debrisItem.gameObject);
                }
            }

            pendingItems.Clear();
        }

        private void TryRequestNetworkSale(DebrisItem debrisItem)
        {
            if (networkSalePending || Time.unscaledTime < nextNetworkSaleTime)
            {
                return;
            }

            if (!IsSpawned)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=zone_not_spawned zone={name}", this);
                nextNetworkSaleTime = Time.unscaledTime + retrySeconds;
                return;
            }

            var itemObject = debrisItem.GetComponentInParent<UtilityItemObject>();
            var itemHolder = debrisItem.GetComponentInParent<TempPlayerItemHolder>();
            var playerNetworkObject = itemHolder == null ? null : itemHolder.GetComponent<NetworkObject>();
            var itemData = itemObject == null ? null : itemObject.ItemData;
            if (itemObject == null || itemData == null || string.IsNullOrWhiteSpace(itemData.ItemId))
            {
                return;
            }

            if (!itemObject.IsHeld)
            {
                TryCompleteWorldDebrisSale(debrisItem, itemData);
                return;
            }

            if (itemHolder == null || playerNetworkObject == null || !playerNetworkObject.IsOwner)
            {
                return;
            }

            networkSalePending = true;
            nextNetworkSaleTime = Time.unscaledTime + retrySeconds;
            RequestSaleServerRpc(new FixedString64Bytes(itemData.ItemId));
        }

        private void TryCompleteWorldDebrisSale(DebrisItem debrisItem, UtilityItemDataSO itemData)
        {
            if (!IsServer || debrisItem == null || itemData == null || !ValidateSetup() || !shopWallet.IsReady)
            {
                return;
            }

            var saleKey = $"debris_sale:world:{debrisItem.GetEntityId()}";
            if (!completedNetworkSales.Add(saleKey))
            {
                return;
            }

            if (!TryCommitNetworkSaleCredit(
                    saleKey,
                    itemData.Price,
                    NetworkManager.ServerClientId,
                    out var creditReason))
            {
                completedNetworkSales.Remove(saleKey);
                Debug.LogError(
                    $"PHS_DEBRIS_SELL_FAILED reason={creditReason} zone={name} debris={debrisItem.name} value={itemData.Price}",
                    this);
                return;
            }

            Debug.Log($"PHS_DEBRIS_SOLD zone={name} debris={debrisItem.name} value={itemData.Price} method=thrown");
            var debrisNetworkObject = debrisItem.GetComponent<NetworkObject>();
            if (debrisNetworkObject != null && debrisNetworkObject.IsSpawned)
            {
                debrisNetworkObject.Despawn(true);
            }
            else
            {
                Destroy(debrisItem.gameObject);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSaleServerRpc(
            FixedString64Bytes requestedItemId,
            ServerRpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            var itemId = requestedItemId.ToString();
            var success = TryCompleteNetworkSale(senderClientId, itemId, out var reason);
            SendSaleResult(senderClientId, requestedItemId, success, reason);
        }

        private bool TryCompleteNetworkSale(ulong senderClientId, string itemId, out string reason)
        {
            reason = null;
            if (!ValidateSetup() || !shopWallet.IsReady)
            {
                reason = "wallet_not_ready";
                return false;
            }

            if (!TryResolveSellableDebris(itemId, out var itemData) || itemData.Price <= 0)
            {
                reason = "item_not_sellable";
                return false;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.ConnectedClients.TryGetValue(senderClientId, out var client) ||
                client.PlayerObject == null)
            {
                reason = "player_missing";
                return false;
            }

            if ((client.PlayerObject.transform.position - sellTrigger.transform.position).sqrMagnitude >
                maximumSaleDistance * maximumSaleDistance)
            {
                reason = "player_too_far";
                return false;
            }

            var itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null || itemRecord.HeldItemId != itemId)
            {
                reason = "held_item_mismatch";
                return false;
            }

            var saleRevision = itemRecord.Revision;
            var saleDurability = itemRecord.CurrentDurability;
            var saleKey = $"debris_sale:held:{senderClientId}:{saleRevision}";
            if (!completedNetworkSales.Add(saleKey))
            {
                reason = "duplicate_sale";
                return false;
            }

            if (!itemRecord.TryConsumeHeldItemServer(itemId, saleRevision))
            {
                completedNetworkSales.Remove(saleKey);
                reason = "record_consume_failed";
                return false;
            }

            if (!TryCommitNetworkSaleCredit(
                    saleKey,
                    itemData.Price,
                    senderClientId,
                    out var creditReason))
            {
                if (!itemRecord.TrySetHeldItemServer(
                        itemId,
                        saleDurability,
                        itemRecord.Revision))
                {
                    Debug.LogError(
                        $"PHS_DEBRIS_SELL_INVARIANT_FAILED reason=record_restore_failed zone={name} owner={senderClientId} item={itemId} saleRevision={saleRevision}",
                        this);
                }

                completedNetworkSales.Remove(saleKey);
                Debug.LogError(
                    $"PHS_DEBRIS_SELL_FAILED reason={creditReason} zone={name} owner={senderClientId} item={itemId}",
                    this);
                reason = creditReason;
                return false;
            }

            Debug.Log(
                $"PHS_DEBRIS_SOLD zone={name} owner={senderClientId} item={itemId} value={itemData.Price}",
                this);
            return true;
        }

        private static bool TryCommitNetworkSaleCredit(
            string transactionId,
            int amount,
            ulong actorClientId,
            out string reason)
        {
            var economyLedger = NetworkRunSessionRoot.Instance?.Economy;
            if (economyLedger == null
                || !economyLedger.IsSpawned
                || economyLedger.Revision == 0U)
            {
                reason = "run_economy_ledger_missing";
                return false;
            }

            if (economyLedger.TryAddCreditsServer(
                    transactionId,
                    amount,
                    NetworkRunEconomyTransactionKind.SaleCredit,
                    actorClientId,
                    out reason))
            {
                return true;
            }

            if (reason == "transaction_already_committed")
            {
                reason = "duplicate_sale";
            }

            return false;
        }

        private void SendSaleResult(
            ulong targetClientId,
            FixedString64Bytes itemId,
            bool success,
            string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };

            CompleteSaleClientRpc(
                itemId,
                success,
                new FixedString64Bytes(reason ?? string.Empty),
                clientRpcParams);
        }

        [ClientRpc]
        private void CompleteSaleClientRpc(
            FixedString64Bytes itemId,
            bool success,
            FixedString64Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            networkSalePending = false;
            nextNetworkSaleTime = Time.unscaledTime + retrySeconds;
            if (!success)
            {
                Debug.LogWarning(
                    $"PHS_DEBRIS_SELL_FAILED reason={reason} zone={name} item={itemId}",
                    this);
                return;
            }

            var networkManager = NetworkManager.Singleton;
            var localPlayer = networkManager == null ? null : networkManager.LocalClient?.PlayerObject;
            var itemHolder = localPlayer == null ? null : localPlayer.GetComponent<TempPlayerItemHolder>();
            if (itemHolder == null || !itemHolder.TryConsumeHeldItem(itemId.ToString()))
            {
                Debug.LogError(
                    $"PHS_DEBRIS_SELL_CLIENT_APPLY_FAILED reason=held_item_consume_failed zone={name} item={itemId}",
                    this);
            }
        }

        private bool TryResolveSellableDebris(string itemId, out UtilityItemDataSO itemData)
        {
            itemData = null;
            if (sellableDebris == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            foreach (var candidate in sellableDebris)
            {
                if (candidate != null && candidate.ItemId == itemId)
                {
                    itemData = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetDebrisItem(Collider other, out DebrisItem debrisItem)
        {
            debrisItem = null;

            if (other == null)
            {
                return false;
            }

            var colliderHasDebrisTag = other.CompareTag(debrisTag);
            debrisItem = other.GetComponentInParent<DebrisItem>();
            if (debrisItem == null)
            {
                if (colliderHasDebrisTag)
                {
                    Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=debris_item_missing zone={name} target={other.name}");
                }

                return false;
            }

            if (!debrisItem.CompareTag(debrisTag))
            {
                return false;
            }

            return ValidateSetup();
        }

        private bool TryResolveDebrisForSale(Collider other, out DebrisItem debrisItem)
        {
            if (!TryGetDebrisItem(other, out debrisItem))
            {
                return false;
            }

            var itemObject = debrisItem.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null)
            {
                Debug.LogError(
                    $"PHS_DEBRIS_SELL_FAILED reason=utility_item_missing zone={name} debris={debrisItem.name}");
                return false;
            }

            return true;
        }

        private bool TryConsumeHeldDebris(DebrisItem debrisItem, out bool isHeldDebris)
        {
            isHeldDebris = false;

            var itemObject = debrisItem.GetComponentInParent<UtilityItemObject>();
            if (itemObject == null || !itemObject.IsHeld)
            {
                return false;
            }

            isHeldDebris = true;

            var itemPrefabData = itemObject.ItemPrefabData;
            if (itemPrefabData == null || string.IsNullOrWhiteSpace(itemPrefabData.ItemId))
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_data_missing zone={name} debris={debrisItem.name}");
                return false;
            }

            var itemHolder = debrisItem.GetComponentInParent<TempPlayerItemHolder>();
            if (itemHolder == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_holder_missing zone={name} debris={debrisItem.name}");
                return false;
            }

            if (!itemHolder.TryConsumeHeldItem(itemPrefabData.ItemId))
            {
                Debug.LogError($"PHS_DEBRIS_SELL_FAILED reason=held_item_consume_failed zone={name} debris={debrisItem.name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }

        private bool ValidateSetup()
        {
            if (sellTrigger == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=sell_trigger_missing zone={name}");
                return false;
            }

            if (!sellTrigger.isTrigger)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=sell_trigger_not_trigger zone={name}");
                return false;
            }

            if (shopWallet == null)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=shop_wallet_missing zone={name}");
                return false;
            }

            if (sellableDebris == null || sellableDebris.Length == 0)
            {
                Debug.LogError($"PHS_DEBRIS_SELL_SETUP_FAILED reason=sellable_debris_missing zone={name}");
                return false;
            }

            return true;
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }
    }
}
