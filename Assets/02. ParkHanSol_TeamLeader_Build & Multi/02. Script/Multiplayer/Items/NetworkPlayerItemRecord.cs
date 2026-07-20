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

        private string standaloneItemId = string.Empty;

        public string HeldItemId => IsSpawned ? heldItemId.Value.ToString() : standaloneItemId;
        public uint Revision => IsSpawned ? revision.Value : 0;
        public event Action<string> HeldItemChanged;

        public override void OnNetworkSpawn()
        {
            heldItemId.OnValueChanged += HandleHeldItemChanged;
            HeldItemChanged?.Invoke(heldItemId.Value.ToString());
        }

        public override void OnNetworkDespawn()
        {
            heldItemId.OnValueChanged -= HandleHeldItemChanged;
            base.OnNetworkDespawn();
        }

        public void ReportHeldItem(string itemId)
        {
            itemId ??= string.Empty;
            if (!IsSpawned)
            {
                standaloneItemId = itemId;
                HeldItemChanged?.Invoke(itemId);
                return;
            }

            if (IsServer)
            {
                ApplyServerRecord(itemId);
                return;
            }

            Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=server_required player={name}", this);
        }

        public bool TrySetHeldItemServer(string itemId, uint expectedRevision)
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

            ApplyServerRecord(itemId);
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
            revision.Value++;
            Debug.Log(
                $"PHS_ITEM_RECORD_CONSUMED player={name} owner={OwnerClientId} item={expectedItemId} revision={revision.Value}",
                this);
            return true;
        }

        private void ApplyServerRecord(string itemId)
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
            revision.Value++;
            Debug.Log($"PHS_ITEM_RECORD_SYNC player={name} owner={OwnerClientId} item={itemId} revision={revision.Value}");
        }

        private void HandleHeldItemChanged(FixedString64Bytes previousValue, FixedString64Bytes currentValue)
        {
            HeldItemChanged?.Invoke(currentValue.ToString());
        }
    }
}
