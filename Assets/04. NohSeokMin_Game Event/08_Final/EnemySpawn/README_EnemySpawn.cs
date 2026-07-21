/*
# EnemySpawn Bundle

## 개요

적 침투 사고(EventId: EnemySpawn, 7102)의 GameReady 완성품입니다.
확정된 위치를 전달받으면, 스폰부터 처치 판정, 풀 반환까지 로컬에서 전체 생애주기가 완결됩니다.
위치 선택, 원장 등록, 최종 피해 확정(Ship/Module HP), 네트워크 동기화는 포함하지 않습니다.

## 구성
- EnemySpawn_Root (Prefab)
  - EnemySpawnRuntime (스크립트, 외부 호출 진입점)
  - EnemyPool (스크립트, Enemy 인스턴스 재사용 관리)
- PlayerAttackEnemy.prefab, DeviceAttackEnemy.prefab (Pool이 다루는 실제 Enemy 프리팹)
- EnemySpawnDataSO (밸런스 데이터: enemyCount, spawnInterval, 프리팹 참조)

## 사용 방법 (외부 호출 API)

| 함수 | 설명 |
|---|---|
| `Telegraph(Transform confirmedLocation)` | 발생 직전, 확정된 위치를 전달받아 준비 |
| `Activate(Transform confirmedLocation)` | 실제 발생. 확정 위치에서 enemyCount만큼 spawnInterval 간격으로 순차 스폰 시작 |
| `Cancel()` | 외부에서 강제 취소 필요 시 호출. 활성 중인 적 전부 즉시 풀 반환 |
| `Cleanup()` | 재사용 전 반드시 호출. 모든 상태/활성 적 초기화 |

## 결과 통보 (event Action 구독 방식)

| 이벤트 | 발생 시점 |
|---|---|
| `OnResolved` | 스폰된 적 전원 처치 완료 시 |
| `OnFailed` | 확정 위치가 없는 등 발생 자체가 실패했을 시 |
| `OnCancelled` | `Cancel()`로 외부에서 강제 취소되었을 시 |

사용 예:
```csharp
runtime.OnResolved += HandleResolved;
runtime.OnFailed += HandleFailed;
runtime.OnCancelled += HandleCancelled;
```

## 동작 방식
- `Activate` 호출 시 `EnemySpawnDataSO`에 지정된 두 프리팹(PlayerAttackEnemy/DeviceAttackEnemy) 중 하나를 랜덤으로 선택하여, 그 한 종류로만 `enemyCount`마리를 `spawnInterval` 간격으로 순차 스폰합니다.
- 스폰된 각 Enemy는 자체 FSM(Chase → Attack → Dead)으로 로컬에서 완결 동작합니다.
- 전원 사망 시 자동으로 `OnResolved` 발행 및 내부 정리.

## 재사용(풀링) 관련
`EnemyPool`이 프리팹 종류별로 인스턴스를 큐로 관리하며, `Cleanup()` 호출 시 활성 중인 모든 Enemy가 자동으로 풀에 반환됩니다.
별도 조치 없이 `Cleanup()` → 재사용 가능한 상태입니다.

## 주의사항
- 반드시 씬에 배치된 인스턴스를 조작해야 합니다.
- `Telegraph`/`Activate`에 전달하는 위치는 현재 `Transform` 타입입니다. (확인 필요 사항 1번 참고)
- 최종 피해 판정(공격 성공 시 Ship/Module HP 반영)은 이 Bundle에 포함되지 않습니다. Enemy AI는 공격 시도까지만 로컬에서 처리합니다.

## 확인 필요 사항
1. `Telegraph`/`Activate`에 넘기는 위치 파라미터를 `Transform`으로 받고 있는데, Location 시스템에 별도 핸들 타입(예: LocationHandle, AnchorId)이 있다면 그 타입으로 맞춰야 하는지 확인 필요.
2. "Request Source 후보 신호"를 저희 쪽에서 능동적으로 발행해야 하는지, 아니면 GameManager 담당 팀원이 타이밍을 결정하고 저희는 `Telegraph`/`Activate` 호출만 수동적으로 받으면 되는지 확인 필요. (이벤트 스케줄러가 폐기된 현재 구조상, 발생 타이밍 결정 주체를 GameManager 쪽과 조율 필요해 보임)
3. 최종 피해 확정(Enemy 공격이 Ship/Module HP에 반영되는 시점)을 어떤 방식으로 통보받아야 하는지 확인 필요.

## 테스트 완료 사항
- Mock 위치(Transform)로 Telegraph → Activate → 3마리 순차 스폰 → 전원 처치 → OnResolved 발행 확인
- Cancel() 호출 시 활성 적 즉시 풀 반환 확인
- Cleanup() 후 재사용 가능 상태 확인, Console 에러 0

## Manifest
manifest.json 참고 (owner, bundleType, contentId, inputs 등) */