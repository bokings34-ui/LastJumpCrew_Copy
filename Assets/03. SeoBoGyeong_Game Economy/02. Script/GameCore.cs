using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 전역 허브 싱글톤.
    /// 실제 일은 하위 매니저/세션이 담당.
    /// 접근: GameCore.Instance.Data.~ / GameCore.Instance.State.~ / GameCore.Instance.Commands.~
    /// 사용하는 스크립트에서 경로 저장해 놓기
    /// </summary>
    public class GameCore : MonoBehaviour
    {
        public static GameCore Instance { get; private set; }


        [SerializeField] private DataManager data;
        // 게임 세션(상태 소유). 지금은 LocalGameSession, 나중 NetworkGameSession 으로 교체.
        [SerializeField] private LocalGameSession session;

        public DataManager Data => data;

        

        /// <summary>인터페이스 기준 서비스 등록/조회. Mock→실구현 교체의 중심축.</summary>
        public ServiceRegistry Services { get; private set; }

        /// <summary>읽기 전용 상태(UI·플레이어·이벤트가 참조). 레지스트리 resolve 단축키.</summary>
        public IGameStateProvider State => Services.Get<IGameStateProvider>();

        /// <summary>클라 의도(명령) 창구. 레지스트리 resolve 단축키.</summary>
        public IGameCommands Commands => Services.Get<IGameCommands>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Init();
        }

        

        private void Init()
        {
            // 초기화 순서: 데이터 -> (나중: 네트워크) -> 세션
            if (data == null)
            {
                Debug.LogError("[GameCore] DataManager 가 인스펙터에 연결되지 않았습니다.");
                return;
            }
            if (session == null)
            {
                Debug.LogError("[GameCore] GameSession 이 인스펙터에 연결되지 않았습니다.");
                return;
            }

            data.Init();

            // 서비스 레지스트리 구성(문서 §5 순서: 데이터 → 레지스트리 기본 등록)
            Services = new ServiceRegistry();

            // Mock 기본 등록 — 담당 파트 실구현이 오면 Register<T>()로 덮어쓴다(문서 §9)
            Services.Register<IShipStatus>(new MockShipStatus());
            Services.Register<IDeathEventGate>(new MockDeathEventGate());

            // 세션에 레지스트리 주입(의존성 resolve) 후 상태/명령 제공자로 등록
            session.Bind(Services);
            Services.Register<IGameStateProvider>(session);
            Services.Register<IGameCommands>(session);

            // TODO(나중 연결): NetworkGameSession/NetworkManager 시작 → 스폰 콜백에서 Register<T>() 재등록
        }
    }
}
