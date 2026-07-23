# PHS 외부 우주 이벤트 시각 연출 설계

- 작성일: 2026-07-24
- 최초 상태: 구현 전 설계 감사
- 현재 상태: Solar Flare presentation-only vertical slice 구현 완료. 전용 interface/binder/view, prefab, telegraph/active/resolve/fail audio를 연결했고 authoring과 prefab validator를 통과했다.
- 온라인 상태: `eventId=0` 미배정. validator 결과는 `online_binding=blocked_event_id_unassigned`이며 `Assets/04` 담당자 합의 전 실제 online route는 완료로 보지 않는다.
- 미검증: Host+Client phase 동기화, late join, resolve/fail, warp/map transition 정리, 실제 청감·시각 QA.
- 범위: 외부 우주 이벤트 연출, 온라인 권위, VFX/SFX, 정리 계약
- 최초 문서 작성 작업은 코드, 씬, 프리팹, `Assets/04. NohSeokMin_Game Event/`를 수정하지 않았다. 이후 PHS 소유 presentation slice를 구현했으며 `Assets/04` EventId/factory는 수정하지 않았다.

## 1. 목표

기존 외부 이벤트와 겹치지 않는 신규 이벤트를 아래 공통 흐름으로 구현한다.

`server telegraph -> server active payload -> server resolve/fail -> client terminal burst -> deterministic cleanup`

필수 조건:

- 결과, 피해, 보상, 스폰, 상태 전이는 서버만 결정한다.
- 클라이언트 VFX/SFX는 복제 상태를 보여 주는 presentation-only 계층이다.
- late join 클라이언트도 같은 이벤트 단계와 남은 시간을 재구성한다.
- 맵 전환, 워프, 상점, Clear, GameOver, 연결 종료 시 잔여 VFX, loop audio, modifier가 없어야 한다.
- 이벤트 연출이 `WarpTransitionPresenter`의 global skybox 제어와 충돌하지 않아야 한다.

## 2. 현재 구조 기준

### 2.1 기존 외부 이벤트

`EventEnum.cs` 기준 외부 이벤트는 세 종류다.

| EventId | 현재 역할 | 신규 기획에서 피할 중복 |
|---|---|---|
| `EnemyScout` 7201 | 적 정찰, 적 침입 계열 | 해적, 정찰선, boarding, 적 추격 |
| `MeteorAttack` 7202 | 운석 공격 | 유성우, 소행성 충돌, debris barrage |
| `EmpAttack` 7203 | EMP 공격 | 전자 장비 마비, ion blackout, 전기 노이즈 공격 |

환경 이벤트도 아래 소재는 신규 외부 이벤트의 주 소재로 재사용하지 않는다.

| EventId | 중복 금지 소재 |
|---|---|
| `PatrolZone` 7301 | 순찰 구역 자체 |
| `MeteorZone` 7302 | 운석 지대 자체 |
| `NebulaZone` 7303 | 성운, 안개, 단순 시야 저하 |
| `PlanetZone` 7304 | 행성 통과 자체 |

내부 사고인 Fire, PowerOff, OxygenLeak, EngineBreak, MicDestroy와 함선 사고 Fire, Power, Oxygen, Device, Hull, Steam, Gravity는 신규 외부 이벤트의 시작 소재로 쓰지 않는다. 단, 외부 이벤트 실패 뒤 서버가 선택하는 내부 결과로는 재사용할 수 있다.

### 2.2 확인된 연결 지점

- `PHSNetworkIncidentDirector`가 persistent 서버 스케줄러다.
- `NetworkRunIncidentLedger`가 active incident, pressure, warp multiplier를 복제한다.
- `PHSMapIncidentCommandConsumer`가 incident command를 EventManager 또는 함선 사고 runtime에 연결한다.
- `NetworkEventCoordinator`의 `NetworkEventLifecycleSnapshot`은 `InstanceId`, `EventId`, `State`, `Revision`, `ChangedAtServerTime`을 복제한다.
- `PHSMapRuntimeContext`가 현재 맵 환경 인스턴스, incident 종료, debris stream 상태를 관리한다.
- `WarpTransitionPresenter`가 `RenderSettings.skybox`를 변경한다.
- `NetworkAudioCue`는 현재 범용 warning은 있으나 외부 이벤트별 telegraph, active loop, impact, resolve cue가 없다.
- 현재 `ExternalEvent.OnTrigger()`는 바로 `InProgress`로 전환한다. 실제 telegraph 시간을 확보하려면 서버가 `Trigger` 상태를 일정 시간 유지하는 계약이 필요하다.

## 3. 공통 상태 계약

### 3.1 단계

| 단계 | 서버 책임 | 클라이언트 책임 | 기본 시간 |
|---|---|---|---:|
| `Ready` | command 수락, seed와 강도 확정 | 아무 연출 없음 | 0~0.2초 |
| `Trigger` | 시작/종료 서버 시간 확정, 아직 피해 없음 | 방향, 위험 영역, ETA, 경보 표시 | 4~8초 |
| `InProgress` | 피해, 보상, force, spawn 등 gameplay 실행 | active VFX/SFX와 HUD 표시 | 이벤트별 12~35초 |
| `Resolve` | 성공을 한 번만 commit | 성공 burst 재생 | 1.5~3초 |
| `Fail` | 실패와 consequence를 한 번만 commit | 실패 burst 재생 | 1.5~3초 |
| 제거 | snapshot/command 제거 | view, audio, modifier 전부 정리 | terminal burst 뒤 즉시 |

`Resolve`와 `Fail`은 상태를 보여 주는 terminal 단계다. 보상과 피해 commit은 상태 진입 순간 서버에서 한 번만 수행한다.

### 3.2 신규 외부 presentation snapshot 제안

기존 lifecycle snapshot만으로는 방향, 강도, seed, active 종료 시간을 late join에서 복원할 수 없다. 아래 별도 구조를 권장한다.

```text
NetworkExternalEventPresentationSnapshot
- ulong InstanceId
- int EventIdValue
- byte PhaseValue
- uint Revision
- uint VisualSeed
- double PhaseStartedAtServerTime
- double PhaseEndsAtServerTime
- Vector3 Direction
- float Intensity01
- byte Variant
```

규칙:

- 서버가 생성하고 갱신한다.
- 클라이언트가 임의 seed, 방향, 종료 시간을 만들지 않는다.
- `remaining = max(0, PhaseEndsAtServerTime - ServerTime.Time)`로 잔여 시간을 계산한다.
- active 중 접속한 클라이언트는 telegraph를 다시 재생하지 않고 active normalized time으로 바로 진입한다.
- terminal 중 접속한 클라이언트는 남은 terminal burst만 재생한다.
- 이미 종료 시간이 지난 snapshot은 view를 만들지 않는다.

## 4. 추천 이벤트 요약

| 우선순위 | 이벤트 | 기존 이벤트와 차이 | 주요 gameplay seam | 구현 난도 |
|---|---|---|---|---|
| P0 | Solar Flare / CME | EMP가 아닌 열파. 사전 전원 차단 대응 | `NetworkShipSystemsState`, ship/module damage, warp multiplier | 낮음 |
| P1 | Derelict Cargo Beacon | 공격이 아닌 회수 기회 | network item spawn, party wallet, deposit completion | 낮음~중간 |
| P1~P2 | Gravity Shear | 내부 중력 고장이 아닌 외부 방향성 조석력 | player/item force gateway, debris flow modifier | 중간 |
| P2 | Pulsar Radiation Sweep | 공간을 가르는 주기적 방사선 빔 | shield volume, `NetworkPlayerLifeState.ApplyDamage` | 중간 |
| P3 선택 | Dark-Matter Eclipse | 성운 안개가 아닌 센서 정보 엄폐 | HUD marker visibility scope, terminal response | 높음/중복 위험 |

구현 순서는 Solar Flare -> Cargo Beacon -> Gravity Shear -> Pulsar Sweep -> Dark-Matter Eclipse다.

## 5. 이벤트별 구현 계약

### 5.1 P0 Solar Flare / Coronal Mass Ejection

정체성:

- 외부 항성에서 접근하는 열파다.
- EMP처럼 전자 장비를 즉시 마비시키지 않는다.
- 플레이어 대응은 위험 구간 전에 함선 전원을 의도적으로 내리는 것이다.

`Trigger` telegraph:

- 함선 한쪽에만 주황/백색 corona ribbon과 밝아지는 solar origin을 표시한다.
- HUD에 `SOLAR WAVE ETA`와 방향 화살표를 표시한다.
- 실내 경고등은 주황 pulse로 바뀐다.
- SFX: 짧은 경보 one-shot, 점점 커지는 저역 rumble, 미세한 전기 crackle.
- 이 단계에서는 피해를 주지 않는다.

`InProgress` active payload:

- 서버가 함선 전원 상태를 검사한다.
- 전원이 켜진 동안만 고정 tick으로 module heat 또는 ship damage를 적용한다.
- 전원이 꺼진 연속 유지 시간이 성공 기준을 넘으면 Resolve한다.
- active 동안 incident warp multiplier는 `0`을 사용한다.
- active VFX는 방향성 solar wall, hull heat shimmer, interior exposure pulse다.
- active loop는 snapshot state로 시작하고 끝낸다. 프레임이나 RPC 수신 횟수에 따라 중첩 재생하지 않는다.

`Resolve`:

- 서버가 안전 전원 차단 유지 성공을 한 번만 commit한다.
- 녹색/청백색 cool-down burst와 짧은 success sting을 재생한다.
- 이벤트가 함선 전원을 자동으로 다시 켜지 않는다. 복구는 플레이어 조작으로 남긴다.

`Fail`:

- 서버가 누적 열손상 결과를 한 번만 commit한다.
- 필요 시 기존 Fire 또는 Device 계열 consequence를 서버가 선택한다.
- 강한 hull flash, spark burst, fail sting을 재생한다.
- 외부 이벤트 view가 내부 사고 prefab을 직접 생성하지 않는다.

Cleanup:

- corona, exposure override, light pulse, HUD ETA, rumble loop를 제거한다.
- event view가 적용한 volume weight와 light intensity만 원래 값으로 되돌린다.
- 다른 시스템이 바꾼 전원, skybox, 맵 ambient 값은 건드리지 않는다.

### 5.2 P1 Derelict Cargo Beacon

정체성:

- 적이나 운석이 아닌 표류 화물 회수 기회다.
- 실패가 반드시 함선 피해로 이어지는 공격형 이벤트가 아니다.

`Trigger` telegraph:

- 먼 거리 cyan beacon, 점멸 cone, 접근 궤적 line을 표시한다.
- SFX: 일정 간격 radio ping. 같은 `InstanceId + Revision`에서 첫 ping sequence는 한 번만 시작한다.

`InProgress` active payload:

- 서버가 event-owned cargo pod `NetworkObject`를 한 개 스폰한다.
- 서버가 pod 이동과 만료 시간을 소유한다.
- 플레이어가 회수 후 지정 deposit에 반납하면 서버가 성공을 commit한다.
- 보상은 party debris/credit wallet에 서버가 한 번만 지급한다.
- pod pickup만으로 성공 처리하지 않는다. deposit completion을 기준으로 한다.

`Resolve`:

- pod를 정상 item lifecycle로 소비한다.
- beacon이 짧게 수축하고 collection sting을 재생한다.

`Fail`:

- 시간이 끝나면 서버가 미회수 pod를 despawn한다.
- 공격 피해 대신 무보상 또는 짧은 warp delay만 허용한다.
- 화면 밖으로 멀어지는 trail과 lost-signal cue를 재생한다.

Cleanup:

- pod, trajectory line, beacon cone, radio loop를 모두 제거한다.
- 이미 다른 lifecycle로 소비된 pod를 다시 despawn하지 않는다.

### 5.3 P1~P2 Gravity Shear / Micro Singularity Pass

정체성:

- 중력 발생기 고장이 아니다. 외부 특이점이 만드는 방향성 조석력이다.
- 함선 중력 시스템이 정상이어도 발생한다.

`Trigger` telegraph:

- 외부 lensing ring, 굽어지는 star/debris streak, 힘 방향 HUD 화살표를 표시한다.
- SFX: 상승하는 sub-bass와 hull creak.
- 서버가 `Direction`, `Intensity01`, `VisualSeed`를 확정한다.

`InProgress` active payload:

- 서버가 고정 tick에서 플레이어와 event 대상 network item에 방향성 force를 적용한다.
- 클라이언트는 화면 흔들림과 dust stream만 보여 준다.
- warp multiplier를 0.25~0.6 범위로 낮춘다.
- 안전 anchor volume을 두는 경우 scene/환경 prefab의 명시적 Inspector 참조로 연결한다.

`Resolve`:

- active 시간 생존 또는 anchor 조건 충족 시 성공한다.
- lensing ring이 수축하고 pressure-release cue를 재생한다.

`Fail`:

- 서버 기준 downed player 또는 ship strain threshold 초과 시 실패한다.
- 실패 consequence는 Gravity 또는 Device 계열로 연결할 수 있다.

Cleanup:

- force를 한 프레임에 끊지 않고 짧게 0으로 감쇠한다.
- 이벤트가 적용한 force handle만 해제한다.
- debris flow의 원래 speed/direction을 저장했다가 정확히 복원한다.

필요 신규 인터페이스 후보:

- `IExternalForceTarget`
- `IExternalDebrisFlowModifier`

인터페이스 스크립트 이름은 반드시 `I`로 시작한다.

### 5.4 P2 Pulsar Radiation Sweep

정체성:

- EMP가 아니다. 공간을 주기적으로 훑는 방사선 빔이다.
- 대응은 전원 차단이 아니라 차폐 구역 이동이다.

`Trigger` telegraph:

- 멀리 회전하는 pulsar lighthouse와 다음 sweep 경로를 표시한다.
- 차폐 구역 outline과 pulse countdown을 표시한다.
- SFX: dosimeter tick이 sweep 직전 빨라진다.

`InProgress` active payload:

- 서버가 pulse index와 pulse server time을 소유한다.
- pulse 순간 authored shield volume 밖 플레이어에 `NetworkPlayerLifeState.ApplyDamage`를 적용한다.
- 차폐 판정은 서버 collider/volume 기준이다.
- 클라이언트 raycast 또는 화면 위치로 안전 여부를 결정하지 않는다.

`Resolve`:

- 정해진 pulse 수가 끝나고 생존 조건을 충족하면 성공한다.
- 마지막 beam이 멀어지고 detector가 안정되는 cue를 재생한다.

`Fail`:

- 서버의 player down 또는 누적 피해 기준으로 실패한다.
- missed pulse별 피해가 이미 적용됐으므로 terminal에서 중복 피해를 주지 않는다.

Cleanup:

- beam plane, safe marker, countdown, dosimeter loop, screen rim effect를 제거한다.
- volume collider는 scene-owned 상태를 유지하고 표시/판정 활성만 원래 값으로 복구한다.

### 5.5 P3 Dark-Matter Eclipse - 선택안

정체성:

- 성운 안개가 아니다. 짧은 시간 동안 별과 원거리 센서 마커를 가리는 occultation이다.
- 함선 전원과 근거리 조명은 정상이다.

`Trigger` telegraph:

- 검보라 occulting disc가 확장하며 배경 별이 부분적으로 사라진다.
- HUD long-range marker에 static이 시작된다.
- SFX: 저역 통신 필터와 느린 sonar ping.

`InProgress` active payload:

- 서버 snapshot이 sensor mask 활성 시간을 소유한다.
- 클라이언트는 허용된 map/incident location marker만 숨긴다.
- health, interaction prompt, teammate 근거리 표시는 숨기지 않는다.
- 기존 terminal을 통한 triangulation 성공 또는 제한 시간 생존을 성공 기준으로 삼을 수 있다.

`Resolve` / `Fail`:

- Resolve: marker가 순차 복구되고 sonar lock cue 재생.
- Fail: warp 감속 또는 기존 Device consequence 요청. 적 스폰은 사용하지 않는다.

Cleanup:

- 숨긴 marker별 이전 visibility를 복원한다.
- occulting disc, static, audio filter를 제거한다.

주의:

- `NebulaZone`의 시야 저하와 플레이 감각이 겹칠 수 있다.
- HUD marker 소유 경계가 합의되지 않으면 구현하지 않는다.
- 대체안 Cryogenic Comet Tail은 구분은 쉽지만 이동 감속/표면 결빙 modifier를 새로 만들어야 하므로 같은 P3다.

## 6. P0 Solar Flare presentation-only prefab

제안 경로:

`Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Events/External/PHS_ExternalSolarFlarePresentation.prefab`

권장 hierarchy:

```text
PHS_ExternalSolarFlarePresentation
├─ PresentationRoot
│  ├─ TelegraphSocket
│  │  ├─ SolarOriginAnchor
│  │  │  ├─ CoronaRibbonVfx
│  │  │  └─ DirectionMarkerVfx
│  │  ├─ InteriorWarningLightRoot
│  │  └─ TelegraphVolume
│  ├─ ActiveSocket
│  │  ├─ DirectionalSolarWaveVfx
│  │  ├─ HullHeatShimmerVfx
│  │  └─ ActiveExposureVolume
│  ├─ ResolveSocket
│  │  └─ CoolDownBurstVfx
│  └─ FailSocket
│     ├─ HullFlashVfx
│     └─ SparkBurstVfx
├─ AudioRoot
│  ├─ TelegraphOneShotSource
│  ├─ ActiveLoopSource
│  ├─ ResolveOneShotSource
│  └─ FailOneShotSource
├─ HudAnchor
└─ CleanupRoot
```

Root view 계약:

- 제안 인터페이스: `IExternalEventPresentationView`.
- 제안 구현: `SolarFlareExternalEventPresentationView`.
- view 입력은 presentation snapshot과 local server time뿐이다.
- view가 서버 RPC, 피해, reward, event resolve를 호출하면 안 된다.
- prefab 내부에 `NetworkObject`, gameplay collider, `Rigidbody`, damage script를 넣지 않는다.
- ParticleSystem/VFX Graph, MeshRenderer, Light, Volume, AudioSource만 허용한다.
- 모든 Inspector 참조를 명시한다. 런타임 `Find`로 socket을 찾지 않는다.
- `OnDisable`과 강제 cleanup이 같은 정리 함수를 사용하며 여러 번 호출해도 안전해야 한다.
- `CleanupRoot`는 runtime 임시 visual을 모으는 부모다. gameplay object를 자식으로 넣지 않는다.

Presentation controller 제안:

- `NetworkExternalEventPresentationController`가 `NetworkEventCoordinator.LifecycleSnapshotsChanged` 또는 신규 snapshot list 변경을 구독한다.
- `(InstanceId, EventId)`별 view 한 개만 유지한다.
- snapshot 전체를 reconcile하여 late join과 삭제를 모두 처리한다.
- map/profile 변경, WarpArrival, Shop, FinalShop, Clear, GameOver에서 모든 view를 강제 정리한다.
- 현재 profile의 dedicated `ExternalEventPresentationRoot` 아래에만 인스턴스화한다.

## 7. 온라인 권위와 중복 방지

### 7.1 서버 전용

- event 선택과 seed 확정
- phase 전이와 시작/종료 시간
- player/ship/module 피해
- force 적용
- cargo pod spawn/despawn
- 보상 지급
- success/fail 판정
- consequence 요청

### 7.2 클라이언트 전용

- ParticleSystem/VFX Graph 재생
- Light/Volume/UI 표시
- camera impulse
- 2D/3D audio 재생과 fade
- 서버 시간 기반 normalized phase 계산

### 7.3 audio dedupe key

one-shot 재생 키:

```text
(InstanceId, Revision, CueKind)
```

loop 소유 키:

```text
(InstanceId, CueKind)
```

규칙:

- 동일 one-shot key는 클라이언트마다 한 번만 재생한다.
- snapshot list 재정렬이나 동일 revision 재수신은 재생 원인이 아니다.
- active loop는 `InProgress` 진입에서 시작하고 상태 이탈, snapshot 삭제, scene unload, disconnect에서 fade-out한다.
- late join이 active 중이면 loop를 normalized offset 또는 허용된 loop 시작점에서 한 번만 시작한다.
- RPC로 프레임마다 audio를 요청하지 않는다.
- 외부 이벤트별 신규 cue가 준비되기 전 범용 `Warning`은 임시 telegraph에만 사용한다.

## 8. skybox 충돌 회피

금지:

- event view에서 `RenderSettings.skybox` 직접 변경
- event view 종료 시 저장해 둔 global skybox를 무조건 덮어쓰기
- warp 중 event가 exposure/skybox를 다시 활성화

허용:

- 카메라 주변 overlay dome 또는 quad
- world-space directional particle/VFX
- event 전용 local Volume
- 맵 환경 아래 event overlay renderer
- `MaterialPropertyBlock` 기반 인스턴스별 intensity

소유권:

- skybox와 warp 화면 전환: `WarpTransitionPresenter`와 `PHSMapRuntimeContext`
- 외부 이벤트 임시 시각물: `NetworkExternalEventPresentationController`
- event volume은 자체 weight만 변경하고 cleanup 때 0으로 돌린다.
- warp/shop/terminal phase가 시작되면 event presentation이 먼저 비활성화된 뒤 맵 presentation이 전환된다.

## 9. 담당자 협의 경계

| 변경 후보 | 소유 영역 | 협의 필요 내용 |
|---|---|---|
| `EventEnum.cs` | `Assets/04. NohSeokMin_Game Event` | 신규 EventId 번호 배정 |
| `EventFactory.cs`, `ExternalEvent.cs`, 신규 domain/SO | `Assets/04. NohSeokMin_Game Event` | Trigger 유지 시간, resolve/fail 계약, SO 생성 |
| `IncidentRequestContentContract.cs` | ParkHanSol multiplayer | 신규 EventId route 등록 |
| `NetworkRunIncidentFamily` | ParkHanSol multiplayer | 신규 family 값 배정과 직렬화 호환성 |
| `PHSMapRuntimeContext.TryResolveExternalIncidentFamily` | ParkHanSol multiplayer | EventId -> family mapping |
| map profile event weights | ParkHanSol map profile | 어느 구역에서 어떤 빈도로 발생할지 |
| HUD label/terminal/minigame route | ParkHanSol events/UI | 대응 방식과 표시 문자열 |
| ship/player damage와 power | ParkHanSol multiplayer | P0 피해 tick, safe dwell, consequence 수치 |
| 신규 VFX/SFX asset | 아트/사운드 담당 | 라이선스, 색, 음량, loop seam, import 설정 |
| sensor marker visibility | UI/맵 담당 | Dark-Matter Eclipse가 숨길 수 있는 표시 범위 |

경계 규칙:

- `Assets/04` 담당자 합의 전 EventId, factory, domain class를 만들지 않는다.
- PHS 전용 별도 스케줄러로 같은 외부 이벤트 source-of-truth를 만들지 않는다.
- 기존 EventManager와 incident ledger를 우회하지 않는다.
- 신규 family enum 값을 기존 값 사이에 삽입하지 않는다. 맨 뒤에 명시적으로 추가한다.
- scene이나 공유 prefab 원본을 직접 바꾸기 전에 ParkHanSol owned copy와 Inspector 연결 지점을 우선한다.

## 10. P0 구현 순서

1. `Assets/04` 담당자와 Solar Flare EventId, Trigger 유지, SO/factory 경계를 확정한다.
2. incident family, content contract, map resolver, profile weight를 한 vertical slice로 추가한다.
3. server presentation snapshot을 추가하고 lifecycle과 같은 InstanceId로 연결한다.
4. `IExternalEventPresentationView`와 controller를 추가한다.
5. Solar Flare presentation-only prefab을 만들고 전용 root에 Inspector로 연결한다.
6. server power-off dwell, damage tick, warp multiplier, terminal result를 연결한다.
7. telegraph/active/resolve/fail SFX를 연결하고 dedupe를 검증한다.
8. Host+Client+late join+map transition matrix를 통과한 뒤 다음 이벤트로 확장한다.

## 11. 검증 매트릭스

| ID | 실행 조건 | 확인 항목 | 통과 기준 |
|---|---|---|---|
| C01 | Unity Refresh/compile | 코드 상태 | Console Error 0 |
| C02 | prefab validator | presentation-only 계약 | NetworkObject, Rigidbody, gameplay collider/damage script 0 |
| C03 | content validator | route 완전성 | EventId, factory, SO, family, contract, resolver, HUD, profile 누락 0 |
| N01 | Host Trigger | 권위 phase | Host가 정한 InstanceId, seed, 시작/종료 시간이 snapshot에 1회 기록 |
| N02 | Host+Client Trigger | telegraph 동기 | 양쪽 방향/ETA/variant 동일, gameplay 피해 0 |
| N03 | Host+Client Active | payload 권위 | 피해/force/reward는 서버에서만 실행, client 중복 commit 0 |
| N04 | active 중 late join | 단계 복원 | telegraph 재시작 없이 active 잔여 시간과 VFX 위치 재현 |
| N05 | terminal 중 late join | terminal 복원 | 남은 burst만 재생, reward/damage 재적용 0 |
| N06 | snapshot 동일 revision 반복 | dedupe | one-shot 추가 재생 0, loop voice 1 |
| N07 | Resolve | 성공 commit | 성공 결과 1회, fail consequence 0 |
| N08 | Fail | 실패 commit | 실패 결과 1회, consequence 1회 이하 |
| N09 | client disconnect/reconnect | 정리/복원 | 기존 client view 0 leak, 재접속 후 현재 phase 1개만 표시 |
| T01 | active 중 WarpArrival | 강제 종료 | event view, loop, volume, HUD, modifier 0 |
| T02 | active 중 Shop/FinalShop | 강제 종료 | cargo/force/damage tick 중단, 잔여 NetworkObject 0 |
| T03 | Clear/GameOver | 강제 종료 | terminal UI와 event overlay 충돌 0 |
| T04 | map profile 교체 | 환경 소유권 | 이전 map root 아래 event child 0 |
| V01 | Solar Flare telegraph 캡처 | 가독성 | 위험 방향과 ETA를 3초 안에 식별 가능 |
| V02 | Solar Flare active 캡처 | 단계 차이 | telegraph와 active 실루엣/색/움직임이 명확히 다름 |
| V03 | warp와 Solar Flare 교차 | skybox 충돌 | warp skybox/exposure가 마지막 상태로 정상 복구 |
| A01 | telegraph->active->resolve | audio lifecycle | one-shot 1회씩, active loop 1개, 종료 뒤 voice 0 |
| A02 | fail/force cleanup | audio fail path | fail sting 1회, resolve sting 0, loop fade 완료 |
| G01 | Solar Flare 전원 OFF 유지 | 성공 조건 | dwell 충족 후 Resolve, active 열손상 중단 |
| G02 | Solar Flare 전원 ON 유지 | 실패 조건 | 고정 tick 손상만 적용, 프레임율별 피해량 차이 없음 |
| G03 | Cargo 회수/반납 | 보상 조건 | deposit 후 party wallet 1회 증가, pod 1회 제거 |
| G04 | Gravity cleanup | modifier 복구 | player/item/debris 원래 동작 복구, 남은 force handle 0 |
| G05 | Pulsar pulse | volume 판정 | 서버 기준 shield 안 피해 0, 밖 고정 피해 1회/pulse |

시각 검증은 로그만으로 완료 처리하지 않는다. Host와 Client 실제 화면 캡처, hierarchy cleanup, AudioSource voice 상태를 함께 확인한다.

## 12. 완료 기준

P0 Solar Flare 완료 판정:

- 기존 EnemyScout, MeteorAttack, EmpAttack과 역할이 겹치지 않는다.
- Trigger가 실제 시간 동안 유지되고 active 전에 위험 방향과 대응 방법을 보여 준다.
- 서버만 결과와 피해를 결정한다.
- late join이 현재 단계와 남은 시간을 복원한다.
- one-shot 중복 0, active loop leak 0이다.
- warp와 map skybox를 직접 변경하지 않는다.
- Resolve/Fail/transition/disconnect 모든 경로에서 presentation object와 modifier가 0개 남는다.
- `Assets/04`와 ParkHanSol 영역의 소유자 협의, EventId/family/route/profile 연결이 문서와 일치한다.
