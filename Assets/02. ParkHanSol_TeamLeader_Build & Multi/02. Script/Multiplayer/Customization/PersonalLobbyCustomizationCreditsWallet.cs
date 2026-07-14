using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    /// <summary>
    /// Owns one local profile's Lobby Customization Credit balance.
    /// This is intentionally separate from SessionPartyCreditsWallet: it is for cosmetic
    /// purchases only, persists in PlayerPrefs, and must not be spent by session systems.
    /// </summary>
    public sealed class PersonalLobbyCustomizationCreditsWallet : NetworkBehaviour
    {
        private const string PreferenceKey = "PHS_PersonalLobbyCustomizationCredits_v1";

        [Header("Lobby Customization Credits")]
        [SerializeField, Min(0)] private int startingCredits = 300;
        [SerializeField, Min(1)] private int maximumCredits = 999999;

        private bool hasServerInitialized;

        // Cosmetic credits are private to their owner. Only the server may change them.
        private readonly NetworkVariable<int> credits = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> Credits => credits;
        public int CurrentCredits => credits.Value;

        public override void OnNetworkSpawn()
        {
            credits.OnValueChanged += HandleCreditsChanged;

            if (!IsOwner)
            {
                return;
            }

            RequestLoadPersonalCreditsServerRpc(LoadPersonalCredits());
        }

        public override void OnNetworkDespawn()
        {
            credits.OnValueChanged -= HandleCreditsChanged;

            if (IsOwner)
            {
                SavePersonalCredits(credits.Value);
            }
        }

        /// <summary>
        /// Server-only shop API. A failed purchase never changes this wallet.
        /// </summary>
        public bool TrySpendCreditsServer(int amount)
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_SPEND_FAILED reason=server_required player={name}");
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_SPEND_FAILED reason=amount_invalid amount={amount} player={name}");
                return false;
            }

            if (credits.Value < amount)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_SPEND_FAILED reason=insufficient balance={credits.Value} amount={amount} player={name}");
                return false;
            }

            credits.Value -= amount;
            return true;
        }

        /// <summary>
        /// Server-only reward API for clear/fail result systems. Shop code must not call this.
        /// </summary>
        public bool TryAddRewardCreditsServer(int amount)
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_REWARD_FAILED reason=server_required player={name}");
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_REWARD_FAILED reason=amount_invalid amount={amount} player={name}");
                return false;
            }

            if (credits.Value > maximumCredits - amount)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_REWARD_FAILED reason=maximum_exceeded balance={credits.Value} amount={amount} maximum={maximumCredits} player={name}");
                return false;
            }

            credits.Value += amount;
            return true;
        }

        [ServerRpc]
        private void RequestLoadPersonalCreditsServerRpc(int savedCredits, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_LOAD_FAILED reason=owner_mismatch player={name}");
                return;
            }

            if (hasServerInitialized)
            {
                Debug.LogError($"PHS_PERSONAL_CREDITS_LOAD_FAILED reason=already_initialized player={name}");
                return;
            }

            hasServerInitialized = true;
            credits.Value = Mathf.Clamp(savedCredits, 0, maximumCredits);
        }

        private int LoadPersonalCredits()
        {
            if (!PlayerPrefs.HasKey(PreferenceKey))
            {
                return Mathf.Clamp(startingCredits, 0, maximumCredits);
            }

            return Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey), 0, maximumCredits);
        }

        private void HandleCreditsChanged(int previousValue, int currentValue)
        {
            if (IsOwner)
            {
                SavePersonalCredits(currentValue);
            }
        }

        private static void SavePersonalCredits(int credits)
        {
            PlayerPrefs.SetInt(PreferenceKey, credits);
            PlayerPrefs.Save();
        }
    }
}
