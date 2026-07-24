using System;
using Unity.Collections;
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
        private const string PreferenceKey =
            Customization.LobbyCustomizationProfileKeys.Credits;

        [Header("Lobby Customization Credits")]
        [SerializeField, Min(0)] private int startingCredits = 300;
        [SerializeField, Min(1)] private int maximumCredits = 999999;

        private bool hasServerInitialized;
        private bool ownerPersistenceReady;
        private bool ownerProfileReady;
        private string ownerProfileFailureReason = string.Empty;

        // Cosmetic credits are private to their owner. Only the server may change them.
        private readonly NetworkVariable<int> credits = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> Credits => credits;
        public int CurrentCredits => credits.Value;
        public bool IsProfileReady => ownerProfileReady;
        public string ProfileFailureReason => ownerProfileFailureReason;

        public event Action StateChanged;

        public override void OnNetworkSpawn()
        {
            credits.OnValueChanged += HandleCreditsChanged;

            if (!IsOwner)
            {
                return;
            }

            if (!TryLoadPersonalCredits(out var savedCredits, out var reason))
            {
                ownerProfileFailureReason = reason;
                Debug.LogError(
                    $"PHS_PERSONAL_CREDITS_LOAD_FAILED reason={reason} player={name}",
                    this);
                StateChanged?.Invoke();
                return;
            }

            RequestLoadPersonalCreditsServerRpc(savedCredits);
        }

        public override void OnNetworkDespawn()
        {
            credits.OnValueChanged -= HandleCreditsChanged;

            if (IsOwner && ownerPersistenceReady)
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
                RejectProfileLoadServer("credits_already_initialized");
                return;
            }

            hasServerInitialized = true;

            if (savedCredits < 0 || savedCredits > maximumCredits)
            {
                Debug.LogError(
                    $"PHS_PERSONAL_CREDITS_LOAD_FAILED reason=saved_value_out_of_range " +
                    $"value={savedCredits} maximum={maximumCredits} player={name}",
                    this);
                RejectProfileLoadServer("saved_credits_out_of_range");
                return;
            }

            credits.Value = savedCredits;
            ConfirmProfileLoadedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        [ClientRpc]
        private void ConfirmProfileLoadedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerPersistenceReady = true;
            ownerProfileReady = true;
            ownerProfileFailureReason = string.Empty;
            StateChanged?.Invoke();
        }

        [ClientRpc]
        private void RejectProfileLoadClientRpc(
            FixedString128Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerProfileReady = false;
            ownerProfileFailureReason = reason.ToString();
            StateChanged?.Invoke();
        }

        private void RejectProfileLoadServer(string reason)
        {
            RejectProfileLoadClientRpc(new FixedString128Bytes(reason), new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        private bool TryLoadPersonalCredits(out int savedCredits, out string reason)
        {
            if (!PlayerPrefs.HasKey(PreferenceKey))
            {
                if (startingCredits < 0 || startingCredits > maximumCredits)
                {
                    savedCredits = 0;
                    reason = $"starting_credits_out_of_range:{startingCredits}:{maximumCredits}";
                    return false;
                }

                savedCredits = startingCredits;
                reason = null;
                return true;
            }

            savedCredits = PlayerPrefs.GetInt(PreferenceKey);
            if (savedCredits < 0 || savedCredits > maximumCredits)
            {
                reason = $"saved_credits_out_of_range:{savedCredits}:{maximumCredits}";
                return false;
            }

            reason = null;
            return true;
        }

        private void HandleCreditsChanged(int previousValue, int currentValue)
        {
            if (IsOwner && ownerPersistenceReady)
            {
                SavePersonalCredits(currentValue);
            }

            StateChanged?.Invoke();
        }

        private static void SavePersonalCredits(int credits)
        {
            PlayerPrefs.SetInt(PreferenceKey, credits);
            PlayerPrefs.Save();
        }
    }
}
