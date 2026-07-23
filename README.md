# ⚓ LastJumpCrew 프로젝트 협업 가이드

이 문서는 프로젝트의 원활한 협업과 파일 충돌 방지를 위한 운영 가이드입니다.

## 최신 BEAVER 2026 Closeout — 2026-07-24

- 현재 브랜치: `codex/beaver-2026-daily-pr`.
- Draft Pull Request: [#71](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/71).
- 문서 업데이트 전 코드·자산 기준선: `d89a16f4`.
- 최신 통합 gate: `PHS_0715_VALIDATE_OK errors=0 scenes=4 prefabs=11`.
- Build Settings: Lobby → Tutorial → Map → Shop, 4개 씬.
- 게임오버 통합 전 Development build와 Relay Host 1 + Client 1 P0는 통과했다. 게임오버 통합 후에는 authoring과 통합 validator만 통과했으며, Host+Client 전체 시퀀스와 restart driver는 다시 실행하지 않았다.
- 사운드 정적 wiring은 foundation 23 clips/item 11 waves로 PASS했다. 실제 청감과 최종 믹스, 현재 fire loop에 연결된 `Sound_Fire.mp3`의 라이선스 확인은 남았다.
- Solar Flare presentation-only prefab은 정적 validator를 통과했지만 EventId가 미배정되어 online binding은 차단 상태다.
- 상세 source-of-truth: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/Validation/PHS_BEAVER_2026_CLOSEOUT_2026-07-24.md`.

아래 PR #40/#52와 0715 검증 수치는 날짜별 작업 이력이며 현재 PR #71의 상태가 아니다.

---

## 📂 1. 폴더 구조 및 작업 영역
* **개인 작업:** `02~06. [이름]_작업파트` 폴더에서만 작업 (**타인 폴더 수정 금지**)
    * 다른 팀원의 자료 수정이 필요할 경우 팀원에게 알리고 내용 수정을 주석으로 남기기
* **공용 자산:** `02. Final_Prefab` 내부 본인 파일들 (최종 합본용, 수정 전 팀 내 공유 필수)
* **외부 에셋:** `99. DownloadAssets` 폴더에 저장

---

## 🔄 2. 깃허브 협업 루틴 (역할 분담)

### ① 작업자
1. **작업 시작 전:** 최신 상태 동기화 진행
    * GitHub Desktop 상단 **[Branch]** 메뉴 클릭 → **[Update from main]** 클릭
    * <img src="https://github.com/user-attachments/assets/794078c0-fde1-4510-a874-832901170331" width="500" />
2. **작업 중:** **본인 브랜치**에서만 작업 (**main 브랜치 작업 절대 금지**)
3. **작업 완료 시:** `[Commit]` 및 `[Push origin]` 후, 팀장이나 관련 확인자에게 자신의 브랜치 확인 요청
4. 확인이 완료되면 깃허브 웹사이트에서 **[Compare & pull request]** → **[Create pull request]** 클릭하여 **"탁현재"**에게 메인 병합 요청

### ② 확인자
1. **검수:** 팀원이 작업한 브랜치에 들어가서 기능 이상 유무 및 '스크립트/파일' 위치 확인
    * 작업 공간이 침해되지 않았는지 확인자가 반드시 체크
2. **피드백:** 수정이 필요하면 소통 채널을 통해 작업자에게 확인 요청
3. **병합:** 이상 없으면 **"탁현재"**에게 요청하여 **[Merge pull request]** 버튼 클릭 (main에 최종 반영)

---

## ⚠️ 주의사항
* **충돌(Conflict) 발생 시:** 혼자 해결하려 하지 말고 즉시 팀장에게 호출하세요.
* **최종 관리:** `main` 병합 권한은 충분한 확인을 거친 후 진행하겠습니다.

---

## 🎮 ParkHanSol 무중력 테스트 조작법

대상 씬: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/ParkHanSol_GravitySpaceTestScene_0715.unity`

| 키 | 동작 |
| --- | --- |
| `1` | 즉시반응형 무중력 이동. WASD 입력을 놓으면 바로 정지 |
| `2` | 관성형 무중력 이동. WASD 입력 후에도 관성이 유지 |
| `3` | 절충형 무중력 이동. 부드럽게 가속/감속, `Shift` 부스터 |
| `4` | 함선 중력 상태 복귀 |
| `5` | 추진제 게이지형. `Space`만 사용하며 카메라 정면으로 추진 |
| `6` | 추진제 전용형. WASD로 카메라 기준 방향 추진 |

- `Ctrl`: 하강 (1~3번 무중력 이동 모드)
- 추진제는 사용 시 게이지를 소모하며, 사용 후 자동 회복합니다.
- 중력 상태에서도 추진제는 자동 회복합니다.
- `Stamina Text`에서 `추진제 현재/최대` 잔량을 확인합니다.
- `PHS_DebrisSellStation`에 데브리 태그 아이템을 넣으면 아이템 가치가 파티 보유 금액에 더해지고 데브리는 제거됩니다.

---

## 🧭 ParkHanSol 0715 통합 작업 인수인계 — 2026-07-16

- 작업 브랜치: `agent/phs-debris-respawn-service-0715`
- Pull Request: [#40](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/40)
- 기준 Unity: `6000.5.2f1`
- 최종 확인 씬: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_ExteriorShopScene.unity`

### 이번 최신화 내용

- `PHS_DebrisCollectionPortal_0715`의 상호작용 수신 영역을 루트로 정리하고 `F` 입력 시 `PHS_DebrisCollectionScene`으로 직접 이동하도록 연결했습니다.
- 워프 충전 완료 후 콘솔이 활성화되도록 상태 연결을 수정하고, 미니게임 단말은 상시 활성화가 아니라 대응 사건이 진행 중일 때만 활성화되도록 변경했습니다.
- `EventScheduler` 서버 자동 시작과 사건 목록을 연결해 Fire, EnemySpawn, OxygenLeak, MeteorAttack, EmpAttack, EnemyScout가 실제로 발생할 수 있게 했습니다.
- 이벤트 알람은 워프 게이지 아래에 배치하고 `화재`, `산소 누출`처럼 사건명만 한 줄로 표시합니다.
- `Tab` 함선 지도는 실제 Room 배치인 `중앙 복도 ← Room A/B/C 세로 구조`로 정리했으며 사고 발생 방에 빨간 마커를 표시합니다.
- 데브리 판매소 중복 ScenePlaced NetworkObject 문제를 제거하고 Host 기준 포탈 이동과 사건 발생을 재검증했습니다.

### 검증 결과

- Unity 컴파일 Error 0.
- `PHS_0715_VALIDATE_OK errors=0 scenes=5 prefabs=7`.
- Host에서 `Fire` 사건 생성 후 알람 `화재`와 `Room B` 사고 마커 표시 확인.
- Host에서 데브리 포탈을 통해 `PHS_DebrisCollectionScene` 진입 확인.
- 최종 상태: EditMode, 씬 Dirty 없음.

### 다음 확인자 체크

- 실제 Host+Client 2인 화면에서 알람과 지도 마커가 양쪽에 동일하게 보이는지 수동 확인합니다.
- Relay/Lobby 실제 서비스 접속은 Unity Services 프로젝트 연결 상태와 분리해서 확인합니다.
- MCP 로컬 설치물과 `ProjectSettings/Packages/com.unity.probuilder/Settings.json`은 커밋 범위에서 제외합니다.

---

## 🔥 PR #52 사건 장소·Fire Runtime 중간 작업 — 2026-07-19~20

- 작업 브랜치: `codex/run-incident-ledger-0718`
- Draft Pull Request: [#52](https://github.com/hyunje0609-sudo/LastJumpCrew/pull/52)
- 0715 Scene Foundation: Zone `4`, Location `15`, Fire Patch `22`, Request Route `10`
- Fire 상태 권한은 박한솔 소유 서버 Coordinator가 담당하고, Patch별 `NetworkObject`를 만들지 않는 Snapshot 구조로 진행 중입니다.
- Scene Patch는 `Interactable` Layer, `PHSFirePatchRuntimeTarget`, Light, Visual Socket을 연결하는 계약입니다.
- 연결 확인용 임시 출력 `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/IncidentFire/PHS_FirePatchPresentation.prefab`을 생성했습니다. Root 아래 `FlameCore`, `FlameOuter`, `Smoke`, `Embers` Particle 4종이며 0719 Authoring 검증을 통과했습니다.
- 노석민 최종 납품은 Presentation-only VFX/Audio/Telegraph/Cleanup/Reset Bundle입니다. Prefab에 `NetworkObject`, `NetworkBehaviour`, 점화·확산·피해·소화 상태 로직을 넣지 않습니다.
- 박한솔이 노석민 최종 Presentation Prefab을 `PHSFireZone.patchPresentationPrefab` 참조로 교체합니다. `PHSFirePatchRuntimeTarget`이 활성화 시 Scene Patch Visual Socket 아래에 지연 생성합니다.

### 현재 검증 상태

| 검증 | 상태 |
| --- | --- |
| Unity Compile Error 0 | 확인 |
| 0719 `ValidateAuthoredScene` | 통과: `ok=True reason=none` |
| 0719 Migration | 통과: `zones=4 locations=15 fireZones=4 firePatches=22 routes=10` |
| 전체 `PHS0715IntegrationValidator` | 통과: `errors=0 scenes=3 prefabs=11` |
| Direct local Host 점화·확산·피해·소화·Containment | 통과 |
| 원격 Client Snapshot 동기화 | 미검증 |
| Late Join 현재 화재 복원 | 미검증 |

Direct local Host 0715 smoke에서 점화 `instance=2`, `fire_surface_room_a`, Patch `103`, Heat `70/Medium`, Target/Light 활성화를 확인했습니다. 자연 확산은 Patch `4`, Heat `176/122/68/39`, 활성 Target/Light `4`, 재생 Particle `28`이었고, 범위 피해는 Host Health `100 -> 0`이었습니다. 소화/Containment는 Hit `24`, Patch `0`, failure 없음, 최종 Fire `0`, Accident `2=false`였습니다.

이 결과는 Fire flow 통과 증거입니다. 같은 run에서 Fire 외 `ParkHanSolGameSettingsController MissingReference` 1건과 EMP terminal impact `power_already_off` 2건을 관찰했으므로 전체 Host clean으로 보지 않습니다.
