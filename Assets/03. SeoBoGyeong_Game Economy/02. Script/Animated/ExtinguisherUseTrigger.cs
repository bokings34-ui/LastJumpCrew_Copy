using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 소화기를 "누르는 동안" NetworkedEffectToggle 을 켜 두는 트리거 어댑터.
    ///
    /// [원리] 팀의 아이템 사용 파이프라인(TempPlayerInteractionScanner)은
    ///  든 아이템이 IContinuousUsableItem 이면 "버튼을 누르는 매 프레임" Use() 를 반복 호출한다.
    ///  그 반복 호출을 그대로 토글의 keep-alive 핑으로 전달한다.
    ///  버튼을 떼면 호출이 멈추고, keepAliveDuration 뒤 토글이 스스로 꺼진다(뗌 신호 불필요).
    ///
    /// [불 대상 불필요] 기존 FireExtinguisherUsableItem(이벤트 수리형)과 달리 대상(target)을 요구하지 않는다.
    ///  → 애니메이션/이펙트 구동·테스트용. 실제 소화(데미지/사고 진압) 판정은 팀 combat 파이프라인의 별도 책임.
    ///
    /// [배치] 소화기 아이템 오브젝트에 NetworkedEffectToggle 과 함께 붙인다.
    ///  이 아이템에는 사용 컴포넌트(IUsableItem)가 이거 하나만 있어야 한다(스캐너는 첫 IUsableItem 만 집는다).
    ///
    /// ※ 멀티 동기화는 토글 쪽 규칙을 따른다: 토글이 붙은 오브젝트가 스폰된 NetworkObject 이고
    ///   핑을 보내는 클라이언트가 그 소유자(또는 서버)여야 전 클라에 반영된다.
    ///   스폰 전(로컬 단독 실행)에는 토글이 로컬 경로로 동작하므로 애니메이션 확인은 바로 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExtinguisherUseTrigger : MonoBehaviour, IUsableItem, IContinuousUsableItem
    {
        [Tooltip("켤 대상 토글. 비워두면 같은 오브젝트에서 자동으로 찾는다.")]
        [SerializeField] private NetworkedEffectToggle toggle;

        private void Awake()
        {
            if (toggle == null)
            {
                toggle = GetComponent<NetworkedEffectToggle>();
            }
            if (toggle == null)
            {
                Debug.LogError($"EXTINGUISHER_TRIGGER_FAILED reason=toggle_missing obj={name}", this);
            }
        }

        // 대상 없이도 사용 가능. 토글만 연결돼 있으면 OK.
        public bool CanUse(IItemHolder user, IInteractable target)
        {
            return toggle != null;
        }

        // 누르는 동안 매 프레임 호출된다 → keep-alive 핑. 떼면 호출이 멈춰 자동 정지.
        public void Use(IItemHolder user, IInteractable target)
        {
            if (toggle == null)
            {
                return;
            }
            toggle.KeepAlivePing();
        }
    }
}
