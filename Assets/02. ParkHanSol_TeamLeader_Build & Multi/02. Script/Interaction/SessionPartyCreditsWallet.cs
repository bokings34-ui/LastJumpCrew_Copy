using System;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [DefaultExecutionOrder(-200)]
    public sealed class SessionPartyCreditsWallet : MonoBehaviour, IPartyCreditsWallet
    {
        public static SessionPartyCreditsWallet Instance { get; private set; }

        [SerializeField, Min(0)] private int credits;

        public int Credits => credits;
        public event Action<int> CreditsChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void AddCredits(int value)
        {
            if (value <= 0)
            {
                Debug.LogError($"PHS_SESSION_CREDITS_ADD_FAILED reason=invalid_value wallet={name} value={value}");
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                Debug.LogError($"PHS_SESSION_CREDITS_ADD_FAILED reason=server_required wallet={name}");
                return;
            }

            credits += value;
            CreditsChanged?.Invoke(credits);
            Debug.Log($"PHS_SESSION_CREDITS_ADDED wallet={name} value={value} total={credits}");
        }

        public bool TrySpendCredits(int value)
        {
            if (value <= 0)
            {
                Debug.LogError($"PHS_SESSION_CREDITS_SPEND_FAILED reason=invalid_value wallet={name} value={value}");
                return false;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && !networkManager.IsServer)
            {
                Debug.LogError($"PHS_SESSION_CREDITS_SPEND_FAILED reason=server_required wallet={name}");
                return false;
            }

            if (credits < value)
            {
                Debug.LogWarning($"PHS_SESSION_CREDITS_SPEND_FAILED reason=insufficient_credits wallet={name} required={value} current={credits}");
                return false;
            }

            credits -= value;
            CreditsChanged?.Invoke(credits);
            Debug.Log($"PHS_SESSION_CREDITS_SPENT wallet={name} value={value} total={credits}");
            return true;
        }
    }
}
