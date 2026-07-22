/*

# EnemySpawn Bundle

## 개요
using SM;

적 침투 사고(EventId: EnemySpawn, 7102)의 GameReady 완성품입니다.
발생 트리거를 받으면, 스폰부터 처치 판정, 풀 반환까지 로컬에서 전체 생애주기가 완결됩니다.
발생 시점 판단, 위치(Room) 최종 선택, 최종 피해 확정(Ship/Module HP), 네트워크 동기화는 포함하지 않습니다.

## 구성
- EnemySpawn_Bundle.prefab
  - EnemyPool (Enemy 인스턴스 재사용 관리)
  - EnemySpawnSetting (스폰 그룹 목록, 자식으로 Spawn Point Transform 등록)
- PlayerAttackEnemy.prefab, DeviceAttackEnemy.prefab (실제 스폰 대상, EnemyPool이 참조)
- EnemySpawnDataSO (밸런스 데이터)

## 발생 트리거 (GameManager 계층 담당)
이 Bundle은 스스로 발생 시점을 판단하지 않습니다.

```csharp
EventManager.Instance.SpawnEvent(EventId.EnemySpawn, targetRoom, onFinishedCallback);
```

## 동작 방식
-발생 시 `EnemySpawnSetting`에 등록된 스폰 그룹 중 하나를 랜덤 선택
- 두 프리팹(PlayerAttackEnemy/DeviceAttackEnemy) 중 하나를 랜덤으로 골라, 그 한 종류로만 `enemyCount`마리를 `spawnInterval` 간격으로 순차 스폰
- 각 Enemy는 자체 FSM(Chase → Attack → Dead)으로 로컬에서 완결 동작하며, **실제로 Player/장치를 추적하고 공격을 시도합니다** (`IDamageable.ApplyDamage()` 호출)
- 전원 사망 시 자동으로 성공 종료

## 주의사항 (중요)
- Enemy는 실제로 Player/장치를 추적하고 공격하며, 공격 성공 시 IDamageable.ApplyDamage()를
  직접 호출하여 개체 단위 피해를 확정합니다. (이 부분은 이 Bundle이 담당합니다)
- 다만 이 피해가 함선 전체 상태(Ship HP, Module 파괴, 게임 승패 조건 등)에
  어떻게 집계·반영되는지는 이 Bundle의 범위 밖입니다.
  개별 IDamageable 구현체(Player, 장치) 또는 그 상위 시스템의 책임입니다.

## 확인 필요 사항
1. 최종 피해 확정(Enemy 공격이 Ship/Module HP에 반영되는 시점)을 어떤 방식으로 통보받아야 하는지
2. `targetRoom` 선택을 GameManager가 특정 Room으로 지정하고 싶은 경우가 있는지

## 테스트 완료 사항
- `EventManager.Instance.SpawnEvent(EventId.EnemySpawn, room, callback)` 호출로 3마리 순차 스폰 → 추적/공격 동작 → 전원 처치 → 성공 콜백 확인
- Console 에러 0

*/