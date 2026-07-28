using System;
using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkShopStockRegistry : NetworkBehaviour
    {
        private enum StockStatus : byte
        {
            Empty,
            Available,
            PickedUp,
            Paid
        }

        private struct StockState : INetworkSerializable, IEquatable<StockState>
        {
            public int SlotIndex;
            public FixedString64Bytes OfferId;
            public ulong NetworkObjectId;
            public StockStatus Status;

            public bool Equals(StockState other)
            {
                return SlotIndex == other.SlotIndex
                    && OfferId.Equals(other.OfferId)
                    && NetworkObjectId == other.NetworkObjectId
                    && Status == other.Status;
            }

            public void NetworkSerialize<T>(BufferSerializer<T> serializer)
                where T : IReaderWriter
            {
                serializer.SerializeValue(ref SlotIndex);
                serializer.SerializeValue(ref OfferId);
                serializer.SerializeValue(ref NetworkObjectId);
                serializer.SerializeValue(ref Status);
            }
        }

        [SerializeField] private ShopDisplaySlot[] displaySlots;
        [SerializeField] private MonoBehaviour purchaseServiceSource;
        [SerializeField] private ShopLocalProductHudPresenter localHudPresenter;

        private readonly NetworkList<StockState> stockStates = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly Dictionary<int, NetworkObject> spawnedStock = new();
        private readonly Dictionary<ShopDisplaySlot, ShopProductData> pendingStock = new();
        private IShopPurchaseService purchaseService;
        private bool localPresenterBound;

        public IReadOnlyList<ShopDisplaySlot> DisplaySlots => displaySlots;

        private void Awake()
        {
            purchaseService = purchaseServiceSource as IShopPurchaseService;
            if (purchaseService != null)
            {
                purchaseService.ProductPurchased += HandleProductPurchased;
            }
        }

        public override void OnNetworkSpawn()
        {
            stockStates.OnListChanged += HandleStockStateChanged;
            ApplyAllStates();

            if (IsServer)
            {
                foreach (var pair in pendingStock)
                {
                    TrySpawnStockServer(pair.Key, pair.Value);
                }
            }

            TryBindLocalPresenter();

            pendingStock.Clear();
        }

        public override void OnNetworkDespawn()
        {
            stockStates.OnListChanged -= HandleStockStateChanged;
            if (localHudPresenter != null)
            {
                localHudPresenter.BindStockRegistry(null);
                localHudPresenter.BindLocalPlayer(null, null);
            }

            localPresenterBound = false;

            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (purchaseService != null)
            {
                purchaseService.ProductPurchased -= HandleProductPurchased;
            }

            base.OnDestroy();
        }

        private void Update()
        {
            if (IsSpawned && IsClient && !localPresenterBound)
            {
                TryBindLocalPresenter();
            }

            if (!IsSpawned || !IsServer || spawnedStock.Count == 0)
            {
                return;
            }

            var missingSlots = new List<int>();
            foreach (var pair in spawnedStock)
            {
                if (pair.Value == null || !pair.Value.IsSpawned)
                {
                    missingSlots.Add(pair.Key);
                }
            }

            foreach (var slotIndex in missingSlots)
            {
                spawnedStock.Remove(slotIndex);
                SetStateStatusServer(slotIndex, StockStatus.PickedUp);
            }
        }

        private void TryBindLocalPresenter()
        {
            if (!IsSpawned
                || !IsClient
                || NetworkManager == null
                || localHudPresenter == null)
            {
                return;
            }

            var localPlayer = NetworkManager.LocalClient?.PlayerObject;
            if (localPlayer == null)
            {
                return;
            }

            var interactionScanner = localPlayer.GetComponent<
                TempPlayerInteractionScanner>();
            if (interactionScanner == null
                || interactionScanner.InteractionCamera == null)
            {
                return;
            }

            localHudPresenter.BindLocalPlayer(
                interactionScanner,
                interactionScanner.InteractionCamera);
            localHudPresenter.BindStockRegistry(this);
            localPresenterBound = true;
        }

        public bool TryPresent(ShopDisplaySlot slot, ShopProductData product)
        {
            if (!TryGetSlotIndex(slot, out _)
                || product == null
                || !product.IsConfigured)
            {
                return false;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                Debug.LogError("PHS_SHOP_STOCK_FAILED reason=network_session_required", this);
                return false;
            }

            if (!IsSpawned)
            {
                if (networkManager.IsServer)
                {
                    pendingStock[slot] = product;
                }

                return true;
            }

            return !IsServer || TrySpawnStockServer(slot, product);
        }

        public void Clear(ShopDisplaySlot slot)
        {
            pendingStock.Remove(slot);
            if (!IsSpawned || !IsServer || !TryGetSlotIndex(slot, out var slotIndex))
            {
                return;
            }

            var removedSpawnedStock = spawnedStock.Remove(slotIndex, out var stock);
            if (removedSpawnedStock
                && stock != null
                && stock.IsSpawned)
            {
                stock.Despawn(true);
            }

            if (removedSpawnedStock || !HasCompletedStockState(slotIndex))
            {
                SetStateServer(slotIndex, default, 0, StockStatus.Empty);
            }
        }

        private bool TrySpawnStockServer(ShopDisplaySlot slot, ShopProductData product)
        {
            if (!IsSpawned || !IsServer || !TryGetSlotIndex(slot, out var slotIndex))
            {
                return false;
            }

            Clear(slot);
            var itemData = product.ItemPrefabData;
            var prefab = itemData.DroppedPrefab;
            if (prefab == null
                || !prefab.TryGetComponent<NetworkObject>(out _)
                || !prefab.TryGetComponent<UtilityItemObject>(out var prefabItem)
                || prefabItem.ItemPrefabData != itemData
                || !prefab.TryGetComponent<Rigidbody>(out _)
                || prefab.GetComponentInChildren<Collider>(true) == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_STOCK_FAILED reason=dropped_prefab_contract offer={product.OfferId}",
                    product);
                return false;
            }

            var anchor = slot.PresentationAnchor;
            var instance = Instantiate(prefab, anchor.position, anchor.rotation);
            instance.name = $"PHS_ShopStock_{slotIndex}_{product.OfferId}";
            var body = instance.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            var networkObject = instance.GetComponent<NetworkObject>();
            try
            {
                networkObject.Spawn(true);
            }
            catch (Exception exception)
            {
                Destroy(instance);
                Debug.LogError(
                    $"PHS_SHOP_STOCK_FAILED reason=spawn_exception offer={product.OfferId} exception={exception.GetType().Name}",
                    this);
                return false;
            }

            spawnedStock[slotIndex] = networkObject;
            SetStateServer(
                slotIndex,
                new FixedString64Bytes(product.OfferId),
                networkObject.NetworkObjectId,
                StockStatus.Available);
            return true;
        }

        private void HandleProductPurchased(ShopProductData product)
        {
            if (!IsSpawned || !IsServer || product == null)
            {
                return;
            }

            for (var index = 0; index < stockStates.Count; index++)
            {
                var state = stockStates[index];
                if (state.OfferId.ToString() == product.OfferId
                    && state.Status == StockStatus.PickedUp)
                {
                    state.Status = StockStatus.Paid;
                    stockStates[index] = state;
                    return;
                }
            }
        }

        private void SetStateStatusServer(int slotIndex, StockStatus status)
        {
            for (var index = 0; index < stockStates.Count; index++)
            {
                if (stockStates[index].SlotIndex != slotIndex)
                {
                    continue;
                }

                var state = stockStates[index];
                state.Status = status;
                stockStates[index] = state;
                return;
            }
        }

        private bool HasCompletedStockState(int slotIndex)
        {
            for (var index = 0; index < stockStates.Count; index++)
            {
                var state = stockStates[index];
                if (state.SlotIndex == slotIndex
                    && (state.Status == StockStatus.PickedUp
                        || state.Status == StockStatus.Paid))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetStateServer(
            int slotIndex,
            FixedString64Bytes offerId,
            ulong networkObjectId,
            StockStatus status)
        {
            var state = new StockState
            {
                SlotIndex = slotIndex,
                OfferId = offerId,
                NetworkObjectId = networkObjectId,
                Status = status
            };
            for (var index = 0; index < stockStates.Count; index++)
            {
                if (stockStates[index].SlotIndex == slotIndex)
                {
                    stockStates[index] = state;
                    return;
                }
            }

            stockStates.Add(state);
        }

        private void HandleStockStateChanged(NetworkListEvent<StockState> changeEvent)
        {
            ApplyAllStates();
        }

        private void ApplyAllStates()
        {
            if (displaySlots == null)
            {
                return;
            }

            foreach (var slot in displaySlots)
            {
                slot?.ApplyStockAvailability(false);
            }

            foreach (var state in stockStates)
            {
                if (state.SlotIndex >= 0 && state.SlotIndex < displaySlots.Length)
                {
                    displaySlots[state.SlotIndex]?.ApplyStockAvailability(
                        state.Status == StockStatus.Available);
                }
            }
        }

        private bool TryGetSlotIndex(ShopDisplaySlot slot, out int slotIndex)
        {
            slotIndex = -1;
            if (slot == null || displaySlots == null)
            {
                return false;
            }

            slotIndex = Array.IndexOf(displaySlots, slot);
            if (slotIndex >= 0)
            {
                return true;
            }

            Debug.LogError($"PHS_SHOP_STOCK_FAILED reason=slot_unregistered slot={slot.name}", slot);
            return false;
        }
    }
}
