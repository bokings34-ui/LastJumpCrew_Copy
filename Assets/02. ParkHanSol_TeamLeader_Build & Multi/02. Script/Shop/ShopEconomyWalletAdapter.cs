using System;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    /// <summary>
    /// Scene-facing wallet adapter. Network sessions read and mutate only the
    /// persistent run economy ledger; standalone scenes retain the local wallet.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopEconomyWalletAdapter : NetworkBehaviour, IShopWallet
    {
        private IWallet offlineWallet;
        private NetworkRunEconomyLedger networkLedger;
        private bool isOfflineWalletSubscribed;
        private bool isNetworkLedgerSubscribed;
        private bool isRootAvailabilitySubscribed;

        public bool IsReady => IsNetworkSessionActive()
            ? IsSpawned && networkLedger != null && networkLedger.Revision > 0U
            : offlineWallet != null;

        public int Credits => IsNetworkSessionActive()
            ? networkLedger?.Credits ?? 0
            : offlineWallet?.Balance ?? 0;

        public event Action<int> CreditsChanged;

        private void Start()
        {
            if (IsNetworkSessionActive())
            {
                return;
            }

            if (BindOfflineWallet())
            {
                CreditsChanged?.Invoke(offlineWallet.Balance);
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            UnbindOfflineWallet();
            SubscribeRootAvailability();
            if (BindNetworkLedger())
            {
                SubscribeNetworkLedger();
                CreditsChanged?.Invoke(networkLedger.Credits);
            }
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeRootAvailability();
            UnsubscribeNetworkLedger();
            UnbindOfflineWallet();
            networkLedger = null;
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            UnsubscribeRootAvailability();
            UnsubscribeNetworkLedger();
            UnbindOfflineWallet();
            networkLedger = null;
            base.OnDestroy();
        }

        public bool TryAddCredits(int amount)
        {
            if (!ValidateMutation(amount, "add"))
            {
                return false;
            }

            if (!IsNetworkSessionActive())
            {
                offlineWallet.Add(amount);
                return true;
            }

            var transactionId = CreateLedgerTransactionId("add");
            if (networkLedger.TryAddCreditsServer(
                    transactionId,
                    amount,
                    NetworkRunEconomyTransactionKind.RewardCredit,
                    NetworkManager.ServerClientId,
                    out var reason))
            {
                return true;
            }

            Debug.LogError(
                $"PHS_SHOP_WALLET_ADD_FAILED reason={reason} adapter={name} transaction={transactionId}",
                this);
            return false;
        }

        public bool TrySpendCredits(int amount)
        {
            if (!ValidateMutation(amount, "spend"))
            {
                return false;
            }

            if (!IsNetworkSessionActive())
            {
                return offlineWallet.TrySpend(amount);
            }

            var transactionId = CreateLedgerTransactionId("spend");
            if (networkLedger.TrySpendCreditsServer(
                    transactionId,
                    amount,
                    NetworkRunEconomyTransactionKind.PenaltyDebit,
                    NetworkManager.ServerClientId,
                    out var reason))
            {
                return true;
            }

            Debug.LogWarning(
                $"PHS_SHOP_WALLET_SPEND_FAILED reason={reason} adapter={name} transaction={transactionId}",
                this);
            return false;
        }

        private bool BindNetworkLedger()
        {
            var runSessionRoot = NetworkRunSessionRoot.Instance;
            if (runSessionRoot == null
                || !runSessionRoot.IsSpawned
                || runSessionRoot.Economy == null)
            {
                return false;
            }

            if (networkLedger == runSessionRoot.Economy)
            {
                return true;
            }

            UnsubscribeNetworkLedger();
            networkLedger = runSessionRoot.Economy;
            return true;
        }

        private void SubscribeRootAvailability()
        {
            if (isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable += HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = true;
        }

        private void UnsubscribeRootAvailability()
        {
            if (!isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable -= HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = false;
        }

        private bool BindOfflineWallet()
        {
            if (offlineWallet != null)
            {
                SubscribeOfflineWallet();
                return true;
            }

            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_WALLET_BIND_FAILED reason=game_core_missing adapter={name}",
                    this);
                return false;
            }

            offlineWallet = gameCore.Services.Get<IWallet>();
            if (offlineWallet == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_WALLET_BIND_FAILED reason=economy_wallet_missing adapter={name}",
                    this);
                return false;
            }

            SubscribeOfflineWallet();
            return true;
        }

        private void SubscribeOfflineWallet()
        {
            if (offlineWallet == null || isOfflineWalletSubscribed)
            {
                return;
            }

            offlineWallet.BalanceChanged += HandleOfflineBalanceChanged;
            isOfflineWalletSubscribed = true;
        }

        private void UnbindOfflineWallet()
        {
            if (offlineWallet != null && isOfflineWalletSubscribed)
            {
                offlineWallet.BalanceChanged -= HandleOfflineBalanceChanged;
            }

            isOfflineWalletSubscribed = false;
            offlineWallet = null;
        }

        private void SubscribeNetworkLedger()
        {
            if (networkLedger == null || isNetworkLedgerSubscribed)
            {
                return;
            }

            networkLedger.SnapshotChanged += HandleNetworkSnapshotChanged;
            isNetworkLedgerSubscribed = true;
        }

        private void UnsubscribeNetworkLedger()
        {
            if (networkLedger != null && isNetworkLedgerSubscribed)
            {
                networkLedger.SnapshotChanged -= HandleNetworkSnapshotChanged;
            }

            isNetworkLedgerSubscribed = false;
        }

        private bool ValidateMutation(int amount, string operation)
        {
            if (amount <= 0)
            {
                Debug.LogError(
                    $"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=invalid_amount adapter={name} amount={amount}",
                    this);
                return false;
            }

            if (IsNetworkSessionActive())
            {
                if (!IsSpawned || !IsServer)
                {
                    Debug.LogError(
                        $"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=server_required adapter={name}",
                        this);
                    return false;
                }

                if (networkLedger == null || networkLedger.Revision == 0U)
                {
                    Debug.LogError(
                        $"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=ledger_unbound adapter={name}",
                        this);
                    return false;
                }

                return true;
            }

            if (offlineWallet == null)
            {
                Debug.LogError(
                    $"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=wallet_unbound adapter={name}",
                    this);
                return false;
            }

            return true;
        }

        private string CreateLedgerTransactionId(string operation)
        {
            return $"wallet:{operation}:{NetworkObjectId}:{networkLedger.Revision + 1U}";
        }

        private void HandleOfflineBalanceChanged(int balance)
        {
            CreditsChanged?.Invoke(balance);
        }

        private void HandleNetworkSnapshotChanged(
            NetworkRunEconomySnapshot previous,
            NetworkRunEconomySnapshot current)
        {
            if (previous.Credits != current.Credits)
            {
                CreditsChanged?.Invoke(current.Credits);
            }
        }

        private void HandleRunSessionRootAvailable(NetworkRunSessionRoot runSessionRoot)
        {
            if (!IsSpawned || runSessionRoot == null || !BindNetworkLedger())
            {
                return;
            }

            SubscribeNetworkLedger();
            CreditsChanged?.Invoke(networkLedger.Credits);
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }
    }
}
