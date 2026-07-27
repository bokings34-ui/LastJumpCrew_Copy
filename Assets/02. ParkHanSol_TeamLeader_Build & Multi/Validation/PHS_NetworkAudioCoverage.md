# PHS Network Audio Coverage

정적 감사 기준: 2026-07-23. Unity Editor 재생/프리팹 저장 없이 코드, YAML, AudioClip만 확인했다.

## 현재 결론

- 프로젝트 전체 오디오 파일은 2개뿐이다.
  - `03. Audio/PHS_ZeroGravityThruster_CC0.ogg`: CC0 출처 문서 있음. 현재 플레이어 thruster loop에 연결됨.
  - `04. NohSeokMin_Game Event/99_Resource/Sound_Fire.mp3`: 라이선스/출처 문서 없음. 출품 빌드 사용 보류.
- 실제 연결된 완성 cue는 zero-G thruster loop 1개뿐이다.
- fire presentation은 `AudioSource` 참조는 있으나 clip이 비어 있다.
- mini-game base는 `clickClip/successClip/failClip` 필드가 있으나 확인된 씬의 clip은 비어 있다.
- 이동, 갈고리, 아이템, 상점, 런 전환, 결과, UI, 튜토리얼, BGM/ambient cue는 전부 미구현이다.
- 팀 원본은 변경하지 않는다. 아래 `대상`은 전부 ParkHanSol owned copy 또는 신규 `PHS_Network*` 프리팹이다.

## 재생 규칙

- 2D UI: `spatialBlend=0`, 같은 cue 재입력 0.05초 제한, UI 동시발음 최대 4, 기본 0.55~0.7.
- 3D one-shot: logarithmic rolloff, `minDistance=1`, `maxDistance=12~20`, doppler 0, 같은 emitter 0.08~0.15초 제한.
- 플레이어 동작: 로컬 입력 즉시 재생. 다른 플레이어에게 필요한 cue만 서버 검증 상태/RPC 결과로 재생한다.
- 지속음: emitter당 loop 1개. 상태 종료 시 fade-out 후 정지한다.
- 경보/BGM: 2D, 경보 동시발음 1, BGM 동시발음 1. 중요한 결과 cue가 재생될 때 ducking 적용 대상이다.
- clip 누락/AudioSource 누락은 fallback하지 않고 `PHS_NETWORK_AUDIO_*_FAILED` Error를 남긴다.

## 커버리지 매트릭스

| 우선 | 영역 / cue | 현재 | 정확한 트리거 후보 | 기존 clip 후보 | 공간 / 음량 / 동시발음 | 대상 owned copy |
|---|---|---|---|---|---|---|
| P1 | 이동 발걸음/부유 이동 | 없음 | `NetworkPlayerController`의 grounded 이동 및 속도 임계값 | 없음 → 자체 제작/CC0 필요 | 3D, 0.35, 1/player, 0.25초 간격 | `03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab` |
| P0 | 점프 | 없음 | `NetworkPlayerController` line 927 `IsGrounded && jump` | 없음 → 자체 제작/CC0 필요 | 3D, 0.65, 1/player | player copy |
| P0 | 착지 | 없음 | `NetworkPlayerController.IsGrounded` false→true 전환 + 하강속도 임계값 | 없음 → 자체 제작/CC0 필요 | 3D, 0.65, 1/player, 충격별 볼륨 | player copy |
| P0 | thruster loop 시작/종료 | 구현됨 | `UpdateLocalThrusterFeedback` → `NetworkPlayerThrusterAudio.SetThrusterActive` | `PHS_ZeroGravityThruster_CC0.ogg` 사용 중 | 3D blend 0.45, min 0.5/max 14, loop 1 | player copy, Tutorial player copy |
| P1 | thruster 연료 부족/종료 | 없음 | thruster 요청 true + fuel 0, 또는 active true→false | 없음 → 자체 제작/CC0 필요 | 로컬 2D, 0.55, 1 | player copy / PlayHud copy |
| P0 | 갈고리 발사 | 없음 | `NetworkPlayerGrappleController.LaunchGrapple` | thruster clip은 부적합 → 신규 필요 | 3D, 0.75, 1/player | `03. Prefab/Grapple/PHS_SciFiRoboticClawHook.prefab` + player copy |
| P0 | 갈고리 적중/래치 | 없음 | `NetworkPlayerGrappleController.LatchHook` | 없음 → 자체 제작/CC0 필요 | 3D hit point, 0.8, 1/hook | grapple claw copy |
| P1 | 갈고리 당김 loop | 없음 | `SetPullRequested(true/false)` 및 `Latched` 상태 | thruster clip pitch 변형 후보이나 식별성 낮음 → 신규 권장 | 3D, 0.45, loop 1 | grapple claw copy |
| P0 | 갈고리 해제/회수 | 없음 | `StopGrapple`, `TryAutoDetachAtStopDistance` | 없음 → 자체 제작/CC0 필요 | 3D, 0.6, 1/hook | grapple claw copy |
| P0 | 상호작용 대상 포커스 | 없음 | `TempPlayerInteractionScanner.RefreshInteractableFocusGlow` 대상 변경 | 없음 → 자체 제작/CC0 필요 | 로컬 2D, 0.35, 1, 0.1초 제한 | PlayHud copy / player copy |
| P0 | 상호작용 성공 | 없음 | `TempPlayerInteractionScanner.TryInteract`의 `Interact` 호출 직후 | 없음 → 자체 제작/CC0 필요 | 로컬 2D, 0.6, 1 | player copy |
| P1 | 상호작용 거부 | 없음 | `CanInteract == false`, 현재 warning 로그 경로 | 없음 → 자체 제작/CC0 필요 | 로컬 2D, 0.55, 1, 0.15초 제한 | player copy |
| P0 | 아이템 획득 | 없음 | `UtilityItemObject.Interact` → `RequestNetworkPickup`; 최종 성공은 `TempPlayerItemHolder.HandleNetworkHeldItemChanged` | 없음 → 자체 제작/CC0 필요 | 3D pickup 위치 + owner 2D 확인음, 각 1 | player copy + owned item copies |
| P0 | 아이템 교체/기존템 내려놓기 | 없음 | `TempPlayerItemHolder.ReplaceHeldItem`/network held item ID A→B | 없음 → 자체 제작/CC0 필요 | owner 2D 0.65 + drop 3D 0.6 | player copy + owned dropped item copies |
| P0 | 아이템 내려놓기 | 없음 | `TempPlayerItemHolder.Drop` 성공, `TryPlaceCurrentItem`/`TryPlaceHeldDebris` 성공 | 없음 → 자체 제작/CC0 필요 | 3D, 0.65, 1/item | owned item/debris dropped prefabs |
| P1 | 아이템 던지기 | 없음 | `TryCreateThrownItem`/`TryReleaseHeldDebrisForThrow` 성공 | 없음 → 자체 제작/CC0 필요 | 3D, 0.6, 1/player | player copy + dropped item copies |
| P0 | 아이템 충돌 | 없음 | dropped item `Collision.impulse` 임계값 | 없음 → 자체 제작/CC0 필요 | 3D, min 1/max 14, 0.1~0.2초 제한, 1/item | owned item/debris dropped prefabs |
| P1 | 도구 사용 | 부분 구조만 있음 | `NetworkPlayerCombatController` wrench/battery/extinguisher effect 실행 | effect 자식 AudioSource는 있으나 clip 확인 안 됨 | 3D, 0.65, 1/player/tool | player copy + owned tool held prefabs |
| P0 | 상점 입장/캐셔 활성 | 없음 | `ShopCheckoutZone.OnTriggerEnter` 첫 유효 shop item | 없음 → 자체 제작/CC0 필요 | 3D counter, 0.55, 1/zone | `03. Prefab/Shop/PHS_NetworkShopCheckoutCounter.prefab` |
| P0 | 상품 선택 | 없음 | `ShopOfferInteractable.Interact` 성공 | 없음 → 자체 제작/CC0 필요 | 로컬 2D, 0.55, 1 | owned shop display copies / PlayHud copy |
| P0 | 계산 버튼 승인/거부 | 없음 | `ShopCheckoutButtonInteractable.Interact`의 `accepted` | 없음 → 자체 제작/CC0 필요 | 3D counter, 0.65, 1, 0.12초 제한 | NetworkShopCheckoutCounter copy |
| P0 | 구매 성공 | 없음 | `ShopCheckoutZone.HandleStandalonePurchaseCompleted` 또는 `HandleNetworkCheckoutResult`, `result.Success=true` | 없음 → 자체 제작/CC0 필요 | 요청자 2D 0.8 + counter 3D 0.55 | NetworkShopCheckoutCounter copy |
| P0 | 구매 실패/잔액 부족 | 없음 | 같은 완료 함수 `result.Success=false`, `ShowTemporaryStatus(..., true)` | 없음 → 자체 제작/CC0 필요 | 요청자 2D, 0.75, 1, 0.2초 제한 | NetworkShopCheckoutCounter copy |
| P1 | 구매 배송/텔레포트 | VFX만 있음 | `PlayCheckoutTeleportEffect` | thruster clip은 부적합 → 신규 필요 | 3D counter, 0.7, 1 | NetworkShopCheckoutCounter copy |
| P0 | incident 시작 경보 | 없음 | `NetworkRunIncidentLedger.CommandChanged`: command `Active`, family별 분기 가능 | `Sound_Fire.mp3` 후보지만 라이선스 불명으로 사용 금지 | 2D 경보 0.8, 동시 1 + 위치 3D cue | `03. Prefab/Integration/PHS_NetworkRunSessionRoot.prefab` + accident presentation copies |
| P0 | incident 해결/실패 | 없음 | ledger command `Resolved`/`Failed`/`Cancelled` | 없음 → 자체 제작/CC0 필요 | 2D, 0.7, 동시 1 | NetworkRunSessionRoot copy |
| P1 | fire loop | AudioSource 구조만 있음, clip 비어 있음 | `PHSTeamFirePatchPresentationAdapter.ApplyState` active/intensity | `Sound_Fire.mp3`는 라이선스 확인 전 사용 금지 | 3D, min 1/max 18, loop 1/patch, 최대 4 voice | `PHS_TeamFirePatchPresentation.prefab` |
| P0 | zone 제한시간 경고 | 없음 | `NetworkRunStageClock` running 잔여 30/10/5초 crossing | 없음 → 자체 제작/CC0 필요 | 2D, 0.75, 경보 동시 1 | NetworkRunSessionRoot / PlayHud copy |
| P0 | warp 충전 | 없음 | `NetworkRunFlowCoordinator.WarpChargeNormalized` 0→1, `TickWarpCharge` | thruster clip 저음 pitch 후보이나 별도 신규 권장 | 2D+3D hybrid, 0.55, loop 1 | NetworkRunSessionRoot + map presentation owned copy |
| P0 | warp 출발 | 없음 | phase changed → warp, `WarpTransitionPresenter.EnterWarp` | thruster clip 후보이나 전환 식별성 낮음 → 신규 필요 | 2D, 0.85, 1 | NetworkRunSessionRoot / PlayHud copy |
| P0 | warp 도착/zone 전환 | 없음 | `WarpTransitionPresenter.ExitWarp`, active map revision commit | 없음 → 자체 제작/CC0 필요 | 2D, 0.75, 1 | NetworkRunSessionRoot / PlayHud copy |
| P0 | Clear 결과 | UI만 있음, 음향 없음 | `NetworkRunResultPanelController.RefreshForPhase(Clear)` | 없음 → 자체 제작/CC0 필요 | 2D, 0.9, 1, BGM duck | `03. Prefab/UI/PHS_NetworkRunResultPanel.prefab` |
| P0 | GameOver 결과 | UI만 있음, 음향 없음 | `RefreshForPhase(GameOver)` | 없음 → 자체 제작/CC0 필요 | 2D, 0.9, 1, BGM duck | NetworkRunResultPanel copy |
| P0 | 재시작 요청/성공/실패 | 없음 | `RestartRun`, `HandleRestartStateChanged`, `ShowRestartFailure` | 없음 → 자체 제작/CC0 필요 | 2D, 0.65~0.8, 1 | NetworkRunResultPanel copy |
| P1 | 로비 버튼 hover/click/back | 없음 | `ParkHanSolLobbyMenuController` 및 Unity Button events | 없음 → 자체 제작/CC0 필요 | 2D, 0.45/0.65, UI 동시 최대 4 | `03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab` |
| P1 | ESC 열기/닫기 | 없음 | `ParkHanSolPauseMenuController.OpenMenu/CloseMenu` | 없음 → 자체 제작/CC0 필요 | 2D, 0.6, 1 | `03. Prefab/UI/PHS_NetworkOwnerPauseUI.prefab` |
| P1 | 옵션 변경/키 리바인드 성공·취소·실패 | 없음 | `NetworkSharedOptionsPanelController` change/close 및 rebind 완료 상태 | 없음 → 자체 제작/CC0 필요 | 2D, 0.45~0.7, UI 동시 최대 4 | StartLobbyUI + OwnerPauseUI copies |
| P1 | 커스터마이징 열기/선택/구매·장착 실패 | 없음 | `LobbyCustomizationPanelController.OpenPanel`, `RequestItemAction`, `RequestColor`, `SetStatus` | 없음 → 자체 제작/CC0 필요 | 2D, 0.5~0.75, UI 동시 최대 4 | `03. Prefab/UI/Customization/PHS_LobbyCustomizationPanel.prefab` |
| P1 | 튜토리얼 단계 완료 | 없음 | `NetworkTutorialDirector.SetStep`의 Movement→…→Complete 전환 | 없음 → 자체 제작/CC0 필요 | 2D, 0.55, 1, 단계당 1회 | Tutorial scene + Tutorial player owned copy |
| P0 | 튜토리얼 완료 | 없음 | `SetStep(Complete)` / `PHS_NETWORK_TUTORIAL_COMPLETE` | Clear cue 변형 가능하나 별도 짧은 신규 권장 | 2D, 0.85, 1 | Tutorial scene completion panel |
| P1 | 미니게임 클릭/성공/실패 | API만 있음, clip 미연결 | `PHSMiniGameBase.PlaySFX(clickClip/successClip/failClip)` | 없음 → 자체 제작/CC0 필요 | 2D, 0.55/0.8, UI 동시 최대 4 | ParkHanSol owned mini-game prefab copies |
| P1 | 로비 BGM | 없음 | lobby scene active 동안 | 없음 → 자체 제작/CC0 필요 | 2D, 0.35, loop 1 | 신규 `03. Prefab/Audio/PHS_NetworkLobbyAudioRoot.prefab` |
| P1 | 게임 BGM | 없음 | gameplay phase 동안, result에서 fade/duck | 없음 → 자체 제작/CC0 필요 | 2D, 0.3, loop 1 | 신규 `03. Prefab/Audio/PHS_NetworkRunAudioRoot.prefab` |
| P1 | 함선 ambient | 없음 | gameplay scene active 동안 | thruster clip은 플레이어 식별음과 충돌 → 신규 필요 | 3D zone bed, 0.25, loop 최대 2 | owned map environment prefab 또는 RunAudioRoot |
| P2 | 상점 ambient | 없음 | shop phase/scene 동안 | 없음 → 자체 제작/CC0 필요 | 2D/3D hybrid, 0.25, loop 1 | 신규 RunAudioRoot / owned shop environment copy |
| P2 | 결과 UI hover | 없음 | 결과 패널 버튼 hover/click | 로비 UI cue 재사용 가능(신규 확보 후) | 2D, 0.45, UI 동시 최대 4 | NetworkRunResultPanel copy |

## P0 구현 순서

1. `06. Audio/NetworkGenerated/`에 아래 recipe로 PCM WAV를 생성하고 Unity asset으로 import한다. Runtime `AudioClip.Create`는 사용하지 않는다.
2. 공용 `INetworkAudioCuePlayer` + 명시적 `AudioSource`/clip 슬롯을 가진 `NetworkAudioCueEmitter` 작성.
3. player owned copy에 jump/land, grapple, pickup/swap/drop cue 연결.
4. dropped item owned copies에 impulse 기반 `NetworkCollisionAudioFeedback` 연결.
5. checkout counter에 enter/button/success/failure cue 연결.
6. RunSessionRoot에 incident/clock/warp cue presenter 연결.
7. result panel과 tutorial completion에 2D cue 연결.
8. 생성 recipe, 샘플레이트, 채널, 정규화 peak와 자체생성 사실을 `PHS_NetworkGeneratedAudio_README.md`에 기록한다.

## 자체 생성 WAV 초안

전부 44.1 kHz, mono, PCM 16-bit, peak -3 dBFS 이하. seed를 고정해 재생성 결과를 동일하게 만든다.

| 파일 | 길이 | 합성 recipe | 주 사용처 |
|---|---:|---|---|
| `PHS_Network_UI_Click.wav` | 0.08s | 920→620 Hz sine chirp + 3 ms noise transient, exponential decay | 로비/ESC/옵션 클릭 |
| `PHS_Network_Item_Pickup.wav` | 0.22s | 660/880/1100 Hz sine arpeggio, 짧은 bell envelope | 획득 |
| `PHS_Network_Item_Drop.wav` | 0.24s | 145→72 Hz low sine thump + low-pass noise | 내려놓기/가벼운 충돌 |
| `PHS_Network_Item_Swap.wav` | 0.30s | 520→360 Hz 하강 chirp 뒤 620→930 Hz 상승 chirp | 기존템 drop + 새템 획득 |
| `PHS_Network_Shop_Success.wav` | 0.64s | C6-E6-G6 major arpeggio + soft sine overtone | 구매 성공 |
| `PHS_Network_Shop_Fail.wav` | 0.42s | 310→205→155 Hz triangle descending, 두 pulse | 구매 불가/실패 |
| `PHS_Network_Warning.wav` | 0.90s | 760/570 Hz 교대 sine pulse 3회 | incident/clock 경고 |
| `PHS_Network_Clear.wav` | 1.45s | C5-E5-G5-C6 상승 major, 긴 release | Clear 결과 |
| `PHS_Network_GameOver.wav` | 1.35s | G4-Eb4-C4 하강 minor, low sine tail | GameOver 결과 |
| `PHS_Network_TutorialComplete.wav` | 0.92s | G5-C6-E6 3-note resolve, 짧은 shimmer | tutorial 완료 |
| `PHS_Network_Jump.wav` | 0.18s | 260→520 Hz sine chirp + 짧은 air noise | 점프 |
| `PHS_Network_Land.wav` | 0.26s | 120→58 Hz sine thump + 감쇠 noise | 착지/중간 충돌 |
| `PHS_Network_GrappleLaunch.wav` | 0.24s | 220→980 Hz chirp + cable noise | 갈고리 발사 |
| `PHS_Network_GrappleLatch.wav` | 0.18s | 1.4 kHz metallic transient + 180 Hz body | 갈고리 적중 |
| `PHS_Network_GrappleRelease.wav` | 0.20s | 720→260 Hz chirp + 짧은 spring tail | 갈고리 해제 |
| `PHS_Network_Warp.wav` | 1.20s | 90→780 Hz layered sine sweep + filtered noise | warp 출발/도착 pitch 변형 |
| `PHS_Network_AmbientLoop.wav` | 8.0s | 48/73 Hz sine bed + 고정 seed low-pass noise, 양끝 equal-power crossfade | 함선 ambient 후보 |

## 검증 기준

- Host/Client 각각 lobby → gameplay → shop → Clear/GameOver에서 Audio 관련 Error 0.
- 동일 입력 연타 시 voice 폭증 없음. loop가 상태 종료/씬 전환 뒤 남지 않음.
- 원격 플레이어 동작은 3D 감쇠되고 UI/결과/경보는 거리 영향 없음.
- 옵션의 master/SFX/UI/BGM 볼륨을 바꿀 때 해당 그룹만 변경됨.
- 라이선스 문서 없는 `Sound_Fire.mp3`는 출품 빌드에서 참조 0.
- 팀 원본 prefab/scene hash 변화 0. ParkHanSol owned copy만 저장.
