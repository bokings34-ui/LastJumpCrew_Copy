# 2026-07-24 비주얼 폴리시 작업 인계

## 체크포인트

- 브랜치: `codex/visual-polish-3d-ui-vfx`
- Unity: `6000.5.2f1`
- 렌더 파이프라인: URP `17.5.0`
- VFX Graph: `17.5.0`
- 목적: 현재 작업 PC의 전체 워크트리를 체크포인트로 보존하고 집 PC에서 이어서 작업한다.
- 주의: 이번 체크포인트에는 비주얼 작업 외 기존 진행 중 변경도 함께 들어간다. 기능별 분리는 집 PC에서 후속 커밋으로 정리한다.

## 오늘 반영

### Modular 3D Text / 한글 UI

- 로비 HUD와 플레이 HUD의 TMP 텍스트에 M3D 미러를 연결했다.
- 한글 지원 M3D 폰트 `PHS_MaplestoryBold_M3D.asset`을 만들었다.
- 로비의 승인된 위치·크기·색·타이포 값은 변경하지 않았다.
- 기준 감사값:
  - RectTransform 368개
  - Graphic 320개
  - TMP 153개
  - M3D 미러 153개
  - 누락 참조 0개

### 상점

- 진열 UI의 아이템 이름·설명·가격 TMP를 제거했다.
- `UtilityItemPrefabData.Icon`을 진열 SpriteRenderer에 표시한다.
- 13개 판매 아이템 Held 프리팹에 `PHS_ItemPrice_M3D`를 연결했다.
- 가격은 World Canvas가 아니라 아이템 자식인 실제 월드 공간 M3D 메시다. 아이템을 따라간다.
- 강화 아이템 5종은 실제 Held 프리팹을 렌더한 아이콘으로 교체했다.

### 렌치

- 흔들기 대신 옆으로 휘두르는 yaw 기반 사용 모션으로 변경했다.
- `Simple Stylized Slash Pack/Slash_B`를 렌치 자식으로 연결해 휘두름을 따라간다.
- 수리 대상 피드백은 별도 전기 이펙트 프리팹으로 분리했다.
- 일반 렌치와 미래형 렌치 Held 프리팹에 연결했다.

### 소화기

- 소화기 흔들기 애니메이션을 제거하고 손 자세를 고정했다.
- VFX Graph `17.Sample Mesh` 방향을 참고한 분사 그래프를 추가했다.
- 분사 범위 안의 유효 화재 후보를 모두 서버에서 처리하도록 수정했다.
- 두 소화기 Held 프리팹에 명시 참조를 연결했다.

### 후크

- 기존 파란 LineRenderer를 비활성화했다.
- VFX Graph `29.Multi-Strip Single Burst`를 참고한 로봇팔형 줄을 연결했다.
- 시작점·끝점 정렬과 길이 스케일은 `PHSRobotArmRopeVfxPresenter`가 처리한다.

### 사건 HUD

- 사건 텍스트 출력을 제거했다.
- Fire, Power, Device, Hull, Steam, Oxygen, Gravity 7종 아이콘을 이미지 생성 후 투명 Sprite로 정리했다.
- 두 플레이 HUD 프리팹에 아이콘 7개와 사건 수리 진행 게이지를 직접 배치했다.
- `PHS Event Alert` 아래 TMP 텍스트 수 0, 아이콘 매핑 7/7, 유효 참조 7/7을 확인했다.

## 현재 검증

- Unity C# 컴파일 오류: 0
- 사건 HUD 두 프리팹: 기존 사건 텍스트 없음
- 사건 아이콘: 7종 Sprite 및 진행 Fill 참조 정상
- 로비 UI 원본 RectTransform/Graphic/TMP 개수 유지
- 모든 신규 Presenter는 Inspector 명시 참조를 요구한다. fallback은 없다.

## 다음 작업

1. 부스터 게이지와 워프 게이지를 그래프형 HUD로 변경한다.
2. PlayMode Host 시작과 로컬 플레이어 스폰을 다시 확인한다.
3. 상점 진열 아이콘과 아이템 부착 가격 M3D를 실제 카메라에서 확인한다.
4. 렌치 Slash·수리 전기, 소화기 분사, 후크 로봇팔 VFX의 위치·크기·색을 캡처 기준으로 조정한다.
5. 소화기 분사 범위 내 복수 화재가 모두 진압되는지 서버 로그로 확인한다.
6. 사건 발생·수리 시 아이콘 활성화와 게이지 진행을 확인한다.
7. 로비 최종 감사값을 다시 계산해 승인된 위치·크기·색 불변을 확인한다.
8. 현재 전체 체크포인트를 기능별 후속 커밋으로 분리 정리한다.

## 주요 경로

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/UI/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Events/PHSNetworkEventHudView.cs`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Grapple/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Items/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Shop/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/IncidentIcons/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Fonts/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/SourceAssets/VFX/PHS_VisualPolish/`

## 집 PC 재개

```powershell
git fetch origin
git switch codex/visual-polish-3d-ui-vfx
git pull --ff-only origin codex/visual-polish-3d-ui-vfx
```

- Unity가 패키지와 에셋을 모두 import할 때까지 기다린다.
- Console을 비운 뒤 컴파일 오류부터 확인한다.
- 실패 시 기능 보강보다 Prefab/Inspector 참조와 VFX Graph import 상태를 먼저 조사한다.
- `com.anklebreaker.unity-mcp`와 Codex 로컬 설정은 저장소에 넣지 않는다.
