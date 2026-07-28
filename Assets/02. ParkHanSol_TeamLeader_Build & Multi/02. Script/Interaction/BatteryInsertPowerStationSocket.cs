using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using LastJumpCrew.ParkHanSol.Items;
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
        [SerializeField] private string interactionPrompt = "배터리 장착";
        [SerializeField] private GameObject installedBatteryVisual;
        [SerializeField] private Transform feedbackPoint;

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

            if (feedbackPoint == null)
            {
                Debug.LogError($"PHS_BATTERY_SOCKET_SETUP_FAILED reason=feedbackPoint_missing target={name}", this);
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
            if (itemHolder == null
                || !HasBatteryFamilyPowerProfile(
                    itemHolder.CurrentItemPrefabData))
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
            if (user == null
                || user.CurrentItem == null
                || user is not TempPlayerItemHolder phsHolder
                || phsHolder.CurrentItemPrefabData == null
                || phsHolder.CurrentItemPrefabData.ItemId
                    != user.CurrentItem.ItemId
                || !HasBatteryFamilyPowerProfile(
                    phsHolder.CurrentItemPrefabData))
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
                && TryResolveBatteryFamilyItem(
                    holderComponent,
                    itemRecord.HeldItemId,
                    out _);
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
            var itemId = new FixedString64Bytes(itemRecord.HeldItemId);
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

            var itemRecord = client.PlayerObject.GetComponent<NetworkPlayerItemRecord>();
            var itemLifecycle =
                client.PlayerObject.GetComponent<NetworkPlayerItemLifecycle>();
            if (itemRecord == null
                || itemLifecycle == null
                || !itemRecord.IsSpawned
                || itemRecord.OwnerClientId != senderClientId)
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

            if (!itemLifecycle.TryResolveHeldItemActionServer(
                    itemId,
                    expectedRevision,
                    UtilityItemActionKind.PowerRestore,
                    out var powerRestoreProfile))
            {
                reason = "power_restore_profile_mismatch";
                return false;
            }

            var shipAccidentCoordinator = PHSNetworkShipAccidentCoordinator.Instance;
            var powerFailureInstanceId = 0U;
            var hasActivePowerFailureAccident = shipAccidentCoordinator != null
                && shipAccidentCoordinator.TryGetSingleActiveAccidentServer(
                    PHSShipAccidentId.PowerFailure,
                    out powerFailureInstanceId,
                    out _);

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

            if (hasActivePowerFailureAccident
                && !shipAccidentCoordinator.TryResolveAccidentServer(
                    powerFailureInstanceId,
                    "battery_insert",
                    out var accidentReason))
            {
                reason = $"accident_resolve_failed:{accidentReason}";
                Debug.LogError(
                    $"PHS_BATTERY_TRANSACTION_FAILED reason={reason} clientId={senderClientId} revision={expectedRevision}",
                    this);
                return false;
            }

            if (!TryResolveBatteryFamilyItem(
                    client.PlayerObject,
                    itemId,
                    out _))
            {
                reason = "battery_family_profile_mismatch";
                return false;
            }

            var feedback =
                client.PlayerObject.GetComponent<PHSNetworkItemUseFeedbackController>();
            feedback?.PublishConfirmedTargetImpactServer(
                UtilityItemActionKind.PowerRestore,
                feedbackPoint.position);
            client.PlayerObject
                .GetComponent<PHSNetworkItemInteractionAudioRelay>()
                ?.TryBroadcastConfirmedServer(
                    NetworkAudioCue.BatteryInstall,
                    expectedRevision);

            Debug.Log(
                $"PHS_BATTERY_INSTALLED target={name} clientId={senderClientId} item={itemId} amount={powerRestoreProfile.Amount} durabilityCost={powerRestoreProfile.DurabilityCost} accidentInstance={powerFailureInstanceId} shipRevision={shipState.Revision}",
                this);
            return true;
        }

        private static bool HasBatteryFamilyPowerProfile(
            UtilityItemPrefabData itemData)
        {
            return itemData != null
                && itemData.UtilityFamily
                    == PHSUtilityFamilyActionKind.Battery
                && itemData.TryGetActionProfile(
                    UtilityItemActionKind.PowerRestore,
                    out _);
        }

        private static bool TryResolveBatteryFamilyItem(
            Component holderComponent,
            string itemId,
            out UtilityItemPrefabData itemData)
        {
            itemData = null;
            if (holderComponent == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            var lifecycle = holderComponent.GetComponent<
                NetworkPlayerItemLifecycle>();
            return lifecycle != null
                && lifecycle.ItemCatalog != null
                && lifecycle.ItemCatalog.TryGetById(itemId, out itemData)
                && HasBatteryFamilyPowerProfile(itemData);
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
