# LastJumpCrew BEAVER 2026 최종 정리 — 2026-07-24

이 문서는 2026-07-24 기준 BEAVER 2026 작업의 최신 source-of-truth다. 이전 인계서와 README의 날짜별 기록은 작업 이력으로만 본다.

## 1. Git / PR 기준선

| 항목 | 현재 값 |
|---|---|
| 브랜치 | `codex/beaver-2026-daily-pr` |
| Pull Request | [#71](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/71), Draft / Open |
| 병합 상태 | `CLEAN`, `MERGEABLE` |
| 코드·자산 기준 commit | `d89a16f4e30c94304124d8a7fe520f52b7053eac` |
| 기준 main | `1fe4193c1e26e3d84875d614d1b2b2b1cb7adbdb` |
| 차이 | 코드·자산 기준선은 main보다 1 commit 앞, 0 commit 뒤. 이 문서는 별도 후속 commit으로 추가 |
| GitHub checks | 등록된 status check 없음 |

`d89a16f4`는 문서 변경을 제외한 코드·자산 기준선이다. closeout 문서는 구현 스냅샷과 구분되는 별도 후속 commit으로 관리한다.

## 2. 빌드 구성

- Unity: `6000.5.2f1`, StandaloneWindows64.
- Build Settings: Lobby → Tutorial → Map → Shop, 총 4개 씬.
- FeatureInspection, DebrisCollection, Legacy 씬은 Release Build 대상이 아니다.

## 3. 현재 검증 증거

아래 로그는 로컬 `Artifacts/`와 `Builds/`에 있으며 Git에는 포함되지 않는다.

| 시각(KST) | Gate | 결과 | 증명 범위 |
|---|---|---|---|
| 01:24 | Game audio foundation | `PHS_GAME_AUDIO_FOUNDATION_VALIDATION_PASSED clips=23 ... mixer=4groups ... fire=loop_3d_sfx settings=2` | 정적 clip/import/mixer/source wiring |
| 01:24 | Item audio | `PHS_ITEM_INTERACTION_AUDIO_VALIDATION_PASSED waves=11 players=2 owner2D=true world3D=true shock3D=true` | 정적 item cue/player prefab wiring |
| 01:25 | Map skybox | `PHS_MAP_SKYBOX_VALIDATE_OK count=4` | 4개 map profile/skybox 정적 연결 |
| 01:25 | Solar Flare | `PHS_EXTERNAL_SOLAR_FLARE_VALIDATE_OK ... network_gameplay_components=0 online_binding=blocked_event_id_unassigned` | presentation-only 계약 통과, 온라인 binding 차단 확인 |
| 01:25 | Room Browser | `PHS_ROOM_BROWSER_RECOVERY_VALIDATION_PASS ... panels=4 prefab_refs=20 ... customize_tutorial_vertical_orange=1` | 로비 계층·참조·버튼 세트 정적 검증 |
| 01:28 | Development build | `PHS_0717_VALIDATION_BUILD_OK ... size=389588488` | 게임오버 통합 전 Windows Development build |
| 01:33 | Relay P0 | `PHS_P0_RESULT PASS ... peers=2 zones=9 shopCycles=3 runPhase=Clear` | 게임오버 통합 전 Relay Host 1 + Client 1 온라인 루프 |
| 01:41 | GameOver authoring | `PHS_GAME_OVER_AUTHOR_OK` | 게임오버 presentation과 scene wiring 생성 |
| 01:41 | 최종 통합 validator | `PHS_0715_VALIDATE_OK errors=0 scenes=4 prefabs=11` | 게임오버 통합 후 compile/정적 씬·프리팹 계약 |

최신 Development build와 Relay P0는 게임오버 통합보다 먼저 실행됐다. 따라서 게임오버 통합 후 온라인 전체 동작을 증명하지 않는다.

## 4. 반영된 범위

- Lobby의 `START`, `SETTINGS`, `TRAINING`, `CUSTOMIZE`를 같은 세로 옵션 세트와 주황 계열로 정리.
- Room Browser Create/List/Refresh/Password/Join 패널과 prefab/scene Inspector 참조 복구.
- 렌치, 소화기, 배터리 계열의 공통 동작과 아이템별 수치 차등 연결.
- 자동 수리 키트 즉시 수리, Foam GLOO의 화재 진압·함선 구멍 봉합 흐름 연결.
- 아이템 상호작용 2D/3D cue, mixer routing, priority/voice 제한 기반 연결.
- 4개 맵 우주 배경과 presentation-only Solar Flare 연출 프리팹 추가.
- 서버 권위 게임오버 시퀀스, 결과 UI 지연, late-join snapshot 구조와 재시작 대기 계약 통합.

## 5. 완료로 주장하지 않는 항목

1. 게임오버 통합 후 Host+Client 전체 시퀀스, late join, restart 전용 driver 재실행.
2. 생성·외부 음원의 실제 청감, 장시간 voice/loop 누수, 최종 볼륨·믹스 확인. 현재 fire loop에 연결된 팀원 `Sound_Fire.mp3`는 라이선스·출처가 불명확하므로 출품 전 문서 확보 또는 교체가 필요하다.
3. Solar Flare EventId 배정과 `Assets/04` 담당자 합의. 현재 EventId는 미배정 상태라 온라인 route가 차단된다.
4. Solar Flare Host+Client, late join, warp/map transition 검증 매트릭스.
5. Tutorial 중앙 magenta 프리팹 문제의 해결 여부. 사용자 지시에 따라 별도 작업 범위로 분리됐다.
6. GitHub CI. PR #71에는 자동 status check가 없다.

## 6. Git 제외와 병합 전 검토

현재 PR diff에 없는 항목:

- `Artifacts/`, `Builds/`.
- `SpecialSkillsEffectsPack`.
- MCP 로컬 설치물과 `.codex*`, `.mcp*` 로컬 설정.
- `Packages/packages-lock.json`의 MCP 오염.
- Asset Store 원본 `.unitypackage.meta` 3개의 삭제. 최종 기준선에서 복구됐다.

다음 항목은 현재 PR diff에 있으므로 “제외됨”으로 쓰면 안 된다. 병합 전에 의도와 소유권을 확인한다.

- `Assets/01` 공용 파일 3개: `DefaultNetworkPrefabs.asset`, `PHS_EventRuntimeSystem.prefab`, `PHS_CuteWhiteGhost_Player.prefab`.
- `Assets/04. NohSeokMin_Game Event/03_Prefab/Fire/Effect_Fire.prefab`.
- `Assets/99. DownloadAssets/TextMesh Pro/Fonts/Maplestory Light SDF.asset`.
- `ProjectSettings/ProjectSettings.asset`.
- `ProjectSettings/EditorBuildSettings.asset`.

## 7. 병합 전 최소 Gate

1. 위 공용·자동 변경 파일의 의도와 담당자 승인 확인.
2. 게임오버 통합 후 Development build 재생성.
3. Relay Host 1 + Client 1에서 GameOver sequence 완료 → 결과 UI → Host restart → 새 run을 확인.
4. dedicated customization/restart validation flag 실행 여부를 로그로 남김.
5. live listening, `Sound_Fire.mp3` 라이선스와 Solar Flare online blocker를 PR 제한 사항에 그대로 유지.
