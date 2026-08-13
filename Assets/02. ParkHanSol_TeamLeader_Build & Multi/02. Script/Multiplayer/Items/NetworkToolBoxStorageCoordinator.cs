using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;
using UtilityItemDataSO = LastJumpCrew.Common.UtilityItemDataSO;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>Server-authoritative storage state for every slot under one ToolBox root.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkToolBoxStorageCoordinator : NetworkBehaviour
    {
        [Header("Catalog")]
        [SerializeField] private UtilityItemCatalogSO itemCatalog;

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 4f;

        private UtilityToolBoxStorageSlotInteractable[] slots;
        private string[] slotKeys;
        private NetworkPersistentToolBoxStorage persistentStorage;

        private void Awake()
        {
            slots = GetComponentsInChildren<UtilityToolBoxStorageSlotInteractable>(true);
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].BindNetworkCoordinator(this, index);
            }

            slotKeys = new string[slots.Length];
            for (var index = 0; index < slots.Length; index++)
            {
                slotKeys[index] = BuildSlotKey(index);
            }
        }

        public override void OnNetworkSpawn()
        {
            NetworkRunSessionRoot.InstanceAvailable += HandleSessionRootAvailable;
            if (!TryBindPersistentStorage(NetworkRunSessionRoot.Instance))
            {
                Debug.Log(
                    $"PHS_TOOL_BOX_NETWORK_BIND_PENDING box={name}",
                    this);
            }
        }

        public override void OnNetworkDespawn()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleSessionRootAvailable;
            if (persistentStorage != null)
            {
                persistentStorage.SlotChanged -= HandlePersistentSlotChanged;
            }
            base.OnNetworkDespawn();
        }

        private void HandleSessionRootAvailable(NetworkRunSessionRoot root)
        {
            if (!TryBindPersistentStorage(root))
            {
                Debug.LogError(
                    $"PHS_TOOL_BOX_NETWORK_SETUP_FAILED reason=persistent_storage_missing box={name}",
                    this);
            }
        }

        private bool TryBindPersistentStorage(NetworkRunSessionRoot root)
        {
            var storage = root?.ToolBoxStorage;
            if (storage == null || !storage.IsSpawned)
            {
                return false;
            }

            if (persistentStorage == storage)
            {
                return true;
            }

            if (persistentStorage != null)
            {
                persistentStorage.SlotChanged -= HandlePersistentSlotChanged;
            }

            persistentStorage = storage;
            persistentStorage.SlotChanged += HandlePersistentSlotChanged;
            if (IsServer)
            {
                InitializeServerStates();
            }

            ApplyAllStates();
            return true;
        }

        public bool IsManaging(UtilityToolBoxStorageSlotInteractable slot)
        {
            return slot != null && Array.IndexOf(slots, slot) >= 0;
        }

        public bool CanRequestInteraction(
            UtilityToolBoxStorageSlotInteractable slot,
            IItemHolder itemHolder)
        {
            if (!TryGetSlotIndex(slot, out var slotIndex)
                || itemHolder is not Component holderComponent
                || !holderComponent.TryGetComponent<NetworkPlayerItemLifecycle>(out var lifecycle)
                || !lifecycle.IsSpawned
                || !lifecycle.IsOwner
                || !TryGetState(slotIndex, out var state))
            {
                return false;
            }

            return !state.IsEmpty || itemHolder.CurrentItemPrefabData != null;
        }

        public void RequestInteraction(
            UtilityToolBoxStorageSlotInteractable slot,
            IItemHolder itemHolder)
        {
            if (!TryGetSlotIndex(slot, out var slotIndex)
                || itemHolder is not Component holderComponent
                || !holderComponent.TryGetComponent<NetworkPlayerItemLifecycle>(out var lifecycle)
                || !lifecycle.IsSpawned
                || !lifecycle.IsOwner)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_NETWORK_REJECTED reason=local_contract box={name}", this);
                return;
            }

            if (IsServer)
            {
                TryInteractServer(lifecycle.OwnerClientId, slotIndex);
                return;
            }

            RequestInteractionServerRpc(slotIndex);
        }

        public bool TryReceiveDeliveryServer(
            UtilityToolBoxStorageSlotInteractable slot,
            UtilityItemDataSO itemPrefabData)
        {
            if (!IsSpawned || !IsServer || itemPrefabData == null
                || !TryGetSlotIndex(slot, out var slotIndex)
                || !TryGetState(slotIndex, out var state)
                || !state.IsEmpty)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_DELIVERY_REJECTED reason=server_contract box={name}", this);
                return false;
            }

            var durability = itemPrefabData.UsesDurability ? itemPrefabData.MaxDurability : 0;
            if (!persistentStorage.TryReceiveDeliveryServer(
                    slotKeys[slotIndex], itemPrefabData.ItemId, durability))
            {
                return false;
            }
            Debug.Log($"PHS_TOOL_BOX_DELIVERY_STORED box={name} slot={slotIndex} item={itemPrefabData.ItemId}", this);
            return true;
        }

        public bool TryResolveItemData(string itemId, out UtilityItemDataSO itemPrefabData)
        {
            itemPrefabData = null;
            if (itemCatalog == null || string.IsNullOrWhiteSpace(itemId)
                || !itemCatalog.TryGetById(itemId, out itemPrefabData))
            {
                Debug.LogError($"PHS_TOOL_BOX_NETWORK_SETUP_FAILED reason=item_catalog_or_id box={name} item={itemId}", this);
                return false;
            }

            return true;
        }

        private void InitializeServerStates()
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogError($"PHS_TOOL_BOX_NETWORK_SETUP_FAILED reason=slots_missing box={name}", this);
                return;
            }

            for (var index = 0; index < slots.Length; index++)
            {
                var slot = slots[index];
                var initialItem = slot.InitialStoredItemPrefabData;
                persistentStorage.TryEnsureSlotServer(
                    slotKeys[index],
                    initialItem == null ? string.Empty : initialItem.ItemId,
                    initialItem != null && initialItem.UsesDurability
                        ? initialItem.MaxDurability
                        : 0);
            }
        }

        private void HandlePersistentSlotChanged(string slotKey)
        {
            var slotIndex = Array.IndexOf(slotKeys, slotKey);
            if (slotIndex >= 0)
            {
                ApplySlotState(slotIndex);
            }
        }

        private void ApplyAllStates()
        {
            for (var index = 0; index < slots.Length; index++)
            {
                ApplySlotState(index);
            }
        }

        private void ApplySlotState(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length
                || !TryGetState(slotIndex, out var state))
            {
                Debug.LogError($"PHS_TOOL_BOX_NETWORK_SYNC_FAILED reason=slot_index box={name} slot={slotIndex}", this);
                return;
            }

            if (state.IsEmpty)
            {
                slots[slotIndex].ApplyNetworkStoredItem(null);
                return;
            }

            if (TryResolveItemData(state.ItemId, out var itemData))
            {
                slots[slotIndex].ApplyNetworkStoredItem(itemData);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(
            int slotIndex,
            ServerRpcParams rpcParams = default)
        {
            TryInteractServer(rpcParams.Receive.SenderClientId, slotIndex);
        }

        private bool TryInteractServer(ulong senderClientId, int slotIndex)
        {
            if (!IsSpawned || !IsServer || slotIndex < 0 || slotIndex >= slots.Length
                || !TryGetPlayerLifecycle(senderClientId, out var lifecycle, out var record)
                || !IsWithinDistance(lifecycle.transform.position, slots[slotIndex].transform.position)
                || !TryGetState(slotIndex, out var state))
            {
                Debug.LogWarning($"PHS_TOOL_BOX_NETWORK_REJECTED reason=server_contract box={name} sender={senderClientId} slot={slotIndex}", this);
                return false;
            }

            var heldItemId = record.HeldItemId;
            if (string.IsNullOrEmpty(heldItemId))
            {
                return TryTakeServer(slotIndex, state, record, senderClientId);
            }

            if (!lifecycle.ItemCatalog.TryGetById(heldItemId, out var heldItemData))
            {
                Debug.LogError($"PHS_TOOL_BOX_NETWORK_REJECTED reason=held_item_catalog box={name} item={heldItemId}", this);
                return false;
            }

            return state.IsEmpty
                ? TryStoreServer(slotIndex, heldItemData, record, senderClientId)
                : TrySwapServer(slotIndex, state, heldItemData, record, senderClientId);
        }

        private bool TryTakeServer(int slotIndex, NetworkPersistentToolBoxStorage.SlotSnapshot state, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            if (state.IsEmpty)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_NETWORK_REJECTED reason=take_empty box={name} sender={senderClientId} slot={slotIndex}", this);
                return false;
            }

            if (!persistentStorage.TryTakeServer(slotKeys[slotIndex], record))
            {
                return false;
            }

            return true;
        }

        private bool TryStoreServer(int slotIndex, UtilityItemDataSO heldItem, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            if (!persistentStorage.TryStoreServer(slotKeys[slotIndex], heldItem, record))
            {
                return false;
            }

            Debug.Log($"PHS_TOOL_BOX_NETWORK_STORED box={name} sender={senderClientId} slot={slotIndex} item={heldItem.ItemId}", this);
            return true;
        }

        private bool TrySwapServer(int slotIndex, NetworkPersistentToolBoxStorage.SlotSnapshot storedState, UtilityItemDataSO heldItem, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            if (!persistentStorage.TrySwapServer(slotKeys[slotIndex], heldItem, record))
            {
                return false;
            }

            Debug.Log($"PHS_TOOL_BOX_NETWORK_SWAPPED box={name} sender={senderClientId} slot={slotIndex} stored={heldItem.ItemId} held={storedState.ItemId}", this);
            return true;
        }

        private bool TryGetPlayerLifecycle(ulong clientId, out NetworkPlayerItemLifecycle lifecycle, out NetworkPlayerItemRecord record)
        {
            lifecycle = null;
            record = null;
            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                || client.PlayerObject == null
                || !client.PlayerObject.TryGetComponent(out lifecycle)
                || !client.PlayerObject.TryGetComponent(out record)
                || lifecycle.OwnerClientId != clientId
                || !record.IsSpawned)
            {
                return false;
            }

            return true;
        }

        private bool TryGetSlotIndex(UtilityToolBoxStorageSlotInteractable slot, out int slotIndex)
        {
            slotIndex = slots == null ? -1 : Array.IndexOf(slots, slot);
            return slotIndex >= 0;
        }

        private bool IsWithinDistance(Vector3 playerPosition, Vector3 slotPosition)
        {
            return (playerPosition - slotPosition).sqrMagnitude
                <= serverInteractionDistance * serverInteractionDistance;
        }

        private bool TryGetState(
            int slotIndex,
            out NetworkPersistentToolBoxStorage.SlotSnapshot state)
        {
            state = default;
            return persistentStorage != null
                && slotIndex >= 0
                && slotIndex < slotKeys.Length
                && persistentStorage.TryGetSlot(slotKeys[slotIndex], out state);
        }

        public bool TryGetStoredState(
            UtilityToolBoxStorageSlotInteractable slot,
            out string itemId,
            out int durability,
            out uint revision)
        {
            itemId = string.Empty;
            durability = 0;
            revision = 0U;
            if (!TryGetSlotIndex(slot, out var slotIndex)
                || !TryGetState(slotIndex, out var state))
            {
                return false;
            }

            itemId = state.ItemId;
            durability = state.Durability;
            revision = state.Revision;
            return true;
        }

        private string BuildSlotKey(int slotIndex)
        {
            var path = name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return gameObject.scene.name + ":" + path + ":" + slotIndex;
        }
    }
}
