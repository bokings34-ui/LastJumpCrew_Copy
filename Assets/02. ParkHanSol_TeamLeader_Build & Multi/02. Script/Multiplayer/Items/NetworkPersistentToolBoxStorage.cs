using System;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UtilityItemDataSO = LastJumpCrew.Common.UtilityItemDataSO;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>Run-scoped, server-authoritative state for scene-bound ToolBox views.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPersistentToolBoxStorage : NetworkBehaviour
    {
        private struct SlotState : INetworkSerializable, IEquatable<SlotState>
        {
            public FixedString128Bytes SlotKey;
            public FixedString64Bytes ItemId;
            public int Durability;
            public uint Revision;

            public SlotState(
                string slotKey,
                string itemId,
                int durability,
                uint revision)
            {
                SlotKey = new FixedString128Bytes(slotKey ?? string.Empty);
                ItemId = new FixedString64Bytes(itemId ?? string.Empty);
                Durability = durability;
                Revision = revision;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer)
                where T : IReaderWriter
            {
                serializer.SerializeValue(ref SlotKey);
                serializer.SerializeValue(ref ItemId);
                serializer.SerializeValue(ref Durability);
                serializer.SerializeValue(ref Revision);
            }

            public bool Equals(SlotState other)
            {
                return SlotKey.Equals(other.SlotKey)
                       && ItemId.Equals(other.ItemId)
                       && Durability == other.Durability
                       && Revision == other.Revision;
            }
        }

        public readonly struct SlotSnapshot
        {
            public SlotSnapshot(string itemId, int durability, uint revision)
            {
                ItemId = itemId;
                Durability = durability;
                Revision = revision;
            }

            public string ItemId { get; }
            public int Durability { get; }
            public uint Revision { get; }
            public bool IsEmpty => string.IsNullOrEmpty(ItemId);
        }

        public event Action<string> SlotChanged;

        private readonly NetworkList<SlotState> slotStates = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            slotStates.OnListChanged += HandleSlotChanged;
            base.OnNetworkSpawn();
        }

        public override void OnNetworkDespawn()
        {
            slotStates.OnListChanged -= HandleSlotChanged;
            base.OnNetworkDespawn();
        }

        public bool TryEnsureSlotServer(
            string slotKey,
            string initialItemId,
            int initialDurability)
        {
            if (!IsSpawned || !IsServer || string.IsNullOrWhiteSpace(slotKey))
            {
                Debug.LogError(
                    $"PHS_TOOL_BOX_PERSISTENCE_FAILED reason=seed_contract slot={slotKey}",
                    this);
                return false;
            }

            if (TryFindState(slotKey, out _, out _))
            {
                return true;
            }

            slotStates.Add(new SlotState(
                slotKey,
                initialItemId,
                initialDurability,
                1U));
            return true;
        }

        public bool TryGetSlot(string slotKey, out SlotSnapshot snapshot)
        {
            snapshot = default;
            if (!TryFindState(slotKey, out _, out var state))
            {
                return false;
            }

            snapshot = new SlotSnapshot(
                state.ItemId.ToString(),
                state.Durability,
                state.Revision);
            return true;
        }

        public bool TryReceiveDeliveryServer(
            string slotKey,
            string itemId,
            int durability)
        {
            if (!TryFindWritableState(slotKey, out var stateIndex, out var state)
                || !state.ItemId.IsEmpty
                || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            slotStates[stateIndex] = ChangedState(state, itemId, durability);
            return true;
        }

        public bool TryTakeServer(
            string slotKey,
            NetworkPlayerItemRecord record)
        {
            if (record == null
                || !TryFindWritableState(slotKey, out var stateIndex, out var state)
                || state.ItemId.IsEmpty
                || !record.TrySetHeldItemServer(
                    state.ItemId.ToString(),
                    state.Durability,
                    record.Revision))
            {
                return false;
            }

            slotStates[stateIndex] = ChangedState(state, string.Empty, 0);
            return true;
        }

        public bool TryStoreServer(
            string slotKey,
            UtilityItemDataSO heldItem,
            NetworkPlayerItemRecord record)
        {
            if (heldItem == null
                || record == null
                || !TryFindWritableState(slotKey, out var stateIndex, out var state)
                || !state.ItemId.IsEmpty)
            {
                return false;
            }

            var durability = record.CurrentDurability;
            if (!record.TryConsumeHeldItemServer(heldItem.ItemId, record.Revision))
            {
                return false;
            }

            slotStates[stateIndex] = ChangedState(state, heldItem.ItemId, durability);
            return true;
        }

        public bool TrySwapServer(
            string slotKey,
            UtilityItemDataSO heldItem,
            NetworkPlayerItemRecord record)
        {
            if (heldItem == null
                || record == null
                || !TryFindWritableState(slotKey, out var stateIndex, out var state)
                || state.ItemId.IsEmpty)
            {
                return false;
            }

            var durability = record.CurrentDurability;
            if (!record.TryReplaceHeldItemServer(
                    heldItem.ItemId,
                    state.ItemId.ToString(),
                    state.Durability,
                    record.Revision))
            {
                return false;
            }

            slotStates[stateIndex] = ChangedState(state, heldItem.ItemId, durability);
            return true;
        }

        private bool TryFindWritableState(
            string slotKey,
            out int stateIndex,
            out SlotState state)
        {
            if (!IsSpawned || !IsServer)
            {
                stateIndex = -1;
                state = default;
                return false;
            }

            return TryFindState(slotKey, out stateIndex, out state);
        }

        private bool TryFindState(
            string slotKey,
            out int stateIndex,
            out SlotState state)
        {
            // ponytail: ToolBoxes have tens of slots; add a server-side index if this reaches hundreds.
            for (var index = 0; index < slotStates.Count; index++)
            {
                var candidate = slotStates[index];
                if (string.Equals(
                        candidate.SlotKey.ToString(),
                        slotKey,
                        StringComparison.Ordinal))
                {
                    stateIndex = index;
                    state = candidate;
                    return true;
                }
            }

            stateIndex = -1;
            state = default;
            return false;
        }

        private static SlotState ChangedState(
            SlotState current,
            string itemId,
            int durability)
        {
            return new SlotState(
                current.SlotKey.ToString(),
                itemId,
                durability,
                current.Revision + 1U);
        }

        private void HandleSlotChanged(NetworkListEvent<SlotState> changeEvent)
        {
            var slotKey = changeEvent.Type == NetworkListEvent<SlotState>.EventType.Remove
                ? changeEvent.PreviousValue.SlotKey.ToString()
                : changeEvent.Value.SlotKey.ToString();
            if (!string.IsNullOrEmpty(slotKey))
            {
                SlotChanged?.Invoke(slotKey);
            }
        }
    }
}
