# UI/UX Audit V1 마감 및 V2 재개 문서

- 작성일: 2026-07-31 KST
- 브랜치: `agent/team-unmerged-integration-20260730`
- Unity: `6000.5.2f1`
- 상태: V1 보존, V2 원본 복제 완료, V2 시각 샘플 제작 전 중단

## 사용자 결정

- 참고 감성: `R.E.P.O.`, `Lethal Company`
- 원본 UI/씬은 유지한다.
- 복제본에서 먼저 수정하고 사용자 통과 후 실제 원본/씬에 적용한다.
- 로비, 튜토리얼, 메인 플레이, 상점을 함께 점검한다.
- 흰색/어두운 배경에서 실제 캡처로 가독성과 일관성을 검증한다.
- 네트워크 없이 플레이어 이동과 상호작용 반응을 확인할 수 있어야 한다.
- V1 시안은 사용자 평가 `구림`으로 탈락했다. V1 위에 덧칠하지 않는다.

## V1 산출물

- 복제 프리팹: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Prototypes/UIUX_Audit/`
- 복제 씬: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/Prototypes/UIUX_Audit/Scenes/`
- 오프라인 입력 보조: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/UI/Prototypes/UIUX_Audit/PHSAuditOfflinePlayerInputActivator.cs`
- 감사 전용 LED/TMP 및 M3D 재질: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Prototypes/UIUX_Audit/Materials/`
- LED UI VFX 원본: `Assets/TurishaderPackages/LedUI/`
- Modular 3D Text 원본: `Assets/Tiny Giant Studio/Modular 3D Text/`

주의: V1 복제 씬은 V1 프리팹 GUID가 아니라 정식 UI 프리팹 인스턴스와 씬 override를 사용한다. 다음 작업에서 V1 프리팹과 V1 씬을 같은 소스로 착각하지 않는다.

## V1 실패 원인

1. 큰 카드와 사각 테두리가 많아 공포 협동 게임 HUD보다 관리자 대시보드처럼 보였다.
2. 청록/주황을 전 화면에 사용해 정보 우선순위와 경고 의미가 흐려졌다.
3. LED 효과를 작은 본문과 장식에 사용해 글자가 깨지고 노이즈처럼 보였다.
4. Lobby는 큰 빈 패널, HUD는 분산된 색/박스, Shop은 평면 카드, Marker는 화면과 분리된 표식이 됐다.
5. M3D 정적 월드 사인과 2D HUD의 재질 언어가 연결되지 않았다.

## V2 확정 방향

- 팔레트: Near Black `#090A0B`, Dirty White `#E4E0D5`, Amber `#E69A2D`, Red `#C94838`.
- 큰 전체 패널을 제거하고 필요한 텍스트 뒤의 작은 backing, 2px rule, 짧은 corner만 쓴다.
- Lobby: 좌측 세로 작업 메뉴 rail. 선택 행만 Amber 표시.
- Main HUD: 모서리 수치와 작은 슬롯. 중앙 시야를 비운다.
- Shop: 화면 카드가 아니라 실물 상품, 짧은 가격표, 체크아웃 반응을 중심으로 한다.
- Marker: 스캔/주시/튜토리얼 조건에서만 작은 번호, 방향, 거리 표시.
- LED: 타이머, 숫자, 게이지, 선택/경고에만 사용. 한 화면 동시 5개 이하.
- M3D: 동적 정보에 쓰지 않는다. Shop의 정적 `SHOP`, `CHECKOUT` 월드 사인에만 쓴다.

## V2 현재 상태

V2 전용 원본 복제본은 생성됐다. 아직 스타일 변경과 캡처는 없다.

- 루트: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/Prototypes/UIUX_Audit_V2/`
- Lobby source: `SourceCopies/PHS_AuditV2_LobbyUI_SOURCE.prefab`
- Main HUD source: `SourceCopies/PHS_AuditV2_MainHudUI_SOURCE.prefab`
- Tutorial HUD source: `SourceCopies/PHS_AuditV2_TutorialHudUI_SOURCE.prefab`
- Shop HUD source: `SourceCopies/PHS_AuditV2_ShopHudUI_SOURCE.prefab`
- Checkout/M3D source: `SourceCopies/PHS_AuditV2_CheckoutCounter_SOURCE.prefab`
- `Samples/`는 비어 있다.

정확한 원본:

- Lobby: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkStartLobbyUI.prefab`
- Main/Shop HUD base: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab`
- Network HUD variant: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/PHS_NetworkPlayHudUI.prefab`
- Tutorial wrench marker는 원본 프리팹이 없다. `PHS_NetworkTutorialScene.unity`의 `PHS_NetworkTutorialRoomSequence/PHS_TutorialPracticeItems/ObjectiveMarkerGroup_1/ObjectiveMarker_1_렌치` 서브트리다.

## 재개 순서

1. V1과 `SourceCopies`는 수정하지 않는다.
2. `UIUX_Audit_V2/{Lobby,MainHUD,Tutorial,Shop,Materials}`에 작업 프리팹을 만든다.
3. 튜토리얼 마커는 원본 씬 서브트리를 V2 프리팹으로 추출한다.
4. clean TMP만 사용해 Shop → Main HUD → Lobby → Marker 순서로 4개 시안을 만든다.
5. 1920×1080 dark/white 캡처를 사용자에게 제시한다.
6. 승인 후 LED 전/후 비교를 만든다.
7. 승인 후 Shop의 정적 M3D 사인을 배치한다.
8. 마지막 승인 전에는 정식 프리팹과 정식 씬을 수정하지 않는다.

단일 캡처 씬 권장 구조:

- `CaptureCamera`
- `EventSystem`
- `LobbyCanvas`
- `MainHUDTarget`
- `TutorialMarkerTarget`
- `ShopHUDTarget`

Main/Shop HUD와 Tutorial Marker는 자체 Canvas가 있다. Lobby만 별도 Canvas 아래에 둔다. 캡처할 target 하나만 활성화한다.

## 승인 게이트

- Gate A: clean TMP 기반 V2 4종 dark/white 승인
- Gate B: LED 적용 전/후 승인
- Gate C: M3D 크기와 월드 위치 승인
- Gate D: 정식 프리팹/씬 반영 범위 승인

## 검증 기준

- Unity 컴파일 Error 0
- Missing Script/GUID 0
- 1920×1080 safe margin 48px, clipping 0
- 흰색/어두운 배경에서 필수 텍스트와 아이콘 소실 0
- 한글 glyph 누락, LED 획 파손, bloom 이웃 침범 0
- selected/disabled/alert/unavailable 상태 구분
- M3D z-fighting, 비균일 scale 왜곡, 동적 정보 중복 0
- 오프라인 플레이어 이동, 튜토리얼/상점 상호작용 반응 확인

마감 시 확인 결과:

- Unity compilation error: 0
- Unity Console error: 0
- `UIUX_Audit` + `UIUX_Audit_V2` 프리팹: 17개, Missing Script 0
- V2 시각 샘플과 dark/white 캡처: 미작성

## 금지

- V1 위에 V2를 덧칠하지 않는다.
- 작은 본문과 한글 전체에 LED shader를 적용하지 않는다.
- M3D로 가격, 타이머, 상태 같은 동적 텍스트를 만들지 않는다.
- 사용자 승인 전 정식 UI 프리팹과 정식 씬에 적용하지 않는다.
