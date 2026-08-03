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

        public UtilityItemCatalogSO ItemCatalog => itemCatalog;

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
                || itemHolder.HasItem
                || !string.IsNullOrEmpty(itemRecord.HeldItemId)
                || itemObject == null
                || !itemCatalog.Contains(itemObject.ItemData))
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
            return itemRecord.TrySetHeldItemServer(
                itemId,
                initialDurability,
                expectedRevision);
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
                networkObject.Spawn();
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

            return itemRecord != null
                && itemRecord.TrySpendHeldItemDurabilityServer(
                    expectedItemId,
                    expectedRevision,
                    actionProfile.DurabilityCost);
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
                || !itemRecord.IsSpawned
                || !string.IsNullOrEmpty(itemRecord.HeldItemId))
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
            if (!itemRecord.TrySetHeldItemServer(
                    itemData.ItemId,
                    pickupDurability,
                    expectedRevision))
            {
                return RejectPickup("record_set_failed", senderClientId, targetNetworkObjectId);
            }

            targetNetworkObject.Despawn(true);
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

            var droppedRigidbody = spawnedItem.GetComponent<Rigidbody>();
            if (!dropMotionProfile.TryApply(droppedRigidbody, normalizedRotation))
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
