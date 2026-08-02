/*
 
# MicDestroy

## 개요
통신 장비 파괴 사고(EventId: MicDestroy, 7106)입니다.
스폰 위치, 오브젝트, Pool, 플레이어 상호작용이 전혀 없는 순수 타이머 기반 신호 이벤트라 별도 프리팹이 없습니다.

## 구성
- MicDestroyEvent.cs (EventBase 상속)
- MicDestroyEventDataSO (밸런스 데이터: disableDuration, 기본 15초)

## 발생 트리거
```csharp
EventManager.Instance.SpawnEvent(EventId.MicDestroy, targetRoom, onFinishedCallback);
```

## 동작 방식
발생 → 즉시 OnMicDisabled 신호 발행 → disableDuration(기본 15초) 대기 → 자동으로 OnMicRestored 신호 발행 → 종료

- 발생과 동시에 마이크 비활성화 신호가 나갑니다.
- 별도 상호작용 없이 시간이 지나면 자동으로 해결됩니다.
- 강제 종료(`ForceTerminate`) 시에도 `OnMicRestored`가 발행되어 마이크가 즉시 복구됩니다.

## 발행하는 신호 (로컬 event, 네트워크 확장 필요 지점)

| 이벤트 | 발생 시점 |
|---|---|
| `OnMicDisabled` | 사고 발생 즉시 |
| `OnMicRestored` | 지속시간 경과 후 자동 복구, 또는 강제 종료 시 |

현재는 순수 C# `event Action`으로만 구현되어 있습니다. 병합 시 네트워크 담당 쪽에서 RuntimeBridge 연동 등 필요한 부분을 추가하는 구조입니다.

## 의존성
없음 (Room, 스폰 포인트, Pool, 아이템 상호작용 전부 불필요)

## 확인 필요 사항
- 마이크 on/off를 실제로 처리하는 쪽이 이 이벤트 인스턴스를 어떻게 참조해서 `OnMicDisabled`/`OnMicRestored`를 구독할지 결정 필요

## 테스트 완료 사항
- `EventManager.Instance.SpawnEvent(EventId.MicDestroy, room, callback)` 호출로 발생 → 15초 후 자동 복구 → 성공 콜백 확인
- Console 에러 0

 */