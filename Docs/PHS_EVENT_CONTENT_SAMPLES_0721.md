# 사건 콘텐츠 샘플 0721

## 목적

팀원이 사건별 로컬 완성품을 만들 때 복제할 기준 Prefab이다. 샘플은 표현·Socket·Reset 구조만 제공한다. 사건 발행, 피해, 성공/실패 확정, Network 상태는 넣지 않는다.

생성 메뉴:

`Tools/ParkHanSol/Build 0721 Incident Event Content Samples`

생성 경로:

`Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/EventSamples/`

## 공통 Hierarchy

```text
PHS_<Event>EventContentSample
  DeliveryContract
    ExpectedAnchor_<LocationKind>
    RequiredTool_<ItemOrMiniGame>
    NoNetworkComponents
  PresentationRoot
    TelegraphSocket
    ActiveSocket
      ReplaceWithOwnVfx
    ResolveSocket
    FailSocket
    CleanupRoot
```

- `TelegraphSocket`: 위험 예고 VFX/Audio/Animation.
- `ActiveSocket`: 사건 진행 중 반복 표현.
- `ResolveSocket`: 성공 표현 1회.
- `FailSocket`: 실패 표현 1회.
- `CleanupRoot`: Stop/Disable/Destroy 대상. 재실행 뒤 잔류 `0`.
- 팀원은 Socket 이름과 Root를 바꾸지 않는다. 자신 표현물만 각 Socket 아래에 넣는다.

## 샘플 목록

| Prefab | 사건 | 실제 Anchor | 대응 |
|---|---|---|---|
| `PHS_FireEventContentSample` | Fire | FireSurface | FireExtinguisher |
| `PHS_PowerFailureEventContentSample` | PowerFailure | Device/PowerCore | WireFix 또는 Repair |
| `PHS_DeviceFailureEventContentSample` | DeviceFailure | Device | Repair |
| `PHS_HullBreachEventContentSample` | HullBreach | HullSurface | Repair |
| `PHS_SteamLeakEventContentSample` | SteamLeak | Pipe/Valve | Repair |
| `PHS_OxygenFailureEventContentSample` | OxygenFailure | Pipe/LifeSupport | Wrench/Repair |
| `PHS_GravityGeneratorFailureEventContentSample` | GravityGeneratorFailure | Device/GravityGenerator | Repair |
| `PHS_EnemyScoutEventContentSample` | EnemyScout | EnemyIngress | PowerSync |
| `PHS_MeteorAttackEventContentSample` | MeteorAttack | HullSurface/ExteriorImpact | Cannon |
| `PHS_EmpAttackEventContentSample` | EmpAttack | Device/Terminal | WireFix |

`EnemySpawn`은 별도 기계 사고가 아니라 EnemyScout 실패 Consequence의 Combat Incident다. EnemyScout 샘플의 EnemyIngress 표현을 기준으로 납품한다.

## Fire 특별 규칙

- Fire Sample Active Socket에는 현재 기준 `PHS_FirePatchPresentation`이 들어 있다.
- 최종 Fire Prefab은 `PHSFireZone.patchPresentationPrefab`에 연결된다.
- Fire Patch/Bounds/Link/Light/RuntimeTarget은 0715 Scene Foundation 소유다. 팀 Prefab에 넣지 않는다.
- Patch당 NetworkObject를 만들지 않는다.
- Fire VFX Prefab에는 Collider, Rigidbody, NetworkObject, NetworkBehaviour, NetworkVariable, RPC를 넣지 않는다.

## 금지

- NetworkObject, NetworkBehaviour, NetworkVariable, RPC, NetworkTransform, 자체 Manager/NetworkList.
- Client가 HP, Item, Ship State, 성공/실패, 사고 Spawn을 직접 바꾸는 코드.
- Particle Collision 피해 판정, Animation Event 성공 확정, client Random.
- Final Scene, Shared Prefab, Default Network Prefabs 변경.

## 납품

사건 1종당 다음만 제출한다.

1. 위 샘플을 복제한 Root Prefab 1개.
2. 사용한 VFX/Audio/Animator/Material과 `.meta`.
3. `BundleId`, revision, Root 경로, Socket 목록, 실제 Anchor 요구사항, Reset 방법을 적은 README.
4. Telegraph -> Active -> Resolve 또는 Fail -> Cleanup -> Reset Sandbox 증거.

박한솔이 Location, Server Authority, Trigger Gateway, Network, 최종 Scene Inspector를 연결한다.
