using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    /// <summary>Keeps the Host-local wallet and pending delivery queue alive across shop and ship scene loads.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class SessionPurchaseStateRoot : MonoBehaviour
    {
        private static SessionPurchaseStateRoot instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
