using System;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkPlayerItemRecord))]
    [RequireComponent(typeof(TempPlayerItemHolder))]
    public sealed class NetworkPlayerItemLifecycle :
        NetworkBehaviour,
        INetworkItemPickupRequester
    {
        public readonly struct ShopEntryDropTransaction
        {
            internal ShopEntryDropTransaction(
                NetworkPlayerItemLifecycle lifecycle,
                string itemId,
                int durability,
                uint consumedRevision,
                NetworkObject droppedItem)
            {
                Lifecycle = lifecycle;
                ItemId = itemId;
                Durability = durability;
                ConsumedRevision = consumedRevision;
                DroppedItem = droppedItem;
            }

            internal NetworkPlayerItemLifecycle Lifecycle { get; }
            internal string ItemId { get; }
            internal int Durability { get; }
            internal uint ConsumedRevision { get; }
            internal NetworkObject DroppedItem { get; }
            public bool IsValid => Lifecycle != null
                && !string.IsNullOrWhiteSpace(ItemId);
        }

        [Header("Catalog")]
        [SerializeField] private UtilityItemCatalogSO itemCatalog;

        [Header("Player References")]
        [SerializeField] private NetworkPlayerItemRecord itemRecord;
        [SerializeField] private TempPlayerItemHolder itemHolder;

        [Header("Drop Motion")]
        [SerializeField] private ItemDropMotionProfile dropMotionProfile;

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverPickupDistance = 3f;
        [SerializeField, Min(0.1f)] private float serverPlaceDistance = 3f;

        private struct HeldItemAssignmentTransaction
        {
            public bool ReplacedExisting;
            public string PreviousItemId;
            public int PreviousDurability;
            public uint CommittedRevision;
            public NetworkObject DroppedPreviousItem;
        }

        public UtilityItemCatalogSO ItemCatalog => itemCatalog;
        public NetworkPlayerItemRecord ItemRecord => itemRecord;

        private void Awake()
        {
            itemRecord ??= GetComponent<NetworkPlayerItemRecord>();
            itemHolder ??= GetComponent<TempPlayerItemHolder>();
        }

        public bool CanRequestNetworkPickup(UtilityItemObject itemObject)
        {
            if (!IsSpawned
                || !IsOwner
                || itemCatalog == null
                || itemRecord == null
                || itemHolder == null
                || !itemRecord.IsSpawned
                || itemObject == null
                || !itemCatalog.Contains(itemObject.ItemData)
                || !CanReplaceCurrentHeldItem())
            {
                return false;
            }

            var targetNetworkObject = itemObject.GetComponent<NetworkObject>();
            return targetNetworkObject != null
                && targetNetworkObject.IsSpawned
                && targetNetworkObject != NetworkObject
                && targetNetworkObject.gameObject.scene == gameObject.scene
                && IsWithinDistance(
                    transform.position,
                    targetNetworkObject.transform.position,
                    serverPickupDistance);
        }

        public void RequestNetworkPickup(UtilityItemObject itemObject)
        {
            if (!CanRequestNetworkPickup(itemObject))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_PICKUP_REJECTED reason=local_contract player={name}",
                    this);
                return;
            }

            var targetNetworkObject = itemObject.GetComponent<NetworkObject>();
            if (IsServer)
            {
                TryPickupItemServer(OwnerClientId, targetNetworkObject.NetworkObjectId);
                return;
            }

            RequestNetworkPickupServerRpc(targetNetworkObject.NetworkObjectId);
        }

        public bool CanRequestPlaceHeldItem()
        {
            return IsSpawned
                && IsOwner
                && itemCatalog != null
                && dropMotionProfile != null
                && itemRecord != null
                && itemRecord.IsSpawned
                && !string.IsNullOrEmpty(itemRecord.HeldItemId)
                && itemCatalog.TryGetById(itemRecord.HeldItemId, out var itemData)
                && itemData.HasDroppedPrefab;
        }

        public bool RequestPlaceHeldItem(Vector3 requestedPosition, Quaternion requestedRotation)
        {
            if (!CanRequestPlaceHeldItem()
                || !IsFinite(requestedPosition)
                || !TryNormalize(requestedRotation, out var normalizedRotation))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_PLACE_REJECTED reason=local_contract player={name}",
                    this);
                return false;
            }

            if (IsServer)
            {
                return TryPlaceHeldItemServer(
                    OwnerClientId,
                    requestedPosition,
                    normalizedRotation);
            }

            RequestPlaceHeldItemServerRpc(requestedPosition, normalizedRotation);
            return true;
        }

        public bool TryAssignHeldItemServer(string itemId)
        {
            return itemRecord != null
                && TryAssignHeldItemServer(itemId, itemRecord.Revision);
        }

        public bool TryAssignHeldItemServer(string itemId, uint expectedRevision)
        {
            if (!IsSpawned || !IsServer || itemCatalog == null || itemRecord == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=server_contract player={name}",
                    this);
                return false;
            }

            if (!itemCatalog.TryGetById(itemId, out var itemData))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=item_not_in_catalog player={name} item={itemId}",
                    this);
                return false;
            }

            var initialDurability = itemData.UsesDurability
                ? itemData.MaxDurability
                : 0;
            return TryCommitHeldItemAssignmentServer(
                itemData,
                initialDurability,
                expectedRevision,
                out _);
        }

        public bool TryAssignHeldItemServer(UtilityItemDataSO itemData)
        {
            return itemData != null
                && itemCatalog != null
                && itemCatalog.Contains(itemData)
                && TryAssignHeldItemServer(itemData.ItemId);
        }

        public bool TryCreateDroppedItemServer(
            string itemId,
            Vector3 position,
            Quaternion rotation,
            out NetworkObject spawnedItem)
        {
            spawnedItem = null;
            if (itemCatalog == null
                || !itemCatalog.TryGetById(itemId, out var itemData))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=item_not_in_catalog player={name} item={itemId}",
                    this);
                return false;
            }

            return TryCreateDroppedItemServer(
                itemId,
                itemData.UsesDurability ? itemData.MaxDurability : 0,
                position,
                rotation,
                out spawnedItem);
        }

        public bool TryCreateDroppedItemServer(
            string itemId,
            int currentDurability,
            Vector3 position,
            Quaternion rotation,
            out NetworkObject spawnedItem)
        {
            spawnedItem = null;
            if (!IsSpawned || !IsServer || itemCatalog == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=server_contract player={name}",
                    this);
                return false;
            }

            if (!itemCatalog.TryGetById(itemId, out var itemData)
                || !itemData.HasDroppedPrefab
                || !IsFinite(position)
                || !TryNormalize(rotation, out var normalizedRotation))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=item_contract player={name} item={itemId}",
                    this);
                return false;
            }

            var instance = Instantiate(
                itemData.DroppedPrefab,
                position,
                normalizedRotation);
            var networkObject = instance == null
                ? null
                : instance.GetComponent<NetworkObject>();
            var itemObject = instance == null
                ? null
                : instance.GetComponent<UtilityItemObject>();
            var physicsAuthority = instance == null
                ? null
                : instance.GetComponent<NetworkItemPhysicsAuthority>();
            var durabilityState = instance == null
                ? null
                : instance.GetComponent<NetworkUtilityItemDurabilityState>();

            if (networkObject == null
                || itemObject == null
                || itemObject.ItemData != itemData
                || physicsAuthority == null
                || itemData.UsesDurability && durabilityState == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=prefab_contract player={name} item={itemId}",
                    this);
                if (instance != null)
                {
                    Destroy(instance);
                }

                return false;
            }

            if (itemData.UsesDurability
                && !durabilityState.PrepareForServerSpawn(
                    itemData,
                    currentDurability))
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=durability_prepare player={name} item={itemId} durability={currentDurability}",
                    this);
                Destroy(instance);
                return false;
            }

            itemObject.OnDropped(position);
            try
            {
                networkObject.Spawn(destroyWithScene: true);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_CREATE_FAILED reason=spawn_exception player={name} item={itemId} exception={exception.GetType().Name}",
                    this);
                Destroy(instance);
                return false;
            }

            spawnedItem = networkObject;
            return true;
        }

        public bool TryDropHeldItemForShopEntryServer(
            out ShopEntryDropTransaction transaction,
            out string reason)
        {
            transaction = default;
            if (!IsSpawned
                || !IsServer
                || itemRecord == null
                || itemHolder == null
                || dropMotionProfile == null
                || !itemRecord.IsSpawned)
            {
                reason = "player_contract";
                return false;
            }

            var itemId = itemRecord.HeldItemId;
            if (string.IsNullOrEmpty(itemId))
            {
                reason = null;
                return true;
            }

            itemHolder.GetDropPose(out var dropPosition, out var dropRotation);
            if (!IsFinite(dropPosition)
                || !TryNormalize(dropRotation, out var normalizedDropRotation)
                || !IsWithinDistance(
                    transform.position,
                    dropPosition,
                    serverPlaceDistance))
            {
                reason = "drop_pose_contract";
                return false;
            }

            var durability = itemRecord.CurrentDurability;
            var expectedRevision = itemRecord.Revision;
            if (!TryCreateDroppedItemServer(
                    itemId,
                    durability,
                    dropPosition,
                    normalizedDropRotation,
                    out var droppedItem))
            {
                reason = "drop_spawn_failed";
                return false;
            }

            if (!TryResolveAndApplyDroppedItemMotion(
                    droppedItem,
                    dropPosition,
                    normalizedDropRotation))
            {
                TryCleanupSpawnedItemServer(
                    droppedItem,
                    "shop_entry_drop_motion_rejected");
                reason = "drop_motion_rejected";
                return false;
            }

            if (!itemRecord.TryConsumeHeldItemServer(
                    itemId,
                    expectedRevision))
            {
                TryCleanupSpawnedItemServer(
                    droppedItem,
                    "shop_entry_record_rejected");
                reason = "record_consume_failed";
                return false;
            }

            transaction = new ShopEntryDropTransaction(
                this,
                itemId,
                durability,
                itemRecord.Revision,
                droppedItem);
            Debug.Log(
                $"PHS_SHOP_ENTRY_ITEM_DROPPED owner={OwnerClientId} item={itemId} networkObjectId={droppedItem.NetworkObjectId} revision={itemRecord.Revision}",
                this);
            reason = null;
            return true;
        }

        public bool TryRollbackShopEntryDropServer(
            ShopEntryDropTransaction transaction,
            out string reason)
        {
            if (!transaction.IsValid
                || transaction.Lifecycle != this
                || !IsSpawned
                || !IsServer
                || itemRecord == null
                || !itemRecord.IsSpawned
                || !string.IsNullOrEmpty(itemRecord.HeldItemId)
                || itemRecord.Revision != transaction.ConsumedRevision)
            {
                reason = "rollback_contract";
                return false;
            }

            if (!itemRecord.TrySetHeldItemServer(
                    transaction.ItemId,
                    transaction.Durability,
                    transaction.ConsumedRevision))
            {
                reason = "record_restore_failed";
                return false;
            }

            var restoredRevision = itemRecord.Revision;
            if (!TryCleanupSpawnedItemServer(
                    transaction.DroppedItem,
                    "shop_entry_rollback"))
            {
                if (!itemRecord.TryConsumeHeldItemServer(
                        transaction.ItemId,
                        restoredRevision))
                {
                    Debug.LogError(
                        $"PHS_SHOP_ENTRY_ROLLBACK_INVARIANT_FAILED reason=duplicate_prevention_failed owner={OwnerClientId} item={transaction.ItemId}",
                        this);
                }

                reason = "drop_cleanup_failed";
                return false;
            }

            Debug.LogWarning(
                $"PHS_SHOP_ENTRY_ITEM_ROLLED_BACK owner={OwnerClientId} item={transaction.ItemId} revision={itemRecord.Revision}",
                this);
            reason = null;
            return true;
        }

        public bool TryResolveHeldItemActionServer(
            string expectedItemId,
            uint expectedRevision,
            UtilityItemActionKind actionKind,
            out UtilityItemActionProfile actionProfile)
        {
            actionProfile = default;
            if (!IsSpawned
                || !IsServer
                || itemCatalog == null
                || itemRecord == null
                || !itemRecord.IsSpawned)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_REJECTED reason=server_contract player={name} action={actionKind}",
                    this);
                return false;
            }

            if (!NetworkShopTransitionVoteCoordinator.TryAuthorizeHeldItemUseServer(
                    OwnerClientId,
                    expectedItemId,
                    out var policyReason))
            {
                Debug.LogWarning(
                    $"PHS_ITEM_ACTION_REJECTED reason={policyReason} player={name} item={expectedItemId} action={actionKind}",
                    this);
                return false;
            }

            if (itemRecord.HeldItemId != expectedItemId
                || itemRecord.Revision != expectedRevision
                || !itemCatalog.TryGetById(expectedItemId, out var itemData)
                || !itemData.TryGetActionProfile(actionKind, out actionProfile))
            {
                Debug.LogWarning(
                    $"PHS_ITEM_ACTION_REJECTED reason=profile_or_record player={name} item={expectedItemId} action={actionKind} expectedRevision={expectedRevision} actualRevision={itemRecord.Revision}",
                    this);
                return false;
            }

            if (actionProfile.DurabilityCost > 0 && !itemData.UsesDurability)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_REJECTED reason=durability_contract item={expectedItemId} action={actionKind}",
                    itemData);
                return false;
            }

            return itemRecord.CanSpendHeldItemDurabilityServer(
                expectedItemId,
                expectedRevision,
                actionProfile.DurabilityCost);
        }

        public bool TryCommitHeldItemActionServer(
            string expectedItemId,
            uint expectedRevision,
            UtilityItemActionProfile actionProfile)
        {
            if (!actionProfile.IsValid)
            {
                Debug.LogError(
                    $"PHS_ITEM_ACTION_COMMIT_FAILED reason=profile_invalid player={name} item={expectedItemId}",
                    this);
                return false;
            }

            if (!NetworkShopTransitionVoteCoordinator.TryAuthorizeHeldItemUseServer(
                    OwnerClientId,
                    expectedItemId,
                    out var policyReason))
            {
                Debug.LogWarning(
                    $"PHS_ITEM_ACTION_COMMIT_FAILED reason={policyReason} player={name} item={expectedItemId}",
                    this);
                return false;
            }

            return itemRecord != null
                && itemRecord.TrySpendHeldItemDurabilityServer(
                    expectedItemId,
                    expectedRevision,
                    actionProfile.DurabilityCost);
        }

        private bool CanReplaceCurrentHeldItem()
        {
            if (itemRecord == null || string.IsNullOrEmpty(itemRecord.HeldItemId))
            {
                return true;
            }

            return itemCatalog != null
                && dropMotionProfile != null
                && itemCatalog.TryGetById(itemRecord.HeldItemId, out var heldItemData)
                && heldItemData.HasDroppedPrefab;
        }

        private bool TryCommitHeldItemAssignmentServer(
            UtilityItemDataSO replacementItemData,
            int replacementDurability,
            uint expectedRevision,
            out HeldItemAssignmentTransaction transaction)
        {
            transaction = default;
            if (!IsSpawned
                || !IsServer
                || itemCatalog == null
                || itemRecord == null
                || itemHolder == null
                || !itemRecord.IsSpawned)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=server_contract player={name}",
                    this);
                return false;
            }

            if (replacementItemData == null
                || replacementDurability < 0
                || !itemCatalog.TryGetById(replacementItemData.ItemId, out var catalogReplacement)
                || catalogReplacement != replacementItemData)
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=replacement_contract player={name} item={(replacementItemData == null ? "missing" : replacementItemData.ItemId)} durability={replacementDurability}",
                    this);
                return false;
            }

            var previousItemId = itemRecord.HeldItemId;
            if (string.IsNullOrEmpty(previousItemId))
            {
                if (!itemRecord.TrySetHeldItemServer(
                        replacementItemData.ItemId,
                        replacementDurability,
                        expectedRevision))
                {
                    return false;
                }

                transaction.CommittedRevision = itemRecord.Revision;
                return true;
            }

            if (itemRecord.Revision != expectedRevision
                || dropMotionProfile == null
                || !itemCatalog.TryGetById(previousItemId, out var previousItemData)
                || !previousItemData.HasDroppedPrefab)
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=previous_item_contract player={name} previousItem={previousItemId} expectedRevision={expectedRevision} actualRevision={itemRecord.Revision}",
                    this);
                return false;
            }

            itemHolder.GetDropPose(out var dropPosition, out var dropRotation);
            if (!IsFinite(dropPosition)
                || !TryNormalize(dropRotation, out var normalizedDropRotation)
                || !IsWithinDistance(transform.position, dropPosition, serverPlaceDistance))
            {
                Debug.LogWarning(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=drop_pose_contract player={name} previousItem={previousItemId} position={dropPosition}",
                    this);
                return false;
            }

            var previousDurability = itemRecord.CurrentDurability;
            if (!TryCreateDroppedItemServer(
                    previousItemId,
                    previousDurability,
                    dropPosition,
                    normalizedDropRotation,
                    out var droppedPreviousItem))
            {
                return false;
            }

            if (!TryResolveAndApplyDroppedItemMotion(
                    droppedPreviousItem,
                    dropPosition,
                    normalizedDropRotation))
            {
                TryCleanupSpawnedItemServer(
                    droppedPreviousItem,
                    "replacement_drop_motion_rejected");
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_ASSIGN_FAILED reason=drop_motion_rejected player={name} previousItem={previousItemId}",
                    this);
                return false;
            }

            if (!itemRecord.TryReplaceHeldItemServer(
                    previousItemId,
                    replacementItemData.ItemId,
                    replacementDurability,
                    expectedRevision))
            {
                TryCleanupSpawnedItemServer(
                    droppedPreviousItem,
                    "replacement_record_rejected");
                return false;
            }

            transaction = new HeldItemAssignmentTransaction
            {
                ReplacedExisting = true,
                PreviousItemId = previousItemId,
                PreviousDurability = previousDurability,
                CommittedRevision = itemRecord.Revision,
                DroppedPreviousItem = droppedPreviousItem
            };
            Debug.Log(
                $"PHS_NETWORK_ITEM_REPLACED player={name} previousItem={previousItemId} replacementItem={replacementItemData.ItemId} droppedNetworkObjectId={droppedPreviousItem.NetworkObjectId} revision={itemRecord.Revision}",
                this);
            return true;
        }

        private bool TryRollbackHeldItemAssignmentServer(
            string replacementItemId,
            HeldItemAssignmentTransaction transaction)
        {
            var recordRestored = transaction.ReplacedExisting
                ? itemRecord.TryReplaceHeldItemServer(
                    replacementItemId,
                    transaction.PreviousItemId,
                    transaction.PreviousDurability,
                    transaction.CommittedRevision)
                : itemRecord.TryConsumeHeldItemServer(
                    replacementItemId,
                    transaction.CommittedRevision);
            if (!recordRestored)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_ASSIGN_ROLLBACK_FAILED reason=record_restore player={name} replacementItem={replacementItemId} revision={transaction.CommittedRevision}",
                    this);
                return false;
            }

            if (transaction.ReplacedExisting
                && !TryCleanupSpawnedItemServer(
                    transaction.DroppedPreviousItem,
                    "assignment_rollback"))
            {
                return false;
            }

            Debug.LogWarning(
                $"PHS_NETWORK_ITEM_ASSIGN_ROLLED_BACK player={name} replacementItem={replacementItemId} previousItem={transaction.PreviousItemId}",
                this);
            return true;
        }

        private bool TryResolveAndApplyDroppedItemMotion(
            NetworkObject droppedItem,
            Vector3 requestedPosition,
            Quaternion requestedRotation)
        {
            if (droppedItem == null
                || dropMotionProfile == null
                || !droppedItem.TryGetComponent<Rigidbody>(out var droppedRigidbody)
                || !dropMotionProfile.TryResolveFloorPlacement(
                    droppedRigidbody,
                    requestedPosition,
                    requestedRotation,
                    transform.root,
                    out var resolvedPosition,
                    out var resolvedRotation))
            {
                return false;
            }

            droppedItem.transform.SetPositionAndRotation(
                resolvedPosition,
                resolvedRotation);

            var itemObject = droppedItem.GetComponent<UtilityItemObject>();
            if (itemObject == null)
            {
                return false;
            }

            itemObject.OnDropped(resolvedPosition);
            return dropMotionProfile.TryApply(droppedRigidbody, resolvedRotation);
        }

        private bool TryCleanupSpawnedItemServer(
            NetworkObject spawnedItem,
            string reason)
        {
            if (spawnedItem == null || !spawnedItem.IsSpawned)
            {
                return true;
            }

            try
            {
                spawnedItem.Despawn(true);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_CLEANUP_FAILED reason={reason} player={name} networkObjectId={spawnedItem.NetworkObjectId} exception={exception.GetType().Name}",
                    this);
                return false;
            }
        }

        [ServerRpc]
        private void RequestNetworkPickupServerRpc(
            ulong targetNetworkObjectId,
            ServerRpcParams rpcParams = default)
        {
            TryPickupItemServer(
                rpcParams.Receive.SenderClientId,
                targetNetworkObjectId);
        }

        [ServerRpc]
        private void RequestPlaceHeldItemServerRpc(
            Vector3 requestedPosition,
            Quaternion requestedRotation,
            ServerRpcParams rpcParams = default)
        {
            TryPlaceHeldItemServer(
                rpcParams.Receive.SenderClientId,
                requestedPosition,
                requestedRotation);
        }

        private bool TryPickupItemServer(
            ulong senderClientId,
            ulong targetNetworkObjectId)
        {
            if (!ValidateServerSender(senderClientId)
                || itemCatalog == null
                || itemRecord == null
                || itemHolder == null
                || !itemRecord.IsSpawned)
            {
                return RejectPickup("player_contract", senderClientId, targetNetworkObjectId);
            }

            var spawnManager = NetworkManager == null
                ? null
                : NetworkManager.SpawnManager;
            if (spawnManager == null
                || !spawnManager.SpawnedObjects.TryGetValue(
                    targetNetworkObjectId,
                    out var targetNetworkObject)
                || targetNetworkObject == null
                || !targetNetworkObject.IsSpawned
                || targetNetworkObject == NetworkObject)
            {
                return RejectPickup("target_missing", senderClientId, targetNetworkObjectId);
            }

            if (targetNetworkObject.gameObject.scene != gameObject.scene)
            {
                return RejectPickup("scene_mismatch", senderClientId, targetNetworkObjectId);
            }

            if (!IsWithinDistance(
                    transform.position,
                    targetNetworkObject.transform.position,
                    serverPickupDistance))
            {
                return RejectPickup("distance", senderClientId, targetNetworkObjectId);
            }

            var itemObject = targetNetworkObject.GetComponent<UtilityItemObject>();
            var itemData = itemObject == null ? null : itemObject.ItemData;
            if (itemData == null
                || !itemCatalog.Contains(itemData)
                || !itemCatalog.TryGetById(itemData.ItemId, out var catalogItem)
                || catalogItem != itemData)
            {
                return RejectPickup("item_not_in_catalog", senderClientId, targetNetworkObjectId);
            }

            var pickupDurability = 0;
            if (itemData.UsesDurability)
            {
                var durabilityState =
                    targetNetworkObject.GetComponent<
                        NetworkUtilityItemDurabilityState>();
                if (durabilityState == null
                    || !durabilityState.TryGetServerDurability(
                        itemData,
                        out pickupDurability))
                {
                    return RejectPickup(
                        "durability_state_missing",
                        senderClientId,
                        targetNetworkObjectId);
                }
            }

            var expectedRevision = itemRecord.Revision;
            if (!TryCommitHeldItemAssignmentServer(
                    itemData,
                    pickupDurability,
                    expectedRevision,
                    out var transaction))
            {
                return RejectPickup("record_set_failed", senderClientId, targetNetworkObjectId);
            }

            try
            {
                targetNetworkObject.Despawn(true);
            }
            catch (Exception exception)
            {
                if (targetNetworkObject == null || !targetNetworkObject.IsSpawned)
                {
                    Debug.LogWarning(
                        $"PHS_NETWORK_ITEM_PICKUP_DESPAWN_WARNING player={name} owner={senderClientId} item={itemData.ItemId} target={targetNetworkObjectId} exception={exception.GetType().Name}",
                        this);
                }
                else
                {
                    if (!TryRollbackHeldItemAssignmentServer(itemData.ItemId, transaction))
                    {
                        Debug.LogError(
                            $"PHS_NETWORK_ITEM_PICKUP_REJECTED reason=rollback_failed player={name} owner={senderClientId} item={itemData.ItemId} target={targetNetworkObjectId}",
                            this);
                    }

                    return RejectPickup("target_despawn_exception", senderClientId, targetNetworkObjectId);
                }
            }

            Debug.Log(
                $"PHS_NETWORK_ITEM_PICKED_UP player={name} owner={senderClientId} item={itemData.ItemId} target={targetNetworkObjectId} revision={itemRecord.Revision}",
                this);
            return true;
        }

        private bool TryPlaceHeldItemServer(
            ulong senderClientId,
            Vector3 requestedPosition,
            Quaternion requestedRotation)
        {
            if (!ValidateServerSender(senderClientId)
                || itemRecord == null
                || dropMotionProfile == null
                || !itemRecord.IsSpawned
                || string.IsNullOrEmpty(itemRecord.HeldItemId)
                || !IsFinite(requestedPosition)
                || !TryNormalize(requestedRotation, out var normalizedRotation))
            {
                return RejectPlace("player_contract", senderClientId);
            }

            if (!IsWithinDistance(
                    transform.position,
                    requestedPosition,
                    serverPlaceDistance))
            {
                return RejectPlace("distance", senderClientId);
            }

            var itemId = itemRecord.HeldItemId;
            var expectedRevision = itemRecord.Revision;
            if (!TryCreateDroppedItemServer(
                    itemId,
                    itemRecord.CurrentDurability,
                    requestedPosition,
                    normalizedRotation,
                    out var spawnedItem))
            {
                return RejectPlace("spawn_failed", senderClientId);
            }

            if (!TryResolveAndApplyDroppedItemMotion(
                    spawnedItem,
                    requestedPosition,
                    normalizedRotation))
            {
                if (spawnedItem.IsSpawned)
                {
                    spawnedItem.Despawn(true);
                }

                return RejectPlace("drop_motion_rejected", senderClientId);
            }

            if (!itemRecord.TryConsumeHeldItemServer(itemId, expectedRevision))
            {
                if (spawnedItem != null && spawnedItem.IsSpawned)
                {
                    spawnedItem.Despawn(true);
                }

                return RejectPlace("record_consume_failed", senderClientId);
            }

            Debug.Log(
                $"PHS_NETWORK_ITEM_PLACED player={name} owner={senderClientId} item={itemId} networkObjectId={spawnedItem.NetworkObjectId} revision={itemRecord.Revision}",
                this);
            return true;
        }

        private bool ValidateServerSender(ulong senderClientId)
        {
            return IsSpawned
                && IsServer
                && senderClientId == OwnerClientId;
        }

        private bool RejectPickup(
            string reason,
            ulong senderClientId,
            ulong targetNetworkObjectId)
        {
            Debug.LogWarning(
                $"PHS_NETWORK_ITEM_PICKUP_REJECTED reason={reason} player={name} owner={senderClientId} target={targetNetworkObjectId}",
                this);
            return false;
        }

        private bool RejectPlace(string reason, ulong senderClientId)
        {
            Debug.LogWarning(
                $"PHS_NETWORK_ITEM_PLACE_REJECTED reason={reason} player={name} owner={senderClientId}",
                this);
            return false;
        }

        private static bool IsWithinDistance(
            Vector3 first,
            Vector3 second,
            float maximumDistance)
        {
            return (first - second).sqrMagnitude
                <= maximumDistance * maximumDistance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z);
        }

        private static bool TryNormalize(
            Quaternion value,
            out Quaternion normalized)
        {
            normalized = Quaternion.identity;
            if (!IsFinite(value.x)
                || !IsFinite(value.y)
                || !IsFinite(value.z)
                || !IsFinite(value.w))
            {
                return false;
            }

            var squaredMagnitude =
                value.x * value.x
                + value.y * value.y
                + value.z * value.z
                + value.w * value.w;
            if (squaredMagnitude < 0.0001f)
            {
                return false;
            }

            normalized = Quaternion.Normalize(value);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
