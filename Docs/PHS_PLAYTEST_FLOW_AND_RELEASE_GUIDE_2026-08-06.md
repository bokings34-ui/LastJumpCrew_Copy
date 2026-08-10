# LastJumpCrew 플레이 흐름·릴리스 인계 - 2026-08-06

## 문서 목적

PR #110의 현재 게임 흐름, 씬/프리팹 연결, 지도 동기화, 빌드 검증 상태를 개발·QA가 같은 기준으로 확인하기 위한 내부 문서다.

## 기준

- Unity: `6000.5.2f1`
- 브랜치: `codex/team-new-work-analysis-20260805`
- PR: `#110`
- Windows 빌드: `C:\Users\hanso\Desktop\LastJumpCrew_Build\LastJumpCrew.exe`
- 테스터 문서: 빌드 폴더의 `TESTER_GUIDE_KR.md`

## 최종 플레이 흐름

1. 로비에서 Host가 방 생성, Client가 방 목록으로 참가한다.
2. 시작 시 플레이어를 `PHS_TravelSystem_0715` 앞 안전지대에 배치한다.
3. 항해 중 사고와 미니게임을 전술 지도에서 확인하고 역할을 나눠 해결한다.
4. 좌우 외곽 문을 통해 상시 무중력 데브리 구역으로 이동한다.
5. 데브리를 회수한 뒤 수동 `F` 귀환 포탈로 함선 출입문 앞에 복귀한다.
6. 캐셔 구역에서 데브리를 판매한다. 성공 시 소형 VFX와 효과음을 재생한다.
7. 상점 단계에는 데브리 출입문 앞 상점 포탈을 활성화하고 파티 투표 후 상점으로 이동한다.
8. 최대 30칸 전시대 중 약 10~15개 상품만 무작위 진열한다. 기본 렌치·소화기·배터리는 제외한다.
9. 결제 상품은 함선으로 배송하고, 복귀 시 데브리 귀환과 같은 함선 출입문 앞에 배치한다.
10. 총 4개 구역 완료 후 Run Phase를 `Clear`로 전환한다.

## 주요 연결 구조

### 멀티플레이

- 기본 스택: Netcode for GameObjects + Unity Transport
- 온라인 확장: Lobby + Relay
- 방 생성·진입과 세션 타입 연결은 `MultiplayerRoomService`가 담당한다.
- 상점 이동은 `NetworkShopTransitionVoteCoordinator`가 파티 투표와 전환을 동기화한다.
- 플레이어 포탈·중력 상태는 `NetworkPlayerController`, `NetworkExteriorAutoPortal`, `NetworkPlayerGravityArea`가 담당한다.

### 외곽/포탈

- 외곽 목적지는 씬의 `NetworkExteriorPortalDestination`으로 노출한다.
- 외곽 진입 시 무중력 상태를 적용하고 귀환 시 함선 내부 상태로 복구한다.
- 귀환 포탈은 자동 진입이 아니라 `ExteriorTestTeleportInteractable`의 상호작용 흐름을 사용한다.
- 씬/프리팹의 목적지 이름과 Inspector 참조가 실제 배치 지점과 일치해야 한다.

### 상점

- `ShopCatalogSO`와 `PHS_ShopCatalog_0715.asset`이 상품 풀을 제공한다.
- 전시 슬롯은 최대 30개, 실제 채움은 10~15개 범위다.
- Expanded 상품 데이터는 함선 체력·수리·후크 파워 중심으로 확장되어 있다.
- 아이템 가격 UI는 아이템 중심에 배치하고 플레이어 방향을 바라본다.

## 전술 지도 규칙

지도에는 다음만 표시한다.

- 로컬 플레이어
- 네트워크 팀원
- 현재 활성 사고/사건
- 현재 활성 미니게임

일반 오브젝트, 바닥 아이템, 자판기, 고정 장비, 종료된 사건은 표시하지 않는다.

`PHSHandheldShipMapController`는 이벤트 ID 17종을 지도 위치와 연결한다. 내부 앵커는 12개이며 누락됐던 `oxygen_life_support`를 포함해 씬과 레이아웃을 12/12로 맞췄다.

미니게임 매핑:

| 이벤트 | 지도 타입 | 터미널 |
| --- | --- | --- |
| EnemyScout | PowerSync / `SYNC` | `PHSFinalMiniGameTerminal` Power Sync |
| MeteorAttack | Cannon / `CAN` | `PHSFinalMiniGameTerminal` Cannon |
| EmpAttack | WireFix / `WIRE` | `PHSFinalMiniGameTerminal` Wire Fix |

배터리 요구 장비의 기준 아이템 ID는 `battery_pack`이다.

## UI/옵션

- 옵션 값은 `NetworkPlayerOptionsStore`를 단일 저장소로 사용한다.
- 플레이와 로비의 Esc 메뉴는 같은 기준 UI로 정리한다.
- 해상도 드롭다운 텍스트는 선택 배경보다 앞 레이어에 있어야 한다.
- 함선 HP와 워프 게이지는 2D 직사각형 막대로 표시한다.
- 부스터 소모량은 이전의 절반이다.

## 검증 결과

- 컴파일 오류: 0
- Windows 빌드: 성공
- 최종 빌드 로그: `PHS_ALL_EVENTS_FINAL_BUILD=Succeeded;errors=0;warnings=150`
- 지도 매핑 검증: `PHS_MAP_ALL_EVENT_MAPPING_PASS count=17`
- 미니게임 포함: `EmpAttack, EnemyScout, MeteorAttack`
- 배터리 요구 ID: `battery_pack`
- 전체 이벤트 시각 검증 이미지: `C:\Users\hanso\Desktop\PHS_Map_AllEvents_Validation.png`
- 단일 사고 시각 검증 이미지: `C:\Users\hanso\Desktop\PHS_Map_Accident_Validation.png`

## 알려진 검증 한계

- 실제 Lobby/Relay 접속은 Unity Services 프로젝트 연결 상태에 따라 별도 검증이 필요하다.
- 현재 Host 런타임 시작 검사에서 `ParkHanSol_AutoRepairKit_Held`, `ParkHanSol_FoamSealantGun_Held`가 `NetworkObject` 없는 NetworkPrefabs 항목이라는 경고가 확인됐다. 정적 지도·UI 검증과 Windows 빌드는 통과했지만, 실제 멀티 회귀 테스트 전 해당 등록 의도를 확인해야 한다.
- 빌드 경고 150개는 오류가 아니지만 제출 전 경고 목록을 한 번 분류해야 한다.

## 릴리스 체크리스트

- [x] 사건/사고 17종 지도 매핑
- [x] 미니게임 3종 지도 표시
- [x] 플레이어/팀원 표시 경로 연결
- [x] 지도 내부 앵커 12/12 연결
- [x] Windows 빌드 성공
- [x] 빌드 폴더 상세 설명서 포함
- [ ] 실제 Host + Client로 외곽, 상점 왕복, 4회 워프 재검증
- [ ] 잘못 등록된 Held NetworkPrefabs 처리 여부 결정
- [ ] Unity Services 연결 환경에서 Lobby/Relay 확인

## 변경 범위 주의

- 사용자 조정 맵 배치는 덮어쓰지 않는다.
- MCP 로컬 설치물, `.codex` 계열 파일, ProBuilder 로컬 설정은 PR에 포함하지 않는다.
- 실험 캐릭터 폴더 삭제와 외부 패키지 `.meta` 삭제는 이번 PR 범위에서 제외한다.
