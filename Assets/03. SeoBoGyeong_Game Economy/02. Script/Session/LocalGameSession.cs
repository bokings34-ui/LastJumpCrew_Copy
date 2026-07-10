using System;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 로컬(비네트워크) 게임 세션. 서버 권위 상태를 소유하고 읽기/명령 인터페이스로 노출한다.
    /// 나중 NGO 연결 시 이 클래스를 NetworkGameSession(NetworkBehaviour)으로 교체한다(구현체만 교체).
    /// - 상태: GameLoopState (권위측만 변경)
    /// - 규칙: GameLoopController (순수 클래스, 카운트다운은 공용 CountdownTimer 위임)
    /// - 의존: IAuthority(로컬=서버), IShipStatus/IDeathEventGate(목)
    /// </summary>
    public sealed class LocalGameSession : MonoBehaviour, IGameStateProvider, IGameCommands
    {
        private readonly GameLoopState state = new();
        private IAuthority authority;
        private GameLoopController rules;

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

        private void Awake()
        {
            // IAuthority는 네트워크 역할(나중 NetworkBehaviour.IsServer 매핑)이라 로컬 소유 유지.
            authority = new LocalAuthority();
        }

        /// <summary>
        /// GameCore.Init()에서 레지스트리 주입. 의존성(ship/deathGate)을 resolve하고 규칙을 구성한다.
        /// 나중 NetworkGameSession으로 교체 시 OnNetworkSpawn 등에서 같은 역할 수행.
        /// </summary>
        public void Bind(ServiceRegistry registry)
        {
            ship = registry.Get<IShipStatus>();
            deathGate = registry.Get<IDeathEventGate>();
            rules = new GameLoopController(state, ship, deathGate);

            // 로컬 테스트 편의: 바인딩 직후 자동으로 게임 시작.
            // TODO(실서비스): 로비/UI에서 StartGame() 을 호출하도록 변경.
            StartGame();
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

        private void Raise() => StateChanged?.Invoke();
    }
}
