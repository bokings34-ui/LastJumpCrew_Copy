using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(UtilityItemObject))]
    public sealed class NetworkUtilityItemDurabilityState : NetworkBehaviour
    {
        [SerializeField] private UtilityItemObject itemObject;

        private readonly NetworkVariable<int> currentDurability = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private bool hasPreparedDurability;
        private int preparedDurability;

        public int CurrentDurability => currentDurability.Value;

        private void Awake()
        {
            if (itemObject == null)
            {
                Debug.LogError(
                    $"PHS_DROPPED_ITEM_DURABILITY_SETUP_FAILED reason=item_object_missing item={name}",
                    this);
            }
        }

        public bool PrepareForServerSpawn(
            UtilityItemDataSO expectedItem,
            int durability)
        {
            if (IsSpawned
                || itemObject == null
                || expectedItem == null
                || itemObject.ItemPrefabData != expectedItem
                || !expectedItem.UsesDurability
                || durability < 0
                || durability > expectedItem.MaxDurability)
            {
                Debug.LogError(
                    $"PHS_DROPPED_ITEM_DURABILITY_PREPARE_FAILED item={name} expected={(expectedItem == null ? "null" : expectedItem.ItemId)} durability={durability}",
                    this);
                return false;
            }

            preparedDurability = durability;
            hasPreparedDurability = true;
            return true;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                var itemData = itemObject == null
                    ? null
                    : itemObject.ItemPrefabData;
                if (itemData == null || !itemData.UsesDurability)
                {
                    Debug.LogError(
                        $"PHS_DROPPED_ITEM_DURABILITY_SPAWN_FAILED reason=item_contract item={name}",
                        this);
                }
                else
                {
                    currentDurability.Value = hasPreparedDurability
                        ? preparedDurability
                        : itemData.MaxDurability;
                    Debug.Log(
                        $"PHS_DROPPED_ITEM_DURABILITY_SYNC item={itemData.ItemId} durability={currentDurability.Value} source={(hasPreparedDurability ? "held_drop" : "scene_full")}",
                        this);
                }
            }

            base.OnNetworkSpawn();
        }

        public bool TryGetServerDurability(
            UtilityItemDataSO expectedItem,
            out int durability)
        {
            durability = 0;
            if (!IsSpawned
                || !IsServer
                || itemObject == null
                || expectedItem == null
                || itemObject.ItemPrefabData != expectedItem
                || !expectedItem.UsesDurability
                || currentDurability.Value < 0
                || currentDurability.Value > expectedItem.MaxDurability)
            {
                Debug.LogError(
                    $"PHS_DROPPED_ITEM_DURABILITY_READ_FAILED item={name} expected={(expectedItem == null ? "null" : expectedItem.ItemId)}",
                    this);
                return false;
            }

            durability = currentDurability.Value;
            return true;
        }
    }
}
