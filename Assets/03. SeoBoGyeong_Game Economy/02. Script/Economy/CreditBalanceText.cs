using TMPro;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    /// <summary>
    /// 파티 공유 Credit 잔액을 화면에 표시하는 UI.
    /// 구체 지갑(CreditWallet)이 아니라 인터페이스 IWallet 만 참조하고,
    /// BalanceChanged 이벤트로만 갱신한다(매 프레임 폴링 없음).
    /// 소지 금액 Test용도의 스크립트.
    /// </summary>
    public class CreditBalanceText : MonoBehaviour
    {
        [Tooltip("잔액을 그릴 TMP 텍스트")]
        [SerializeField] private TMP_Text textUI;

        [Tooltip("숫자 앞에 붙일 접두사")]
        [SerializeField] private string prefix = "Credit : $";

        // 규율: Services.Get<T>() 는 딕셔너리 조회 — 매 프레임 금지. 시작 시 1회 캐싱한다.
        private IWallet wallet;

        private void Start()
        {
            // 모든 Awake 가 끝난 뒤 Start 가 돌므로, 이 시점엔 GameCore.Init 이 끝나 IWallet 이 등록돼 있다.
            if (GameCore.Instance == null)
            {
                Debug.LogError("[CreditUI] GameCore.Instance 가 없다 — 씬에 GameCore 가 있는지 확인.");
                return;
            }

            wallet = GameCore.Instance.Services.Get<IWallet>();
            if (wallet == null)
            {
                Debug.LogError("[CreditUI] IWallet 이 등록되지 않았다 — GameCore.Init/Bind 순서 확인.");
                return;
            }

            wallet.BalanceChanged += Refresh;  // 이후 변경분(구매·판 시작 초기화 등) 구독
            Refresh(wallet.Balance);           // 현재 잔액으로 즉시 1회 표시(초기값 시드)
        }

        private void OnDestroy()
        {
            // 이벤트 누수 방지 — 반드시 구독 해제.
            if (wallet != null) wallet.BalanceChanged -= Refresh;
        }

        // 잔액이 바뀔 때마다 호출된다. 텍스트만 갱신한다.
        private void Refresh(int balance)
        {
            if (textUI != null) textUI.text = prefix + balance.ToString();
        }
    }
}
