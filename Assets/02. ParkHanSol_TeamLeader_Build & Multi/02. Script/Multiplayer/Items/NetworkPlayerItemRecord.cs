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

            if (!IsOwner)
            {
                Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=owner_required player={name}", this);
                return;
            }

            ReportHeldItemServerRpc(new FixedString64Bytes(itemId));
        }

        [ServerRpc]
        private void ReportHeldItemServerRpc(
            FixedString64Bytes itemId,
            ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_ITEM_RECORD_FAILED reason=owner_mismatch player={name}", this);
                return;
            }

            ApplyServerRecord(itemId.ToString());
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
