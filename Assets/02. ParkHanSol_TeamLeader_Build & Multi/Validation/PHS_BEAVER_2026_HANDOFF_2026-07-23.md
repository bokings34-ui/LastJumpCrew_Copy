# LastJumpCrew BEAVER 2026 작업 인계서

작성 시각: 2026-07-23 19:30 KST  
작업 브랜치: `codex/beaver-2026-item-swap`  
프로젝트: `E:\UNITY\LastJumpCrew`
Unity: `6000.5.2f1`, StandaloneWindows64  
제출 페이지: https://beaverrocks.com/creator  
제출 마감: 2026-07-27 18:00 KST

## 최신 체크포인트 — 2026-07-23 23:35 KST

- 이 절이 아래의 19:30 중간 체크포인트보다 최신이다.
- Unity Editor `6000.5.2f1`은 Play/compile 중이 아니며, 마지막 검증 뒤 Console Error는 0이다. Build Settings는 Lobby → Tutorial → Map → Shop 4개를 유지한다.
- Lobby의 `CUSTOMIZE`와 `TRAINING`은 `START`/`SETTINGS`와 같은 세로 열에 배치했고, 메인 Lobby의 주황 계열 색으로 통일했다.
- 수동 Room Browser는 Create/List/Refresh/Password/Join 4개 패널, prefab-local 참조 20개, scene `roomService` 참조를 복구했다. 전용 validator는 PASS했고 기존 Create/Join 및 Customize/Training 배치는 보존됐다.
- 첫 수동 2-process 클릭 시도는 smoke 증거로 채택하지 않는다. 재시도에서 exact LastJumpCrew PID/HWND와 정상 `LAST JUMP CREW` START 화면을 확인해 wrong scene/overlay는 기각했다. START 클릭 직전 사용자 입력 충돌이 감지돼 Shadowverse를 보호하고 중단했다. 재시도 profile 이름도 30자를 넘어 초기화가 거부됐으므로 다음 수동 smoke는 짧은 고유 profile로 다시 실행한다.
- 자동 수리 키트는 수리 계열 공통 기능을 사용하며 즉시 완료, 최대 내구도/소모량 1이다. 렌치/미래형 렌치, 소화기/Tripo, 배터리 계열은 각 family 기능을 공유하고 수치만 다르다. 미래형 캐니스터는 이번 기능 배정에서 제외했다.
- Foam GLOO는 서버 권위 투사체의 비행 → 부착 → 경화 → 용해 상태를 동기화한다. 화재 4발, 함선 구멍 6발, 표면 폼 3발 기준이며 late join/상한/정리 계약을 validator로 확인했다.
- Foam dropped prefab의 내구도 state 누락을 수정했다. 최신 Full P0에서 host throw, remote pickup/throw, peer 동기화, 내구도 100을 통과했다.
- 아이템 상호작용용 생성 WAV는 총 23개다. item cue validator는 11개 cue와 owner 2D/world 3D를 확인했고, 배터리 shock 3D AudioSource는 main/tutorial player prefab 양쪽에 연결했다. 실제 청감 QA와 정적 wiring PASS는 구분한다.
- `PHS_FOAM_GLOO_VALIDATION_PASS`, `PHS_ROOM_BROWSER_RECOVERY_VALIDATION_PASS`, `PHS_ITEM_INTERACTION_AUDIO_VALIDATION_PASSED`, `PHS_0715_VALIDATE_OK errors=0 scenes=4 prefabs=11`을 확인했다.
- Item 전용 온라인 P0는 `Artifacts/OnlineItemP0_20260723_2259_final`에서 `peers=2 items=3` PASS했다.
- 최신 Full 온라인 P0는 `Artifacts/OnlineFullP0_20260723_232541`에서 `peers=2`, `zones=9`, `shopCycles=3`, `runPhase=Clear` PASS했다. Host/Client는 서로 다른 `-phsProfile`과 PlayerId를 사용했다.
- 최종 Development build는 386 files / 400,016,106 bytes, tree SHA256 `DA0A83289B0B0AC26E9AAA683B2D663E0990EC301136F0DD15761F68A42E5BCE`다.
- 최종 Release build는 239 files / 326,571,297 bytes, tree SHA256 `26519FE1F79EFE3502ECE73F44C9323ECABC8AC05DBBAAC63F32D0DFB5F43F47`다. Release Assembly-CSharp SHA256은 `2A15DFB0BC6E3EB129977D138F1A5B822E1F81CE74A0361C7261802F60CC8CA3`다.
- 깨진 팀원 프리팹/`Assets/06`은 사용자 지시에 따라 제외했다. Git stage/commit/push는 하지 않았다. MCP/Artifacts/사용자·팀 변경을 보존하고 승인된 PHS 범위만 별도로 stage해야 한다.

## 1. 현재 체크포인트

- Unity Editor는 Play 중이 아니며 컴파일도 끝난 상태다.
- 최종 `Assets/Refresh` 뒤 Unity Console Error는 0이다.
- 활성 씬은 `PHS_NetworkTutorialScene`이다. 최종 domain reload 뒤 Editor가 dirty로 표시했으며 이 상태는 저장하지 않았다. 다음 시작 때 `Don't Save`로 닫고 다른 BEAVER 씬에서 검증을 시작한다.
- 이번 작업은 기능 완료가 아니라 중간 체크포인트다.
- 팀 Map/팀 프리팹 원본은 수정하지 않는 원칙을 유지했다.
- BEAVER 씬과 신규 프리팹은 `Assets/02. ParkHanSol_TeamLeader_Build & Multi/` 아래의 `PHS_Network` 복사본을 사용한다.
- 19:26:40에 Tutorial authoring 중복 실행 오류 2건이 발생했었다. 컴파일 오류는 아니며 최종 Refresh 뒤 Console 0으로 확인했다.
  - `Overwriting the same path as another open scene is not allowed.`
  - `PHS_NETWORK_TUTORIAL_AUTHORING_FAILED reason=scene_save_failed`
- 원인: 이미 열린 Tutorial 씬을 다른 authoring 요청이 같은 경로로 다시 저장하려고 한 실행 순서 충돌이다.
- 다음 작업 시작 때 다른 BEAVER 씬을 먼저 연 뒤 Tutorial authoring을 단독 실행하고, Console Error 0을 다시 확인해야 한다.

## 2. 브랜치와 씬 구성

### BEAVER 전용 씬

| 용도 | 경로 | GUID | 상태 |
|---|---|---|---|
| Lobby | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/ParkHanSol_LobbyScene.unity` | `7f6ea5184deab8440939957aad9e95ad` | 복사본, 옵션 UI 연결 |
| Tutorial | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Tutorial/PHS_NetworkTutorialScene.unity` | `7ab67b955719b1641abfc1778b40bfc1` | 조립 완료, 아래 magenta 이슈 남음 |
| Map | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity` | `50a12aa76331aa04796f77f318ce226c` | 팀 Map 복사본, 팀 작업 영역은 열어 둠 |
| Shop | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity` | `dbdb852546180384f885696430f292c9` | 캐셔 복사본 연결 |
| Legacy Debris | `Assets/02. ParkHanSol_TeamLeader_Build & Multi/99. Legacy/Scenes/Retired/` | `782a1efe2d080044fb88ec0fbd8f50a3` | Release 제외 |

### Build Settings

현재 `ProjectSettings/EditorBuildSettings.asset`의 활성 순서:

1. Lobby
2. Tutorial
3. Map
4. Shop

`FeatureInspection`과 `DebrisCollection/Legacy`는 Release Build에서 제외했다.

## 3. 구현 완료 또는 코드 반영된 항목

### 3.1 아이템 획득·교체·상호작용

관련 파일:

- `TempPlayerItemHolder.cs`
- `TempPlayerInteractionScanner.cs`
- `NetworkPlayerItemLifecycle.cs`
- `NetworkPlayerItemRecord.cs`

반영 내용:

- 아이템 A를 든 상태에서 B 획득 시 A를 서버 권위로 내려놓고 B를 든다.
- 내려놓은 기존 아이템의 내구도와 물리 상태를 보존한다.
- interaction raycast가 현재 들고 있는 아이템과 자기 collider를 건너뛴다.
- 아이템을 들었을 때 상호작용이 꺼지는 원인 경로를 수정했다.

남은 검증:

- 실제 Host/Client에서 A 획득 → B 교체 → A 월드 드롭을 직접 확인해야 한다.
- 원격 Client 관찰에서 held/dropped 상태와 물리 위치가 일치해야 한다.

### 3.2 Party Credits HUD

신규 파일:

- `NetworkPartyCreditsHudBinding.cs`

반영 내용:

- `PartyCreditsHudBinder`가 활성화되는 순간 `IShopWallet`이 없어서 실패하던 구조를 RunSessionRoot availability와 Economy snapshot 기반으로 바꿨다.
- 임의 `Find` 또는 fallback 없이 명시적 세션 이벤트를 사용한다.

남은 검증:

- Host/Client 각각에서 `PHS_PARTY_CREDITS_HUD_BIND_FAILED` 0회 확인.
- 상점 결제 직후 양쪽 HUD 금액 동기화 확인.

### 3.3 상점 Checkout와 구매 불가 UI

관련 파일:

- `ShopCheckoutZone.cs`
- `ShopPurchaseService.cs`
- `INetworkShopPurchaseReceiptService.cs`
- `PHS_NetworkShopCheckoutCounter.prefab`

반영 내용:

- Checkout은 sender, 동일 scene, 거리, trigger snapshot, NetworkObject, catalog, 중복 ID를 서버에서 검증한다.
- 서버 purchase receipt commit이 성공한 뒤에만 item을 despawn한다.
- Client가 임의 RPC로 다른 UtilityItemObject를 삭제하는 직접 경로를 제거했다.
- 캐셔 상단 노란 임시 구매 불가 문구를 제거했다.
- 구매 불가는 캐셔 아래쪽 붉은 `PURCHASE UNAVAILABLE` 표시를 사용한다.
- 정상 가격 행에 `HAVE ... NOT ENOUGH` 문구를 붙이지 않는다.
- BEAVER Shop에는 `PHS_NetworkShopCheckoutCounter`가 1개 연결되어 있다.

주의:

- 사용자가 결제 공격 QA의 우선순위를 내렸다. 구현은 유지하지만 공격 시나리오 추가 검증은 후순위다.

### 3.4 결과 UI, 로비 복귀, Host 재시작

신규/수정 파일:

- `NetworkRunResultPanelController.cs`
- `NetworkRunResultPanelView.cs`
- `NetworkSessionExitService.cs`
- `INetworkSessionExitService.cs`
- `NetworkRunRestartCoordinator.cs`
- `NetworkRunRestartState.cs`
- `INetworkRunRestartService.cs`
- `NetworkRunSessionRootBootstrap.cs`
- `PHS_NetworkRunResultPanel.prefab`

반영 내용:

- Clear/GameOver 결과, zones, shop cycles, party credits를 표시한다.
- `RETURN TO LOBBY` 흐름이 있다.
- `RESTART RUN`은 Host만 활성화되고 Client는 `HOST ONLY` 상태다.
- 중복 restart pending을 차단한다.
- restart 실패 reason을 UI와 로그에 표시한다.
- restart 중 old player despawn이 로비 복귀를 호출하지 않도록 막았다.
- restart는 terminal phase에서만 허용하고 scene load barrier 후 fresh RunSessionRoot/player를 생성하도록 구성했다.

정적 검증:

- Result prefab local YAML 참조 40/40 resolve, duplicate object ID 0.
- 마지막 importer 이후 prefab 오류는 발견되지 않았다.

남은 검증:

- 실제 Host Clear/GameOver → Restart → 새 run 시작.
- Client 버튼 `HOST ONLY`.
- restart 중 Client 이탈/재접속.
- 실패 환경에서 non-empty failure reason.

### 3.5 게임 규칙 4·8 구역 상점

관련 파일:

- `GameLoopController.cs`
- `GameLoopState.cs`
- `README_GameEconomy_Integration.md`

반영 내용:

- 실제 상점 규칙을 4구역과 8구역으로 통일했다.
- 남아 있던 `3구역마다`, `3·6구역` 주석/문서 불일치를 정리했다.
- 전체 run은 9구역 기준이다.

### 3.6 Release Build 분리

신규/수정 파일:

- `PHSNetworkBeaverReleaseBuilder.cs`
- `PHS0715IntegrationValidator.cs`
- `ProjectSettings/EditorBuildSettings.asset`

반영 내용:

- Windows64, `BuildOptions.None`의 비Development 전용 Builder를 만들었다.
- Release scene은 Lobby, Tutorial, Map, Shop만 사용한다.
- `FeatureInspection`, `DebrisCollection`, `/Legacy/`가 Release 목록에 있으면 실패한다.
- Integration validator의 필수 씬 수를 4로 바꾸고 Tutorial 필수 Inspector 참조 검사를 추가했다.

남은 검증:

- `Tools/ParkHanSol/Build BEAVER 2026 Release Player` 실제 실행은 아직 하지 않았다.
- 출력 예상 경로: `Builds/BEAVER_2026/LastJumpCrew_BEAVER_2026.exe`.

### 3.7 Lobby/ESC 옵션 UI

신규/수정 파일:

- `INetworkPlayerOptionsStore.cs`
- `NetworkPlayerOptionsStore.cs`
- `INetworkOptionsPanel.cs`
- `NetworkSharedOptionsPanelController.cs`
- `NetworkOwnerUiRoot.cs`
- `PHSNetworkOptionsAuthoring.cs`
- `PlayerControlInput.cs`
- `PlayerControlRebindPanel.cs`
- `ParkHanSolPauseMenuController.cs`
- `PHS_NetworkStartLobbyUI.prefab`
- `PHS_NetworkPlayHudUI.prefab`
- `PHS_NetworkOwnerPauseUI.prefab`

반영 내용:

- Lobby와 게임 중 ESC에서 같은 저장소를 사용하는 마우스 감도, 키 리바인드, 해상도, 창 모드 옵션을 제공한다.
- 중복 키는 거부하고 이전 binding을 복구하며 `KEY IN USE`를 표시한다.
- 잘못된 PlayerPrefs는 fallback하지 않고 `INVALID SETTING` 오류를 표시한다.
- OwnerPause의 중복 Options 패널을 제거했다.
- 고정 Card를 무너뜨리던 잘못된 ContentSizeFitter를 제거했다.
- Video row와 Controls 영역을 분리했다.
- PlayHud의 Options를 Pause Menu 자식에서 sibling으로 옮겨 transition 중 사라지는 문제를 수정했다.

검증 완료:

- ESC → Options 표시 → Back → Pause 복귀 Play 검증 PASS.
- Error 0.
- 1280×720, 1920×1080, 2560×1440, 3440×1440에서 overlap 0, clipping 0.
- Tutorial/Lobby missing refs 0.

### 3.8 Tutorial

신규/수정 파일:

- `PHSNetworkTutorialAuthoring.cs`
- `NetworkTutorialDirector.cs`
- `NetworkTutorialInteractionStation.cs`
- `PHS_NetworkTutorialPlayer.prefab`
- `PHS_NetworkTutorialInteractionStation.prefab`
- `PHS_NetworkTutorialWall.prefab`
- `PHS_NetworkTutorialDoor.prefab`
- `PHS_NetworkTutorialDisplayDesk.prefab`
- `PHS_NetworkTutorialScene.unity`

단계:

1. WASD 이동
2. Zero-G와 thruster
3. grapple
4. item pickup
5. item drop
6. item swap
7. F 상호작용
8. 완료와 Lobby 복귀

조립 상태:

- 기존 `PHS_NetworkPlayHudUI`를 사용한다.
- 별도 임시 HUD는 제거했다.
- Tutorial player의 중복 OwnerPause는 0개다.
- 상점 배정 에셋 복사본으로 벽 82, 문 1, 진열대 2, workstation을 조립했다.
- legacy primitive floor/wall/end는 0개다.
- missing refs 0.

남은 P0:

- 중앙 grapple target가 GameView에서 magenta로 보인다.
- `PHS_NetworkTutorialWall` 또는 정상 URP/Lit material을 가진 owned prefab instance로 교체해야 한다.
- 교체 후 `PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot`가 빈 문자열이 아니어야 한다.
- shader가 `Universal Render Pipeline/Lit`인지 확인하고 GameView magenta 0으로 검증한다.
- 전체 Tutorial 단계를 직접 완주하지 않았다.

### 3.9 8K Tutorial 우주 배경

신규 파일:

- `PHSNetworkTutorialSpaceSkyboxAuthoring.cs`
- `03. Environment/Tutorial/PHS_NetworkTutorialSpaceSkybox.jpg`
- `03. Environment/Tutorial/PHS_NetworkTutorialSpaceSkybox.mat`
- `03. Environment/Tutorial/PHS_NetworkTutorialSpaceSkybox_LICENSE.md`

상태:

- 실제 이미지 크기 8192×4096, 24-bit RGB.
- 외부 이미지, 모델, 텍스처, 생성형 서비스를 사용하지 않은 자체 procedural artwork다.
- Tutorial `RenderSettings.skybox`에 직접 연결했다.
- exposure 1.3, mipmap true, Standalone MaxSize 8192, DXT1, readable false.
- 좌우 seam mean RGB diff 0.263, max 15. GameView에서 seam은 보이지 않았다.
- Error 0, missing refs 0으로 확인했다.

### 3.10 사운드

완료:

- `NetworkPlayerThrusterAudio.cs` 구현.
- Player prefab에 `PHS_NetworkThrusterAudio` child와 CC0 thruster clip 연결.
- 공간 감쇠와 loop 상태 연결.
- 전체 오디오 감사 문서 `Validation/PHS_NetworkAudioCoverage.md` 작성.

감사 결과:

- 프로젝트 전체 AudioClip은 2개뿐이었다.
- 사용 가능: `PHS_ZeroGravityThruster_CC0.ogg`.
- 사용 보류: `Sound_Fire.mp3`는 라이선스/출처 문서가 없다.
- UI, item, shop, result, incident, tutorial, BGM/ambient 대부분이 미구현이다.

중단된 작업:

- 17종 자체 PCM WAV 생성기와 audio component는 설계만 했고 파일 저장 전 중단했다.
- 계획 경로: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/06. Audio/NetworkGenerated/`.
- Runtime `AudioClip.Create` fallback은 사용하지 않고, Editor에서 실제 44.1 kHz mono PCM16 WAV를 생성/import한 뒤 Inspector에 연결하는 방향이다.

### 3.11 Lobby 장신구 구매·장착·3D 미리보기

Backend 반영 파일:

- `INetworkLobbyCustomizationService.cs`
- `NetworkPlayerCustomization.cs`
- `PersonalLobbyCustomizationCreditsWallet.cs`
- `LobbyCustomizationPanelController.cs`

Backend 상태:

- local preview head/back/color와 `PreviewChanged` 이벤트.
- purchase/equip/unequip/color 요청의 Try API.
- server catalog, ownership, slot, color 검증.
- corrupt/unknown/duplicate/unowned equipped profile과 out-of-range credits를 fail-closed 처리.
- 최초 정상 profile은 white + personal credits 300.

제한:

- 재접속 영속 source는 기존 Client PlayerPrefs다.
- live session은 서버 권위지만 악성 Client가 재접속 시 owned 목록을 주장하는 것까지 완전 제거하려면 Cloud Save/계정 저장이 필요하다.

미구현 Frontend:

- `PHS_NetworkLobbyCustomizationPanel` 새 GUID 복사본.
- visual-only preview rig, Camera, 1024 RenderTexture.
- drag rotate, scroll zoom.
- accessory list, price, balance, BUY/EQUIP/EQUIPPED, unequip/color UI.
- 실제 player NetworkObject를 preview에 복제하면 안 된다.
- Lobby `TRAINING` 버튼과 Tutorial scene roundtrip.
- 16:9/21:9 QA.

## 4. 저장됐지만 아직 검증하지 않은 자동 검증 드라이버

중단 직전에 아래 파일이 저장됐다.

- `PHS_NetworkCustomizationValidationDriver.cs`
- `PHS_NetworkRunRestartValidationDriver.cs`

Customization driver flag:

- `-phsNetworkCustomizationValidation normal`
- `-phsNetworkCustomizationValidation corrupt`

Restart driver flag:

- `-phsNetworkRunRestartValidation success`
- `-phsNetworkRunRestartValidation expect-failure`

주의:

- 저장 뒤 Unity가 compile false 상태로 돌아왔고 최신 Console에 CS 오류는 보이지 않지만, 네 가지 flag 실제 실행은 하지 않았다.
- `expect-failure`는 고의 fault injection을 넣지 않았다. 실제 safe-zone 누락, scene load fault, timeout 같은 장애 환경에서만 failure reason을 검증한다.

## 5. 다음 컴퓨터에서 이어서 할 정확한 순서

### P0-A. 체크포인트 복구와 Tutorial 정리

1. `codex/beaver-2026-item-swap` checkout/pull.
2. Unity 6000.5.2f1로 연다.
3. BEAVER Shop 또는 Lobby를 먼저 연다. Tutorial을 열린 상태로 Tutorial authoring하지 않는다.
4. Console을 비운 뒤 script compile Error 0 확인.
5. 중앙 grapple target magenta의 material/prefab source를 수정한다.
6. Tutorial authoring을 단독으로 1회 실행한다.
7. Tutorial scene clean, missing refs 0, GameView magenta 0, Play Error 0 확인.
8. Tutorial 전 단계를 직접 완주하고 Lobby 복귀를 확인한다.

### P0-B. 사운드

1. `PHS_NetworkAudioCoverage.md`를 기준으로 P0부터 구현한다.
2. 라이선스 불명 `Sound_Fire.mp3` 참조는 만들지 않는다.
3. 자체 PCM WAV generator와 recipe/license note를 만든다.
4. item pickup/swap/drop, shop success/fail, warning, Clear/GameOver, restart, tutorial complete부터 연결한다.
5. 3D emitter 거리, 중복 발음 제한, loop 종료를 검증한다.
6. 이후 P1의 UI, grapple, incident, ambient/BGM을 연결한다.

### P0-C. Lobby 커스터마이징 Frontend

1. `INetworkLobbyCustomizationService` 기반 controller를 만든다.
2. 기존 `PHS_LobbyCustomizationPanel.prefab`은 수정하지 않고 `PHS_NetworkLobbyCustomizationPanel`로 새 GUID 복사한다.
3. visual-only preview rig를 구성한다.
4. catalog `VisualPrefab`에 `NetworkObject`가 있으면 오류를 내고 preview를 비활성화한다. fallback 금지.
5. BUY/EQUIP/EQUIPPED/price/balance/unequip/color를 연결한다.
6. `TRAINING` 버튼으로 network shutdown 완료 후 Tutorial을 연다.
7. Host/Client purchase/equip 동기화와 재접속 profile을 확인한다.
8. 1280×720, 1920×1080, 2560×1440, 3440×1440에서 QA한다.

### P0-D. 전체 Host/Client QA

반드시 실제 조작으로 다음을 확인한다.

1. Lobby Host 시작과 Client 참가.
2. 돈 HUD 양쪽 오류 0.
3. item A 획득 → item B 획득 → A 내려놓기.
4. 1~9구역 진행.
5. 4구역과 8구역에서 Shop 진입/복귀.
6. Checkout 성공과 잔액 부족 UI.
7. Clear와 GameOver 결과 화면.
8. Host Restart와 Lobby Return.
9. Client 이탈, 재접속.
10. 방장 종료.
11. 모든 단계에서 Error 0.

### P0-E. Release Build

1. `Tools/ParkHanSol/Validate 0715 Integration` 실행.
2. Error 0, missing reference 0.
3. `Tools/ParkHanSol/Build BEAVER 2026 Release Player` 실행.
4. Windows64 비Development build 성공.
5. Build에 Lobby/Tutorial/Map/Shop만 포함됐는지 확인.
6. FeatureInspection, DebrisCollection, Legacy 참조 0 확인.
7. 빌드 실행 파일에서 Tutorial과 Host/Client smoke test.

## 6. Unity 메뉴

- Options 저작: `Tools/ParkHanSol/BEAVER/Author Network Options UI`
- Tutorial 저작: `Tools/ParkHanSol/BEAVER/Author Network Tutorial`
- Tutorial skybox 저작: `Tools/ParkHanSol/BEAVER/Author Network Tutorial Space Skybox`
- 통합 검증: `Tools/ParkHanSol/Validate 0715 Integration`
- Release build: `Tools/ParkHanSol/Build BEAVER 2026 Release Player`

위 메뉴 이름은 현재 Editor script의 `MenuItem`과 대조했다.

## 7. Git에 포함하지 않을 로컬/사용자 변경

이번 체크포인트 commit에서 아래 파일은 제외한다.

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/MCPForUnityLocal/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/MCPPackages~/`
- `Library/MCPForUnity/`
- `Library/PackageCache/com.anklebreaker.unity-mcp*/`
- `.codex/`, `.codex.json`, `.codex.toml`, `.mcp.json`, `mcp.json`
- `Packages/packages-lock.json`의 `com.anklebreaker.unity-mcp` 오염
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/05. Material/Items/Feedback/PHS_ItemRangeOutline.mat`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/05. Material/Items/Feedback/PHS_WrenchSpark.mat`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/_ThirdParty/Fonts/Maplestory Light SDF.asset`
- `LastJumpCrew.slnx`
- `ProjectSettings/ProjectSettings.asset`

위 파일은 기존 사용자/Unity/MCP 변경이므로 되돌리지도, stage하지도 않는다.

## 8. 작업 원칙

- 팀원이 프리팹을 바꾸면 원본을 직접 수정하지 않고 `Assets/02`로 복사한 뒤 새 GUID와 `PHS_Network` 이름으로 수정한다.
- 팀 Map 원본과 팀 제작 중인 Map 영역은 열어 둔다.
- 네트워크 작업 스크립트/프리팹은 `Network` 이름을 포함해 팀원 파일과 충돌을 피한다.
- interface script 이름은 `I`로 시작한다.
- Inspector 참조 누락을 코드 fallback으로 숨기지 않는다. 명확한 `PHS_*_FAILED` 로그를 낸다.
- 문제가 작동하지 않을 때 기능 보강 전에 코드 구조, 참조, Inspector 연결을 기준으로 원인을 분석한다.

## 9. Notion 동기화 상태

- 이 Markdown은 개인 Notion 인계 문서의 원본으로 작성했다.
- 현재 Codex 세션에는 Notion MCP write 도구가 노출되지 않아 Notion page 생성/업로드는 수행하지 못했다.
- 다음 세션에서 Notion 연결을 활성화한 뒤 이 문서를 개인 Engineering/Reference 문서로 업로드한다.
- 권장 제목: `LastJumpCrew BEAVER 2026 작업 인계 — 2026-07-23`
- 권장 상태: `Draft`
- 권장 Tags: `Unity`, `Netcode`, `BEAVER 2026`, `Handoff`, `PHS Network`
