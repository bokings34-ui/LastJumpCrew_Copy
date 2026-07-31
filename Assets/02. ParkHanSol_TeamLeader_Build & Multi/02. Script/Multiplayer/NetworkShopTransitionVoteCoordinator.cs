using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Shop;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkShopTransitionVoteCoordinator :
        NetworkBehaviour,
        IShopTransitionVoteService,
        IShopItemUsePolicy
    {
        private const string DefaultShopSceneName = "PHS_ExteriorShopScene";

        [SerializeField, Min(5f)] private float voteDurationSeconds = 20f;
        [SerializeField] private string shopSceneName = DefaultShopSceneName;

        private readonly NetworkVariable<bool> voteActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> shopExitVote = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> agreeCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> requiredAgreeCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> eligiblePlayerCount = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FixedString128Bytes> destinationScene = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<ShopSceneTransitionMode> transitionMode = new(
            ShopSceneTransitionMode.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly HashSet<ulong> eligibleClientIds = new();
        private readonly HashSet<ulong> agreeingClientIds = new();
        private readonly HashSet<ulong> decliningClientIds = new();
        private float voteDeadline;

        public static NetworkShopTransitionVoteCoordinator Instance { get; private set; }

        public bool IsVoteActive => voteActive.Value;
        public bool IsShopExitVote => shopExitVote.Value;
        public int AgreeCount => agreeCount.Value;
        public int RequiredAgreeCount => requiredAgreeCount.Value;
        public int EligiblePlayerCount => eligiblePlayerCount.Value;
        public string DestinationSceneName => destinationScene.Value.ToString();

        public static bool TryAuthorizeHeldItemUseServer(
            ulong playerClientId,
            string itemId,
            out string reason)
        {
            IShopItemUsePolicy policy = Instance;
            if (policy != null)
            {
                return policy.CanUseHeldItemServer(
                    playerClientId,
                    itemId,
                    out reason);
            }

            if (SceneManager.GetActiveScene().name == DefaultShopSceneName)
            {
                reason = "shop_item_use_policy_missing";
                return false;
            }

            reason = null;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"PHS_SHOP_VOTE_SETUP_FAILED reason=duplicate_coordinator current={name} existing={Instance.name}",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            eligibleClientIds.Clear();
            agreeingClientIds.Clear();
            decliningClientIds.Clear();
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !voteActive.Value)
            {
                return;
            }

            RefreshEligibleVoters();
            if (TryApproveVote())
            {
                return;
            }

            if (Time.unscaledTime >= voteDeadline)
            {
                Debug.Log(
                    $"PHS_SHOP_VOTE_CANCELLED reason=timeout agree={agreeingClientIds.Count}/{requiredAgreeCount.Value}",
                    this);
                ResetVote();
            }
        }

        public bool TryStartVote(
            ulong initiatorClientId,
            string destinationSceneName,
            ShopSceneTransitionMode requestedTransitionMode,
            bool isShopExit,
            out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (!NetworkManager.ConnectedClients.TryGetValue(initiatorClientId, out var initiator)
                || initiator.PlayerObject == null)
            {
                reason = "initiator_missing";
                return false;
            }

            if (voteActive.Value)
            {
                if (!destinationScene.Value.Equals(new FixedString128Bytes(destinationSceneName))
                    || transitionMode.Value != requestedTransitionMode
                    || shopExitVote.Value != isShopExit)
                {
                    reason = "different_vote_in_progress";
                    return false;
                }

                RefreshEligibleVoters();
                if (!eligibleClientIds.Contains(initiatorClientId))
                {
                    reason = "initiator_not_eligible";
                    return false;
                }

                RecordVote(initiatorClientId, true);
                TryApproveVote();
                reason = null;
                return true;
            }

            if (!CanExecuteTransition(destinationSceneName, requestedTransitionMode, false, out reason))
            {
                return false;
            }

            eligibleClientIds.Clear();
            agreeingClientIds.Clear();
            decliningClientIds.Clear();
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                if (pair.Value.PlayerObject != null
                    && pair.Value.PlayerObject.GetComponent<NetworkPlayerLifeState>() == null)
                {
                    Debug.LogError(
                        $"PHS_SHOP_VOTE_ELIGIBILITY_FAILED reason=life_state_missing player={pair.Value.PlayerObject.name}",
                        pair.Value.PlayerObject);
                }

                if (IsAlivePlayer(pair.Value.PlayerObject))
                {
                    eligibleClientIds.Add(pair.Key);
                }
            }

            if (!eligibleClientIds.Contains(initiatorClientId))
            {
                reason = "initiator_not_eligible";
                return false;
            }

            destinationScene.Value = new FixedString128Bytes(destinationSceneName);
            transitionMode.Value = requestedTransitionMode;
            shopExitVote.Value = isShopExit;
            eligiblePlayerCount.Value = eligibleClientIds.Count;
            requiredAgreeCount.Value = eligibleClientIds.Count;
            agreeCount.Value = 0;
            voteDeadline = Time.unscaledTime + voteDurationSeconds;
            voteActive.Value = true;
            RecordVote(initiatorClientId, true);

            Debug.Log(
                $"PHS_SHOP_VOTE_STARTED exit={isShopExit} scene={destinationSceneName} eligible={eligiblePlayerCount.Value} required={requiredAgreeCount.Value} initiator={initiatorClientId}",
                this);
            TryApproveVote();
            reason = null;
            return true;
        }

        public void SubmitLocalVote(bool agree)
        {
            if (!IsSpawned || !voteActive.Value)
            {
                Debug.LogError("PHS_SHOP_VOTE_SUBMIT_FAILED reason=vote_inactive", this);
                return;
            }

            if (IsServer)
            {
                RecordVote(NetworkManager.LocalClientId, agree);
                TryApproveVote();
                return;
            }

            SubmitVoteServerRpc(agree);
        }

        public bool TryExecuteImmediate(
            string requestedDestination,
            ShopSceneTransitionMode requestedMode,
            out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (!CanExecuteTransition(
                    requestedDestination,
                    requestedMode,
                    false,
                    out reason))
            {
                return false;
            }

            var entryDropTransactions =
                new List<NetworkPlayerItemLifecycle.ShopEntryDropTransaction>();
            if (requestedMode == ShopSceneTransitionMode.RequireShopPhase
                && !TryDropHeldItemsForShopEntry(
                    entryDropTransactions,
                    out reason))
            {
                return false;
            }

            if (requestedMode == ShopSceneTransitionMode.CompleteShop
                && !TryValidateShopExitHandsEmpty(out reason))
            {
                RollbackShopEntryDrops(entryDropTransactions);
                return false;
            }

            if (!CanExecuteTransition(
                    requestedDestination,
                    requestedMode,
                    true,
                    out reason))
            {
                RollbackShopEntryDrops(entryDropTransactions);
                return false;
            }

            var status = NetworkManager.SceneManager.LoadScene(requestedDestination, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                reason = $"scene_load_{status}";
                RollbackShopEntryDrops(entryDropTransactions);
                return false;
            }

            Debug.Log($"PHS_NETWORK_PORTAL_LOAD scene={requestedDestination}", this);
            reason = null;
            return true;
        }

        public bool CanUseHeldItemServer(
            ulong playerClientId,
            string itemId,
            out string reason)
        {
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            var configuredShopScene = string.IsNullOrWhiteSpace(shopSceneName)
                ? DefaultShopSceneName
                : shopSceneName;
            if (SceneManager.GetActiveScene().name == configuredShopScene)
            {
                reason = "shop_item_use_blocked";
                return false;
            }

            reason = null;
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitVoteServerRpc(bool agree, ServerRpcParams rpcParams = default)
        {
            RecordVote(rpcParams.Receive.SenderClientId, agree);
            TryApproveVote();
        }

        private void RecordVote(ulong clientId, bool agree)
        {
            if (!voteActive.Value || !eligibleClientIds.Contains(clientId))
            {
                Debug.LogError(
                    $"PHS_SHOP_VOTE_SUBMIT_FAILED reason=voter_not_eligible clientId={clientId}",
                    this);
                return;
            }

            agreeingClientIds.Remove(clientId);
            decliningClientIds.Remove(clientId);
            if (agree)
            {
                agreeingClientIds.Add(clientId);
            }
            else
            {
                decliningClientIds.Add(clientId);
            }

            agreeCount.Value = agreeingClientIds.Count;
            Debug.Log(
                $"PHS_SHOP_VOTE_UPDATED clientId={clientId} agree={agree} total={agreeCount.Value}/{requiredAgreeCount.Value}",
                this);
        }

        private bool TryApproveVote()
        {
            if (!voteActive.Value || agreeingClientIds.Count < requiredAgreeCount.Value)
            {
                return false;
            }

            var approvedDestination = destinationScene.Value.ToString();
            var approvedMode = transitionMode.Value;
            var approvedExit = shopExitVote.Value;
            ResetVote();

            if (!TryExecuteImmediate(approvedDestination, approvedMode, out var reason))
            {
                Debug.LogError(
                    $"PHS_SHOP_VOTE_APPROVAL_FAILED reason={reason} scene={approvedDestination} mode={approvedMode}",
                    this);
                return true;
            }

            Debug.Log(
                $"PHS_SHOP_VOTE_APPROVED exit={approvedExit} scene={approvedDestination}",
                this);
            return true;
        }

        private static bool CanExecuteTransition(
            string requestedDestination,
            ShopSceneTransitionMode requestedMode,
            bool applyTransition,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(requestedDestination))
            {
                reason = "destination_missing";
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(requestedDestination))
            {
                reason = "scene_not_in_build";
                return false;
            }

            if (requestedMode == ShopSceneTransitionMode.None)
            {
                reason = null;
                return true;
            }

            var adapter = FindAnyObjectByType<ShopRunFlowAdapter>(FindObjectsInactive.Include);
            IShopRunFlowService runFlow = adapter;
            if (runFlow == null || !runFlow.IsReady)
            {
                reason = "run_flow_missing";
                return false;
            }

            if (requestedMode == ShopSceneTransitionMode.RequireShopPhase)
            {
                return runFlow.CanEnterShop(out reason);
            }

            if (requestedMode != ShopSceneTransitionMode.CompleteShop)
            {
                reason = $"transition_mode_invalid:{requestedMode}";
                return false;
            }

            return applyTransition
                ? runFlow.TryCompleteShop(out reason)
                : runFlow.CanCompleteShop(out reason);
        }

        private bool TryDropHeldItemsForShopEntry(
            ICollection<NetworkPlayerItemLifecycle.ShopEntryDropTransaction> transactions,
            out string reason)
        {
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                {
                    reason = $"shop_entry_player_missing:{pair.Key}";
                    RollbackShopEntryDrops(transactions);
                    return false;
                }

                var itemRecord = playerObject.GetComponent<NetworkPlayerItemRecord>();
                var itemLifecycle = playerObject.GetComponent<NetworkPlayerItemLifecycle>();
                if (itemRecord == null
                    || itemLifecycle == null
                    || !itemRecord.IsSpawned)
                {
                    reason = $"shop_entry_item_boundary_missing:{pair.Key}";
                    RollbackShopEntryDrops(transactions);
                    return false;
                }

                if (string.IsNullOrEmpty(itemRecord.HeldItemId))
                {
                    continue;
                }

                if (!itemLifecycle.TryDropHeldItemForShopEntryServer(
                        out var transaction,
                        out var dropReason))
                {
                    reason = $"shop_entry_drop_failed:{pair.Key}:{dropReason}";
                    Debug.LogError(
                        $"PHS_SHOP_ENTRY_CANCELLED reason={dropReason} clientId={pair.Key} item={itemRecord.HeldItemId}",
                        this);
                    RollbackShopEntryDrops(transactions);
                    return false;
                }

                if (transaction.IsValid)
                {
                    transactions.Add(transaction);
                }
            }

            reason = null;
            return true;
        }

        private bool TryValidateShopExitHandsEmpty(out string reason)
        {
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var playerObject = pair.Value.PlayerObject;
                var itemRecord = playerObject == null
                    ? null
                    : playerObject.GetComponent<NetworkPlayerItemRecord>();
                if (itemRecord == null || !itemRecord.IsSpawned)
                {
                    reason = $"shop_exit_item_record_missing:{pair.Key}";
                    Debug.LogError(
                        $"PHS_SHOP_EXIT_REJECTED reason=item_record_missing clientId={pair.Key}",
                        this);
                    return false;
                }

                if (string.IsNullOrEmpty(itemRecord.HeldItemId))
                {
                    continue;
                }

                reason = $"shop_exit_held_item:{pair.Key}:{itemRecord.HeldItemId}";
                Debug.LogWarning(
                    $"PHS_SHOP_EXIT_REJECTED reason=held_item clientId={pair.Key} item={itemRecord.HeldItemId}",
                    this);
                return false;
            }

            reason = null;
            return true;
        }

        private void RollbackShopEntryDrops(
            ICollection<NetworkPlayerItemLifecycle.ShopEntryDropTransaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
            {
                return;
            }

            var orderedTransactions =
                new List<NetworkPlayerItemLifecycle.ShopEntryDropTransaction>(transactions);
            for (var index = orderedTransactions.Count - 1; index >= 0; index--)
            {
                var transaction = orderedTransactions[index];
                if (!transaction.IsValid
                    || transaction.Lifecycle.TryRollbackShopEntryDropServer(
                        transaction,
                        out var rollbackReason))
                {
                    continue;
                }

                Debug.LogError(
                    $"PHS_SHOP_ENTRY_ROLLBACK_FAILED reason={rollbackReason} index={index}",
                    this);
            }
        }

        private void RefreshEligibleVoters()
        {
            foreach (var pair in NetworkManager.ConnectedClients)
            {
                if (IsAlivePlayer(pair.Value.PlayerObject))
                {
                    eligibleClientIds.Add(pair.Key);
                }
            }

            eligibleClientIds.RemoveWhere(clientId =>
                !NetworkManager.ConnectedClients.TryGetValue(clientId, out var client)
                || !IsAlivePlayer(client.PlayerObject));
            agreeingClientIds.RemoveWhere(clientId => !eligibleClientIds.Contains(clientId));
            decliningClientIds.RemoveWhere(clientId => !eligibleClientIds.Contains(clientId));
            eligiblePlayerCount.Value = eligibleClientIds.Count;
            requiredAgreeCount.Value = eligibleClientIds.Count;
            agreeCount.Value = agreeingClientIds.Count;

            if (eligibleClientIds.Count == 0)
            {
                Debug.Log("PHS_SHOP_VOTE_CANCELLED reason=no_eligible_players", this);
                ResetVote();
            }
        }

        private static bool IsAlivePlayer(NetworkObject playerObject)
        {
            if (playerObject == null)
            {
                return false;
            }

            var lifeState = playerObject.GetComponent<NetworkPlayerLifeState>();
            return lifeState != null && lifeState.IsSpawned && lifeState.IsAlive;
        }

        private void ResetVote()
        {
            voteActive.Value = false;
            shopExitVote.Value = false;
            agreeCount.Value = 0;
            requiredAgreeCount.Value = 0;
            eligiblePlayerCount.Value = 0;
            destinationScene.Value = default;
            transitionMode.Value = ShopSceneTransitionMode.None;
            eligibleClientIds.Clear();
            agreeingClientIds.Clear();
            decliningClientIds.Clear();
            voteDeadline = 0f;
        }
    }
}
