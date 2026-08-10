# 2026-08-05 작업 및 다른 PC 인계 문서

- 작성 기준: 2026-08-05 18:39 KST
- 저장소: `hyunje0609-sudo/LastJumpCrew`
- Unity: `6000.5.2f1`
- 현재 브랜치: `codex/utility-item-data-single-source-20260805`
- 현재 HEAD: `3ff848799d798f522d553bce10298f7835051aea`
- 원격 동기화: `origin/codex/utility-item-data-single-source-20260805`와 `0/0`
- 현재 PR: [#108 feat: unify item lifecycle and data source](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/108)
- PR 상태: `OPEN`, `DRAFT`, `CONFLICTING / DIRTY`, 자동 checks 없음

## 1. 가장 먼저 알아야 할 현재 상태

1. 오늘 기능 커밋은 최신 `3ff84879`까지 원격 브랜치에 push되어 있다.
2. PR #108은 총 4개 고유 커밋이며 현재 GitHub 기준 61 files, `+1635/-558`이다.
3. PR #108 생성 뒤 `main`에 PR #105와 #106이 병합되어 현재 충돌 상태다.
4. 다른 PC에서는 바로 기능 작업을 추가하지 말고 최신 `main`과 충돌을 먼저 정리한다.
5. 팀원 폴더 원본을 일괄 덮어쓰지 않는다. 특히 `Assets/04`, `Assets/05`, `Assets/06`의 최신 `main` 변경을 보존한다.
6. 로컬에 남은 폰트/RenderTexture/패키지 설정 변경은 원격에 없으며 이번 기능 커밋에도 포함하지 않았다.

## 2. 다른 PC에서 시작하는 방법

### 새 clone

```powershell
git clone https://github.com/hyunje0609-sudo/LastJumpCrew.git
Set-Location LastJumpCrew
git fetch --all --prune
git switch --track origin/codex/utility-item-data-single-source-20260805
git lfs pull
git status --short --branch
```

### 기존 clone

작업 중인 변경이 있으면 먼저 별도 브랜치나 커밋으로 보존한다. 다른 사람의 변경을 reset하지 않는다.

```powershell
git fetch origin --prune
git switch codex/utility-item-data-single-source-20260805
git pull --ff-only
git lfs pull
git status --short --branch
```

### Unity 시작

1. Unity Hub에서 `6000.5.2f1`로 프로젝트를 연다.
2. Git LFS 다운로드가 끝난 뒤 씬/프리팹을 연다.
3. `PHS_Map_ver1`이 깨져 보이면 먼저 `git lfs status`와 `git lfs pull`을 확인한다.
4. MCP는 로컬 도구다. `Packages/packages-lock.json`에 `com.anklebreaker.unity-mcp`가 생겨도 커밋하지 않는다.

## 3. 오늘 전체 작업 목록

### 3.1 함선 맵과 휴대용 맵

| 커밋 | 작업 |
|---|---|
| `cc82fb5c` | 함선 맵에 터미널과 배터리 위치 표시 |
| `576d96a4` | 탭 함선 맵을 가로형으로 만들고 가독성 개선 |
| `1c79cc37` | 로컬 싱글 플레이 런처 컴파일 오류 수정 |
| `e8fee111` | 손에 드는 탭 맵을 가로 방향으로 회전 |

### 3.2 PR 정리와 맵 에셋 복원

| PR/커밋 | 결과 |
|---|---|
| PR #100 | `CLOSED`, 직접 병합하지 않음 |
| PR #101 | `MERGED`, #99 안정화와 #100 애니메이션 일부 선별 통합 |
| PR #102 | `MERGED`, 튜토리얼 안내 에셋과 유틸리티 프리팹 복구 |
| PR #103 | `MERGED`, 우주 맵 추가 |
| PR #104 | `MERGED`, 맵 에셋 GUID 복원과 설정 연결 |
| `38069cdd` | MCP lock과 불필요한 vendor meta 변경 제거 |
| `194db4d9` | 맵 vendor 에셋 GUID 복원 |

### 3.3 환경설정, 캐릭터 액션, 테스트 씬

| 커밋 | 작업 |
|---|---|
| `58a98a62` | 환경설정 오디오 실제 연결, 캐릭터 스케일에 안전한 액션 연결 |
| `b94de574` | 독립 플레이어 아이템 모션 테스트 씬 추가 |
| `49363b83` | 테스트 씬 아이템 줍기 활성화, 재생 후 멈추던 행동 애니메이션 loop 보완 |

테스트 목적:

- 빈 씬에서 3인칭 카메라로 플레이어 프리팹 관찰
- 이동, 점프, 아이템 줍기, 들기, 사용, 놓기, 던지기 확인
- 새 애니메이션의 스케일과 loop 상태 확인

### 3.4 튜토리얼 연속 수정

| 커밋 | 작업 |
|---|---|
| `7fc9a131` | 튜토리얼 안내 에셋과 유틸리티 아이템 프리팹 복구 |
| `f72c53cf` | 목표 장판의 불필요한 글자 제거 및 목표 마커 수정 |
| `3b35a7e7` | 뒤집힌 튜토리얼 마커/팝업 facing 수정 |
| `4f5954a8` | 후크 목표물을 고정형으로 만들고 명중 안정화 |
| `74665018` | 후크 위치와 후크 팝업 위치 조정 |
| `ef728af2` | 마지막 승선 상호작용 대상 크기, 판정 범위, 충돌 보강 |
| `834e66c9` | 설명 패널을 왼쪽 아래로 이동 |
| `9b8df398` | 설명 패널을 하단 정중앙으로 최종 배치 |
| `8634d421` | 특정 데브리가 아니라 모든 데브리를 튜토리얼 회수 대상으로 허용 |
| `b02dfa9f` | 외부 튜토리얼 계약과 진행 흐름 복구 |
| `e2adb093` | 튜토리얼 authoring을 고정해 재실행 시 임의 변형 방지 |

튜토리얼 최종 방향:

- 씬의 수동 수정에 의존하지 않고 정본 프리팹과 authoring 계약으로 유지
- 장판, 후크, 상호작용 대상, 설명 패널 위치가 authoring 재실행으로 되돌아가지 않아야 함
- 데브리 회수는 개별 이름이 아니라 데브리 계약으로 판정

### 3.5 상점

커밋 `519d6123 feat: refine shop cadence and price tags`

- 상점 방문 주기를 3/6/9 wave로 변경
- 가격표를 플레이어 머리 위가 아니라 상품 아이템에 부착
- 플레이어 시선과 무관하게 가격표가 보이도록 billboard 처리
- 상품 표시 수량 확대
- 가격 폰트를 두껍고 직선적인 스타일로 변경해 가독성 강화
- 런타임 상점 재고 12개 생성 확인

## 4. PR #108 상세 작업

PR #107은 닫았고, 해당 기능 커밋을 PR #108 첫 커밋으로 옮겨 하나로 통합했다.

### 4.1 `2869d2c5 feat: unify item drop lifecycle`

목적: PR #105 전체를 그대로 덮지 않고 필요한 파손/투척 기능만 현재 구조에 안전하게 반영.

적용 내용:

- 일반 아이템과 배터리 투척에 기존 궤도를 유지하면서 각속도 적용
- `ItemDropMotionProfile`을 회전과 바닥 배치 정본으로 재사용
- 카메라가 바닥을 볼 때 드롭 지점이 바닥 아래로 들어가지 않게 표면 보정
- 아이템 내구도 0에서 손 아이템을 앞으로 배출
- 파손 아이템 서버 자동삭제
- 배터리 첫 충돌 뒤 폭발 처리 및 3초 뒤 삭제
- 렌치, 소화기, 배터리 dropped prefab 연결

최종 PR에서 허용한 `Assets/06` 변경은 아래 5개뿐이다.

- `BatteryThrownImpact.cs`
- `BrokenItemAutoDespawn.cs`
- `BrokenItemAutoDespawn.cs.meta`
- `NetworkPlayerCombatController.cs`
- `ParkHanSol_BatteryPack_00_Dropped.prefab`

주의:

- `Item3.zip`은 모델/프리팹 묶음이 아니고 파손 동작 코드 묶음이었다.
- ZIP 전체 import는 하지 않았다.
- PR #105 원본은 이후 `main`에 병합되었으므로 충돌 해결 때 양쪽 기능을 다시 비교해야 한다.

### 4.2 `fb390a12 refactor: unify utility item data source`

목적: 모든 활성 씬에서 아이템이 하나의 런타임 정본을 보도록 통합.

- 활성 런타임 아이템 데이터 정본: `Assets/02.../04. Data/UtilityItems/`
- `UtilityItemCatalogSO` 항목 18개
- 상점 상품 12개가 같은 canonical `UtilityItemDataSO` 참조
- 플레이어 `NetworkPlayerItemLifecycle`도 같은 Utility Catalog 사용
- 튜토리얼 플레이어 프리팹 체인도 canonical player와 catalog 사용
- 메인 맵 ToolBox/아이템 배출 경로도 같은 catalog 사용
- 상점 구매 서비스와 랜덤 진열 컨트롤러가 같은 Shop Catalog 사용

이 커밋 단독으로는 `Assets/01`과 `Assets/03`의 레거시 복사본도 제거했으나 팀원 소유 범위 침범 우려가 있어 다음 커밋에서 되돌렸다.

### 4.3 `ec08fc7b refactor: scope item truth to active runtime`

목적: 정본은 하나로 유지하되 팀원 폴더를 삭제하거나 고치지 않도록 범위를 축소.

- 실제 Build Settings 활성 씬과 `Assets/02` 런타임만 canonical 정본 사용
- `Assets/01`, `Assets/03`, 추가 `Assets/06` 과잉 변경 복원
- 최종 PR diff에서 `Assets/01`, `Assets/03` 변경 0
- 레거시 타입은 팀원 콘텐츠 컴파일 호환을 위해 남김
- 활성 4개 씬에서 레거시 `UtilityConnect`, `RangeItemSpawner`, 구 ItemPrefabData GUID 의존 0 확인

### 4.4 `3ff84879 feat(gameplay): improve gravity, items, and doors`

#### 중력 상태 플레이어 회전

원인:

- ShipGravity에서도 무중력용 `SmoothDamp`와 최대 회전속도 제한을 사용해 마우스 입력을 늦게 따라감
- 비호스트 Client는 yaw가 ServerRpc 왕복 뒤 적용되어 Host보다 더 느림

수정:

- ShipGravity yaw/pitch는 입력 각도를 즉시 적용
- 비호스트 owner도 RPC 전 로컬 예측 회전 적용
- 서버 RPC와 NetworkTransform 서버 정본은 유지
- 무중력/우주유영의 기존 감쇠는 유지

#### 아이템 내구도 세그먼트 UI

- 숫자 한 줄 대신 고정 패널 내부 사각 칸으로 표시
- 칸 수 정본: `ceil(MaxDurability / DurabilityCostPerUse)`
- 20열 자동 줄바꿈, 정사각형 셀과 중앙 정렬
- 사용 시 오른쪽 칸부터 감소
- 일반 렌치 검증: `100 / 5 = 20칸`, 사용 후 `19칸`
- 150회 아이템도 압축하지 않고 여러 줄로 150칸 표시
- 비용이 0인 내구도 아이템은 fallback하지 않고 오류 로그 후 UI 숨김

주요 파일:

- `ParkHanSolHeldItemDurabilitySegments.cs`
- `ParkHanSolPlayHudMockPresenter.cs`
- `ParkHanSol_PlayHudUI.prefab`
- `PHSPlayHudSingleSourceAuthoring.cs`

#### 문 시스템

메인 맵의 기존 `DoorDoubleSlide` 20개를 대상으로 한다. 팀원 `Assets/05`의 `Black_Ship_ver.prefab` 원본은 수정하지 않고 `Assets/02` 메인 씬에 네트워크 문 계층을 배치했다.

구현:

- 서버 `NetworkList<DoorState>`가 문 내구도, 잠금, 파괴, 열림 상태의 정본
- 적/플레이어 감지 시 잠기지 않은 문 자동 개폐
- 닫힌 문에 Solid Collider와 carving `NavMeshObstacle` 적용
- 문마다 색상 상태가 보이는 상호작용 잠금 버튼 배치
- Client 버튼 요청은 서버 RPC와 플레이어 거리로 검증
- 잠긴 문 근처 적은 주기적으로 공격 애니메이션과 피해 적용
- 내구도 0이면 door leaf 비활성, Collider/NavMesh 차단 해제, 통로 개방
- 기존 `IRepairable` 계약을 사용해 렌치로 복구
- 통과 중 잠그면 즉시 닫지 않고 센서가 비워진 뒤 닫아 캐릭터가 Collider에 갇히지 않게 처리

주요 파일:

- `PHSNetworkShipDoorCoordinator.cs`
- `PHSShipDoorTarget.cs`
- `PHSShipDoorLockButton.cs`
- `PHSShipDoorAuthoring.cs`
- `PHS_Map_ver1.unity`

Authoring/검증 메뉴:

- `Tools/ParkHanSol/Doors/Author Main Map Doors`
- `Tools/ParkHanSol/Doors/Validate Main Map Doors`

현재 씬 연결 수량:

- Door binding 20
- Lock button 20
- Repair/damage target 20
- Solid Collider/NavMeshObstacle 20

## 5. Git/PR 상태와 충돌 처리

### PR 현황

| PR | 상태 | 설명 |
|---|---|---|
| #107 | CLOSED, 미병합 | 아이템 투척 수명주기 초안. 내용은 #108에 포함 |
| #108 | OPEN, DRAFT, CONFLICTING | 현재 작업 브랜치. 최신 커밋까지 push 완료 |
| #105 | MERGED | 팀원 배터리 투척 후 삭제/아이템 삭제 |
| #106 | MERGED | 팀원 경제/아이템 작업 |

`main`은 현재 브랜치의 merge-base 이후 11개 커밋 진행했다. PR #108의 고유 커밋은 4개다.

### 안전한 충돌 정리 순서

1. 현재 원격 브랜치에서 안전 분기 생성.
2. 최신 `origin/main`을 fetch.
3. 공유 중인 PR 브랜치이므로 우선 merge 방식으로 충돌 범위를 확인.
4. 팀원 #105/#106 변경을 삭제하지 말고 수동 병합.
5. 컴파일과 프리팹 GUID 확인 전 커밋 금지.

예시:

```powershell
git switch codex/utility-item-data-single-source-20260805
git switch -c backup/pr108-before-main-sync-20260805
git switch codex/utility-item-data-single-source-20260805
git fetch origin
git merge --no-commit --no-ff origin/main
```

충돌 내용을 확인한 뒤에만 해결을 계속한다. 불확실하면 `git merge --abort`로 원래 상태로 돌아간다.

### 예상 충돌 핵심 파일

- `Assets/06.../BatteryThrownImpact.cs`
- `Assets/06.../BrokenItemAutoDespawn.cs`
- `Assets/06.../BrokenItemAutoDespawn.cs.meta`
- `Assets/06.../NetworkPlayerCombatController.cs`
- 배터리 dropped prefab
- 일부 animation/controller meta GUID

병합 시 반드시 유지할 계약:

- 일반 투척의 배터리 분기와 `BatteryDischarge` 공격 판정
- 일반/배터리 투척 모두 회전값 적용
- 배터리 첫 충돌 후 3초 삭제
- 파손 아이템 네트워크/비네트워크 자동삭제
- 바닥 배치 표면 보정
- 팀원 #105/#106의 최신 아이템과 경제 변경
- `Assets/01`, `Assets/03` 팀원 원본 비수정

## 6. 검증 완료 항목

| 항목 | 결과 |
|---|---|
| Runtime C# 빌드 | 오류 0 |
| Editor C# 빌드 | 오류 0, 기존 obsolete 경고 존재 |
| Build Settings | Lobby, Tutorial, Map, ExteriorShop 4개 확인 |
| Utility Catalog | canonical item 18개 |
| Shop Catalog | 상품 12개, canonical UtilityItemDataSO 참조 |
| Utility visual truth validator | PASS: items=3, wrappers=6, buildRuntime=4, scenes=2 |
| Foam/Gloo validator | PASS |
| Host 시작 | 성공 기록 있음 |
| 로컬 플레이어 spawn | 성공 |
| 상점 재고 | 런타임 12개 생성 |
| 렌치 투척 | 회전, Collider, NetworkObject spawn 확인 |
| 파손 아이템 자동삭제 | 확인 |
| 배터리 전용 투척 | 메인 맵에서 성공 |
| 이벤트 스케줄 | 시작 확인 |
| MeteorAttack | 생성/활성 확인 |
| 문 Author/Validate | 20개 PASS |
| 문 상태 전이 | 잠금, 피해, 파괴, 복구 서버 상태 확인 |
| 내구도 UI | 20칸에서 19칸 감소 확인 |

## 7. 남은 검증과 알려진 문제

### P0: 다른 PC에서 바로 확인

1. Unity를 새로 시작해 UDP 7777 점유가 없는 상태로 Host 실행.
2. 별도 Client를 연결해 비호스트 중력 yaw/pitch 즉시 추종 확인.
3. 실제 렌치 사용 시 20칸에서 한 칸씩 줄고 Host/Client HUD가 같은지 확인.
4. 문 열린 상태에서 통과 중 잠금 버튼을 눌러 통과 후 닫히는지 확인.
5. 적이 잠기지 않은 문을 감지해 열고 지나간 뒤 문이 닫히는지 확인.
6. 잠긴 문을 적이 공격하고 내구도 0에서 문 모델/차단이 사라지는지 확인.
7. 파괴된 문을 렌치로 수리하면 문 모델과 차단이 복구되는지 확인.

### 마지막 Host 재시험이 중단된 이유

- 같은 Unity 프로세스가 `127.0.0.1:7777` UDP를 종료 뒤에도 점유했다.
- `StartHost`가 `address already in use`로 실패했다.
- Unity 재시작으로 해제되는 로컬 Editor 상태 문제이며 코드 fallback은 추가하지 않았다.

### 기존 미해결 경고

1. `ParkHanSol_AutoRepairKit_Held`, `ParkHanSol_FoamSealantGun_Held`가 `NetworkObject` 없이 NetworkPrefab 목록에 등록되어 시작 시 제거됨.
2. 상점 진열 Rigidbody가 Kinematic인데 선속도/각속도 초기화를 시도해 경고 발생.
3. 일부 이벤트 명령이 `compatible_anchor_unavailable`로 취소됨.
4. `PHS0715IntegrationValidator` 전체는 기존 맵/HUD/로비/이벤트 16건 때문에 실패하며 아이템 관련 실패는 0.
5. GitHub Actions checks가 PR #108에 없음.

## 8. 현재 로컬에만 남은 변경

아래 파일은 이번 기능 커밋에 넣지 않았고 원격에도 없다. 다른 PC에서 자동으로 따라오지 않는다.

- `PHS_NetworkLobbyCustomizationPreview.renderTexture`
- `SUIT Bold SDF.asset`
- `SUIT Korean Dynamic Fallback SDF.asset`
- `SUITE Bold SDF.asset`
- `Assets/99.../Maplestory Bold SDF.asset`
- `Packages/packages-lock.json`
- `ProjectSettings/Packages/com.unity.probuilder/Settings.json`

처리 원칙:

- 폰트 SDF는 동적 atlas 생성/로컬 시각 작업 가능성이 있으므로 출처 확인 전 커밋하지 않는다.
- RenderTexture와 ProBuilder settings는 기능 diff가 아니므로 제외 유지.
- `packages-lock.json`의 `com.anklebreaker.unity-mcp` 추가는 명시적인 MCP 오염이므로 커밋 금지.
- 다른 PC에서 이 변경을 복원하려고 할 필요 없다.

## 9. 담당 경계

- 이번 신규 런타임/씬/UI 작업: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/`
- PR #105 선별 반영 때문에 허용된 공용 변경: 위에 명시한 `Assets/06` 5개
- 이벤트 팀 원본: `Assets/04. NohSeokMin_Game Event/` 수정 금지
- 맵 팀 원본: `Assets/05. TakHyunJae_Map & MiniGame/` 수정 금지
- 플레이어 팀 원본 추가 수정: `Assets/06. JoHanYong_PlayerSystem/` 충돌 해결 외 수정 금지
- MCP/Codex 로컬 설정: Git 포함 금지

## 10. 완료 기준

다른 PC에서 다음 조건을 만족하면 PR #108을 Ready로 전환할 수 있다.

- 최신 `main` 충돌 해결 완료
- Runtime/Editor 컴파일 오류 0
- 문 20개 validator PASS
- Host+Client 비호스트 회전 확인
- Host+Client 아이템 내구도 세그먼트 동기화 확인
- 자동문/잠금/적 공격/파괴/렌치 복구 수동 시나리오 PASS
- 배터리 첫 충돌과 3초 삭제 PASS
- MCP/폰트/ProBuilder 자동 변경이 stage되지 않음
- PR diff에 의도하지 않은 `Assets/01`, `Assets/03`, `Assets/04`, `Assets/05` 변경이 없음

## 11. 주요 로그 키워드

다른 PC에서 Console 검색에 사용한다.

- `PHS_DIRECT_SCENE_TEST_HOST_STARTED`
- `PHS_ITEM_THROW_EXECUTED`
- `PHS_BATTERY_THROW_EXECUTED`
- `PHS_BATTERY_FIRST_IMPACT`
- `PHS_BATTERY_EXPLODED`
- `PHS_BROKEN_ITEM_EJECTED`
- `PHS_BROKEN_ITEM_DESPAWN_ARMED`
- `PHS_SHIP_DOORS_READY`
- `PHS_SHIP_DOOR_LOCK_CHANGED`
- `PHS_SHIP_DOOR_DESTROYED`
- `PHS_SHIP_DOOR_REPAIRED`
- `PHS_SHIP_DOOR_AUTHORING_PASSED`
- `PHS_SHIP_DOOR_VALIDATION_PASSED`
