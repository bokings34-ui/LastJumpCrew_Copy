# LastJumpCrew 제출 인계 - 2026-08-06

- 저장소: `hyunje0609-sudo/LastJumpCrew`
- 기준 브랜치: `codex/team-new-work-analysis-20260805`
- 기능 커밋: `8603ab9c feat(release): finalize block map and event validation`
- Pull Request: [#110](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/110) (Draft)
- Unity: `6000.5.2f1`
- 상세 플레이·릴리스 문서: `Docs/PHS_PLAYTEST_FLOW_AND_RELEASE_GUIDE_2026-08-06.md`
- 테스터 상세 설명서: Windows 빌드 폴더의 `TESTER_GUIDE_KR.md`

## 완료 내용

- 4회 워프 완료 후 최종 승리 상태를 `Clear`로 전환한다.
- TAB 함선 지도는 실제 함선 실루엣을 기준으로 단순 블록 형태를 사용한다.
- 지도에서 자판기와 고정 장비 표시는 제거하고 이벤트 위치와 종류를 우선 표시한다.
- 함선 체력은 `현재/최대`, 워프는 `%` 형식으로 표시하며 상단 잘림을 제거했다.
- MicDestroy 발생 중 멀티 음성 입력을 억제하고 이벤트 종료 시 원래 음소거 설정을 보존하며 복구한다.
- 통신장비 파괴 시 구형 랜덤 EventScheduler를 생성하던 우회 경로를 제거했다.
- 기본 렌치에 누락된 `DeviceRepair` 프로필을 복구했다.

## 최종 검증

### 정적/빌드 검증

- `PHS_INTEGRATED_RELEASE_VALIDATION_PASS scenes=4 items=3 missingPrefabs=0 missingScripts=0`
- Windows Release 빌드 성공:
  - `Builds/BEAVER_2026/LastJumpCrew_BEAVER_2026.exe`
  - 로그: `Logs/Build_Release_TabMap_Events_20260806.log`
- MCP/Codex 로컬 파일 및 `Packages/packages-lock.json` stage 없음.

### Host + Client 2프로세스 검증

- 최종 결과: `PHS_P0_RESULT PASS`
- 이벤트: 총 8종 동기화 확인.
- 물리 사고: EngineBreak, SteamLeak, OxygenGeneratorFailure, GravityGeneratorFailure의 실제 spawn, peer 동기화, 렌치 수리, 제거 확인.
- MicDestroy: 양쪽 음성 억제 확인, 약 14.75초 후 자연 종료와 복구 확인.
- 데브리 구역: 함선 외부 진입과 함선 내부 귀환 모두 확인.
- 진행: 4구역 완료, 상점 2회, 최종 `runPhase=Clear` 확인.
- 런타임 로그 건강 검사 통과: NullReference, MissingReference, 중복 spawn 등 지정 오류 패턴 없음.
- 로그: `Builds/PHS0717Validation/p0-host.log`, `p0-client.log`.

## 제출 파일

- 파일: `Builds/Submission_20260806/LastJumpCrew_Submission_BlockMap_Events8_4Warp_20260806.zip`
- 크기: `457,312,695 bytes`
- ZIP 항목: `240`
- 실행 파일 포함: 확인.
- `BurstDebugInformation_DoNotShip`: 제외 확인.
- SHA-256: `903C182CBE94B3F98F66542D25DC94A3551B739A586C1553195FBE01C2067180`

## PR 범위 주의

- PR #110은 이번 지도/이벤트 커밋과 직전 4회 워프 제출 안정화 커밋을 함께 포함한다.
- 직전 커밋에는 MapVer3/Creepy_Cat 런타임 참조 복구를 위한 vendor `.meta` GUID 변경 281개가 포함된다.
- 빌드와 ZIP은 Git에 포함하지 않았다.
