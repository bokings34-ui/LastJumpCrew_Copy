# 아이템 제작 전달 규격

## 역할 분리

- 아이템 제작자: 모델, 아이콘, VFX, Held/Dropped 프리팹, `UtilityItemPrefabData` SO와 수치 작성.
- 런타임: 아이템 사용 요청, 서버 판정, 내구도 차감, 피해·수리·진압 적용, 네트워크 동기화 담당.
- 이벤트 대상: 허용 액션과 진행도만 관리. 아이템 수치를 복사해 갖지 않는다.
- 클라이언트가 수치나 결과를 직접 전달하지 않는다. 서버가 현재 보유 아이템의 SO를 조회해 판정한다.

## 제출물

아이템 1종마다 다음을 제출한다.

1. `Held.prefab`: 손에 들린 전용 프리팹.
2. `Dropped.prefab`: 월드 드롭·투척 전용 프리팹.
3. `UtilityItemPrefabData` SO.
4. 아이콘과 사용 VFX.

`itemId`는 영문 `snake_case`로 작성하고 등록 후 변경하지 않는다. Held와 Dropped는 반드시 분리한다.

## SO 작성

기본 필드:

- `itemId`, `displayName`, `icon`, `price`
- `hasDurability`, `maxDurability`
- `heldPrefab`, `droppedPrefab`
- `firstPersonHeldPose`, `worldHeldPose`
- `actionProfiles`

`actionProfiles` 한 줄은 다음 의미다.

- `ActionKind`: 아이템이 간섭하는 기능.
- `Amount`: 실제 수리·진압·배터리 공격 수치. `PowerRestore`는 현재 값 존재만 검사하며 복구량에는 아직 반영되지 않는다.
- `DurabilityCost`: 성공 1회당 내구도 소모량. 단, 배터리 삽입은 이 값과 무관하게 아이템 1개를 전부 소비한다.

같은 `ActionKind`를 한 SO에 중복 등록하지 않는다. 잘못된 값은 `PHS_UTILITY_ITEM_PROFILE_INVALID` 로그로 실패해야 한다.

현재 기준값:

| 계열 | 내구도 | 액션 수치 | 성공 비용 |
|---|---:|---|---:|
| 렌치 | 100 | 일반/함선 구멍/파이프/발생기 수리 20, 산소 누출 봉인 1 | 1 |
| 소화기 | 100 | 화재 진압 35 | 1 |
| 배터리 | 100 | 전력 복구 100, 투척 피해 20 | 100 |

렌치 몬스터 피해 15와 소화기 피해 2/0.5초는 현재 SO가 아니라 플레이어 전투 컴포넌트 값이다. 배터리 투척 피해는 SO의 `BatteryDischarge.Amount`를 사용한다. 배터리 `PowerRestore.Amount`와 `DurabilityCost`는 현재 실제 정전 복구량·소모량을 조절하지 않으므로 고급 배터리 밸런스 수치로 사용하지 않는다.

## Held 프리팹

- 루트에 `UtilityItemObject`와 해당 `IUsableItem` 구현 컴포넌트를 둔다.
- 렌치/소화기는 기존 `PHSAnimatedWrenchItemUse`, `PHSAnimatedFireExtinguisherItemUse` 연결본을 복제 기준으로 삼는다.
- `NetworkObject`, `NetworkTransform`, `ThrownItemImpact`를 넣지 않는다.
- 손 위치 보정은 프리팹 Transform을 임의로 틀지 말고 SO의 `firstPersonHeldPose`, `worldHeldPose`로 조정한다.
- 로컬은 `holdPoint`, 원격 플레이어는 `visibleHandHoldPoint`에서 확인한다.

## Dropped 프리팹

루트 필수 구성:

- `UtilityItemObject`
- Collider와 Rigidbody
- `NetworkObject`, `NetworkTransform`
- `NetworkItemPhysicsAuthority`
- `ThrownItemImpact`
- 내구도 아이템이면 `NetworkUtilityItemDurabilityState`

`UtilityItemObject.itemPrefabData`와 내구도 상태의 `itemObject`를 해당 SO/컴포넌트에 연결한다. Dropped 프리팹만 아래 현재 빌드용 네트워크 프리팹 목록에 등록한다. 동명 asset이 여러 개이므로 이름 검색만으로 고르지 않는다. Held 프리팹은 등록하지 않는다. SO는 `PHS_UtilityItemCatalog_0717.asset`에 등록한다.

`Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/DefaultNetworkPrefabs.asset`

## 온라인 동작 규칙

- 사용자는 요청만 보낸다.
- 타격, 수리, 진압, 내구도 차감, 투척 오브젝트 생성은 서버가 판정한다.
- 클라이언트 스크립트에서 피해, 수리 진행도, 내구도를 직접 변경하지 않는다.
- 사용 수치는 요청 인자가 아니라 서버가 확인한 보유 아이템 SO에서 가져온다.
- Host와 Client 모두 원격 손 모델, 사용 VFX, 투척 위치, 내구도 결과가 같아야 한다.

## VFX·Collider 규칙

- 소화기 분사, 렌치 사용, 배터리 충돌 같은 순수 VFX에는 Collider를 넣지 않는다.
- 이벤트 수리용 Collider만 Trigger로 허용한다.
- 플레이어 이동을 막는 물리 Collider와 피해 판정은 VFX가 아니라 별도 서버 Hazard 컴포넌트가 담당한다.
- 이벤트 표시 VFX는 `EventEffectPresentationView` 하위에 둔다.
- 프리팹 Inspector에서 VFX 하위 Collider가 0개인지 직접 확인한다. 이벤트 표시 프리팹은 Validator의 `event_presentation_collider_count_invalid`, 수리 대상은 런타임의 `unsafe_repair_collider` 로그도 확인한다. 아이템 VFX Collider 전용 자동 검사는 아직 없다.

## 고급 아이템 주의

현재 전투와 일부 사고 수리는 `wrench`, `fire_extinguisher` 같은 정확한 `itemId`를 검사한다. 따라서 새 `itemId`를 가진 고급 렌치/소화기는 모델, 프리팹, SO, 희망 수치까지만 먼저 제출한다. 실제 Catalog 등록 전에 프로그래머가 아이템 계열 허용 계약을 확장해야 한다. 기존 `itemId`를 재사용해 우회하지 않는다.

신규 Catalog 항목을 추가하면 통합 담당자가 Validator의 고정 항목 수 18도 함께 갱신한다.

## 제출 전 체크리스트

1. Held/Dropped/SO 상호 참조 연결.
2. 1인칭과 원격 손 위치 확인.
3. 줍기, 사용, 내구도 감소, 던지기, 재줍기 확인.
4. Host/Client에서 원격 손 모델, 사용 VFX, 투척 위치, 내구도 동기화 확인.
5. VFX 하위 Collider 0개를 Inspector에서 확인. Console에 `*_FAILED`, `PHS_ITEM_ACTION_REJECTED`, `event_presentation_collider_count_invalid`, `unsafe_repair_collider`가 없어야 함.
6. `Tools/ParkHanSol/Validate 0715 Integration` 실행.
7. P0 로그 `PHS_P0_RESULT PASS`, `PHS_P0_LOG_HEALTH_OK` 확인.

## 기준 코드

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Items/UtilityItemPrefabData.cs`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Interaction/TempPlayerItemHolder.cs`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Multiplayer/NetworkPlayerItemLifecycle.cs`
- `Assets/06. JoHanYong_PlayerSystem/02. Script/NetworkPlayerCombatController.cs`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/02. Script/Editor/PHS0715IntegrationValidator.cs`
