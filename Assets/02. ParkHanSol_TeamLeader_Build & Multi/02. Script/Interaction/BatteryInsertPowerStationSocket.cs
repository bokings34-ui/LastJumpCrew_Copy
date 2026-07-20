using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class BatteryInsertPowerStationSocket :
        NetworkBehaviour,
        IInteractable,
        CommonInteraction.IInteractable,
        IBatteryUseTarget
    {
        [Header("Battery")]
        [SerializeField] private string requiredItemId = "battery_pack";
        [SerializeField] private string interactionPrompt = "Insert Battery";
        [SerializeField] private GameObject installedBatteryVisual;

        // Kept for existing prefab serialization. Network installation always consumes the held battery.
        [SerializeField] private bool destroyInsertedBattery = true;

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 4f;

        private readonly HashSet<string> completedRequests = new();
        private NetworkShipSystemsState boundShipState;
        private bool requestPending;
        private float nextStateBindingAttemptTime;

        public string InteractionPrompt => interactionPrompt;
        public bool IsBatteryInstalled => boundShipState != null && boundShipState.IsBatteryInstalled;
        public float ServerInteractionDistance => serverInteractionDistance;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(requiredItemId))
            {
                Debug.LogError($"PHS_BATTERY_SOCKET_SETUP_FAILED reason=required_item_id_missing target={name}", this);
            }

            if (installedBatteryVisual == null)
            {
                Debug.LogError($"PHS_BATTERY_SOCKET_SETUP_FAILED reason=installedBatteryVisual_missing target={name}", this);
                return;
            }

            if (!destroyInsertedBattery)
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_SOCKET_SETUP_WARNING reason=network_install_always_consumes_item target={name}",
                    this);
            }

            installedBatteryVisual.SetActive(false);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            TryBindShipState();
            RefreshInstalledVisual();
        }

        public override void OnNetworkDespawn()
        {
            UnbindShipState();
            completedRequests.Clear();
            requestPending = false;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || Time.unscaledTime < nextStateBindingAttemptTime)
            {
                return;
            }

            if (boundShipState == NetworkShipSystemsState.Instance && boundShipState != null)
            {
                return;
            }

            nextStateBindingAttemptTime = Time.unscaledTime + 0.25f;
            TryBindShipState();
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (itemHolder == null || itemHolder.CurrentItemPrefabData == null
                || itemHolder.CurrentItemPrefabData.ItemId != requiredItemId)
            {
                return false;
            }

            return itemHolder is Component holderComponent
                && CanSubmitRequest(holderComponent);
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (itemHolder is not Component holderComponent || !CanInteract(itemHolder))
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_INTERACT_FAILED reason=interaction_unavailable target={name}",
                    this);
                return;
            }

            TryRequestBatteryInstall(holderComponent);
        }

        bool CommonInteraction.IInteractable.CanInteract(CommonInteraction.IItemHolder itemHolder)
        {
            return CanUseBattery(itemHolder);
        }

        void CommonInteraction.IInteractable.Interact(CommonInteraction.IItemHolder itemHolder)
        {
            TryUseBattery(itemHolder);
        }

        public bool CanUseBattery(CommonInteraction.IItemHolder user)
        {
            if (user == null || user.CurrentItem == null || user.CurrentItem.ItemId != requiredItemId)
            {
                return false;
            }

            return user is Component holderComponent
                && CanSubmitRequest(holderComponent);
        }

        public bool TryUseBattery(CommonInteraction.IItemHolder user)
        {
            if (user is not Component holderComponent || !CanUseBattery(user))
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_USE_FAILED reason=interaction_unavailable target={name}",
                    this);
                return false;
            }

            return TryRequestBatteryInstall(holderComponent);
        }

        public bool IsServerRequestInRange(Vector3 playerPosition)
        {
            return (playerPosition - transform.position).sqrMagnitude
                <= serverInteractionDistance * serverInteractionDistance;
        }

        private bool CanSubmitRequest(Component holderComponent)
        {
            if (requestPending || installedBatteryVisual == null || !IsSpawned
                || boundShipState == null || !boundShipState.IsSpawned
                || boundShipState.IsBatteryInstalled
                || (boundShipState.IsPowerEnabled && boundShipState.IsGravityEnabled))
            {
                return false;
            }

            var player = holderComponent.GetComponent<NetworkPlayerController>();
            var itemRecord = holderComponent.GetComponent<NetworkPlayerItemRecord>();
            return player != null && player.IsSpawned && player.IsOwner
                && itemRecord != null && itemRecord.IsSpawned && itemRecord.IsOwner
                && itemRecord.HeldItemId == requiredItemId;
        }

        private bool TryRequestBatteryInstall(Component holderComponent)
        {
            var player = holderComponent.GetComponent<NetworkPlayerController>();
            var itemRecord = holderComponent.GetComponent<NetworkPlayerItemRecord>();
            if (player == null || itemRecord == null || !CanSubmitRequest(holderComponent))
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_REQUEST_FAILED reason=player_item_record_unavailable target={name}",
                    this);
                return false;
            }

            requestPending = true;
            var itemId = new FixedString64Bytes(requiredItemId);
            var expectedRevision = itemRecord.Revision;
            if (IsServer)
            {
                CompleteServerRequest(player.OwnerClientId, itemId, expectedRevision);
                return true;
            }

            RequestBatteryInstallServerRpc(itemId, expectedRevision);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestBatteryInstallServerRpc(
            FixedString64Bytes itemId,
            uint expectedRevision,
            ServerRpcParams rpcParams = default)
        {
            CompleteServerRequest(rpcParams.Receive.SenderClientId, itemId, expectedRevision);
        }

        private void CompleteServerRequest(
            ulong senderClientId,
            FixedString64Bytes itemId,
            uint expectedRevision)
        {
            var success = TryInstallBatteryOnServer(
                senderClientId,
                itemId.ToString(),
                expectedRevision,
                out var reason);
            SendResult(senderClientId, success, itemId, reason);
        }

        private bool TryInstallBatteryOnServer(
            ulong senderClientId,
            string itemId,
            uint expectedRevision,
            out string reason)
        {
            reason = null;
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                reason = "player_missing";
                return false;
            }

            if (!IsServerRequestInRange(client.PlayerObject.transform.position))
            {
                reason = "player_too_far";
                return false;
            }

            if (itemId != requiredItemId)
            {
                reason = "wrong_item";
                return false;
            }

            var itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            if (itemRecord == null || !itemRecord.IsSpawned || itemRecord.OwnerClientId != senderClientId)
            {
                reason = "item_record_missing";
                return false;
            }

            if (itemRecord.HeldItemId != itemId || itemRecord.Revision != expectedRevision)
            {
                reason = "item_record_mismatch";
                return false;
            }

            var shipState = NetworkShipSystemsState.Instance;
            if (shipState == null || !shipState.IsSpawned)
            {
                reason = "ship_state_missing";
                return false;
            }

            if (!shipState.CanRestorePowerWithBattery(out reason))
            {
                return false;
            }

            var requestKey = $"{senderClientId}:{expectedRevision}";
            if (!completedRequests.Add(requestKey))
            {
                reason = "duplicate_request";
                return false;
            }

            if (!itemRecord.TryConsumeHeldItemServer(itemId, expectedRevision))
            {
                completedRequests.Remove(requestKey);
                reason = "item_consume_failed";
                return false;
            }

            // Both calls run synchronously on the server main thread. The preflight above
            // guarantees no valid state transition can fail after the item is consumed.
            if (!shipState.TryRestorePowerWithBattery(out reason))
            {
                Debug.LogError(
                    $"PHS_BATTERY_TRANSACTION_FAILED reason={reason} clientId={senderClientId} revision={expectedRevision}",
                    this);
                return false;
            }

            Debug.Log(
                $"PHS_BATTERY_INSTALLED target={name} clientId={senderClientId} item={itemId} shipRevision={shipState.Revision}",
                this);
            return true;
        }

        private void SendResult(
            ulong targetClientId,
            bool success,
            FixedString64Bytes itemId,
            string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
            CompleteRequestClientRpc(
                success,
                itemId,
                new FixedString64Bytes(reason ?? string.Empty),
                clientRpcParams);
        }

        [ClientRpc]
        private void CompleteRequestClientRpc(
            bool success,
            FixedString64Bytes itemId,
            FixedString64Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            requestPending = false;
            if (!success)
            {
                Debug.LogWarning(
                    $"PHS_BATTERY_REQUEST_FAILED reason={reason} target={name}",
                    this);
                return;
            }

            var localPlayerObject = NetworkManager == null
                ? null
                : NetworkManager.LocalClient?.PlayerObject;
            var itemHolder = localPlayerObject == null
                ? null
                : localPlayerObject.GetComponent<TempPlayerItemHolder>();
            if (itemHolder == null || !itemHolder.TryConsumeHeldItem(itemId.ToString()))
            {
                Debug.LogError(
                    $"PHS_BATTERY_LOCAL_VISUAL_CONSUME_FAILED target={name} item={itemId}",
                    this);
            }
        }

        private void TryBindShipState()
        {
            var nextState = NetworkShipSystemsState.Instance;
            if (boundShipState == nextState)
            {
                return;
            }

            UnbindShipState();
            boundShipState = nextState;
            if (boundShipState == null)
            {
                return;
            }

            boundShipState.StateChanged += HandleShipStateChanged;
            RefreshInstalledVisual();
        }

        private void UnbindShipState()
        {
            if (boundShipState != null)
            {
                boundShipState.StateChanged -= HandleShipStateChanged;
            }

            boundShipState = null;
        }

        private void HandleShipStateChanged()
        {
            RefreshInstalledVisual();
        }

        private void RefreshInstalledVisual()
        {
            if (installedBatteryVisual == null)
            {
                return;
            }

            installedBatteryVisual.SetActive(
                boundShipState != null && boundShipState.IsBatteryInstalled);
        }
    }
}
