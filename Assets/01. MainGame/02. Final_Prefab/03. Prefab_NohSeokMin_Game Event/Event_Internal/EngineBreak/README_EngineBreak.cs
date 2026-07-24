/*

# EngineBreak Event

## 개요
엔진 고장 사고(EventId: EngineBreak, 7105)입니다.
스페이스 워프 게이지 상승 속도 감소, 일정 시간마다 연료 손실이 발생하며,
엔진룸의 기존 콘솔 오브젝트를 렌치로 수리하면 해결됩니다.

## 구성
- EngineBreakEvent.cs (InternalEvent 상속)
- EngineBreakEventDataSO (maxRepairProgress = 10, fuelLossInterval)
- EngineRoomConsole.cs — 씬에 이미 배치된 엔진 콘솔에 부착 (신규 스폰 없음)

Pool, SpawnSetting 없음 (기존 오브젝트를 그대로 활용).

## 발생 트리거
EventManager.Instance.SpawnEvent(EventId.EngineBreak, targetRoom, onFinishedCallback);

## 동작 방식
발생 → OnEngineBroken 신호 발행 (게이지 상승 속도 감소 시작)
     → fuelLossInterval마다 OnFuelLoss 신호 발행 (연료 차감)
     → 렌치로 EngineRoomConsole 상호작용 → ApplyRepair(렌치 아이템 SO의 수리량 값)
     → 누적 수리량이 maxRepairProgress(10) 도달 시 자동 OnResolve() → OnEngineRestored 신호 발행

## 발행하는 신호

| 이벤트 | 발생 시점 |
|---|---|
| OnEngineBroken | 사고 발생 즉시 |
| OnFuelLoss | fuelLossInterval마다 반복 |
| OnEngineRestored | 수리 완료 시, 또는 강제 종료 시 |

## 수리 연동
EngineRoomConsole(엔진 콘솔, 씬에 이미 존재)이 IInteractable + IRequireHeldItem(Wrench) 구현.
렌치 아이템의 IUsableItem.Use()에서 EngineRoomConsole.ApplyRepairToEngine(아이템 SO의 수리량 값) 호출.
수리량 자체는 이 이벤트가 아니라 렌치 아이템 데이터에서 결정됩니다.

## 조회 방식
EventManager.GetActiveEvent(EventId) 메서드 사용 (로컬 테스트용으로 추가, 병합 시 조율 필요).

## 확인 필요 사항
- 게이지 상승 속도 감소 폭, 연료 손실량 등 실제 수치는 워프 게이지 시스템 담당 결정 사항
- EngineRoomConsole이 씬에 몇 개 배치될 예정인지 (엔진룸이 여러 개인지)

## 테스트 완료 사항
- SpawnEvent 호출로 발생 → OnEngineBroken/OnFuelLoss 신호 발행 확인
- ApplyRepairToEngine 임의 수치로 반복 호출하여 maxRepairProgress(10) 도달 시 수리 완료 및
  OnEngineRestored 확인
- Console 에러 0

*/