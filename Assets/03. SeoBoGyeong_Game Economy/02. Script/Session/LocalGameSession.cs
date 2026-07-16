using System;
using LastJumpCrew.SeoBoGyeong.Economy;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 로컬(비네트워크) 게임 세션. 서버 권위 상태를 소유하고 읽기/명령 인터페이스로 노출한다.
    /// 나중 NGO 연결 시 이 클래스를 NetworkGameSession(NetworkBehaviour)으로 교체한다(구현체만 교체).
    /// - 상태: GameLoopState (권위측만 변경)
    /// - 규칙: GameLoopController (순수 클래스, 카운트다운은 공용 CountdownTimer 위임)
    /// - 재화: CreditWallet (런타임·파티 공유. StartGame 시 초기화, 세션 종료 시 소멸)
    /// - 의존: IAuthority(로컬=서버), IShipStatus/IDeathEventGate(목)
    /// </summary>
    public sealed class LocalGameSession : MonoBehaviour, IGameStateProvider, IGameCommands
    {
        [Tooltip("StartGame 시 초기화되는 파티 공유 Credit 시작 잔액")]
        [SerializeField] private int startingCredits = 500;

        private readonly GameLoopState state = new();
        private IAuthority authority;
        private GameLoopController rules;
        private DataManager data;
        private CreditWallet creditWallet;

        // 나중 교체 지점: 목 -> 실제 구현
        private IShipStatus ship;
        private IDeathEventGate deathGate;

        // ── IGameStateProvider (읽기 전용) ──
        public GamePhase Phase => state.Phase;
        public int ClearedZoneCount => state.ClearedZoneCount;
        public int SelectedZoneId => state.SelectedZoneId;
        public float StageTimeRemaining => state.StageTimeRemaining;
        public GameOverReason LastGameOverReason => state.LastGameOverReason;
        public event Action StateChanged;
        public event Action<int, bool> PurchaseResolved;

        private void Awake()
        {
            // IAuthority는 네트워크 역할(나중 NetworkBehaviour.IsServer 매핑)이라 로컬 소유 유지.
            authority = new LocalAuthority();
            
        }

        /// <summary>
        /// GameCore.Init()에서 레지스트리·데이터 주입. 의존성(ship/deathGate)을 resolve하고 규칙을 구성한다.
        /// Credit 지갑(파티 공유)을 만들어 IWallet 으로 등록 — 소비자는 Services.Get&lt;IWallet&gt;() 로 접근.
        /// 나중 NetworkGameSession으로 교체 시 OnNetworkSpawn 등에서 같은 역할 수행.
        /// 게임 시작은 로비/UI 가 Commands.StartGame() 을 호출한다(자동 시작 없음).
        /// </summary>
        public void Bind(ServiceRegistry registry, DataManager dataManager)
        {
            ship = registry.Get<IShipStatus>();
            deathGate = registry.Get<IDeathEventGate>();
            data = dataManager;
            rules = new GameLoopController(state, ship, deathGate);

            // Credit 지갑은 세션 소유(런타임 수명 일치). 나중 NetworkVariable<int> 로 승격.
            creditWallet = new CreditWallet(500);//임시로 500
            registry.Register<IWallet>(creditWallet);
        }

        private void Update()
        {
            if (rules == null) return;   // Bind 이전 프레임 방어
            if (!authority.IsServer) return;
            if (rules.TickStageTimer(Time.deltaTime)) Raise();
        }

        // ── IGameCommands (클라이언트 의도) ──
        public void StartGame()
        {
            if (!authority.IsServer) return;
            rules.StartGame();
            creditWallet.ResetBalance(startingCredits); // 휘발성 재화 — 판 시작마다 초기화
            Raise();
        }

        public void SelectZone(int zoneId)
        {
            if (!authority.IsServer) return;
            rules.OnZoneSelected(zoneId);
            Raise();
        }

        public void RequestJump()
        {
            if (!authority.IsServer) return;
            if (rules.TryJump()) Raise();
            else Debug.Log("[GameLoop] 점프 거부: 제한시간 초과 또는 함선 파괴 상태");
        }

        public void SetStageTimerPaused(bool paused)
        {
            if (!authority.IsServer) return;
            rules.SetStageTimerPaused(paused);
            Raise();
        }

        public void CloseShop()
        {
            if (!authority.IsServer) return;
            rules.OnShopClosed();
            Raise();
        }

        public void ReportGameOver(GameOverReason reason)
        {
            if (!authority.IsServer) return;
            rules.ForceGameOver(reason);
            Raise();
        }

        /// <summary>
        /// 상점 구매 요청. 권위측 검증(아이템 존재·구매 가능·잔액) 후 Credit 차감.
        /// 결과는 PurchaseResolved 로 통지 — 나중 NGO 연결 시 이 메서드가 [ServerRpc],
        /// 통지가 ClientRpc 로 매핑된다.
        /// </summary>
        public void RequestPurchase(int itemId)
        {
            if (!authority.IsServer) return;

            bool success = TryPurchase(itemId, out string reason);
            if (!success) Debug.Log($"[Shop] 구매 거부 (itemId={itemId}): {reason}");

            PurchaseResolved?.Invoke(itemId, success);
            if (success) Raise();
        }

        private bool TryPurchase(int itemId, out string reason)
        {
            if (data == null) { reason = "데이터 미연결(Bind 전)"; return false; }

            var item = data.Items.Get(itemId);
            if (item == null) { reason = "존재하지 않는 아이템"; return false; }
            if (!item.CanBuy) { reason = "구매 불가 아이템"; return false; }
            if (!creditWallet.TrySpend(item.Price))
            {
                reason = $"잔액 부족 (가격 {item.Price} / 잔액 {creditWallet.Balance})";
                return false;
            }

            reason = null;
            return true;
        }

        private void Raise() => StateChanged?.Invoke();
    }
}
