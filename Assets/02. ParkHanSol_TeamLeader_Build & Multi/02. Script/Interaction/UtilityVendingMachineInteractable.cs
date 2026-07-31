using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 자판기 오브젝트에 붙는 상호작용 컴포넌트다.
    // 연결된 UtilityVendingMachineData의 아이템을 플레이어 손에 지급한다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class UtilityVendingMachineInteractable : NetworkBehaviour, IInteractable, LastJumpCrew.Common.IInteractable
    {
        // 어떤 아이템을 지급할지 담은 ScriptableObject다. Inspector에서 직접 연결한다.
        [SerializeField] private UtilityVendingMachineData vendingMachineData;

        // 상호작용 UI에 보여줄 행동 문구다. 입력 키는 HUD 배지가 별도로 표시한다.
        [SerializeField] private string interactionPrompt = "물품 받기";

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 3f;

        private bool requestPending;

        public string InteractionPrompt => interactionPrompt;
        public UtilityVendingMachineData VendingMachineData => vendingMachineData;

        public bool CanInteract(IItemHolder itemHolder)
        {
            // 지급 전에 holder와 데이터 참조를 모두 검사해서 Inspector 연결 누락을 로그로 드러낸다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            if (IsNetworkSessionActive())
            {
                return CanRequestNetworkItem(itemHolder);
            }

            return itemHolder.CanReplaceHeldItem(itemPrefabData);
        }

        public void Interact(IItemHolder itemHolder)
        {
            // 실제 지급 시에도 CanInteract와 같은 검사를 반복한다.
            // 외부에서 CanInteract 없이 바로 호출해도 잘못된 참조가 조용히 통과하지 않게 한다.
            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return;
            }

            if (IsNetworkSessionActive())
            {
                RequestNetworkItem(itemHolder);
                return;
            }

            if (!itemHolder.CanReplaceHeldItem(itemPrefabData))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={itemPrefabData.ItemId}");
                return;
            }

            itemHolder.ReplaceHeldItem(itemPrefabData, transform);
        }

        bool LastJumpCrew.Common.IInteractable.CanInteract(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            // 공용 Common 인터페이스를 쓰는 다른 시스템에서도 같은 자판기를 사용할 수 있게 연결한다.
            if (itemHolder is TempPlayerItemHolder networkHolder
                && IsNetworkSessionActive())
            {
                return CanInteract(networkHolder);
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return false;
            }

            if (!TryGetCommonItem(out var item))
            {
                return false;
            }

            return itemHolder.CanHold(item);
        }

        void LastJumpCrew.Common.IInteractable.Interact(LastJumpCrew.Common.IItemHolder itemHolder)
        {
            if (itemHolder is TempPlayerItemHolder networkHolder
                && IsNetworkSessionActive())
            {
                Interact(networkHolder);
                return;
            }

            if (itemHolder == null)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_missing target={name}");
                return;
            }

            if (!TryGetCommonItem(out var item))
            {
                return;
            }

            if (!itemHolder.CanHold(item))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=itemHolder_rejected target={name} item={item.ItemId}");
                return;
            }

            itemHolder.Hold(item);
        }

        private bool IsNetworkSessionActive()
        {
            return NetworkManager != null && NetworkManager.IsListening;
        }

        private bool CanRequestNetworkItem(IItemHolder itemHolder)
        {
            if (requestPending || !IsSpawned)
            {
                return false;
            }

            if (itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() is not { IsSpawned: true, IsOwner: true }
                || holderComponent.GetComponent<NetworkPlayerItemLifecycle>() is not { IsSpawned: true, IsOwner: true })
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=network_player_missing target={name}");
                return false;
            }

            return true;
        }

        private void RequestNetworkItem(IItemHolder itemHolder)
        {
            if (!CanRequestNetworkItem(itemHolder))
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason=network_request_unavailable target={name}");
                return;
            }

            requestPending = true;
            if (IsServer)
            {
                CompleteNetworkItemRequest(OwnerClientId);
                requestPending = false;
                return;
            }

            RequestItemServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestItemServerRpc(ServerRpcParams rpcParams = default)
        {
            CompleteNetworkItemRequest(rpcParams.Receive.SenderClientId);
        }

        private void CompleteNetworkItemRequest(ulong senderClientId)
        {
            var success = TryGrantItemOnServer(senderClientId, out var reason);
            SendNetworkItemResult(senderClientId, success, reason);
        }

        private bool TryGrantItemOnServer(ulong senderClientId, out string reason)
        {
            reason = null;
            if (!IsSpawned || !IsServer || NetworkManager == null)
            {
                reason = "server_required";
                return false;
            }

            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                reason = "vending_data_missing";
                return false;
            }

            if (!NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                reason = "sender_player_missing";
                return false;
            }

            var playerObject = client.PlayerObject;
            var itemLifecycle = playerObject.GetComponent<NetworkPlayerItemLifecycle>();
            if (itemLifecycle == null
                || !itemLifecycle.IsSpawned
                || itemLifecycle.ItemCatalog == null
                || !itemLifecycle.ItemCatalog.Contains(itemPrefabData))
            {
                reason = "item_catalog_rejected";
                return false;
            }

            if (playerObject.gameObject.scene != gameObject.scene)
            {
                reason = "scene_mismatch";
                return false;
            }

            if ((playerObject.transform.position - transform.position).sqrMagnitude
                > serverInteractionDistance * serverInteractionDistance)
            {
                reason = "distance";
                return false;
            }

            if (!itemLifecycle.TryAssignHeldItemServer(itemPrefabData))
            {
                reason = "held_item_replace_rejected";
                return false;
            }

            Debug.Log($"PHS_VENDING_ITEM_GRANTED target={name} clientId={senderClientId} item={itemPrefabData.ItemId}", this);
            return true;
        }

        private void SendNetworkItemResult(ulong targetClientId, bool success, string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
            CompleteItemRequestClientRpc(
                success,
                new FixedString64Bytes(reason ?? string.Empty),
                clientRpcParams);
        }

        [ClientRpc]
        private void CompleteItemRequestClientRpc(
            bool success,
            FixedString64Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            requestPending = false;
            if (!success)
            {
                Debug.LogWarning($"PHS_VENDING_INTERACT_FAILED reason={reason} target={name}", this);
            }
        }

        private bool TryGetItemPrefabData(out UtilityItemPrefabData itemPrefabData)
        {
            itemPrefabData = null;

            // 자판기 asset 또는 asset 내부 아이템 참조가 빠지면 지급할 대상이 없으므로 실패 처리한다.
            if (vendingMachineData == null)
            {
                Debug.LogWarning($"PHS_VENDING_DATA_MISSING target={name}");
                return false;
            }

            itemPrefabData = vendingMachineData.ItemPrefabData;
            if (itemPrefabData == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_DATA_MISSING target={name} vendingData={vendingMachineData.name}");
                return false;
            }

            return true;
        }

        private bool TryGetCommonItem(out LastJumpCrew.Common.IHoldableItem item)
        {
            item = null;

            // Common 경로는 프리팹 루트에 IHoldableItem 구현체가 있어야 한다.
            if (!TryGetItemPrefabData(out var itemPrefabData))
            {
                return false;
            }

            if (itemPrefabData.HeldPrefab == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_PREFAB_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            item = itemPrefabData.HeldPrefab.GetComponent<LastJumpCrew.Common.IHoldableItem>();
            if (item == null)
            {
                Debug.LogWarning($"PHS_VENDING_ITEM_CONTRACT_MISSING target={name} item={itemPrefabData.ItemId}");
                return false;
            }

            return true;
        }
    }
}
