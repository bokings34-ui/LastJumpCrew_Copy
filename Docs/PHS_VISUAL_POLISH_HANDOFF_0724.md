# 2026-07-24 전체 작업 체크포인트 인계

## 체크포인트

- 브랜치: `codex/visual-polish-3d-ui-vfx`
- Unity: `6000.5.2f1`
- 렌더 파이프라인: URP `17.5.0`
- VFX Graph: `17.5.0`
- 체크포인트 커밋: `87f20e92`
- 원격 브랜치: `origin/codex/visual-polish-3d-ui-vfx`
- Git LFS: 780개, 643 MB 업로드 완료
- 목적: 현재 작업 PC의 전체 워크트리를 체크포인트로 보존하고 집 PC에서 이어서 작업한다.
- 주의: 이번 체크포인트에는 비주얼 작업 외 기존 진행 중 변경도 함께 들어간다. 기능별 분리는 집 PC에서 후속 커밋으로 정리한다.

### 커밋 규모

- 전체 2,140개 파일 변경, 212,089줄 추가, 645줄 삭제.
- 신규 2,036개, 수정 104개. 실파일 1,045개와 Unity `.meta` 1,095개.
- ParkHanSol 담당 경로 389개: C# 64, Prefab 32, Scene 3, ScriptableObject 13, Audio 37, Image 25, VFX Graph 4.
- 외부 에셋: Modular 3D Text 801개, Simple Stylized Slash 161개, 오디오 원본 팩 768개.
- LFS 추적 변경 파일 842개 중 원격에 없던 고유 오브젝트 780개를 업로드했다.
- MCP/Codex 로컬 설정과 `com.anklebreaker.unity-mcp` package-lock 오염은 제외했다.

## 오늘 반영

### Modular 3D Text / 한글 UI

- 로비 HUD와 플레이 HUD의 TMP 텍스트에 M3D 미러를 연결했다.
- 한글 지원 M3D 폰트 `PHS_MaplestoryBold_M3D.asset`을 만들었다.
- League Spartan, Liberation Sans, Maplestory Bold, Reggae One M3D 폰트 4종과 전용 Material을 추가했다.
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

### 로비 커스터마이징 로컬 프로필

- `LocalLobbyCustomizationService`와 `LobbyCustomizationProfileKeys`를 추가했다.
- 크레딧, 보유 코스튬, 머리/등 장비, 바디 컬러를 PlayerPrefs 프로필로 저장한다.
- 미리보기와 실제 장착 상태를 분리하고 구매·장착·해제·색상 변경 실패 사유를 로그로 남긴다.
- 로비 커스터마이징 UI, 플레이어 외형 동기화, 검증 드라이버와 프리팹 연결을 갱신했다.

### 게임오버 시퀀스와 런 플로우

- `IGameOverSequencePresentation`, `IGameOverSequenceStatus` 계약을 추가했다.
- 서버 상태 스냅샷, 게임오버 시퀀스 Coordinator, 로컬 Presenter를 분리했다.
- 적 함대·주인공 함선·포격 빔으로 구성된 `PHS_GameOverCinematicPresentation.prefab`과 전용 Material을 추가했다.
- RunFlow, Restart, SessionRoot, 결과 패널과 통합 프리팹 연결을 갱신했다.

### 튜토리얼 룸 진행

- Movement부터 Complete까지 8개 룸 상태를 관리하는 `NetworkTutorialRoomGate`를 추가했다.
- 이전 룸 완료 전 다음 문을 열 수 없고, 실패 시 명시 오류를 표시한다.
- 마지막 완료 룸 진입을 감지하는 `NetworkTutorialCompletionRoomTrigger`를 추가했다.
- 룸 모니터, 문 패널, 버튼, Barrier, Material, 도어/UI 오디오를 프리팹과 튜토리얼 씬에 직접 연결했다.
- 완료 패널은 마지막 룸 진입 후 표시하도록 진행 단계를 분리했다.

### 오디오 피드백

- `PHSCuratedAssetSfxAuthoring`으로 아이템·상점·사건·워프·튜토리얼·이동 SFX 연결 지점을 정리했다.
- `NetworkPlayerMovementAudioFeedback`으로 걷기/달리기/점프 피드백을 추가했다.
- `NetworkRunWarpAudioPresenter`로 워프 시작/종료 오디오를 런 상태와 연결했다.
- 아이템 획득·드롭·교체, 렌치 충격, 소화기 분사, 수리 완료, 상점 성공/실패, 사건 경고, 미션 성공/실패 Cue를 확장했다.
- 외부 원본 오디오 팩은 `Assets/Assets/Free`, `Assets/Casual Game UI Sound`, `Assets/Electric Sfx`에 보존했다.
- 실제 게임용 선별본 37개는 `06. Audio/CuratedAssetSfx`에 배치했다.

### 사건·화재 프레젠테이션

- 산소 누출 전용 `PHS_OxygenLeakEffect.prefab`과 URP Material/Shader/Texture 의존성을 추가했다.
- 기존 산소 누출 EventPresentation 프리팹에 새 연출을 연결했다.
- 화재 패치에 소유 Billboard Shader/Material을 추가하고 Presentation Adapter 연결을 조정했다.
- Hull Breach, Event Runtime, MiniGame Runtime 통합 프리팹 연결을 갱신했다.

### 맵·스카이박스

- Waste Orbit, Asteroid Field, Broken Satellites, Nebula Debris 4종 파노라마와 Skybox Material을 연결했다.
- 각 `PHS_Map_800x` 데이터가 대응 Skybox를 사용하도록 갱신했다.

### 옵션·상호작용·공용 HUD

- 공유 옵션 저장과 로비/일시정지/카테고리 UI 연결을 갱신했다.
- 로비에서 싱글 플레이 직접 진입 흐름과 기본 방 이름 형식을 갱신했다.
- 해상도 목록에 1440p부터 4K까지 후보를 추가하고 옵션 저장 SFX를 연결했다.
- 무중력 바닥 충돌 시 반사 대신 표면 접선 방향으로 미끄러지도록 이동 처리를 변경했다.
- 플레이어 이동, 아이템 스왑, Debris 판매, 맵 표시, 미니게임, 경고 HUD 오디오·피드백 참조를 보강했다.
- Release Builder가 빌드·사전검사 실패 보고서를 생성하도록 보강했다.
- Inspector 참조가 없으면 fallback하지 않고 명시 오류를 남기는 정책을 유지했다.

### 외부 패키지와 프로젝트 설정

- Modular 3D Text 전체 런타임·Editor·Font·예제 에셋을 포함했다.
- Simple Stylized Slash vol2와 공용 Mesh/Shader/Texture를 포함했다.
- `com.unity.visualeffectgraph` `17.5.0`을 manifest와 packages-lock에 등록했다.
- Standalone define에 `MODULAR_3D_TEXT`를 추가했다.
- PackageManager prerelease 허용 상태와 VFXManager 런타임 Shader/Resource 설정을 체크포인트에 포함했다.

### 씬·프리팹 배치 규모

- 프로젝트 씬 3개를 수정했다: `PHS_Map_ver1`, `ParkHanSol_LobbyScene`, `PHS_NetworkTutorialScene`.
- 프로젝트 프리팹 32개를 변경했다: 신규 4개, 수정 28개.
- 신규 프리팹은 Oxygen Leak, GameOver Cinematic, Wrench Electric Feedback, Tutorial Room Gate다.
- M3D 샘플 씬 16개와 Slash 샘플 씬 1개도 체크포인트에 포함됐다.

## 종료 전 검증

- Unity 종료 전 C# 컴파일 오류: 0
- 사건 HUD 두 프리팹: 기존 사건 텍스트 없음
- 사건 아이콘: 7종 Sprite 및 진행 Fill 참조 정상
- 로비 UI 원본 RectTransform/Graphic/TMP 개수 유지
- 모든 신규 Presenter는 Inspector 명시 참조를 요구한다. fallback은 없다.
- Git 원격과 로컬 브랜치 동기화 완료, 작업트리 clean.
- 런타임 Host/Client와 전체 신규 연출은 아직 최종 승인 상태가 아니다.

### 확인이 필요한 위험

- 사건 HUD는 외부 Event Alert 문자열도 표시하지 않는다. 사건 외 알림이 필요한지 확인한다.
- 사건 HUD 7개 아이콘·Sprite·Fill 중 하나라도 Inspector에서 빠지면 HUD 구성이 실패한다.
- 부스터·워프 HUD는 현재 지속 표시만 반영됐고 그래프 시각화는 미구현이다.
- 소화기 복수 화재 처리 시 내구도 소비가 대상 수만큼 발생하는지 확인한다.
- 게임오버 Presenter가 활성 Canvas를 제어하므로 늦은 참가자·재시작·원래 비활성 UI 복원을 확인한다.
- 강제 추가한 고해상도가 실제 모니터 미지원 모드까지 표시하는지 확인한다.
- 로비 씬 파일이 약 145 KB에서 5.2 MB로 증가했다. M3D 생성 메시 중복과 씬 저장 팽창을 점검한다.
- M3D와 Slash 샘플 전체가 포함됐다. 후속 분리 시 저장소 용량·라이선스·실사용 의존성을 같이 확인한다.
- 외부 플러그인 원본에는 Unity `.meta`와 ShaderGraph 공백 경고가 남아 있다. 프로젝트 C# 변경은 `git diff --check`를 통과했다.

## 다음 작업

1. 부스터 게이지와 워프 게이지를 그래프형 HUD로 변경한다.
2. PlayMode Host 시작과 로컬 플레이어 스폰을 다시 확인한다.
3. 상점 진열 아이콘과 아이템 부착 가격 M3D를 실제 카메라에서 확인한다.
4. 렌치 Slash·수리 전기, 소화기 분사, 후크 로봇팔 VFX의 위치·크기·색을 캡처 기준으로 조정한다.
5. 소화기 분사 범위 내 복수 화재가 모두 진압되는지 서버 로그로 확인한다.
6. 사건 발생·수리 시 아이콘 활성화와 게이지 진행을 확인한다.
7. 로비 최종 감사값을 다시 계산해 승인된 위치·크기·색 불변을 확인한다.
8. 현재 전체 체크포인트를 기능별 후속 커밋으로 분리 정리한다.
9. 로컬 커스터마이징 구매·장착·재실행 저장을 확인한다.
10. 게임오버 시퀀스 시작·완료·재시작 입력 차단을 Host에서 확인한다.
11. 튜토리얼 8개 룸 잠금·해제·완료 룸 진입을 처음부터 끝까지 확인한다.
12. 워프·이동·아이템·상점·사건 SFX의 중복 재생과 볼륨을 확인한다.
13. 산소 누출·화재 Billboard·4종 Skybox를 URP Game View에서 확인한다.

## 주요 경로

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/UI/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Events/PHSNetworkEventHudView.cs`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Grapple/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Customization/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/RunFlow/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Tutorial/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/Audio/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Items/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Shop/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/IncidentIcons/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UI/Fonts/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/SourceAssets/VFX/PHS_VisualPolish/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/GameOver/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Events/OxygenLeak/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/06. Audio/CuratedAssetSfx/`

## 집 PC 재개

```powershell
git fetch origin
git switch codex/visual-polish-3d-ui-vfx
git pull --ff-only origin codex/visual-polish-3d-ui-vfx
git lfs pull
```

- Unity가 패키지와 에셋을 모두 import할 때까지 기다린다.
- `git lfs pull` 후 누락 LFS 파일이 없는지 확인한다.
- Console을 비운 뒤 컴파일 오류부터 확인한다.
- 실패 시 기능 보강보다 Prefab/Inspector 참조와 VFX Graph import 상태를 먼저 조사한다.
- `com.anklebreaker.unity-mcp`와 Codex 로컬 설정은 저장소에 넣지 않는다.
