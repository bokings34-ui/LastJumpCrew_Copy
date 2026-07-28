================================================================
  게임 경제(Game Economy) - 코드 기능 & 연동 API 설명서
================================================================

  담당   : 서보경 (03. SeoBoGyeong_Game Economy)
  기준일 : 2026-07-13
  범위   : 2단계(로컬 완성) 코드

  이 문서는 이미 작성된 코드의 기능과, 다른 스크립트가 직접 붙일 수
  있는 연동 API를 설명하는 레퍼런스다.
  게임 경제 파트(게임 루프 / 구역 / 상점 / 재화 / 세이브)는 전부
  'GameCore' 라는 하나의 창구로만 열려 있다.
  NGO(온라인 멀티) 3단계로 넘어가도 아래 인터페이스는 바뀌지 않는다.
  -> 지금 붙여둔 코드는 그대로 동작한다.


  ----------------------------------------------------------------
   목차
  ----------------------------------------------------------------
   A. 개요 & 접근 규칙
   B. 스크립트별 기능 설명
   C. 연동 API 레퍼런스
   D. 연동 지점 (무슨 작업이 어느 API에 붙는가)
   E. 상점 구매 흐름 (시퀀스)
   F. 주의사항 & 확장 지점



================================================================
  A. 개요 & 접근 규칙
================================================================

  [ 유일한 진입점 - GameCore.Instance ]

   - 씬에 GameCore 오브젝트가 1개 있고, DontDestroyOnLoad 로
     씬이 바뀌어도 유지된다.
   - 다른 스크립트는 구체 클래스를 직접 참조하지 않고,
     GameCore.Instance 가 노출하는 4개 창구 + 재화 지갑으로만
     접근한다.

   접근 창구
     · GameCore.Instance.Data
         타입: DataManager
         담는 것: 정적 데이터(아이템/구역) 조회
     · GameCore.Instance.State
         타입: IGameStateProvider
         담는 것: 런타임 상태 '읽기' (단계/타이머 등)
     · GameCore.Instance.Commands
         타입: IGameCommands
         담는 것: 런타임 '명령' (시작/점프/구매 등)
     · GameCore.Instance.Profile
         타입: IPlayerProfile
         담는 것: 지속(세이브) 데이터 (Token/치장)
     · GameCore.Instance.Services.Get<IWallet>()
         타입: IWallet
         담는 것: 재화 지갑(Credit)


  [ 반드시 지킬 규칙 2가지 ]

   1) 시작 시 1회 캐싱
      창구 프로퍼티와 Services.Get<T>() 는 내부적으로 딕셔너리
      조회다. Update() 에서 매 프레임 부르지 말고, Start()/스폰
      시점에 지역 필드에 담아두고 쓴다.

   2) 인터페이스로만
      LocalGameSession 같은 구체 클래스를 직접 잡지 말 것.
      3단계에서 NetworkGameSession 으로 교체돼도 인터페이스만
      쓰면 코드 변경이 없다.

   권장 패턴: Start에서 캐싱

       private IGameCommands _commands;
       private IGameStateProvider _state;

       private void Start()
       {
           _commands = GameCore.Instance.Commands;
           _state    = GameCore.Instance.State;
           _state.StateChanged += OnGameStateChanged; // 필요 시 구독
       }

   참고) 미등록 서비스를 조회하면 ServiceRegistry 가 에러 로그를
   남기고 null 을 반환한다. null 체크로 방어하고, 임의 대체
   동작을 만들지 말 것.



================================================================
  B. 스크립트별 기능 설명
================================================================

  폴더 구조(02. Script/) 순서. "무슨 역할인지 + 알아둘 주요 멤버"만.


  [ 루트 ]

   GameCore
     전역 허브 싱글톤. 부팅 시 데이터 로드 -> 서비스 등록 ->
     세션 바인딩. 4창구 노출.
     주요 멤버: Instance, Data, State, Commands, Profile, Services

   DataManager
     정적 데이터(아이템/도구/구역)를 DataCatalog 에서 로드해
     저장소로 보관.
     주요 멤버: Items, Tools, Zones, Init()

   DataRepository<T>
     Id(int)로 정적 데이터를 찾는 제네릭 저장소(Dictionary 색인).
     주요 멤버: Get(id), TryGet(id, out), All, Count


  [ Interface/ ]  (계약 - 소비자는 이것만 참조)

   IGameStateProvider  런타임 상태 읽기 + 변경 이벤트
                       (세션 소유, 읽기 전용 노출)
   IGameCommands       클라 명령(요청). 상태를 직접 바꾸지 않음
                       (세션 소유)
   IAuthority          권위(누가 상태를 바꾸나) 판정
                       (로컬 = 항상 서버)
   IWallet             재화 지갑 공통 계약
                       (Credit = 세션, Token = Profile)
   IPlayerProfile      지속 데이터(Token/치장) 접근 (Profile 소유)
   IPlayerProfileStore 세이브 저장/로드 계약 (JSON 구현)
   IShipStatus         함선 생존 여부 조회
                       (현재 Mock - 항상 생존)
   IDeathEventGate     제한시간 초과 시 확정 사망 이벤트 진입점
                       (현재 Mock - 로그만)


  [ GameRule/ ]

   GameLoopState
     게임 루프 런타임 상태(데이터) + 규칙 상수.
     주요 멤버: Phase, ClearedZoneCount,
               상수 SHOP_INTERVAL(4) / TOTAL_ZONES(9) /
               STAGE_TIME_LIMIT(300)

   GameLoopController
     상태 전이 규칙(순수 클래스, SRP). 권위측에서만 호출.
     주요 멤버: StartGame, OnZoneSelected, TryJump,
               TickStageTimer, OnShopClosed, ForceGameOver


  [ Session/ ]

   LocalGameSession
     로컬 게임 세션. 상태 소유 + 명령 실행 + Credit 지갑 소유.
     IGameStateProvider / IGameCommands 구현.
     3단계에서 NetworkGameSession 으로 교체될 자리.
   LocalAuthority
     IAuthority 로컬 구현 - 항상 IsServer=true.
   ServiceRegistry
     인터페이스 기준 구현 등록/조회. Mock->실구현 교체의 중심축.
     Register<T>, Get<T>, TryGet<T>.
   MockShipStatus
     IShipStatus 임시 목 - 항상 생존.
   MockDeathEventGate
     IDeathEventGate 임시 목 - 로그만.


  [ Economy/ ]

   CreditWallet
     런타임 재화(Credit) 지갑. IWallet 구현.
     파티 공유, 판마다 초기화, 저장 안 함.
   TokenWallet
     지속 재화(Token) 지갑. IWallet 구현.
     개인 소지, 영구 저장(Profile이 소유).
   ItemCheckout
     상점 계산대. IInteractable 구현.
     계산 구역의 진열품을 모아 F로 구매 요청.
   RangeItemSpawner
     상점 선반에 판매 아이템 랜덤 진열.
     스폰 직후 ShopItemTag 부착.
   ShopItemTag
     진열품에 판매용 ItemId 를 실어주는 태그.
     계산대가 이걸로 상품을 인식.


  [ Profile/ ]

   PlayerProfile
     지속 데이터 런타임 로직(IPlayerProfile). Token은
     TokenWallet 에 위임, 변경 시 자동 저장.
   PlayerProfileData
     세이브 파일에 직렬화되는 순수 DTO
     (tokens, ownedCosmetics).
   JsonPlayerProfileStore
     IPlayerProfileStore 구현 - persistentDataPath 에
     JSON 저장/로드.


  [ SO/ · Common/ · Editor/ ]

   ItemData        아이템 정의 SO(Id/이름/가격/프리팹/구매·판매
                   여부). IGameData.
   UtilityItemData ItemData 상속 + 아이템 타입/내구도.
                   상점 진열 대상(도구류).
   ZoneData        구역 정의 SO(Id/이름). IGameData.
   DataCatalog     Item/Zone 리스트를 담는 정적 데이터 묶음 SO.
                   DataManager 가 참조.
   CountdownTimer  재사용 카운트다운(순수 로직).
                   Start/Tick/Stop, Remaining/IsExpired.
   DataCatalogEditor  DataCatalog 인스펙터에 "폴더 스캔 ->
                   자동 등록" 버튼 추가(에디터 전용).



================================================================
  C. 연동 API 레퍼런스
================================================================

  호출 예시는 모두 Start() 에서 캐싱했다고 가정한다.


  ----------------------------------------------------------------
   C-1. GameCore.Data  -  정적 데이터 조회 (DataManager)
  ----------------------------------------------------------------
   정적·불변. 동기화 대상이 아니며, 서로 'Id만' 주고받으면 된다.

     Data.Items.Get(int id)            -> ItemData (없으면 null)
     Data.Items.TryGet(id, out item)   -> bool     (안전 조회)
     Data.Tools                        -> 도구류(상점 진열 대상)
     Data.Zones                        -> 구역
     .All / .Count                     -> 전체 목록 / 개수

   예시:
       var item = GameCore.Instance.Data.Items.Get(2101);
       if (item != null)
           Debug.Log($"{item.DisplayName} / {item.Price}G");


  ----------------------------------------------------------------
   C-2. GameCore.State  -  런타임 상태 읽기 (IGameStateProvider)
  ----------------------------------------------------------------
   읽기 전용. 상태를 바꾸려면 C-3의 Commands를 쓴다.

     Phase               현재 단계(ZoneSelect/Play/Shop/
                         GameClear/GameOver ...)
     ClearedZoneCount    클리어한 구역 수 (0~9)
     SelectedZoneId      현재 선택된 구역 Id
     StageTimeRemaining  남은 제한시간(초)
     LastGameOverReason  마지막 게임오버 사유
     event StateChanged        상태가 바뀔 때마다 발행 (UI 갱신용)
     event PurchaseResolved    구매 결과 통지 (itemId, 성공여부)

   예시:
       _state.StateChanged += () =>
       {
           timerText.text =
               Mathf.CeilToInt(_state.StageTimeRemaining).ToString();
           if (_state.Phase == GamePhase.GameOver)
               ShowGameOver(_state.LastGameOverReason);
       };


  ----------------------------------------------------------------
   C-3. GameCore.Commands  -  런타임 명령 (IGameCommands)
  ----------------------------------------------------------------
   명령은 '요청'일 뿐이다. 권위측(세션)이 검증 후 상태를 바꾸고,
   결과는 State 의 이벤트로 돌아온다.

     StartGame()
         로비/UI에서 게임 시작.
         (자동 시작 없음 - 반드시 호출해야 진행)
     SelectZone(int zoneId)
         구역 선택 완료 -> 플레이 시작.
     RequestJump()
         점프(워프) 버튼. 제한시간 내 && 함선 생존이면 클리어.
     CloseShop()
         상점 종료 -> 다음 구역 선택으로.
     ReportGameOver(GameOverReason reason)
         함선 파괴/크루 전멸 등 외부 실패 보고.
     RequestPurchase(int itemId)
         상점 구매 요청. 검증·차감은 세션이 수행.

   예시:
       GameCore.Instance.Commands.SelectZone(zoneId);
       GameCore.Instance.Commands.RequestJump();


  ----------------------------------------------------------------
   C-4. GameCore.Profile  -  지속(세이브) 데이터 (IPlayerProfile)
  ----------------------------------------------------------------
   플레이 밖(로비/메타)에서 쓰는 개인 데이터. 변경 즉시 자동
   저장된다. 인게임 네트워크 상태가 아니다.

     Tokens              보유 Token(메타 치장 재화)
     OwnedCosmetics      보유 치장 아이템 Id 목록
     AddTokens(int)      Token 추가(음수 불가)
     TrySpendTokens(int) 충분하면 차감 후 true, 부족하면 false
     UnlockCosmetic(id)  치장 해금
     HasCosmetic(id)     보유 여부
     event ProfileChanged  프로필 변경 시 발행 (로비 UI 갱신용)

   예시:
       var profile = GameCore.Instance.Profile;
       if (profile.TrySpendTokens(price))
           profile.UnlockCosmetic(cosmeticId);


  ----------------------------------------------------------------
   C-5. 재화 지갑  -  Credit (IWallet)
  ----------------------------------------------------------------
   런타임 Credit 지갑은 세션이 소유하며 레지스트리로 노출된다.
   파티 공유.

     Balance             현재 잔액
     TrySpend(int)       충분하면 차감 후 true
     Add(int)            재화 추가(음수 불가)
     event BalanceChanged  잔액 변경 시 발행 (Action<int>)

   예시:
       var wallet = GameCore.Instance.Services.Get<IWallet>();
       creditText.text = wallet.Balance.ToString();
       wallet.BalanceChanged += v => creditText.text = v.ToString();

   참고) 구매는 보통 Commands.RequestPurchase 로 하고, 지갑 직접
   호출은 잔액 표시나 외부 재화 소비 연동에 쓴다.



================================================================
  D. 연동 지점 - "무슨 작업이 어느 API에 붙는가"
================================================================

  담당자가 아니라, 직접 연결될 작업/기능 단위로 정리한다.


   1) 게임 진행 제어
        붙는 API : Commands.StartGame / SelectZone /
                   RequestJump / CloseShop
        방법     : 입력/UI -> 명령 호출.
                   게임은 StartGame() 호출 전엔 시작되지 않는다.

   2) 함선 생존 판정 연결
        붙는 API : IShipStatus
        방법     : 함선 파괴 판정 로직을 IShipStatus 구현으로
                   만들어 Services.Register<IShipStatus>(impl).
                   현재는 MockShipStatus (항상 생존).

   3) 확정 사망 이벤트 연결
        붙는 API : IDeathEventGate
        방법     : 제한시간 초과 시 연출/사고 이벤트를
                   IDeathEventGate 구현으로 등록.
                   현재는 MockDeathEventGate (로그).

   4) 게임오버 보고
        붙는 API : Commands.ReportGameOver(reason)
        방법     : 크루 전멸·함선 파괴 처리 로직 ->
                   사유와 함께 보고.

   5) 상점 구매 아이템 지급
        붙는 API : State.PurchaseResolved
        방법     : (itemId, true) 수신 시 해당 아이템의
                   IHoldableItem 을 플레이어에게 지급.
                   ItemCheckout.OnPurchaseResolved 의 TODO seam.

   6) 외부 재화 통합
        붙는 API : Services.Get<IWallet>()
        방법     : 별도 재화 소비 로직(예: 고철 판매)이 같은
                   Credit 지갑을 공유해 호출.

   7) UI 표시
        붙는 API : State.StateChanged / State.PurchaseResolved /
                   IWallet.BalanceChanged / Profile.ProfileChanged
        방법     : HUD/상점/로비 UI가 이벤트를 구독해 갱신.


   Services.Register<T> 로 실구현을 등록하는 시점은 스폰/씬 로드
   등 라이프사이클 이벤트에서 1회. Mock을 나중에 실구현으로
   덮어쓰면 소비자 코드는 그대로다.



================================================================
  E. 상점 구매 흐름 (시퀀스)
================================================================

   RangeItemSpawner
        |  스폰 + ShopItemTag(itemId) 부착
        v
    [진열품]
        |  플레이어가 계산 구역 진입
        v
   ItemCheckout.OnTriggerEnter : 장바구니에 담음
        |
        |  플레이어 F 입력
        v
   ItemCheckout.Interact()
        |  Commands.RequestPurchase(itemId)
        v
   LocalGameSession (권위)
        |  서버권위 검증: 아이템 존재? CanBuy? 잔액 충분?
        |  (성공) Credit 차감
        |  PurchaseResolved(itemId, true) 발행
        v
   ItemCheckout.OnPurchaseResolved(itemId, true)
        |  진열품 제거 + 아이템 지급(seam)
        v
      완료

   - 검증·차감은 세션(권위)이 한다. 계산대는 요청과 결과 반영만.
     (서버 권위 원칙 -> 3단계에서 RequestPurchase 가 [ServerRpc],
      PurchaseResolved 가 ClientRpc 로 매핑)
   - 실패 시 세션이 거부 사유를 로그로 남기고 (itemId, false) 를
     통지한다.



================================================================
  F. 주의사항 & 확장 지점
================================================================

   - 서버 권위
       Commands 는 "요청"이다. 상태를 직접 바꾸지 말고
       명령 -> 검증 -> 상태변경 -> 이벤트 흐름을 따른다.

   - 캐싱 규율
       Services.Get<T>() / 창구는 Start() 에서 1회만.
       매 프레임 조회 금지.

   - Mock 교체 지점
       IShipStatus / IDeathEventGate 는 현재 Mock 이다.
       실구현이 준비되면 Services.Register 로 덮어쓴다.

   - 재화 수명 구분
       Credit = 런타임 (판마다 초기화, 저장 안 함, 파티 공유)
       Token  = 지속   (영구 저장, 개인 소지)
       서로 다른 지갑·계층이다.

   - 3단계(NGO) 불변 약속
       위 인터페이스(IGameStateProvider / IGameCommands /
       IWallet / IPlayerProfile 등)는 온라인 멀티 전환 후에도
       바뀌지 않는다. 지금 붙인 코드는 그대로 동작한다.
