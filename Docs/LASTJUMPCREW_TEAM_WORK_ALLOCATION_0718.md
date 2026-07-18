# Last Jump Crew 통합 작업 배분표

- 작성일: `2026-07-18`
- 기준 문서: `LASTJUMPCREW_INTEGRATED_GAMEPLAY_NETWORK_SPEC_0718.md`
- 프리팹 접수 기준: `LASTJUMPCREW_TEAM_PREFAB_INTAKE_SPEC_0718.md`
- 상태: 팀 배포 전 검토본
- 배분 원칙:
  - 기존 담당 폴더와 Notion 담당을 유지한다.
  - 팀원은 자기 담당 구역의 게임 투입 가능한 GameReady 최종 완성 Prefab을 납품한다.
  - 공용 씬, NetworkManager, Network Prefab, Build Settings는 통합 담당자만 수정한다.
  - NetworkObject/NetworkBehaviour/RPC와 네트워크 권위는 `02` 통합 계층에만 둔다.
  - 박한솔은 팀 콘텐츠를 대신 완성하지 않고 배치·포트 연결·네트워크 조립만 한다.

## 1. 최종 소유권

| 담당 | 최종 소유 | 폴더 |
|---|---|---|
| 박한솔 | 온라인 세션, NGO 권위, Run/Ship 지속 상태, 통합 씬, Network Prefab, Validator, Build | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/` |
| 서보경 | 오브젝트 애니메이션 Prefab/Controller/Clip과 상태 표현 | `Assets/03. SeoBoGyeong_Game Economy/` |
| 박한솔/사용자 | Network Economy 원장 통합, 가격·보상·재고·확률 최종 밸런스 승인 | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/` |
| 신규 Shop 콘텐츠 담당 | Shop/Catalog/Display 신규 제작 `[확인 필요]` | 담당 폴더 `[확인 필요]` |
| 노석민 | 외부 사건 콘텐츠, 내부 사고 규칙, 적, 사건 SO/Pool/Outcome | `Assets/04. NohSeokMin_Game Event/` |
| 탁현재 | 함선 공간, Room/Device 배치, Map 환경, Fire 표면, 미니게임 View, Warp 연출 | `Assets/05. TakHyunJae_Map & MiniGame/` |
| 조한용 | 플레이어 전투/체력/넉백, 아이템 공격·사용·투척, 도구 UX | `Assets/06. JoHanYong_PlayerSystem/` |

### 1.1 한 사람 한 구역·완성품 경계

| 담당 | 자기 구역에서 끝내서 줄 최종 완성품 | 박한솔이 마지막에 할 일 |
|---|---|---|
| 서보경 | Door/Console/Generator/Repair/Shop Object Animation Prefab, Animator/Clip/Parameter, 상태 전환과 Reset | 서버 Snapshot을 상태 입력 포트에 연결하고 실제 Device 아래 배치 |
| 노석민 | 외부 사건·내부 사고·Fire Content·Enemy Prefab, SO/Outcome, 로컬 전체 생명주기와 Cleanup | Incident Director 명령, RNG/Budget, 서버 피해와 Network Snapshot 연결 |
| 탁현재 | Ship Layout/Room/Device, Fire Surface Graph, Minigame View, Map Environment/Warp Prefab | Scene Parent/Anchor, Minigame Session, 서버 Seed/Result 연결 |
| 조한용 | Player Combat/Health/Knockback Module, Held/Dropped Tool Prefab, 사용·투척·피드백·Reset | 기존 Player NetworkObject, RPC/소유권/Spawn, 서버 판정 연결 |
| Shop 담당 `[확인 필요]` | Shop Display/Catalog Presentation Prefab, 상품 표시·선택·피드백·Reset | Economy Ledger, 승인 Catalog, 구매/배송 네트워크 연결 |

최종 완성품 기준:

- 내부 Inspector 참조, Collider, Layer, Animator, Material, VFX, Audio, Data가 모두 연결돼 있다.
- 담당자 Sandbox에서 나타남 → 작동 → 성공/실패/취소 → Cleanup/Reset까지 실행된다.
- 외부에 남기는 것은 Manifest에 적은 상태 입력, 요청 출력, Scene Anchor, Registry 항목뿐이다.
- `NetworkObject`, `NetworkBehaviour`, `NetworkVariable`, RPC는 포함하지 않는다.
- 박한솔이 내부 자식·Clip·Collider·로컬 규칙을 추가해야 하면 완성품이 아니므로 원 담당자에게 반려한다.

## 2. 공유 파일 잠금

아래 파일은 박한솔 통합자만 직접 수정한다.

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/ParkHanSol_LobbyScene.unity`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_Map_ver1.unity`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_ExteriorShopScene.unity`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab`
- `Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/DefaultNetworkPrefabs.asset`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab`
- `Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/Integration0716/PHS_EventRuntimeSystem.prefab`
- `Assets/01. MainGame/03. Common_Script/Interfaces/`
- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`

팀원 납품 방식:

1. 자기 폴더에서 GameReady Root Prefab과 필요한 SO/Script/표현 자산을 완성한다.
2. 외부 통합 포트를 제외한 모든 Inspector 참조를 연결한다.
3. 같은 최종 Prefab을 Sandbox Scene에서 전체 생명주기로 증명한다.
4. Manifest/README에 외부 통합 포트와 실제 자산 GUID를 기록한다.
5. 박한솔은 Final Prefab/0715 Scene에 배치하고 `02` Network Adapter만 연결한다.

## 3. 공용 계약 동결

### M0에서 먼저 확정할 것

- 활성 Player Prefab GUID 하나.
- 활성 Network Prefab List 하나.
- ItemId `lower_snake_case`.
- MapId `8000~8999`.
- EventId:
  - 내부 Legacy `710x`
  - 외부 `720x`
  - 환경 `730x`
- Ship Accident Wire Id `1~7`.
- InstanceId와 Revision 사용 규칙.
- 9구역 / 3구역마다 Shop.
- 제품 4인 / 기술 상한 8인.
- Run 중 신규 참가 금지.
- Shop Phase Gate.

### 공유 인터페이스 담당

박한솔이 골격을 만들고 각 담당자가 자기 계약만 검토한다.

| 계약 | 검토 담당 |
|---|---|
| `INetworkRunSessionState` | 박한솔 |
| `IMapRuleProvider` | 박한솔, 사용자 |
| `IExternalIncidentRuntime` | 박한솔 |
| `IExternalIncidentContent` | 노석민 |
| `IShipAccidentRuntime` | 박한솔, 노석민 |
| `IMiniGameSessionService` | 박한솔 |
| `IMiniGameView` | 탁현재 |
| `IIncidentConsequencePolicy` | 노석민 |
| `IAtomicSaleService` | 박한솔 |
| `IUtilityAttackTarget` 확장 여부 | 노석민, 조한용 |

## 4. 박한솔 배정

### PHS-P0-01 Persistent RunSessionRoot

상태: `P0 핵심 생명주기·Stage Clock·Economy·RNG 원장 완료 / Incident 이후 연결 중`

완료:

- 서버 소유 `NetworkRunSessionRoot`와 Lobby Bootstrap 제작.
- 활성 Network Prefab List 등록과 Inspector 연결.
- `NetworkRunFlowCoordinator` Player Prefab 분리.
- `NetworkShipSystemsState` Map Ship Runtime 분리.
- External Event Impact Adapter Root 이동.
- Compile 0, 0715 Validator 0, Lobby -> Map 및 Lobby -> Shop 상태 유지 확인.
- NGO Scene Populate 완료 전 Debris Spawn으로 발생하던 `GlobalObjectIdHash` 중복 원인 수정.
- 다섯 Debris Prefab의 Rigidbody Inspector 참조와 Validator 계약 추가.
- 새 Development Build에서 2인, 9구역, Shop 3회, `FinalShop -> Clear` 자동 루프 통과.
- 같은 루프에서 Root `NetworkObjectId=2` 단일 생성과 Ship State `revision=17` 재바인딩 확인.
- Headless 시각 검증 오탐 제거, MiniGame Lamp 발광 Material과 Inspector Validator 수정.
- Map Profile 기반 서버 권위 `NetworkRunStageClock`과 Host/Client HUD 단일 조회 연결.
- 2인 전체 루프에서 Stage Clock sequence `1~9`, Shop 복귀 Commit, Remaining 최대 차 `0.054초`, Pause 안정 `0.000초` 검증.
- `NetworkRunEconomyLedger`에서 Wallet과 Delivery Queue를 Root 수명으로 통합.
- 구매 차감+Delivery 추가 단일 커밋, 판매 거래 ID 중복 방지, 수리 결제/환불 원장 기록 연결.
- 개별 PurchaseId 영속 중복 차단, Root 늦은 Spawn 재바인딩, Snapshot/Delivery revision 관찰 순서 보강.
- 미수령 배송품의 Scene 왕복 보존은 `Boxed/Collected + EntryId/Slot + 서버 수령` 계약으로 후속 분리.
- 2인 전체 루프에서 구매 실패 무변경, `pending=1`, Map 복귀 `delivered=1` Peer 동기화 검증.
- `NetworkRunRandomLedger`의 Seed/Algorithm Snapshot과 8개 고정 Stream ID 계약 구현.
- Map Choice를 다음 구역 Scope 기반 결정론 RNG로 전환하고 다른 Stream 소비와 격리.
- 2인 9구역 루프에서 실제 Map Choice를 원장 재생 기대값과 9회 대조하고, 다른 Stream 소비 비간섭과 `algorithm=1` golden vector를 검증.

남음:

- Compatibility/Session Approval.
- 4/8인과 Late Join.

구현 문서:

- `Docs/PHS_RUN_SESSION_ROOT_IMPLEMENTATION_0718.md`

목표:

- Run/Ship/Wallet/RNG가 Scene과 Player 생명주기에 종속되지 않게 한다.

작업:

- Server-Owned Persistent NetworkObject 생성.
- `NetworkRunFlowCoordinator`를 Player Prefab에서 분리.
- `NetworkShipSystemsState`를 Map Scene 상태에서 분리.
- Stage Deadline, Active Map, Cleared Zone, Shop Cycle 복제.
- Party Wallet/Delivery Queue 지속성 연결. — 완료
- Scene View 재바인딩 계약 추가.

완료 기준:

- Map -> Shop -> Map 후 Ship HP, Module Fault, Wallet 유지.
- Host Player 교체와 Player Despawn 뒤에도 Run 유지.
- Client HUD Timer/Phase 일치.

### PHS-P0-02 Run 규칙 통합 Adapter

작업:

- 03의 `IGameStateProvider/IGameCommands`를 Server Adapter로 연결.
- 9구역/3구역 Shop 규칙 적용.
- Map Profile의 제한시간/보상/Advance 규칙 사용.
- Shop 경계에서도 Active Map Commit.
- 같은 Map 연속 선택 방지.
- Server Seed 기반 선택지 2개.

완료 기준:

- 3/6 구역 후 Shop.
- 9구역 후 FinalShop/Clear.
- ActiveMap과 CurrentProfile이 모든 Peer에서 일치.

### PHS-P0-03 Network 설정

작업:

- 제품 4인, 기술 상한 8인 적용.
- Run 시작 후 Session Join 잠금.
- Approval Payload에 Protocol/Build/Content Hash.
- Connection Reject Reason 표준화.
- Network Prefab List 단일화.
- 중복 Player Prefab 등록 제거.

완료 기준:

- 2/4/8인 연결.
- Protocol/Content mismatch 거절.
- Run 중 신규 참가 거절.

### PHS-P0-04 Item/거래 권위

작업:

- 현재 미추적 핵심 파일의 소유권과 제출 범위 확정:
  - `INetworkItemPickupRequester`
  - `NetworkPlayerItemLifecycle`
  - `NetworkItemPhysicsAuthority`
  - `UtilityItemCatalogSO`
- Pickup/Drop/Throw에 LOS와 Rate Limit.
- Sale를 원자 트랜잭션으로 변경.
- Purchase/Sale/Delivery Idempotency 검증.
- Held/Dropped Prefab과 Network Prefab 등록 검증.

완료 기준:

- Wallet 실패 시 Item 유지.
- Replay 판매/구매 거절.
- 벽 너머 Pickup 거절.

### PHS-P0-05 Incident Network Authority

작업:

- 외부/내부 Incident 공용 Pressure Budget.
- Legacy Scheduler 활성 0개 Validator.
- External 720x만 NGO Scheduler에 허용.
- 내부 1~7은 Ship Accident Coordinator만 허용.
- Consequence 단계별 Idempotency.
- MiniGame Session/Nonce/Occupancy/Expiry.

완료 기준:

- 외부 사건 한 번당 Consequence 한 번.
- Legacy/New Fire 이중 발생 없음.
- MiniGame Replay/원거리/동시 점유 거절.

### PHS-P0-06 Scene 통합

작업:

- Shop Portal을 Shop/FinalShop Phase로 제한.
- Map 내부 외부 플랫폼에 Collection/Safe/Danger/Death Volume 배치.
- `PHS_DebrisCollectionScene`을 Legacy로 Validator에 명시.
- 팀 납품 Prefab을 0715 Scene에 Inspector 연결.
- Build Settings 3씬 정책 재검증.

완료 기준:

- Play 중 Shop 진입 거부.
- 외부 잔류자가 Safe 인원 계산에 반영.
- Scene Missing Reference 0.

### PHS-P1-01 Validator/빌드

완료:

- Contract Validator.
- 2인 Runtime Validation.
- 9구역 전체 Loop 자동 검증.
- Incident/Shop/Item 자동 회귀 검증.
- Debris Hash/Physics/Scene Event/MiniGame Indicator 오류를 Runner Health 실패 조건에 추가.

남음:

- Fire Patch 확산·범위 피해 회귀 검증.
- 4/8인 Runtime Validation.
- Late Join/복구 검증.

### 박한솔이 직접 하지 않는 것

- 미니게임 UI 로직 제작.
- 적 AI 콘텐츠 제작.
- Fire VFX 직접 제작.
- Map Geometry와 Room 배치 제작.
- 플레이어 공격 감각과 도구 애니메이션 제작.
- 상품 가격/보상 수치 단독 결정.
- 팀 완성품의 누락 Hierarchy/Collider/Animator/VFX/Audio/로컬 기능 보완.

## 5. 서보경 배정

최신 사용자 지시로 신규 담당을 `오브젝트 애니메이션`으로 변경한다. 기존 03 경제 코드와 자산은 이력·GUID를 보존하지만, 새 경제/밸런스 작업을 자동 배정하지 않는다.

### SBG-P0-01 오브젝트 애니메이션 표현 번들

대상:

- 함선 Door/Console/Generator/Power Device.
- 사고 Telegraph/Active/Resolve/Cleanup 표현.
- 상점 진열대·버튼·배송 상자의 동작 표현.
- 수리 성공/실패와 고장/복구 상태 표현.

납품:

- GameReady 독립 Prefab.
- Animator Controller와 사용 Clip.
- Parameter 표.
- 시작/Loop/복구/종료 상태표.
- Animation Event 목록.
- 샌드박스 실행 캡처.
- Prefab/SO/Controller/Clip의 `.meta`.
- 외부 상태 입력 포트를 구동하는 Local Test Driver.

필수 계약:

- Animator와 시각 자식은 Network 상태를 소유하지 않는다.
- NetworkVariable, ServerRpc, ClientRpc, Scheduler, Ship HP 직접 변경을 넣지 않는다.
- 게임 결과는 박한솔 통합 Adapter가 확정하고, 애니메이션은 전달받은 상태만 표시한다.
- 제안 View 계약 이름은 `IObjectAnimationView`이며 실제 공용 인터페이스 추가 전 `[확인 필요]`다.
- Telegraph/Active/Cleanup이 눈에 보이게 분리돼야 한다.

완료 기준:

- 상태별 Clip 누락 0.
- Loop에서 Resolve/Cleanup으로 정상 이탈.
- Animator Warning 0.
- Prefab 비활성→활성→정리 재사용 가능.
- 실제 Device에 배치하기 전 내부 추가 작업 0.

### SBG-P1-01 오브젝트 세트 확장

- Door/Engine/Gravity/Power/Repair/Shop 계열을 같은 Parameter 계약으로 확장.
- 사건별 전용 연출은 노석민 VFX/사건 Prefab과 합성 가능한 시각 자식으로 제출.
- 활성 Ship/Map/Shop 씬과 Final Prefab은 직접 수정하지 않는다.

### 경제·밸런스 재배정

- Network Wallet/Delivery 거래 원장은 박한솔이 통합한다.
- 가격, 보상, 재고, 확률의 최종값은 박한솔/사용자가 승인한다.
- 신규 Shop/Catalog/Display 콘텐츠 담당은 `[확인 필요]`다.
- 기존 03 경제 자산 수정이 필요하면 변경 목록을 먼저 합의하고 별도 번들로 받는다.

### 서보경이 건드리지 않는 것

- NetworkObject.
- ServerRpc/ClientRpc.
- Network Prefab List.
- 0715 통합 씬.
- Player Prefab.
- Event Scheduler.
- Wallet/Delivery Root 원장.
- 가격·보상·확률 최종값.

## 6. 노석민 배정

### NSM-P0-01 사건 Source of Truth 정리

작업:

- 7201 EnemyScout.
- 7202 MeteorAttack.
- 7203 EmpAttack.
- 사건별 Start/Success/Fail/Expire 표 작성.
- 피해 시점 하나만 선택.
- Fail Consequence를 Ship Accident Request로 표현.

완료 기준:

- 사건당 피해/Child Incident 최대 1회.
- EventInstanceId 기반 결과 추적.
- 사건별 GameReady Root Prefab에서 Telegraph → Active → Resolve/Fail/Expire → Cleanup 재현.

### NSM-P0-02 Legacy Scheduler 격리

대상:

- `EventScheduler`
- `ZoneEventScheduler`
- Legacy 710x Fire/Oxygen/Enemy

작업:

- 자체 Update Spawn 경로를 통합 Prefab에서 비활성.
- Legacy Event는 콘텐츠 Adapter로 호출될 때만 실행.
- Fire/Oxygen의 직접 Ship Damage 중복 제거.
- 기존 Pool과 SO를 삭제하지 않고 콘텐츠 계층으로 유지.

완료 기준:

- 통합 씬에서 Legacy Scheduler active 0.
- 서버 한 곳에서만 사건 생성.

### NSM-P0-03 Fire 도메인

작업:

- `PHSFireZone`.
- `PHSFirePatch`.
- `PHSFirePatchLink`.
- Heat/Intensity 전이.
- 인접 확산 후보 계산.
- 범위 피해 대상 수집 규칙.
- 산소/문/풍향은 P1 Hook만 제공.

권위 제한:

- 순수 계산과 Scene Authoring Component를 제공.
- NetworkBehaviour, NetworkList, RPC, NetworkObject Spawn은 소유하지 않는다.

완료 기준:

- 점이 아닌 면적 Patch.
- 인접하지 않은 Patch 확산 없음.
- 동일 대상 Collider 중복 피해 제거 가능.
- Fire Content GameReady Prefab의 VFX/Audio/피해 Volume/소화/Reset 내부 연결 완료.

### NSM-P0-04 적 침투 콘텐츠

작업:

- EnemySpawn 콘텐츠.
- Player/Device 우선순위.
- Health/State/Target/Death Snapshot에 필요한 읽기 계약.
- 서버 AI와 Client Presentation 분리.

### NSM-P1-01 환경 사건

- 730x Zone 이벤트를 Map Profile용 콘텐츠로 정리.
- Map 환경별 위협 가중치/연출 제공.

### 노석민이 건드리지 않는 것

- NetworkManager.
- NGO Scheduler 권위.
- 0715 통합 씬.
- Player Inventory.
- Wallet.
- MiniGame 결과 확정.

## 7. 탁현재 배정

### THJ-P0-01 함선 Incident Layout

작업:

- 실제 Room/Device 기준 Incident Zone 제작.
- 실제 Generator/Battery/Engine/Panel 위치에 Anchor.
- Fire 표면 Patch와 Neighbor Link 배치.
- Presentation Root와 Collider 연결.
- Repair 접근 동선 검토.

납품:

- GameReady 함선 Layout Prefab.
- Anchor ID 표.
- Room ID 표.
- Fire Patch Link 표.

완료 기준:

- Generic 빈 Box Anchor 없음.
- 실제 설비 또는 표면에 사고 발생.
- Presentation Root 위치 오류 없음.
- 박한솔이 Anchor/Collider/표현 자식을 추가하지 않아도 됨.

### THJ-P0-02 MiniGame View

작업:

- Cannon.
- PowerSync.
- WireFix.
- `IMiniGameView` 구현.
- Server Session이 제공한 Seed/Nonce를 사용.
- 결과는 View가 확정하지 않고 Session Service에 제출.
- Terminal Occupied UI.

완료 기준:

- EventInstance와 TerminalInstance를 표시 가능.
- Timeout/Cancel/Disconnect 처리.
- DoorKeypad는 P0 Event 매핑에서 제외.
- 각 GameReady View Prefab이 Local Test Driver로 Start/Progress/Success/Fail/Timeout/Cancel/Reset을 모두 재현.

### THJ-P0-03 Warp/Map Presentation

작업:

- 기존 WarpManager의 로컬 Phase 권위 제거.
- `IWarpTransitionView` 기반 VFX/Audio만 제공.
- Map Environment Prefab 교체 구조.

### THJ-P1-01 4개 Map 차별화

| Map | 필수 차이 |
|---|---|
| 8001 폐기물 궤도 | 많은 Debris, 낮은 외부 위협 |
| 8002 소행성 지대 | Meteor 빈도/시각 장애 |
| 8003 파손 위성군 | Device/EMP 위험 |
| 8004 성운 잔해지대 | 시야/미니맵 방해 |

납품:

- Map별 GameReady Environment Prefab.
- Skybox/Lighting.
- Debris Spawn Volume.
- 위험 Volume.

### 탁현재가 건드리지 않는 것

- Run Phase.
- Scene Load.
- NetworkVariable.
- Event Spawn 권위.
- Network Prefab List.
- Wallet/Reward.

## 8. 조한용 배정

### JHY-P0-01 플레이어 Source of Truth

작업:

- 06의 Player Prefab 복사본을 활성 Prefab으로 사용하지 않는다.
- Player Combat/Health/Knockback GameReady Module Prefab.
- 로컬 입력·피격·넉백·사망/복구 표현.
- 활성 02 Player Prefab에 붙일 완성 Module Prefab과 Socket Manifest 납품.

완료 기준:

- 이동 권위는 02 `NetworkPlayerController` 하나.
- 전투/체력/넉백은 중복 Component 없음.
- Local Test Driver에서 공격→피격→넉백→사망/복구→Reset 완료.
- 내부 Animator/VFX/Audio/Collider/Layer 참조 null 0.

### JHY-P0-02 기본 도구

작업:

- Wrench.
- Fire Extinguisher.
- Battery.
- Use/Throw/Impact.
- Held ItemId 검증.
- Cooldown/Range/Continuous Use Rate 순수 규칙.
- Held/Dropped 전환 표현과 Local Test Driver.

완료 기준:

- ItemId/Range/Cooldown/연속 사용 규칙 테스트 통과.
- Frame마다 무제한 요청 Event/VFX 생성 없음.
- Wrong Item/원거리/연속 요청 거절 기대값 제공.
- 실제 서버 Owner/Revision/Replay 검증은 박한솔 Network Adapter 검증 항목으로 전달.

### JHY-P0-03 사고 대응 연결

작업:

- Wrench -> Device/Steam/Oxygen/Gravity Repair.
- Extinguisher -> Fire Patch Heat 감소.
- Battery -> Power Socket.
- Foam Sealant -> Hull Breach.
- 입력은 하나의 Utility Attack 경로로 통합.

완료 기준:

- F 수리와 LMB 수리가 같은 사고에 이중 Progress를 주지 않는다.
- 사고별 공식 입력 방식이 하나다.

### JHY-P1-01 고급 도구

대상:

- Auto Repair Kit.
- Foam Sealant Gun.
- Futuristic Adjustable Wrench.
- Futuristic Canister.
- Tripo Fire Extinguisher.

작업:

- Placeholder 로그를 실제 효과로 교체.
- 내구도 또는 횟수 정책.
- 기본 도구 대비 성능 차이.

### JHY-P1-02 외부 수집 UX

- 무중력 이동/충돌.
- Debris 들기/던지기.
- Safe/Danger 피드백.
- 사망 시 Held Item 처리.

### 조한용이 건드리지 않는 것

- NetworkManager.
- RunFlow.
- Wallet.
- Shop 가격.
- Event Scheduler.
- 0715 통합 씬.

## 9. 의존성 순서

```mermaid
flowchart LR
    M0["M0 계약 동결"] --> M1A["박한솔 SessionRoot"]
    M0 --> M1B["서보경 Object Animation"]
    M0 --> M1C["노석민 Incident 콘텐츠"]
    M0 --> M1D["탁현재 Layout/MiniGame View"]
    M0 --> M1E["조한용 Player/Item"]
    M1B --> M2["박한솔 Network Adapter/Prefab 통합"]
    M1C --> M2
    M1D --> M2
    M1E --> M2
    M1A --> M2
    M2 --> M3["2인 Host/Client"]
    M3 --> M4["4인 전체 Run"]
    M4 --> M5["8인 부하/회귀"]
```

### M0 계약 동결

- 코드 착수 전.
- 공용 ID와 Interface Signature만 확정.
- 공유 Scene 수정 금지.

### M1 팀별 독립 납품

- 팀 폴더 안에서 작업.
- GameReady 최종 Prefab을 같은 Sandbox Scene에서 기능 증명.
- 내부 기능·표현·참조·Reset까지 완성.
- NGO 권위 없이 순수 Domain/View와 선언된 통합 포트만 납품.

### M2 통합

- 박한솔이 Final Player/Ship/Event/Map Prefab 조립.
- Network Prefab 등록.
- 0715 Scene Inspector 연결.
- Validator 실행.

### M3~M5 검증

- 2인 기능 계약.
- 4인 전체 게임 루프.
- 8인 연결/부하/동시 사고.

## 10. 팀별 제출 체크리스트

각 팀원:

- [ ] 자기 폴더만 수정.
- [ ] Interface 파일명 `I` 시작.
- [ ] Inspector 참조 Null 없음.
- [ ] 자동 `Find` fallback 없음.
- [ ] GameReady 최종 Root Prefab 제공.
- [ ] SO/Data 제공.
- [ ] 선언 포트 외 Inspector 참조 null 0.
- [ ] Animator/Collider/VFX/Audio/Reset 내부 완성.
- [ ] 같은 최종 Prefab으로 전체 생명주기 Sandbox 증명.
- [ ] NetworkObject/NetworkBehaviour/NetworkVariable/RPC 없음.
- [ ] 입력/출력 계약 기록.
- [ ] 실패 로그 이유 기록.
- [ ] Compile Error 0.
- [ ] 통합자가 수정할 파일 목록 기록.

통합자:

- [ ] 팀원 원본 파일을 이동/삭제하지 않음.
- [ ] GUID 유지.
- [ ] Network Prefab 중복 없음.
- [ ] Scene Missing Reference 0.
- [ ] Build Settings 검증.
- [ ] MCP/AI 설정 파일 Stage 안 됨.
- [ ] 범위 밖 Unity 자동 변경 Stage 안 됨.

## 11. 완료 정의

팀 배분 작업 완료는 코드 작성이 아니라 아래 결과까지다.

1. 팀별 GameReady 최종 Prefab/SO/Script 납품.
2. 공용 계약 준수.
3. 0715 통합 씬 연결.
4. Host+Client 실제 실행.
5. 4인 전체 Run 9구역.
6. Scene 전환 뒤 Run/Ship/Wallet 유지.
7. 사건/사고/미니게임 단일 권위.
8. 문서와 실제 Inspector 상태 일치.

## 12. 박한솔 현재 직접 할당 요약

새 기능 배정은 더 추가하지 않는다. 박한솔은 기존 공용 권위·통합 범위만 끝낸다.

완료된 선행 작업:

1. Persistent RunSessionRoot와 Run/Ship 상태의 Scene 독립.
2. Debris NGO Scene Load 생명주기와 Rigidbody 참조 수정.
3. 2인 9구역/Shop 3회/FinalShop/Clear 자동 검증.
4. Wallet/Delivery Economy 원장, 구매 원자 커밋, Map 복귀 Delivery 동기화.
5. Run RNG 원장과 Map Choice 결정론적 Stream/Scope 연결.

현재 직접 남음:

1. Incident Pressure/Budget 원장과 통합 Scheduler 계약.
2. Debris/Shop RNG 소비자 연결.
3. Compatibility와 Session Approval 계약.
4. Run 규칙/Active Map Commit의 남은 통합 검증.
5. 외부 수집 Safe/Danger의 남은 통합.
6. Legacy Scheduler 차단과 MiniGame Session Authority.
7. 팀 납품 Prefab의 최종 Scene/Inspector 조립.
8. 4/8인과 Late Join 검증.

박한솔이 기다려야 하는 입력:

- 서보경: Object Animation GameReady Prefab/Controller/Clip/Parameter/Reset 완성본.
- 신규 Shop 담당 `[확인 필요]`: Shop Display/Catalog Presentation GameReady 완성본.
- 노석민: Incident/Fire/Enemy GameReady Prefab과 Outcome/SO 완성본.
- 탁현재: Layout/Fire Surface/MiniGame View/Map Environment GameReady 완성본.
- 조한용: Player Combat/도구/투척 GameReady Module/Prefab 완성본.

즉, 박한솔은 완성품의 내부를 고치지 않는다. 공용 권위, 배치, 선언 포트, Network Adapter, Registry와 검증만 맡는다.

## 13. 팀 배포용 요약문

### 서보경 전달

`03`의 신규 담당은 오브젝트 애니메이션입니다. Door/Console/Generator/Repair/Shop별로 내부 참조가 모두 연결된 GameReady Prefab, Animator Controller, Clip, Parameter/상태표, Local Test Driver, 전체 Reset 증거를 함께 제출해주세요. NetworkObject/NetworkBehaviour/NetworkVariable/RPC/Scheduler/게임 결과 판정은 넣지 않습니다. 박한솔은 상태 입력 포트와 실제 Device 배치만 연결합니다. 기존 경제 자산은 GUID와 이력을 유지하며 별도 합의 없이 이동하거나 재작성하지 않습니다.

### 노석민 전달

`04`에서 외부 사건, 내부 사고 규칙, Fire, Enemy 콘텐츠를 GameReady Prefab으로 완성합니다. 720x Outcome/SO, Telegraph→Active→Resolve/Fail/Expire→Cleanup, Fire 면적·피해·소화·Reset, Enemy 상태/표현까지 Sandbox에서 완결해야 합니다. NetworkObject/NetworkBehaviour/RPC, 자동 Spawn 권위, 직접 Network Ship Damage는 넣지 않습니다. 박한솔은 Incident 명령·Budget·RNG·서버 피해와 Snapshot만 연결합니다.

### 탁현재 전달

`05`에서 함선 공간, 실제 사고 위치, Fire 표면, 미니게임 View, Warp/Map 연출을 GameReady Prefab으로 완성합니다. 실제 Device/Room Anchor, Fire Patch 링크, Collider/Layer, Cannon/PowerSync/WireFix 전체 UI·입력·성공/실패/취소·Reset까지 내부 연결해서 납품해주세요. NetworkObject/NetworkBehaviour/RPC, Run Phase·Scene Load·결과 확정은 넣지 않습니다. 박한솔은 Scene Parent와 Incident/Minigame Session 포트만 연결합니다.

### 조한용 전달

`06`에서 Player Combat/Health/Knockback Module과 Wrench/Extinguisher/Battery Held/Dropped Tool을 GameReady Prefab으로 완성합니다. 공격·피격·넉백·사용·투척·VFX/Audio·실패·Cleanup/Reset과 ItemId/Range/Cooldown 순수 규칙까지 Sandbox에서 끝내주세요. NetworkObject/NetworkBehaviour/NetworkVariable/RPC는 넣지 않고 06 Player 복사본을 활성본으로 만들지 않습니다. 박한솔은 기존 Player NetworkObject, 소유권/Spawn/RPC와 서버 판정만 연결합니다.

### 박한솔 통합

`02`에서 Persistent RunSessionRoot, Run/Ship/Wallet/RNG/Incident 원장, Network 설정, Scene/Prefab 조립, Incident/MiniGame Session 권위, Registry, Validator와 2·4·8인 검증을 맡습니다. 팀원 GameReady Prefab 내부는 수정하지 않고 배치, 선언된 포트, Network Adapter와 최종 Inspector 연결만 담당합니다. 내부 미완성은 원 담당자에게 revision 반려합니다.
