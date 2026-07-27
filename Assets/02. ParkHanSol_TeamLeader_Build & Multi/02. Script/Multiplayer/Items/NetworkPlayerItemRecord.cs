using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerItemRecord : NetworkBehaviour
    {
        private readonly NetworkVariable<FixedString64Bytes> heldItemId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<uint> revision = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> currentDurability = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private string standaloneItemId = string.Empty;
        private int standaloneCurrentDurability;

        public string HeldItemId => IsSpawned ? heldItemId.Value.ToString() : standaloneItemId;
        public uint Revision => IsSpawned ? revision.Value : 0;
        public int CurrentDurability => IsSpawned
            ? currentDurability.Value
            : standaloneCurrentDurability;
        public event Action<string> HeldItemChanged;
        public event Action<int> DurabilityChanged;

        public override void OnNetworkSpawn()
        {
            heldItemId.OnValueChanged += HandleHeldItemChanged;
            currentDurability.OnValueChanged += HandleDurabilityChanged;
            HeldItemChanged?.Invoke(heldItemId.Value.ToString());
            DurabilityChanged?.Invoke(currentDurability.Value);
        }

        public override void OnNetworkDespawn()
        {
            heldItemId.OnValueChanged -= HandleHeldItemChanged;
            currentDurability.OnValueChanged -= HandleDurabilityChanged;
            base.OnNetworkDespawn();
        }

        public void ReportHeldItem(string itemId)
        {
            ReportHeldItem(itemId, 0);
        }

        public void ReportHeldItem(string itemId, int durability)
        {
            itemId ??= string.Empty;
            durability = string.IsNullOrEmpty(itemId)
                ? 0
                : Mathf.Max(0, durability);
            if (!IsSpawned)
            {
                standaloneItemId = itemId;
                standaloneCurrentDurability = durability;
                HeldItemChanged?.Invoke(itemId);
                DurabilityChanged?.Invoke(durability);
                return;
            }

            if (IsServer)
            {
                ApplyServerRecord(itemId, durability);
                return;
            }

            Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=server_required player={name}", this);
        }

        public bool TrySetHeldItemServer(
            string itemId,
            int durability,
            uint expectedRevision)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_ITEM_RECORD_SET_FAILED reason=server_required player={name}", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"PHS_ITEM_RECORD_SET_FAILED reason=item_missing player={name}", this);
                return false;
            }

            if (!heldItemId.Value.IsEmpty || revision.Value != expectedRevision)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_RECORD_SET_FAILED reason=record_mismatch player={name} requestedItem={itemId} actualItem={heldItemId.Value} expectedRevision={expectedRevision} actualRevision={revision.Value}",
                    this);
                return false;
            }

            if (durability < 0)
            {
                Debug.LogError(
                    $"PHS_ITEM_RECORD_SET_FAILED reason=durability_invalid player={name} item={itemId} durability={durability}",
                    this);
                return false;
            }

            ApplyServerRecord(itemId, durability);
            return true;
        }

        public bool CanSpendHeldItemDurabilityServer(
            string expectedItemId,
            uint expectedRevision,
            int durabilityCost)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError(
                    $"PHS_ITEM_DURABILITY_REJECTED reason=server_required player={name}",
                    this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedItemId)
                || heldItemId.Value.ToString() != expectedItemId
                || revision.Value != expectedRevision)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_DURABILITY_REJECTED reason=record_mismatch player={name} expectedItem={expectedItemId} actualItem={heldItemId.Value} expectedRevision={expectedRevision} actualRevision={revision.Value}",
                    this);
                return false;
            }

            if (durabilityCost < 0
                || durabilityCost > currentDurability.Value)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_DURABILITY_REJECTED reason=insufficient player={name} item={expectedItemId} current={currentDurability.Value} cost={durabilityCost}",
                    this);
                return false;
            }

            return true;
        }

        public bool TrySpendHeldItemDurabilityServer(
            string expectedItemId,
            uint expectedRevision,
            int durabilityCost)
        {
            if (!CanSpendHeldItemDurabilityServer(
                    expectedItemId,
                    expectedRevision,
                    durabilityCost))
            {
                return false;
            }

            if (durabilityCost == 0)
            {
                return true;
            }

            var previousDurability = currentDurability.Value;
            currentDurability.Value -= durabilityCost;
            revision.Value++;
            Debug.Log(
                $"PHS_ITEM_DURABILITY_SPENT player={name} owner={OwnerClientId} item={expectedItemId} durability={previousDurability}->{currentDurability.Value} cost={durabilityCost} revision={revision.Value}",
                this);
            return true;
        }

        public bool TryConsumeHeldItemServer(string expectedItemId, uint expectedRevision)
        {
            if (!IsSpawned || !IsServer)
            {
                Debug.LogError($"PHS_ITEM_RECORD_CONSUME_FAILED reason=server_required player={name}", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedItemId) ||
                heldItemId.Value.ToString() != expectedItemId ||
                revision.Value != expectedRevision)
            {
                Debug.LogWarning(
                    $"PHS_ITEM_RECORD_CONSUME_FAILED reason=record_mismatch player={name} expectedItem={expectedItemId} actualItem={heldItemId.Value} expectedRevision={expectedRevision} actualRevision={revision.Value}",
                    this);
                return false;
            }

            heldItemId.Value = default;
            currentDurability.Value = 0;
            revision.Value++;
            Debug.Log(
                $"PHS_ITEM_RECORD_CONSUMED player={name} owner={OwnerClientId} item={expectedItemId} revision={revision.Value}",
                this);
            return true;
        }

        private void ApplyServerRecord(string itemId, int durability)
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=server_required player={name}", this);
                return;
            }

            var nextValue = new FixedString64Bytes(itemId ?? string.Empty);
            if (heldItemId.Value.Equals(nextValue))
            {
                return;
            }

            heldItemId.Value = nextValue;
            currentDurability.Value = nextValue.IsEmpty ? 0 : durability;
            revision.Value++;
            Debug.Log(
                $"PHS_ITEM_RECORD_SYNC player={name} owner={OwnerClientId} item={itemId} durability={currentDurability.Value} revision={revision.Value}");
        }

        private void HandleHeldItemChanged(FixedString64Bytes previousValue, FixedString64Bytes currentValue)
        {
            HeldItemChanged?.Invoke(currentValue.ToString());
        }

        private void HandleDurabilityChanged(int previousValue, int currentValue)
        {
            DurabilityChanged?.Invoke(currentValue);
        }
    }
}
