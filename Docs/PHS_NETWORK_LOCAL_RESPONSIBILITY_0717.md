# PHS 네트워크 / 로컬 책임 분리

기준: 공유 게임 상태는 서버가 결정하고, 클라이언트는 요청과 표현만 담당한다.

## 책임 표

| 영역 | 서버 / 네트워크 | 각 클라이언트 로컬 |
|---|---|---|
| 월드 아이템 | Spawn, Despawn, 위치·물리 권한, 중복 줍기 방지 | Raycast, 상호작용 입력, 프롬프트 |
| 플레이어 보유 상태 | itemId, revision, pickup/place/throw 검증 | 손 Held 프리팹, HUD |
| 줍기 | 대상 NetworkObject, 거리, 같은 씬, 카탈로그, 빈손 검증 | owner가 요청 전송 |
| 내려놓기·투척 | Dropped 프리팹 1개 생성·Spawn 후 record 원자적 비움 | 입력, 사운드·VFX |
| 파편 스트림 | 서버만 생성·이동·재생성 | NetworkTransform 결과 표시 |
| 상점 표시 | 구매·배송 결과와 overflow 월드 Spawn | 진열용 Held 프리팹 표시 |
| 판매 | 서버가 1회 판정, 크레딧 반영, Despawn | 성공 UI·효과 |

## 프리팹 계약

- `HeldPrefab`: 로컬 표현 전용. `NetworkObject`, `NetworkTransform`, `ThrownItemImpact` 금지.
- `DroppedPrefab`: 공유 월드 개체. `NetworkObject`, `NetworkTransform`, `NetworkItemPhysicsAuthority`, `Rigidbody`, `UtilityItemObject` 필수.
- `UtilityItemCatalogSO`: 서버가 허용한 itemId와 Held/Dropped 연결의 단일 목록.
- 씬 배치 Dropped seed: NGO 씬 관리가 자동 Spawn한다. 스트림이 수동 Spawn하지 않는다.
- 런타임 보충·교체 파편: 캐시한 `DroppedPrefab`을 서버가 생성하고 수동 Spawn한다.

## 상태 흐름

### Pickup

1. owner 클라이언트가 월드 `NetworkObjectId`로 요청한다.
2. 서버가 sender, 거리, 씬, spawn 상태, 카탈로그, held revision을 검증한다.
3. 서버가 held record를 설정하고 월드 개체를 `Despawn`한다.
4. 모든 피어는 record 변경을 받아 자기 화면에만 Held 프리팹을 만든다.

### Place / Throw

1. owner 클라이언트가 위치·회전 또는 투척 입력을 요청한다.
2. 서버가 held record와 요청 범위를 검증한다.
3. 서버가 Dropped 프리팹을 생성·`Spawn`한다.
4. Spawn 성공 뒤 record를 비운다. 실패하면 생성 개체를 rollback한다.
5. 모든 피어는 record 변경으로 Held 표현과 owner HUD를 비운다.

## 검증 완료 기준

- 원격 클라이언트 실제 pickup → throw 요청 성공.
- 양 피어가 같은 월드 `NetworkObjectId`를 본다.
- pickup 뒤 원본 ID가 양 피어에서 사라진다.
- Held 표현 내부 `NetworkObject` 수는 0이다.
- held record와 Held 표현이 함께 비워진다.
- 13개 utility item의 Held/Dropped 계약과 NetworkPrefab 등록이 정적 Validator를 통과한다.

## 후속 경계

현재 0715 빌드 씬 밖의 레거시 `UtilityToolBoxStorageSlotInteractable`은 슬롯 상태가 로컬이다.
다시 활성 빌드에 넣을 때는 슬롯 itemId를 서버 상태로 올리고 Take/Store/Swap을 서버 원자 작업으로 전환해야 한다.
