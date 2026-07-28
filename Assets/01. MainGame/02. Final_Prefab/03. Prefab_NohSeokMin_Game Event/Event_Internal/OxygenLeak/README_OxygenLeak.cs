/*

# OxygenLeak Bundle

## 개요
산소 유출 사고(EventId: OxygenLeak, 7104)의 GameReady 완성품입니다.
발생 트리거를 받으면, 스폰부터 밀봉(수리) 판정, 풀 반환까지 로컬에서 전체 생애주기가 완결됩니다.

## 구성
- OxygenLeak_Bundle.prefab
  - OxygenLeakEffectPool (이펙트 인스턴스 재사용 관리)
  - OxygenLeakSpawnSetting (스폰 위치 목록)
- OxygenLeakEffect.prefab (실제 누출구 이펙트)
- OxygenLeakEventDataSO (밸런스 데이터: outerPullRadius, innerDamageRadius, pullSpeed, centerDamage, damageTickInterval, maxRepairProgress)

## 발생 트리거
```csharp
EventManager.Instance.SpawnEvent(EventId.OxygenLeak, targetRoom, onFinishedCallback);
```

## 동작 방식
-발생 시 `OxygenLeakSpawnSetting`에 등록된 스폰 위치 중 하나를 랜덤 선택
- `Physics.OverlapSphere`로 범위 내 Player(CharacterController)를 직접 스캔하여 서서히 중심으로 당김 (첫 5초간 흡입력 감소 후 소멸)
- 중심 반경(`innerDamageRadius`) 도달 시 주기적으로 데미지
- **Collider 없음**: 범위 판정을 `OverlapSphere`로 직접 처리하므로 트리거 Collider가 불필요함
- 밀봉(수리) 완료 시 자동 종료, 이펙트 풀 반환

## 주의사항
- 범위 내 Player에게 `IDamageable.ApplyDamage()`로 중심부 피해를 직접 확정합니다. (이 Bundle이 담당)
- 다만 이 피해가 함선 전체 상태(Ship HP 등)에 어떻게 반영되는지는 이 Bundle의 범위 밖입니다.
- Player 이동이 CharacterController 기반임을 전제로 당김 로직이 구현되어 있습니다.
- 스폰 위치(`OxygenLeakSpawnSetting`)는 씬에 배치 후 인스펙터에서 직접 등록해야 합니다.

## 확인 필요 사항
1. 최종 피해 확정(중심부 데미지가 Ship/Module HP에 반영되는 시점)을 어떤 방식으로 통보받아야 하는지
2. `targetRoom` 선택을 GameManager가 특정 Room으로 지정하고 싶은 경우가 있는지

## 테스트 완료 사항
- `EventManager.Instance.SpawnEvent(EventId.OxygenLeak, room, callback)` 호출로 발생 → 당김/피해 동작 → 밀봉 완료 → 성공 콜백 확인
- Console 에러 0

*/