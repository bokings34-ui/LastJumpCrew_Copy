using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Items;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkPlayerController))]
    [RequireComponent(typeof(NetworkPlayerItemRecord))]
    [RequireComponent(typeof(NetworkPlayerItemLifecycle))]
    [RequireComponent(typeof(NetworkPlayerLifeState))]
    public sealed class NetworkPlayerUpgradeState : NetworkBehaviour
    {
        private readonly NetworkVariable<float> hookPowerMultiplier = new(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> thrusterCapacityBonus = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private NetworkPlayerItemRecord itemRecord;
        [SerializeField] private NetworkPlayerItemLifecycle itemLifecycle;
        [SerializeField] private NetworkPlayerLifeState playerLifeState;

        public float HookPowerMultiplier => IsSpawned ? hookPowerMultiplier.Value : 1f;
        public float ThrusterCapacityBonus => IsSpawned ? thrusterCapacityBonus.Value : 0f;

        public bool RequestUseHeldUpgrade()
        {
            if (!IsSpawned
                || !IsOwner
                || itemRecord == null
                || itemLifecycle == null
                || itemLifecycle.ItemCatalog == null)
            {
                return false;
            }

            if (IsServer)
            {
                return TryUseHeldUpgradeServer(OwnerClientId);
            }

            UseHeldUpgradeServerRpc();
            return true;
        }

        [ServerRpc]
        private void UseHeldUpgradeServerRpc(ServerRpcParams rpcParams = default)
        {
            TryUseHeldUpgradeServer(rpcParams.Receive.SenderClientId);
        }

        private bool TryUseHeldUpgradeServer(ulong senderClientId)
        {
            if (!IsServer
                || senderClientId != OwnerClientId
                || itemRecord == null
                || itemLifecycle == null
                || itemLifecycle.ItemCatalog == null)
            {
                return Reject("server_contract", senderClientId, string.Empty);
            }

            var itemId = itemRecord.HeldItemId;
            if (string.IsNullOrWhiteSpace(itemId)
                || !itemLifecycle.ItemCatalog.TryGetById(itemId, out var itemData)
                || itemData == null
                || !itemData.IsUpgradeItem)
            {
                return Reject("upgrade_item_missing", senderClientId, itemId);
            }

            if (!CanApply(itemData, out var reason))
            {
                return Reject(reason, senderClientId, itemId);
            }

            var expectedRevision = itemRecord.Revision;
            if (!itemRecord.TryConsumeHeldItemServer(itemId, expectedRevision))
            {
                return Reject("consume_failed", senderClientId, itemId);
            }

            if (!Apply(itemData, out reason))
            {
                Debug.LogError(
                    $"PHS_UPGRADE_ITEM_INVARIANT_FAILED reason={reason} owner={senderClientId} item={itemId}",
                    this);
                return false;
            }

            Debug.Log(
                $"PHS_UPGRADE_ITEM_APPLIED owner={senderClientId} item={itemId} effect={itemData.UpgradeEffect} amount={itemData.UpgradeAmount}",
                this);
            return true;
        }

        private bool CanApply(UtilityItemDataSO itemData, out string reason)
        {
            if (itemData.UpgradeAmount <= 0f)
            {
                reason = "positive_amount_required";
                return false;
            }

            switch (itemData.UpgradeEffect)
            {
                case UtilityItemUpgradeEffect.RestoreShipHp:
                    if (Mathf.RoundToInt(itemData.UpgradeAmount) <= 0)
                    {
                        reason = "positive_rounded_amount_required";
                        return false;
                    }

                    var repairState = NetworkRunSessionRoot.Instance?.ShipSystems;
                    if (repairState == null)
                    {
                        reason = "ship_systems_missing";
                        return false;
                    }

                    if (!repairState.IsShipAlive)
                    {
                        reason = "ship_destroyed";
                        return false;
                    }

                    if (repairState.CurrentShipHp >= repairState.MaximumShipHp)
                    {
                        reason = "ship_durability_full";
                        return false;
                    }

                    break;
                case UtilityItemUpgradeEffect.IncreaseShipMaximumHp:
                    var maximumIncrease = Mathf.RoundToInt(itemData.UpgradeAmount);
                    if (maximumIncrease <= 0)
                    {
                        reason = "positive_rounded_amount_required";
                        return false;
                    }

                    var maximumState = NetworkRunSessionRoot.Instance?.ShipSystems;
                    if (maximumState == null)
                    {
                        reason = "ship_systems_missing";
                        return false;
                    }

                    if (!maximumState.IsShipAlive)
                    {
                        reason = "ship_destroyed";
                        return false;
                    }

                    if (maximumState.MaximumShipHp > int.MaxValue - maximumIncrease)
                    {
                        reason = "maximum_ship_hp_overflow";
                        return false;
                    }

                    break;
                case UtilityItemUpgradeEffect.IncreaseHookPower:
                case UtilityItemUpgradeEffect.IncreaseThrusterDuration:
                    if (playerController == null
                        || !playerController.IsSpawned
                        || !playerController.IsServer)
                    {
                        reason = "player_controller_missing";
                        return false;
                    }

                    break;
                case UtilityItemUpgradeEffect.IncreasePlayerMaximumHp:
                    var playerMaximumIncrease = Mathf.RoundToInt(itemData.UpgradeAmount);
                    if (playerMaximumIncrease <= 0)
                    {
                        reason = "positive_rounded_amount_required";
                        return false;
                    }

                    if (playerLifeState == null
                        || !playerLifeState.IsSpawned
                        || !playerLifeState.IsServer)
                    {
                        reason = "player_life_state_missing";
                        return false;
                    }

                    if (!playerLifeState.IsAlive)
                    {
                        reason = "player_dead";
                        return false;
                    }

                    if (playerLifeState.MaximumHealth > int.MaxValue - playerMaximumIncrease
                        || playerLifeState.CurrentHealth > int.MaxValue - playerMaximumIncrease)
                    {
                        reason = "maximum_player_hp_overflow";
                        return false;
                    }

                    break;
                default:
                    reason = "upgrade_effect_invalid";
                    return false;
            }

            reason = null;
            return true;
        }

        private bool Apply(UtilityItemDataSO itemData, out string reason)
        {
            switch (itemData.UpgradeEffect)
            {
                case UtilityItemUpgradeEffect.RestoreShipHp:
                    return NetworkRunSessionRoot.Instance.ShipSystems.TryRestoreShipDurabilityAtDock(
                        Mathf.RoundToInt(itemData.UpgradeAmount),
                        out reason);
                case UtilityItemUpgradeEffect.IncreaseShipMaximumHp:
                    return NetworkRunSessionRoot.Instance.ShipSystems.TryIncreaseMaximumShipHpAtDock(
                        Mathf.RoundToInt(itemData.UpgradeAmount),
                        out reason);
                case UtilityItemUpgradeEffect.IncreaseHookPower:
                    hookPowerMultiplier.Value += itemData.UpgradeAmount;
                    reason = null;
                    return true;
                case UtilityItemUpgradeEffect.IncreaseThrusterDuration:
                    if (!playerController.TryRestoreThrusterFuelForUpgrade(
                            itemData.UpgradeAmount,
                            out reason))
                    {
                        return false;
                    }

                    thrusterCapacityBonus.Value += itemData.UpgradeAmount;
                    return true;
                case UtilityItemUpgradeEffect.IncreasePlayerMaximumHp:
                    return playerLifeState.TryIncreaseMaximumHealthServer(
                        Mathf.RoundToInt(itemData.UpgradeAmount),
                        out reason);
                default:
                    reason = "upgrade_effect_invalid";
                    return false;
            }
        }

        private bool Reject(string reason, ulong senderClientId, string itemId)
        {
            Debug.LogWarning(
                $"PHS_UPGRADE_ITEM_REJECTED reason={reason} owner={senderClientId} item={itemId}",
                this);
            return false;
        }
    }
}
