using System;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ShopEconomyWalletAdapter : NetworkBehaviour, IShopWallet
    {
        private readonly NetworkVariable<int> synchronizedBalance = new(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private IWallet wallet;
        private bool isWalletBalanceSubscribed;
        private bool isSynchronizedBalanceSubscribed;

        public bool IsReady => IsNetworkSessionActive()
            ? IsSpawned && synchronizedBalance.Value >= 0 && (!IsServer || wallet != null)
            : wallet != null;

        public int Credits => IsNetworkSessionActive()
            ? Mathf.Max(0, synchronizedBalance.Value)
            : wallet?.Balance ?? 0;

        public event Action<int> CreditsChanged;

        private void Start()
        {
            if (IsNetworkSessionActive())
            {
                return;
            }

            if (BindWallet())
            {
                CreditsChanged?.Invoke(wallet.Balance);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                if (!BindWallet())
                {
                    return;
                }

                synchronizedBalance.Value = wallet.Balance;
            }
            else
            {
                UnbindWallet();
            }

            SubscribeSynchronizedBalance();
            CreditsChanged?.Invoke(synchronizedBalance.Value);
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeSynchronizedBalance();
            UnbindWallet();
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            UnsubscribeSynchronizedBalance();
            UnbindWallet();
            base.OnDestroy();
        }

        public bool TryAddCredits(int amount)
        {
            if (!ValidateMutation(amount, "add"))
            {
                return false;
            }

            wallet.Add(amount);
            return true;
        }

        public bool TrySpendCredits(int amount)
        {
            if (!ValidateMutation(amount, "spend"))
            {
                return false;
            }

            return wallet.TrySpend(amount);
        }

        private bool BindWallet()
        {
            if (wallet != null)
            {
                SubscribeWalletBalance();
                return true;
            }

            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                Debug.LogError($"PHS_SHOP_WALLET_BIND_FAILED reason=game_core_missing adapter={name}", this);
                return false;
            }

            wallet = gameCore.Services.Get<IWallet>();
            if (wallet == null)
            {
                Debug.LogError($"PHS_SHOP_WALLET_BIND_FAILED reason=economy_wallet_missing adapter={name}", this);
                return false;
            }

            SubscribeWalletBalance();
            return true;
        }

        private void SubscribeWalletBalance()
        {
            if (wallet == null || isWalletBalanceSubscribed)
            {
                return;
            }

            wallet.BalanceChanged += HandleWalletBalanceChanged;
            isWalletBalanceSubscribed = true;
        }

        private void UnbindWallet()
        {
            if (wallet != null && isWalletBalanceSubscribed)
            {
                wallet.BalanceChanged -= HandleWalletBalanceChanged;
            }

            isWalletBalanceSubscribed = false;
            wallet = null;
        }

        private void SubscribeSynchronizedBalance()
        {
            if (isSynchronizedBalanceSubscribed)
            {
                return;
            }

            synchronizedBalance.OnValueChanged += HandleSynchronizedBalanceChanged;
            isSynchronizedBalanceSubscribed = true;
        }

        private void UnsubscribeSynchronizedBalance()
        {
            if (!isSynchronizedBalanceSubscribed)
            {
                return;
            }

            synchronizedBalance.OnValueChanged -= HandleSynchronizedBalanceChanged;
            isSynchronizedBalanceSubscribed = false;
        }

        private bool ValidateMutation(int amount, string operation)
        {
            if (IsNetworkSessionActive() && !IsServer)
            {
                Debug.LogError($"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=server_required adapter={name}", this);
                return false;
            }

            if (wallet == null)
            {
                Debug.LogError($"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=wallet_unbound adapter={name}", this);
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogError($"PHS_SHOP_WALLET_{operation.ToUpperInvariant()}_FAILED reason=invalid_amount adapter={name} amount={amount}", this);
                return false;
            }

            return true;
        }

        private void HandleWalletBalanceChanged(int balance)
        {
            if (IsNetworkSessionActive())
            {
                if (!IsServer)
                {
                    Debug.LogError($"PHS_SHOP_WALLET_SYNC_FAILED reason=server_required adapter={name}", this);
                    return;
                }

                synchronizedBalance.Value = balance;
                return;
            }

            CreditsChanged?.Invoke(balance);
        }

        private void HandleSynchronizedBalanceChanged(int previousBalance, int currentBalance)
        {
            CreditsChanged?.Invoke(currentBalance);
        }

        private static bool IsNetworkSessionActive()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening;
        }
    }
}
