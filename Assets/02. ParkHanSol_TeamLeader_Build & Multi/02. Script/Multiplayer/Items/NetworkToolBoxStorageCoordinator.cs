using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>Server-authoritative storage state for every slot under one ToolBox root.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkToolBoxStorageCoordinator : NetworkBehaviour
    {
        private struct SlotState : INetworkSerializable, IEquatable<SlotState>
        {
            public FixedString64Bytes ItemId;
            public int Durability;

            public SlotState(string itemId, int durability)
            {
                ItemId = new FixedString64Bytes(itemId ?? string.Empty);
                Durability = durability;
            }

            public bool IsEmpty => ItemId.IsEmpty;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer)
                where T : IReaderWriter
            {
                serializer.SerializeValue(ref ItemId);
                serializer.SerializeValue(ref Durability);
            }

            public bool Equals(SlotState other)
            {
                return ItemId.Equals(other.ItemId) && Durability == other.Durability;
            }
        }

        [Header("Catalog")]
        [SerializeField] private UtilityItemCatalogSO itemCatalog;

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 3f;

        private UtilityToolBoxStorageSlotInteractable[] slots;
        private readonly NetworkList<SlotState> slotStates = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            slots = GetComponentsInChildren<UtilityToolBoxStorageSlotInteractable>(true);
            for (var index = 0; index < slots.Length; index++)
            {
                slots[index].BindNetworkCoordinator(this, index);
            }
        }

        public override void OnNetworkSpawn()
        {
            slotStates.OnListChanged += HandleSlotStateChanged;
            if (IsServer)
            {
                InitializeServerStates();
            }

            ApplyAllStates();
        }

        public override void OnNetworkDespawn()
        {
            slotStates.OnListChanged -= HandleSlotStateChanged;
            base.OnNetworkDespawn();
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
                || slotIndex >= slotStates.Count)
            {
                return false;
            }

            var state = slotStates[slotIndex];
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
            UtilityItemPrefabData itemPrefabData)
        {
            if (!IsSpawned || !IsServer || itemPrefabData == null
                || !TryGetSlotIndex(slot, out var slotIndex)
                || slotIndex >= slotStates.Count
                || !slotStates[slotIndex].IsEmpty)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_DELIVERY_REJECTED reason=server_contract box={name}", this);
                return false;
            }

            var durability = itemPrefabData.HasDurability ? itemPrefabData.MaxDurability : 0;
            slotStates[slotIndex] = new SlotState(itemPrefabData.ItemId, durability);
            Debug.Log($"PHS_TOOL_BOX_DELIVERY_STORED box={name} slot={slotIndex} item={itemPrefabData.ItemId}", this);
            return true;
        }

        public bool TryResolveItemData(string itemId, out UtilityItemPrefabData itemPrefabData)
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

            if (slotStates.Count != 0)
            {
                return;
            }

            foreach (var slot in slots)
            {
                var initialItem = slot.InitialStoredItemPrefabData;
                slotStates.Add(initialItem == null
                    ? new SlotState(string.Empty, 0)
                    : new SlotState(
                        initialItem.ItemId,
                        initialItem.HasDurability ? initialItem.MaxDurability : 0));
            }
        }

        private void HandleSlotStateChanged(NetworkListEvent<SlotState> changeEvent)
        {
            ApplySlotState(changeEvent.Index);
        }

        private void ApplyAllStates()
        {
            for (var index = 0; index < slotStates.Count; index++)
            {
                ApplySlotState(index);
            }
        }

        private void ApplySlotState(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length
                || slotIndex >= slotStates.Count)
            {
                Debug.LogError($"PHS_TOOL_BOX_NETWORK_SYNC_FAILED reason=slot_index box={name} slot={slotIndex}", this);
                return;
            }

            var state = slotStates[slotIndex];
            if (state.IsEmpty)
            {
                slots[slotIndex].ApplyNetworkStoredItem(null);
                return;
            }

            if (TryResolveItemData(state.ItemId.ToString(), out var itemData))
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
            if (!IsSpawned || !IsServer || slotIndex < 0 || slotIndex >= slotStates.Count
                || slotIndex >= slots.Length
                || !TryGetPlayerLifecycle(senderClientId, out var lifecycle, out var record)
                || !IsWithinDistance(lifecycle.transform.position, slots[slotIndex].transform.position))
            {
                Debug.LogWarning($"PHS_TOOL_BOX_NETWORK_REJECTED reason=server_contract box={name} sender={senderClientId} slot={slotIndex}", this);
                return false;
            }

            var state = slotStates[slotIndex];
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

        private bool TryTakeServer(int slotIndex, SlotState state, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            if (state.IsEmpty)
            {
                Debug.LogWarning($"PHS_TOOL_BOX_NETWORK_REJECTED reason=take_empty box={name} sender={senderClientId} slot={slotIndex}", this);
                return false;
            }

            if (!record.TrySetHeldItemServer(state.ItemId.ToString(), state.Durability, record.Revision))
            {
                return false;
            }

            slotStates[slotIndex] = new SlotState(string.Empty, 0);
            return true;
        }

        private bool TryStoreServer(int slotIndex, UtilityItemPrefabData heldItem, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            var heldDurability = record.CurrentDurability;
            if (!record.TryConsumeHeldItemServer(heldItem.ItemId, record.Revision))
            {
                return false;
            }

            slotStates[slotIndex] = new SlotState(heldItem.ItemId, heldDurability);
            Debug.Log($"PHS_TOOL_BOX_NETWORK_STORED box={name} sender={senderClientId} slot={slotIndex} item={heldItem.ItemId}", this);
            return true;
        }

        private bool TrySwapServer(int slotIndex, SlotState storedState, UtilityItemPrefabData heldItem, NetworkPlayerItemRecord record, ulong senderClientId)
        {
            var heldDurability = record.CurrentDurability;
            if (!record.TryReplaceHeldItemServer(
                    heldItem.ItemId,
                    storedState.ItemId.ToString(),
                    storedState.Durability,
                    record.Revision))
            {
                return false;
            }

            slotStates[slotIndex] = new SlotState(heldItem.ItemId, heldDurability);
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
    }
}
