using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// "네트워크로 동기화되는 이펙트 on/off 스위치" — 범용 컴포넌트.
    /// 소화기뿐 아니라 다른 상호작용 아이템·배치 오브젝트(함선 장치 등) 어디에나 붙여서
    /// "지금 이펙트가 켜져 있는가?"를 모든 클라이언트에 똑같이 보이게 만든다.
    ///
    /// [역할 분리]
    ///  - 이 컴포넌트: 네트워크 상태(켜짐/꺼짐) 동기화 + 켜고 끄는 타이밍 관리만 한다.
    ///  - 실제 연출: IEffectPresenter 구현체(AnimateFireExtinguisher 등)가 담당. 여기선 그걸 모르고 호출만 한다.
    ///  - 누가 켜고 끄나(트리거): 바깥에서 Activate()/KeepAlivePing()/Deactivate()를 호출한다.
    ///    · 소화기 → 소유 플레이어가 "누르는 동안 매 프레임" KeepAlivePing() 호출
    ///    · 배치 오브젝트 → 서버가 사고 이벤트/상호작용 시점에 Activate()/Deactivate() 호출
    ///
    /// [동기화 방식 = keep-alive(하트비트)]
    ///  기존 소화기 구조와 동일한 방식이다. "뗌(release)" 신호가 따로 없어도,
    ///  "켜라"는 핑이 계속 오는 동안 유지되고, 핑이 끊기면 keepAliveDuration 뒤 자동으로 꺼진다.
    ///
    /// ※ 이 컴포넌트가 스폰되려면 같은 오브젝트(또는 부모)에 NetworkObject 가 있어야 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkedEffectToggle : NetworkBehaviour
    {
        [Header("표현(연출) 대상")]
        [Tooltip("IEffectPresenter 를 구현한 컴포넌트들. (예: AnimateFireExtinguisher) 인터페이스로만 호출하므로 어떤 이펙트든 OK.")]
        [SerializeField] private MonoBehaviour[] presenterSources;

        [Tooltip("켜면 자식 오브젝트에서 IEffectPresenter 를 자동으로도 찾아 붙인다.")]
        [SerializeField] private bool autoFindPresentersInChildren = true;

        [Header("추가 훅(디자이너용)")]
        [Tooltip("이펙트가 켜질 때 실행. 코드 없이 인스펙터에서 소리·라이트 등 자유롭게 연결.")]
        [SerializeField] private UnityEvent onActivated;

        [Tooltip("이펙트가 꺼질 때 실행.")]
        [SerializeField] private UnityEvent onDeactivated;

        [Header("서버 설정 (서버 환경에 맞춰 조정)")]
        [Tooltip("true = 누르는 동안 유지되는 지속형(소화기). false = 명시적으로 끌 때까지 켜져 있는 래치형.")]
        [SerializeField] private bool useKeepAlive = true;

        [Tooltip("keep-alive 유지 시간(초). 마지막 핑 이후 이 시간이 지나면 자동 정지. 서버 틱/지연에 맞춰 조정.")]
        [SerializeField, Min(0.05f)] private float keepAliveDuration = 0.65f;

        // ── 네트워크 동기화 상태 ─────────────────────────────────────────────
        // 읽기: 모두 / 쓰기: 서버만.  → 이펙트는 서버가 정한 값으로 전 클라이언트에 복제되어 "공용으로" 보인다.
        // (늦게 접속한 클라이언트도 스폰 시 현재 값을 받아 자동으로 상태가 맞춰진다.)
        private readonly NetworkVariable<bool> isActive = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // 서버가 관리하는 "이 시각이 지나면 자동 정지" 마감시각(keep-alive용).
        private float keepAliveDeadline;

        // 네트워크 스폰 전(로컬 단독 실행/테스트) 경로용 상태. NetworkVariable 은 스폰 후에만 쓸 수 있어서 따로 둔다.
        private bool localActive;
        private float localKeepAliveDeadline;

        private readonly List<IEffectPresenter> presenters = new List<IEffectPresenter>();

        private void Awake()
        {
            ResolvePresenters();
        }

        public override void OnNetworkSpawn()
        {
            // 상태가 바뀔 때마다 표현을 갱신하도록 구독.
            isActive.OnValueChanged += HandleActiveChanged;
            // 스폰 시점의 현재 값으로 즉시 맞춘다(늦게 접속한 클라이언트 동기화 복원).
            ApplyPresentation(isActive.Value);
        }

        public override void OnNetworkDespawn()
        {
            isActive.OnValueChanged -= HandleActiveChanged;
        }

        // ── 바깥에서 호출하는 트리거 API ────────────────────────────────────

        /// <summary>이펙트를 켠다(래치형). 배치 오브젝트가 상호작용/이벤트 시 서버에서 호출하는 용도.</summary>
        public void Activate() => RequestSetActive(true);

        /// <summary>이펙트를 끈다(래치형).</summary>
        public void Deactivate() => RequestSetActive(false);

        /// <summary>
        /// "지금 켜져 있어라"는 핑. 지속형(소화기)에서 누르는 동안 매 프레임 호출한다.
        /// 핑이 오는 동안 유지되고, 끊기면 keepAliveDuration 뒤 자동으로 꺼진다.
        /// </summary>
        public void KeepAlivePing()
        {
            // ── 서버 연결부 ①: 아직 스폰 전이면 NetworkVariable 을 못 쓰므로 로컬로만 처리 ──
            if (!IsSpawned)
            {
                SetLocalActive(true);
                localKeepAliveDeadline = Time.time + keepAliveDuration;
                return;
            }

            // ── 서버 연결부 ②: 공유 상태는 "서버만" 쓸 수 있다 ──
            if (IsServer)
            {
                // 배치 오브젝트처럼 서버가 소유/호출하는 경우 → 바로 서버 권위로 반영.
                KeepAliveOnServer();
            }
            else if (IsOwner)
            {
                // 아이템처럼 소유 클라이언트가 호출하는 경우 → 서버에 "요청"만 보낸다(직접 못 씀).
                KeepAliveServerRpc();
            }
            // 그 외(소유자도 서버도 아닌 클라이언트)는 상태를 못 바꾼다 — 정상 동작.
        }

        private void RequestSetActive(bool active)
        {
            // ── 서버 연결부 ①: 스폰 전 로컬 경로 ──
            if (!IsSpawned)
            {
                SetLocalActive(active);
                return;
            }

            // ── 서버 연결부 ②: 서버만 쓰기 / 소유자는 요청 ──
            if (IsServer)
            {
                SetActiveOnServer(active);
            }
            else if (IsOwner)
            {
                RequestSetActiveServerRpc(active);
            }
        }

        // ── 서버 권위 실제 처리 (여기서만 NetworkVariable 을 쓴다) ───────────

        private void SetActiveOnServer(bool active)
        {
            isActive.Value = active; // ★ NetworkVariable 쓰기는 서버에서만. 여기서 값이 바뀌면 전 클라에 복제됨.
            if (active && useKeepAlive)
            {
                keepAliveDeadline = Time.time + keepAliveDuration;
            }
        }

        private void KeepAliveOnServer()
        {
            isActive.Value = true;                             // 이미 true면 값 변화 없음(콜백도 안 뜸) → 그냥 아래 마감시각만 연장.
            keepAliveDeadline = Time.time + keepAliveDuration; // 핑이 올 때마다 자동 정지 시각을 뒤로 민다.
        }

        // ── 소유 클라이언트 → 서버 요청 (RPC) ──────────────────────────────
        // RequireOwnership = false : 오브젝트를 소유하지 않은 쪽에서도 요청 가능하게 열어둔다.
        // 필요하면 여기서 ServerRpcParams 로 보낸 사람을 검증해 권한을 더 조일 수 있다.
        [ServerRpc(RequireOwnership = false)]
        private void RequestSetActiveServerRpc(bool active) => SetActiveOnServer(active);

        [ServerRpc(RequireOwnership = false)]
        private void KeepAliveServerRpc() => KeepAliveOnServer();

        // ── 자동 정지 감시 ─────────────────────────────────────────────────
        private void Update()
        {
            if (!useKeepAlive)
            {
                return; // 래치형은 명시적 Deactivate() 로만 꺼지므로 감시 불필요.
            }

            if (IsSpawned)
            {
                // ── 서버 연결부 ③: keep-alive 만료 판정은 서버만 한다(공유 상태 쓰기 주체이므로) ──
                if (!IsServer)
                {
                    return;
                }
                if (isActive.Value && Time.time > keepAliveDeadline)
                {
                    isActive.Value = false; // 핑이 끊겨 시간이 지남 → 자동 정지(전 클라에 복제됨).
                }
            }
            else
            {
                // 스폰 전 로컬 경로도 동일하게 처리.
                if (localActive && Time.time > localKeepAliveDeadline)
                {
                    SetLocalActive(false);
                }
            }
        }

        // ── 상태 → 표현 반영 ───────────────────────────────────────────────

        private void HandleActiveChanged(bool previous, bool current) => ApplyPresentation(current);

        private void SetLocalActive(bool active)
        {
            if (localActive == active)
            {
                return; // 중복 호출 방지.
            }
            localActive = active;
            ApplyPresentation(active);
        }

        private void ApplyPresentation(bool active)
        {
            foreach (var presenter in presenters)
            {
                if (presenter == null)
                {
                    continue;
                }
                if (active)
                {
                    presenter.PlayEffect();
                }
                else
                {
                    presenter.StopEffect();
                }
            }

            if (active)
            {
                onActivated?.Invoke();
            }
            else
            {
                onDeactivated?.Invoke();
            }
        }

        private void ResolvePresenters()
        {
            presenters.Clear();

            if (presenterSources != null)
            {
                foreach (var source in presenterSources)
                {
                    if (source is IEffectPresenter presenter)
                    {
                        presenters.Add(presenter);
                    }
                    else if (source != null)
                    {
                        Debug.LogError(
                            $"NETEFFECT_TOGGLE_FAILED reason=not_IEffectPresenter source={source.GetType().Name} obj={name}",
                            this);
                    }
                }
            }

            if (autoFindPresentersInChildren)
            {
                foreach (var presenter in GetComponentsInChildren<IEffectPresenter>(true))
                {
                    if (!presenters.Contains(presenter))
                    {
                        presenters.Add(presenter);
                    }
                }
            }
        }
    }
}
