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
                // 씬의 구매 서비스는 Inspector 참조 대상으로 남아 있어야 한다.
                // 중복 지속 루트 컴포넌트만 제거하고 같은 오브젝트의 서비스는 유지한다.
                Destroy(this);
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
