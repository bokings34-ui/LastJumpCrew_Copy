# 네트워크 연결 지점 문서

## 개요

9개 이벤트 전부 **싱글플레이 기준 로컬 완결** 원칙으로 설계됨. 
각 이벤트는 발생/진행/해결까지 스스로 처리하며, 
네트워크 담당은 아래 **연결 지점(신호, 조회 메서드)**만 구독/호출하면 됨. 
이벤트 내부 로직을 직접 건드릴 필요 없음.

역할 분담:
- **석민(맵/이벤트)**: 스폰 포인트, NavMesh, 이벤트 조건/진행/결과
- **네트워크**: 서버 권한 판정, RPC, NetworkVariable, 네트워크 스폰, 중도 참가 동기화

---

## 공용 진입점

### EventManager

```csharp
// 발생 트리거 (GameManager 또는 스케줄러가 호출)
EventManager.Instance.SpawnEvent(EventId id, IRoom targetRoom, Action<EventBase, bool> onFinished = null);

// 현재 진행 중인지 조회
bool EventManager.Instance.IsActive(EventId id);

// 현재 활성 인스턴스 조회 (특정 이벤트에 직접 접근/제어할 때)
EventBase EventManager.Instance.GetActiveEvent(EventId id);

// 미니게임 결과 통보 대상 조회 (외부 경고 3종용)
IMiniGameTarget EventManager.Instance.GetMiniGameTarget(string targetId); // targetId = EventId.ToString()

// 전체 강제 종료 (스테이지 종료 시)
EventManager.Instance.ForceClearAll();
```

### 스케줄러 (GameManager가 호출)

```csharp
// 내부 사고 6종
EventScheduler.Instance.StartScheduler();
EventScheduler.Instance.StopScheduler();
EventScheduler.Instance.ForceClearAll();

// 외부 경고 3종 (Zone 기반)
ZoneEventScheduler.Instance.SetCurrentZone(ZoneType zone); // StartScheduler 전에 먼저 호출
ZoneEventScheduler.Instance.StartScheduler();
ZoneEventScheduler.Instance.StopScheduler();
```

---

## 내부 사고 6종

### 1. Fire (7101)

| 항목 | 내용 |
|---|---|
| 클래스 | FireEvent |
| 스폰 방식 | ShipSpawnPointConfig 기반, 이웃 그래프 따라 확산 (레벨업 = 활성 이펙트 개수) |
| 종료 조건 | 모든 이펙트 소화 완료 시 자동 OnResolve() |
| 발행 신호 | 없음 (event Action 없이, 순수 이펙트 스폰/제거로만 진행) |
| 조회 필요 시 | EventManager.GetActiveEvent(EventId.Fire) as FireEvent |
| 수리 연동 | FireEffectInstance.ApplyRepair(float amount) — 소화기 아이템이 직접 호출 |
| Presentation | FirePresentationController/PHSFirePresentationRuntimeAdapter 폐기됨. 현재 FireEffectInstance 기반 단일 이펙트(Effect_Fire_Big)로 로직+비주얼 통합 |
| 확인 필요 | Small/Medium/Large 강도 개념 없음. 셰이더 Z-fighting/방향별 미표시 이슈 미해결 (보류 중) |

### 2. EnemySpawn (7102)

| 항목 | 내용 |
|---|---|
| 클래스 | EnemySpawnEvent |
| 스폰 방식 | ShipSpawnPointConfig에서 포인트 1개, enemyCount마리 순차 스폰 (PlayerAttackEnemy/DeviceAttackEnemy 랜덤 선택) |
| 종료 조건 | 스폰된 적 전원 사망 시 자동 OnResolve() |
| 발행 신호 | 없음 |
| 위치 동기화 필요 | Enemy는 NavMeshAgent로 계속 이동 — PublishEffectPositionChanged 등 실시간 위치 브로드캐스트 필요 (다른 이벤트와 달리 지속 갱신 필요) |
| 최종 피해 확정 | 이 Bundle이 IDamageable.ApplyDamage()로 개체 단위 피해는 확정. Ship/Module HP 집계 반영은 범위 밖 |

### 3. PowerOff (7103)

| 항목 | 내용 |
|---|---|
| 클래스 | PowerOffEvent |
| 스폰 방식 | 없음 (신호만 발행) |
| 종료 조건 | 자동 종료 없음 — 외부에서 NotifyPowerRestored()를 반드시 호출해야 끝남 |
| 발행 신호 | OnPowerOff (발생 시), OnPowerRestored (복구 시/강제종료 시) |
| 상태 조회 | PowerOffEvent.IsPowerOffActive (bool) — event 구독보다 이 값을 폴링하는 걸 권장 (신호 발행 타이밍이 구독 시점보다 항상 빨라서 event 방식은 놓칠 수 있음) |
| 종료 진입점 | (EventManager.GetActiveEvent(EventId.PowerOff) as PowerOffEvent)?.NotifyPowerRestored() — 발전기 배터리 재장착 감지 시스템이 호출 |
| 처리 필요 항목 | 문 잠금, 배터리 삭제는 이 신호를 구독하는 담당 시스템(장치/문 담당)이 처리. 조명 어둡게 연출은 여러 방법 시도했으나 실패, 미해결 상태로 남음 |

### 4. OxygenLeak (7104)

| 항목 | 내용 |
|---|---|
| 클래스 | OxygenLeakEvent |
| 스폰 방식 | ShipSpawnPointConfig에서 포인트 1개, 단일 누출구 이펙트 스폰 |
| 종료 조건 | 밀봉(수리) 완료 시 자동 OnResolve() |
| 발행 신호 | 없음 |
| 수리 연동 | OxygenLeakEffectInstance.ApplyRepair(float amount) — 렌치 아이템이 직접 호출 |
| 알려진 이슈 | ShipWall Layer가 벽+구조물 전체를 포함해서, 같은 구역 내에서도 Linecast가 걸려 당기기 판정 오작동 가능 (보류 중, Layer 분리 필요) |

### 5. EngineBreak (7105)

| 항목 | 내용 |
|---|---|
| 클래스 | EngineBreakEvent |
| 스폰 방식 | 없음 (씬에 이미 배치된 EngineRoomConsole 재사용) |
| 종료 조건 | 수리 진행도가 maxRepairProgress 도달 시 자동 OnResolve() |
| 발행 신호 | OnEngineBroken (발생 시), OnFuelLoss (fuelLossInterval마다 반복), OnEngineRestored (수리 완료/강제종료 시) |
| 수리 연동 | EngineRoomConsole.ApplyRepairToEngine(float amount) → 내부적으로 EventManager.GetActiveEvent(EventId.EngineBreak) as EngineBreakEvent를 찾아 ApplyRepair() 호출. 렌치 아이템이 콘솔의 이 메서드를 호출 |
| 처리 필요 항목 | 워프 게이지 상승 속도 감소(OnEngineBroken)와 연료 차감(OnFuelLoss)은 워프 게이지 시스템 담당이 구독해서 처리 |

### 6. MicDestroy (7106)

| 항목 | 내용 |
|---|---|
| 클래스 | MicDestroyEvent |
| 스폰 방식 | 없음 (순수 타이머 기반 신호 이벤트) |
| 종료 조건 | disableDuration(기본 15초) 경과 시 자동 OnResolve() |
| 발행 신호 | OnMicDisabled (발생 시), OnMicRestored (자동 복구/강제종료 시) |
| 처리 필요 항목 | 마이크 on/off 처리는 음성 시스템 담당이 이 신호를 구독 |

---

## 외부 경고 3종 (미니게임 연동)

공통 베이스: ExternalEvent (IMiniGameTarget 구현)

| 항목 | 내용 |
|---|---|
| 종료 조건 | 미니게임 성공/실패 신호 수신 시 (자동 타임아웃 없음 — 무한정 대기, 별도 제한시간 필요 시 미니게임 쪽에서 자체 관리) |
| 결과 통보 | EventManager.Instance.GetMiniGameTarget(eventId.ToString()) → IMiniGameTarget.OnMiniGameSucceeded() / OnMiniGameFailed() 호출 |
| 실패 시 확산 | MeteorAttack → OxygenLeak, EnemyScout → EnemySpawn, EmpAttack → Fire (내부 사고로 편입, 자동 대기열 처리됨) |
| 발생 트리거 | ZoneEventScheduler가 Zone별 매핑(ZoneBehaviorConfigSO)에 따라 자동 발생, 30초 고정 주기 |
| Presentation | 별도 프리팹/시각 실체 없음. 미니게임 담당의 UI/장치가 별도로 존재 |

### 7. EnemyScout (7201)
EnemyScoutEvent — 확산 대상: EnemySpawn

### 8. MeteorAttack (7202)
MeteorAttackEvent — 확산 대상: OxygenLeak

### 9. EmpAttack (7203)
EmpAttackEvent — 확산 대상: Fire

---

## Zone(우주환경) 시스템

| 항목 | 내용 |
|---|---|
| ZoneType | PatrolZone, MeteorZone, NebulaZone, PlanetZone |
| GameManager 호출 | SetCurrentZone(zone) 먼저, 이후 StartScheduler() |
| Zone별 매핑 | ZoneBehaviorConfigSO 애셋에 Zone → EventId 리스트로 등록 |
| NebulaZone 예외 | 이벤트 발생 아님, OnNebulaTriggered 신호만 발행 (미니맵 담당이 구독) |

```csharp
public event Action OnNebulaTriggered; // ZoneEventScheduler에 위치
```

---

## 핵심 원칙 요약

1. 모든 이벤트는 로컬에서 완전히 동작함. 브릿지(Context.RuntimeBridge)가 없어도 에러 없이 끝까지 진행됨 — 즉 네트워크 계층을 얹기 전에도 항상 로컬 테스트 가능.
2. 상태는 event(구독)보다 프로퍼티 폴링이 안전함. OnTrigger() 안에서 즉시 신호가 발행되는 구조라, 늦게 구독하면 신호를 놓칠 수 있음 (PowerOffEvent.IsPowerOffActive가 그 예시).
3. 이벤트 인스턴스는 EventManager.GetActiveEvent(EventId)로 언제든 조회 가능. 이 인스턴스에 직접 RPC/NetworkVariable을 씌우면 됨.
4. 최종 피해 확정(Ship/Module HP)은 전부 이 Bundle들의 범위 밖. IDamageable.ApplyDamage()까지만 확정하고, 그 이상의 집계는 별도 시스템 담당.
5. Enemy 위치처럼 지속적으로 변하는 값은 매 프레임 동기화가 필요하지만, Fire/OxygenLeak처럼 "스폰 위치 고정 + 진행도만 변하는" 이벤트는 스폰 시점과 완료 시점만 동기화하면 충분.

---

## 확인이 필요한 나머지 사항

- IRepairable 인터페이스 통합 여부 (팀원 쪽에서 구현 예정, 추후 main에서 pull)
- PowerOff 조명 연출 미해결 상태 (담당자 재배정 또는 추후 재시도 필요)