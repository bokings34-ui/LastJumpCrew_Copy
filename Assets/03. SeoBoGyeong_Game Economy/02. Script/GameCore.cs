using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 게임 전역 허브 싱글톤. 얇게 유지하고 실제 일은 하위 매니저가 담당한다.
    /// 접근: GameCore.Instance.Data.~ / GameCore.Instance.Loop.~
    /// </summary>
    public class GameCore : MonoBehaviour
    {
        public static GameCore Instance { get; private set; }


        [SerializeField] private DataManager data;
        public DataManager Data => data;

        // 런타임 게임 루프 상태(데이터만 보관). 전이 규칙은 GameLoopController 가 처리.
        public GameLoopState Loop { get; private set; } = new();

        // TODO( NGO 병합 후): NetworkManager 참조 등록
        // public NetworkManager Net => net;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Init();
        }

        private void Init()
        {
            // 초기화 순서: 데이터 -> (네트워크) -> 플레이어 생성
            if (data == null)
            {
                Debug.LogError("[GameCore] DataManager 가 인스펙터에 연결되지 않았습니다.");
                return;
            }

            data.Init();
            // TODO(병합 후): net.Init();  // 여기서 NetworkManager 시작
        }
    }
}
