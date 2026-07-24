/*
 * 
# PowerOff Event

## 개요
전력 차단 사고(EventId: PowerOff, 7103)입니다.
발전기를 제외한 주요 시설 방의 문 잠금 및 정전(장착된 배터리 삭제)이 발생하고,
발전기에 새로운 배터리를 장착하면 복구되는 사고입니다.

Fire/OxygenLeak과 달리 새로운 오브젝트를 스폰하지 않습니다.
이미 존재하는 발전기/문/조명 시스템의 상태를 바꾸는 "신호 발행"만 담당하며,
실제 문 잠금/정전/배터리 삭제/복구 처리는 신호를 구독하는 담당 매니저가 수행합니다.

## 구성
- PowerOffEvent.cs (EventBase 상속)
- PowerOffEventDataSO (밸런스 수치 없음, Registry 등록을 위한 최소 SO — 박한솔님 폴더 소재)

Pool, SpawnSetting, 별도 프리팹 없음 (새 오브젝트를 생성하지 않으므로 불필요).

## 발생 트리거
EventManager.Instance.SpawnEvent(EventId.PowerOff, targetRoom, onFinishedCallback);

## 동작 방식
발생 → OnPowerOff 신호 발행
     → (발전기에 새 배터리 장착 - 이벤트 시스템 밖에서 일어나는 일)
     → 담당 매니저가 배터리 재장착을 감지하면 NotifyPowerRestored() 호출
     → OnResolve() → OnPowerRestored 신호 발행 → 종료

강제 종료(ForceTerminate) 시에도 OnPowerRestored가 발행되어
문 잠금/정전 상태가 즉시 복구되도록 처리됩니다.

## 발행하는 신호 (로컬 event, 네트워크 확장 필요 지점)

| 이벤트 | 발생 시점 |
|---|---|
| OnPowerOff | 사고 발생 즉시 (문 잠금 + 정전 + 배터리 삭제 신호) |
| OnPowerRestored | NotifyPowerRestored() 호출 시, 또는 강제 종료 시 |

현재는 순수 C# event Action으로만 구현되어 있습니다.
병합 시 네트워크 담당 쪽에서 RuntimeBridge 연동 등 필요한 부분을 추가하는 구조입니다.

## 종료 진입점
NotifyPowerRestored() — 배터리 재장착을 감지한 담당 시스템이 직접 호출해야
이벤트가 정상 종료됩니다. 자동 타이머로 종료되지 않습니다.

호출 방법 (EventManager.GetActiveEvent(EventId) 메서드 추가됨):

var evt = EventManager.Instance.GetActiveEvent(EventId.PowerOff) as PowerOffEvent;
evt?.NotifyPowerRestored();

이 조회 메서드는 로컬 테스트를 위해 EventManager에 추가한 것으로,
병합 시 담당 팀원의 네트워크 인프라 방식과 조율이 필요할 수 있습니다.

## 의존성
스폰 포인트, Pool, 아이템 상호작용 없음.
문/조명/발전기 배터리 시스템은 이 Bundle 범위 밖이며, 신호 구독을 통해서만 연결됩니다.

## 확인 필요 사항
- OnPowerOff/OnPowerRestored 신호를 어떤 방식으로 구독/참조할지
  (EventManager 조회 방식, RuntimeBridge 방식 등 담당 팀원 인프라에 맞춰 결정 필요)

## 테스트 완료 사항
- EventManager.Instance.SpawnEvent(EventId.PowerOff, room, callback) 호출로 발생
  → OnPowerOff 신호 발행 확인
- NotifyPowerRestored() 수동 호출로 정상 종료 및 OnPowerRestored 신호 발행 확인
- ForceTerminate() 호출 시 OnPowerRestored 신호 발행 확인
- Console 에러 0

*/