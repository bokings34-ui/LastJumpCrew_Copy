# LastJumpCrew 팀 프리팹 접수·검수·통합 명세 0718

## 0. 문서 목적

이 문서는 각 팀원이 제작한 프리팹, ScriptableObject, 로컬 표현 자산을 박한솔 통합 라인에 안전하게 전달하기 위한 단일 접수 규격이다.

목표는 다음과 같다.

1. 팀원이 통합 씬과 공용 프리팹을 직접 수정하지 않고도 자기 구역의 게임 투입 가능한 최종 완성 Prefab을 제출하게 한다.
2. 프리팹의 루트 컴포넌트, 자식 소켓, Collider/Layer, ID, 네트워크 경계를 제출 전에 고정한다.
3. 정적 검수, 샌드박스 검증, 최종 통합, 네트워크 검증의 책임을 분리한다.
4. `.meta`와 GUID를 보존하여 프리팹 참조가 병합 과정에서 끊기지 않게 한다.
5. 모든 사고·화재·미니게임·아이템·맵·상점·표현 자산을 동일한 접수 상태 흐름으로 관리한다.

이 문서는 제출 형식을 정한다. 런타임 권한과 게임 규칙은 아래 문서를 우선한다.

- `Docs/LASTJUMPCREW_INTEGRATED_GAMEPLAY_NETWORK_SPEC_0718.md`
- `Docs/PHS_SHIP_INCIDENT_SYSTEM_DETAILED_SPEC_0718.md`
- `Docs/LASTJUMPCREW_TEAM_WORK_ALLOCATION_0718.md`

문서 간 충돌 시 우선순위는 다음과 같다.

1. 최신 사용자 지시
2. 통합 게임플레이·네트워크 명세
3. 함선 사고 상세 명세
4. 본 접수 명세
5. 기존 팀 작업 배분 문서

---

## 1. 최신 담당 기준

### 1.1 담당 배정

| 영역 | 제작 담당 | 최종 통합·네트워크 담당 | 비고 |
|---|---|---|---|
| 온라인 세션, NGO 권한, Run/Ship 영속 상태 | 박한솔 | 박한솔 | 팀원 제출 대상 아님 |
| Player 모듈, 체력, 피격, 넉백 | 조한용 | 박한솔 | 활성 Player Prefab은 박한솔만 수정 |
| Item 사용, 보유 표시, 드롭, 투척 UX | 조한용 | 박한솔 | 서버 소유권·스폰은 박한솔 |
| 내부/외부 Incident 콘텐츠와 규칙 | 노석민 | 박한솔 | 스케줄러·RPC·NetworkObject 제외 |
| Fire 규칙, 피해 계산, 표현 콘텐츠 | 노석민 | 박한솔 | 공간 배치는 탁현재와 공동 계약 |
| 함선 Room/Device 공간, Fire Surface | 탁현재 | 박한솔 | 실제 배치는 05 담당 프리팹에서 제출 |
| Minigame View, 입력, 퍼즐 표현 | 탁현재 | 박한솔 | 세션 권한·결과 확정은 박한솔 |
| Map Environment, Warp Presentation | 탁현재 | 박한솔 | Map Profile과 서버 스폰은 박한솔 |
| Object Animation | 서보경 | 박한솔 | 최신 지시로 변경된 담당 |
| Shop/Catalog/Display 신규 제작 | 역할 담당자 `[확인 필요]` | 박한솔 | 기존 03 경제 자산 소유와 신규 담당을 분리 |
| 수치 밸런스 최종 승인 | 박한솔/사용자 | 박한솔 | 팀원은 제안값과 근거를 제출 |
| 공용 UI 최종 조립 | 각 도메인 View 담당 | 박한솔 | 공용 HUD/설정 화면 직접 수정 금지 |
| Audio/VFX | 각 도메인 표현 담당 | 박한솔 | 아래 12장 상세 배정 적용 |

### 1.2 서보경 담당 변경 처리

- 이 문서부터 서보경의 신규 주 담당은 `Object Animation`이다.
- 기존 `Assets/03. SeoBoGyeong_Game Economy/` 안의 경제 자산은 소유권과 변경 이력을 보존한다.
- 기존 경제 자산을 보유하고 있다는 이유만으로 신규 Shop/Catalog 구현까지 자동 배정하지 않는다.
- 신규 Shop/Catalog/Display 제작 담당자는 `[확인 필요]`로 둔다.
- 가격, 보상, 재고, 확률 등 수치 밸런스는 담당자가 제안할 수 있으나 박한솔/사용자 승인 전에는 최종값으로 취급하지 않는다.

### 1.3 최종 완성품 납품 원칙

팀원이 제출하는 것은 코드 조각, 구성표, 미완성 View가 아니다. 자기 담당 범위 안에서는 바로 게임에 넣을 수 있는 `GameReady Prefab Bundle`이어야 한다.

완성품에 포함되는 것:

- 대표 Root Prefab 1개 또는 명세에 적은 소수의 완결된 Prefab 세트
- 필요한 ScriptableObject/Data와 안정적인 ID
- 모든 시각 Mesh, Material, Animator Controller, Clip, VFX, Audio
- 모든 내부 Child Socket, Collider, Layer, Rigidbody와 Inspector 참조
- 시작 → 작동 → 성공/실패/취소 → Cleanup/Reset 전체 로컬 동작
- 재사용과 Pool 복귀 동작
- 실제 완성품을 그대로 실행하는 Sandbox Scene 또는 Test Driver
- `.meta`, Manifest, README, 변경 내역, 정적/실행 증거

팀원이 비워둘 수 있는 것은 Manifest에 선언된 외부 통합 포트뿐이다.

1. 서버 Snapshot/상태 입력 포트
2. 플레이어 요청/결과 제출 출력 포트
3. 최종 Scene Parent/Anchor/Socket
4. 박한솔이 등록할 Catalog/Registry/Network Prefab 항목

이 외의 내부 참조는 `null`이면 안 된다. 박한솔이 자식 오브젝트, Collider, Animator 상태, VFX, Audio, 게임 규칙 또는 누락 Script를 추가해야 하는 제출물은 최종 완성품이 아니다.

박한솔은 완성품을 받은 뒤 다음만 수행한다.

1. 잠금 Scene 또는 canonical Prefab에 배치
2. 선언된 외부 통합 포트 연결
3. `02` 소유 Network Adapter 부착
4. ID/Catalog/Registry/Network Prefab List 등록
5. Host/Client/Late Join 검증

콘텐츠 내부를 고치거나 기능을 대신 완성하는 일은 조립에 포함하지 않는다. 통합 중 완성품 내부 결함이 발견되면 원 담당자에게 같은 `BundleId`의 다음 revision으로 돌려보낸다.

### 1.4 담당자별 최종 완성품

| 담당 | 받아야 하는 최종 완성품 | 내부에서 반드시 끝낼 것 | 박한솔이 연결할 것 |
|---|---|---|---|
| 서보경 | Device/Object Animation GameReady Prefab 세트 | Animator, Clip, Parameter, Telegraph/Active/Resolve/Cleanup, Reset | 서버 상태 → 애니메이션 상태 Adapter와 실제 Device 배치 |
| 노석민 | External/Internal Incident, Fire Content, Enemy GameReady Prefab 세트 | 사건 규칙, Outcome, 로컬 생명주기, Fire 면적/피해 규칙, 적 상태/표현, Cleanup | Incident 명령/예산/RNG, 서버 피해 확정, Network Snapshot |
| 탁현재 | Ship Layout/Room/Device, Fire Surface Graph, Minigame View, Map Environment GameReady Prefab 세트 | 실제 공간 배치, Anchor/Socket, Collider/Layer, 퍼즐 UI·입력·Reset, Map/Warp 표현 | Scene Parent, Incident/Minigame Session Adapter, 서버 Seed/Result |
| 조한용 | Player Combat Module과 Held/Dropped Tool GameReady Prefab 세트 | 공격/피격/넉백 감각, 도구 사용/투척, Animator/VFX/Audio, 로컬 규칙/Reset | 기존 Player NetworkObject, 소유권/Spawn/RPC, 서버 판정 Adapter |
| Shop 담당 `[확인 필요]` | Shop Display/Catalog Presentation GameReady Prefab 세트 | 진열/선택/구매 피드백, 상품 View, Reset | Economy Ledger, Catalog 승인값, 구매/배송 Network Adapter |

---

## 2. 통합 잠금 자산

아래 자산은 팀원이 제출물을 만들기 위해 직접 수정하지 않는다. 최종 변경자는 박한솔이다.

### 2.1 잠금 씬

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/ParkHanSol_LobbyScene.unity`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_Map_ver1.unity`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_ExteriorShopScene.unity`

### 2.2 잠금 프리팹·공용 목록

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab`
- `Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab`
- `Assets/01. MainGame/02. Final_Prefab/Integration0716/PHS_EventRuntimeSystem.prefab`
- `Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/DefaultNetworkPrefabs.asset`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab`

### 2.3 잠금 설정

- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- 공용 인터페이스와 공용 네트워크 계약

팀원은 잠금 자산을 복제하여 “최종본”으로 제출하지 않는다. 자기 담당 폴더에 독립 프리팹, 컴포넌트, SO, 샌드박스 씬을 만들고 Manifest에 최종 통합 목적지를 적는다.

---

## 3. 공통 제출 번들

### 3.1 제출 단위

한 번의 제출은 하나의 `BundleId`로 묶는다.

권장 형식:

```text
<담당자약어>_<도메인>_<YYYYMMDD>_<3자리순번>
```

예:

```text
NSM_FIRE_20260718_001
THJ_MINIGAME_WIREFIX_20260718_001
JHY_ITEM_EXTINGUISHER_20260718_001
SBG_OBJECTANIM_GENERATOR_20260718_001
```

### 3.2 번들 문서 구조

실제 Unity 자산은 담당자 소유 폴더에 그대로 둔다. 접수 문서와 증거만 아래 구조로 묶는다.

```text
Docs/
  Handoffs/
    <담당역할>/
      <BundleId>/
        <BundleId>.manifest.json
        README.md
        CHANGELOG.md
        Evidence/
          Static/
          Sandbox/
          Network/
```

규칙:

- Unity 자산을 번들 폴더에 다시 복사하지 않는다.
- Manifest가 실제 자산 경로와 GUID를 참조한다.
- `README.md`에는 실행 방법과 필요한 Inspector 연결을 적는다.
- `CHANGELOG.md`에는 제출 revision별 변경 이유를 적는다.
- `Evidence/Static`에는 Inspector, Hierarchy, Project 창, Validator 결과를 둔다.
- `Evidence/Sandbox`에는 샌드박스 실행 캡처와 로그를 둔다.
- `Evidence/Network`는 박한솔 통합 후 Host/Client/Late Join 검증 자료를 추가한다.
- 대용량 동영상은 저장소 정책에 맞는 외부 링크를 Manifest에 기록한다. 저장 위치는 `[확인 필요]`다.

### 3.3 실제 자산 제출 위치

| 담당 | 기존 소유 루트 | 신규 세부 폴더 원칙 |
|---|---|---|
| 서보경 | `Assets/03. SeoBoGyeong_Game Economy/` | 기존 `02. Script`, `03. Prefab`, `05. Object` 하위 사용 |
| 노석민 | `Assets/04. NohSeokMin_Game Event/` | 기존 `02_Script`, `03_Prefab`, `04_SO`, `07_UseAssets` 하위 사용 |
| 탁현재 | `Assets/05. TakHyunJae_Map & MiniGame/` | 기존 `01. Scene`, `02. Script`, `03. Prefab`, `04. Material`, `05. Image` 하위 사용 |
| 조한용 | `Assets/06. JoHanYong_PlayerSystem/` | 기존 `01. Scene`, `02. Script`, `03. Prefab`, `04.Assets` 하위 사용 |

현재 없는 신규 세부 폴더 이름은 이 문서에서 제안하되 `[확인 필요]`로 표시한다. 폴더 확정 전에는 기존 자산을 이동하지 않는다.

---

## 4. `.meta`와 GUID 보존 규칙

### 4.1 필수 규칙

1. Unity 자산과 해당 `.meta`는 항상 함께 제출한다.
2. 기존 자산을 삭제 후 재생성하여 GUID를 바꾸지 않는다.
3. 파일 이동과 이름 변경은 Unity Project 창에서 수행한다.
4. 기존 프리팹을 복사한 뒤 활성 프리팹으로 교체하지 않는다.
5. 폴더 `.meta`도 누락하지 않는다.
6. `.meta` 충돌을 “새 GUID 생성”으로 해결하지 않는다.
7. Asset Store 원본은 재임포트하거나 중복 제출하지 않고 참조만 기록한다.
8. Final Prefab, 활성 Player Prefab, Network Prefab List의 GUID는 박한솔 승인 없이 변경하지 않는다.

### 4.2 Manifest GUID 항목

각 자산은 최소 다음을 기록한다.

- `path`
- `guid`
- `role`
- `isNew`
- `replacesGuid`

`replacesGuid`는 교체 승인을 받은 경우만 사용한다. 일반 수정은 기존 GUID를 유지하므로 `null`이다.

### 4.3 GUID 거절 조건

다음 중 하나면 즉시 반려한다.

- `.meta` 누락
- 동일 경로인데 GUID 변경
- 동일 GUID의 중복 파일
- 활성 Player Prefab 복제품 제출
- Final Prefab 복제품 제출
- Network Prefab List를 팀원 제출물에 포함
- Asset Store 자산 전체 재임포트

---

## 5. Manifest 규격

### 5.1 필수 필드

| 필드 | 의미 |
|---|---|
| `schemaVersion` | 현재 `1` |
| `bundleId` | 제출 단위의 고유 ID |
| `revision` | 같은 Bundle의 재제출 번호 |
| `owner` | 이름과 역할 |
| `status` | 접수 상태 |
| `sourceCommit` | 제출 기준 commit SHA 또는 작업 브랜치 SHA |
| `summary` | 한 문장 기능 설명 |
| `assets` | 경로, GUID, 역할, 신규 여부 |
| `rootContract` | 루트 컴포넌트와 필수 자식 |
| `colliderLayerContract` | Collider, Trigger, Layer, LayerMask |
| `ids` | ItemId, MapId, AccidentId 등 |
| `networkContract` | 허용/금지된 네트워크 요소 |
| `inspectorBindings` | 최종 통합 시 연결할 참조 |
| `tests` | 정적/샌드박스/네트워크 증거 |
| `integrationTargets` | 최종 목적지 |
| `knownAmbiguities` | 아직 확정되지 않은 항목 |

### 5.2 Manifest 예시

아래 GUID와 commit 값은 반드시 실제 값으로 교체한다.

```json
{
  "schemaVersion": 1,
  "bundleId": "NSM_FIRE_20260718_001",
  "revision": 1,
  "owner": {
    "name": "NohSeokMin",
    "role": "IncidentFireContent"
  },
  "status": "SUBMITTED",
  "sourceCommit": "REPLACE_WITH_COMMIT_SHA",
  "summary": "CommandRoom 화재 패치 정의와 로컬 표현 프리팹",
  "assets": [
    {
      "role": "entryPrefab",
      "path": "Assets/04. NohSeokMin_Game Event/03_Prefab/Fire/NSM_CommandRoomFireZone.prefab",
      "guid": "REPLACE_WITH_META_GUID",
      "isNew": true,
      "replacesGuid": null
    },
    {
      "role": "definition",
      "path": "Assets/04. NohSeokMin_Game Event/04_SO/Event_Internal/NSM_Fire_CommandRoom.asset",
      "guid": "REPLACE_WITH_META_GUID",
      "isNew": true,
      "replacesGuid": null
    }
  ],
  "rootContract": {
    "requiredComponents": [
      "PHSFireZone"
    ],
    "requiredChildren": [
      "Patch_000",
      "PresentationRoot"
    ]
  },
  "colliderLayerContract": {
    "hazardCollidersAreTriggers": true,
    "damageableLayersAreSerialized": true,
    "createsNewProjectLayer": false
  },
  "ids": {
    "accidentWireId": 1,
    "zoneId": "command_room",
    "patchIds": [
      0,
      1,
      2
    ]
  },
  "networkContract": {
    "containsNetworkObject": false,
    "containsRpc": false,
    "containsNetworkVariable": false,
    "serverAuthorityAssumed": true
  },
  "inspectorBindings": [
    "PHSFireZone.incidentZone",
    "PHSFireZone.fireAccidentAnchor",
    "PHSFireZone.patches",
    "PHSFirePatch.neighbors"
  ],
  "tests": [
    {
      "type": "STATIC",
      "result": "PASS",
      "evidence": "Evidence/Static/fire_zone_inspector.png"
    },
    {
      "type": "SANDBOX",
      "result": "PASS",
      "evidence": "Evidence/Sandbox/fire_spread_sequence.md"
    },
    {
      "type": "NETWORK",
      "result": "PENDING_INTEGRATION",
      "evidence": null
    }
  ],
  "integrationTargets": [
    "Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab",
    "Assets/01. MainGame/02. Final_Prefab/Integration0716/PHS_EventRuntimeSystem.prefab"
  ],
  "knownAmbiguities": []
}
```

---

## 6. 공통 프리팹 계약

### 6.1 루트 원칙

- 루트에는 제출물의 책임을 대표하는 컴포넌트만 둔다.
- 기능과 무관한 `NetworkManager`, `NetworkObject`, `NetworkTransform`, 세션 매니저를 추가하지 않는다.
- 최종 프리팹에 이미 존재하는 `NetworkObject`를 팀원 프리팹에 복제하지 않는다.
- 참조는 Inspector에서 보이게 직렬화한다.
- `Find`, 태그 검색, 전역 싱글턴 검색으로 누락 참조를 런타임에 숨겨 보강하지 않는다.
- 공용 인터페이스가 필요하면 이름을 `I`로 시작한다.
- 공용 인터페이스는 박한솔이 먼저 뼈대를 확정한 뒤 팀원이 구현한다.
- 프리팹 Variant를 사용할 경우 원본 경로와 Variant 이유를 Manifest에 기록한다.

### 6.2 자식 소켓 원칙

- 외부에서 참조하는 Transform은 명시적인 이름을 가진 자식으로 둔다.
- 소켓 이름은 제출 후 임의로 변경하지 않는다.
- 같은 의미의 소켓을 여러 개 만들지 않는다.
- 필수 소켓이 없는 경우 빈 Transform을 런타임 생성하지 않는다.
- 기존 자식 이름과 신규 표준 이름이 다르면 Manifest에 `sourceSocket → integrationSocket` 매핑을 기록한다.

### 6.3 Collider와 Layer

현재 프로젝트에서 접수 계약에 사용하는 Layer는 다음과 같다.

| Layer | 번호 | 용도 |
|---|---:|---|
| `Default` | 0 | 일반 비상호작용 오브젝트 |
| `Interactable` | 3 | 플레이어 상호작용 탐지 대상 |
| `UI` | 5 | UI |
| `Player` | 6 | 플레이어 본체 |
| `Enemy` | 7 | 적 본체 |
| `NoPlayerInteract` | 8 | 상호작용 탐지에서 제외할 Trigger/표현 보조 |
| `ShipWall` | 9 | 함선 충돌 벽·구조물 |

규칙:

- 신규 Layer를 팀원이 임의 추가하지 않는다.
- 상호작용 대상 Collider는 `Interactable`을 기본으로 한다.
- 상점 Sell/Checkout 같은 서버 판정용 Trigger는 기존 계약처럼 `NoPlayerInteract`를 사용할 수 있다.
- 함선 물리 벽은 `ShipWall`을 사용한다.
- UI는 `UI`를 사용한다.
- 화재 피해 대상은 특정 신규 Layer를 강제하지 않고 `damageableLayers` 직렬화 LayerMask로 받는다.
- Trigger/비 Trigger 여부, Rigidbody 소유 위치, 충돌 의도를 Manifest에 기록한다.
- 표현용 Particle/VFX 자식에는 불필요한 Collider를 두지 않는다.

### 6.4 ID 원칙

- `ItemId`: `lower_snake_case`, UTF-8 64 bytes 이하
- `MapId`: 8000–8999, 현재 기준 8001–8004
- 내부 사고 Wire ID: 1–7
- 외부 사건 기존 범위: 720x
- Fire Patch ID: Zone 안에서 유일한 `ushort`
- Zone/Anchor/Device ID: 안정적인 `lower_snake_case`
- 표시 이름과 네트워크/저장 ID를 분리한다.
- ID 변경은 신규 콘텐츠 추가가 아니라 마이그레이션으로 취급하며 박한솔 승인이 필요하다.

### 6.5 네트워크 공통 금지

신규 팀 제출 Prefab과 Script에는 예외 없이 다음을 넣지 않는다.

- `NetworkManager`
- 독립 `NetworkObject`
- 독립 `NetworkTransform`
- `NetworkBehaviour`
- `NetworkVariable`, `NetworkList`
- Network Prefab List 수정
- `ServerRpc`/`ClientRpc`
- NGO Singleton 또는 Transport 직접 조회
- 클라이언트의 체력·지갑·재고·보상 확정
- 클라이언트의 사고 발생·종료 확정
- 클라이언트의 아이템 소유권 변경
- Fire Patch별 NetworkObject
- Audio/VFX별 NetworkObject
- 씬 로드와 Run Phase 변경

네트워크가 필요한 동작은 `I`로 시작하는 공용 입력/출력 계약과 순수 데이터로만 노출한다. 팀 Sandbox에서는 Local Test Driver가 그 계약을 구동한다. 실제 `NetworkBehaviour`, RPC, Snapshot, Spawn/Despawn, Ownership 코드는 박한솔이 `02` Network Adapter에 작성하고 최종 Prefab에 부착한다.

기존 팀 폴더에 이미 있는 Network Script는 자동 삭제하거나 재작성하지 않는다. 다만 신규 최종 완성품 Bundle에는 포함하지 않으며, 필요한 순수 도메인/View를 분리해 제출한다.

---

## 7. 카테고리별 접수 요약

| 카테고리 | 제작 담당 | 제출 루트 | 박한솔 최종 목적지 |
|---|---|---|---|
| Player Module | 조한용 | `Assets/06. JoHanYong_PlayerSystem/` | 활성 `PHS_CuteWhiteGhost_Player.prefab` |
| Ship/Room/Device | 탁현재 | `Assets/05. TakHyunJae_Map & MiniGame/` | `PHS_ShipRuntime.prefab`, 통합 Map Scene |
| Incident | 노석민 | `Assets/04. NohSeokMin_Game Event/` | `PHS_EventRuntimeSystem.prefab`, `PHS_ShipRuntime.prefab` |
| Fire | 노석민 + 탁현재 | 04 규칙/표현, 05 공간/표면 | `PHS_ShipRuntime.prefab` |
| Minigame View | 탁현재 | `Assets/05. TakHyunJae_Map & MiniGame/` | 통합 Terminal/Device와 HUD |
| Item Held/Dropped/Data | 조한용 + 박한솔 | 06 기능 제출, 02 최종 데이터 | 활성 Player, canonical Item Prefab |
| Map Environment/Profile | 탁현재 + 박한솔 | 05 환경, 02 Profile | `PHS_Map_ver1.unity` |
| Shop/Catalog/Display | 담당자 `[확인 필요]` | 기존 03 경제 자산 또는 확정 담당 폴더 | Exterior Shop, Shop Catalog |
| Object Animation | 서보경 | `Assets/03. SeoBoGyeong_Game Economy/` | 각 Device/Shop/Ship/Map 프리팹 |
| UI | 각 도메인 View 담당 | 담당자 소유 폴더 | 공용 HUD와 통합 씬 |
| Audio/VFX | 각 도메인 표현 담당 | 담당자 소유 폴더 | 각 로컬 Presentation Root |

---

## 8. Player Module 접수 규격

### 8.1 담당과 경로

- 제작: 조한용
- 소스: `Assets/06. JoHanYong_PlayerSystem/02. Script/`
- 제출 프리팹 제안: `Assets/06. JoHanYong_PlayerSystem/03. Prefab/PlayerModules/` `[확인 필요]`
- 샌드박스: `Assets/06. JoHanYong_PlayerSystem/01. Scene/Tset/`
- 최종 통합: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab`

### 8.2 루트 컴포넌트

제출 프리팹 루트는 다음만 가진다.

- `Transform`
- 해당 기능 컴포넌트
- 해당 기능에 필요한 로컬 표현 컴포넌트

새 범용 Wrapper를 접수용으로 만들 필요는 없다. Manifest의 `rootContract.requiredComponents`에 최종 Player Root에 붙일 컴포넌트 순서를 기록한다.

기존 활성 Player에 이미 있는 다음 종류의 컴포넌트를 제출 프리팹에 중복하지 않는다.

- `NetworkObject`
- `NetworkTransform`
- Player 이동 Controller
- Player Life State
- 공용 Interaction Scanner
- 공용 Item Record/Lifecycle

### 8.3 필수 소켓 매핑

기능에 필요한 것만 Manifest에 매핑한다.

- `FirstPersonCameraRoot`
- `BatteryThrowOrigin`
- `GeneralThrowOrigin`
- `ExtinguisherSprayOrigin`
- `WrenchAttackPoint`
- `TempHoldPoint`
- `CustomizationHeadSlot`
- `CustomizationBackSlot`
- `GrappleRig`
- `RopeOrigin`
- `AimMarker`
- `HookVisual`

활성 Player의 기존 소켓 이름을 바꾸지 않는다.

### 8.4 Collider/Layer

- Player 본체 Collider: `Player`
- 공격/사용 판정은 별도 직렬화 LayerMask 사용
- 로컬 시각 보조 Collider는 기본적으로 제거
- 기능 Trigger가 필요하면 목적과 충돌 행렬을 Manifest에 기록

### 8.5 ID와 네트워크

허용:

- 순수 입력/출력 인터페이스와 요청 데이터
- Local Test Driver
- 로컬 입력, 카메라, 애니메이션, 로컬 피드백

금지:

- Player Prefab 복제품 활성화
- 새 Player `NetworkObject`
- `NetworkBehaviour`, RPC, NetworkVariable
- 클라이언트 체력/피격/아이템 소유 확정
- 입력만으로 직접 서버 상태를 덮어쓰기
- 씬 검색으로 공용 컴포넌트 자동 생성

아이템 관련 모듈이면 `ItemId`, `requestSequence`, `revision`, 거리, 쿨다운, 사용 가능 Phase 검증 항목을 함께 제출한다.

### 8.6 필수 증거

- Root Inspector 전체 캡처
- 사용 소켓 연결 캡처
- 로컬 샌드박스에서 사용/피격/넉백/투척 중 해당 기능의 시작과 종료
- 비정상 거리, 잘못된 ItemId, 연속 요청 거절 기대 결과
- 최종 통합 후 Host/Client에서 한 번만 적용되는지 박한솔 검증

---

## 9. Ship/Room/Device 접수 규격

### 9.1 담당과 경로

- 공간/배치: 탁현재
- 사고 규칙 입력: 노석민
- 오브젝트 애니메이션: 서보경
- 제출 프리팹 제안: `Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/ShipLayout/` `[확인 필요]`
- 최종 통합: `PHS_ShipRuntime.prefab`, `PHS_Map_ver1.unity`

### 9.2 루트 컴포넌트

함선 사고 배치 루트:

- `PHSShipIncidentLayout`

Zone 루트:

- `PHSShipIncidentZone`
- Zone Bounds용 `Collider`

사고 발생점:

- `PHSShipAccidentAnchor`

실제 타입이 아직 공용 스켈레톤에 없으면 팀원이 동명 임시 타입을 만들지 않는다. 공용 타입 생성 완료를 기다리거나 순수 Transform 배치 프리팹으로 제출하고 Manifest에 예정 컴포넌트를 기록한다.

### 9.3 권장 계층

```text
PHS_IncidentLayout
  Zone_CommandRoom
  Zone_Bridge
  Zone_MainHall
  Zone_AftCorridor
  Zone_EntryWingA
  Zone_EntryWingB
```

각 Zone에는 다음 자식 또는 참조가 필요하다.

- `AccidentAnchors`
- `FireZone`
- `RepairAnchors`
- `AlarmPresentationRoot`
- 실제 Device Root
- 인접 Zone 참조

Anchor는 빈 좌표가 아니라 실제 장치, 벽 패널, 파이프, 가연성 표면의 자식이어야 한다.

### 9.4 Collider/Layer

- Zone Bounds: Trigger, 상호작용 대상 아님
- 벽/바닥 물리: `ShipWall`
- Repair Interactable: `Interactable`
- 표현/판정 보조 Trigger: `NoPlayerInteract`
- Device 물리 Collider와 Repair Trigger를 한 Collider로 겸용하지 않는다.
- Anchor는 Zone Bounds 안에 있어야 하며 예외는 Manifest에 사유 기록

### 9.5 ID

필수:

- 고유 `zoneId`
- 고유 `anchorId`
- `primaryModule`
- 지원 `accidentWireId`
- `adjacentZones`

권장 안정 ID:

- `power_core`
- `engine_device`
- `oxygen_generator`
- `gravity_generator`
- 신규 Hull/Steam/Fire ID는 `lower_snake_case`

### 9.6 네트워크

허용:

- 순수 배치 데이터
- 로컬 Presentation Root
- 직렬화 참조

금지:

- Zone/Anchor별 `NetworkObject`
- Zone 자체 사고 스케줄러
- 클라이언트 사고 확정
- NetworkVariable로 장치 상태 중복 소유
- 런타임 `Find`로 누락 Device 연결

### 9.7 필수 증거

- 전체 Hierarchy
- 각 Zone Bounds와 Anchor 위치 Gizmo
- Zone ID, Anchor ID 중복 검사
- Anchor가 실제 Device/Wall/Pipe/Surface 아래에 있는 캡처
- 인접 Zone의 null/self/duplicate 없음
- 모든 Presentation Root가 Anchor 5m 이내
- 샌드박스에서 Activate → Repair → Deactivate가 원위치로 복귀

---

## 10. Incident 접수 규격

### 10.1 담당과 경로

- 콘텐츠/규칙: 노석민
- 제출:
  - `Assets/04. NohSeokMin_Game Event/02_Script/Event/`
  - `Assets/04. NohSeokMin_Game Event/03_Prefab/`
  - `Assets/04. NohSeokMin_Game Event/04_SO/Event_Internal/`
  - `Assets/04. NohSeokMin_Game Event/04_SO/Event_External/`
- 샌드박스:
  - `Assets/04. NohSeokMin_Game Event/01_Scene/Scene_Test_Seokmin/`
  - `Assets/04. NohSeokMin_Game Event/01_Scene/0710_Map_ver1_Test_Seokmin/`
- 최종 통합:
  - `PHS_EventRuntimeSystem.prefab`
  - `PHS_ShipRuntime.prefab`

### 10.2 사고 ID

| Wire ID | 사고 |
|---:|---|
| 1 | Fire |
| 2 | PowerFailure |
| 3 | DeviceFailure |
| 4 | HullBreach |
| 5 | SteamLeak |
| 6 | OxygenFailure |
| 7 | GravityGeneratorFailure |

외부 사건은 기존 720x 계약을 사용한다.

- 7201: EnemyScout → PowerSync → EnemySpawn
- 7202: Meteor → Cannon → HullBreach
- 7203: EMP → WireFix → PowerFailure

### 10.3 루트 컴포넌트와 소켓

Incident 제출물은 둘 중 하나다.

1. 순수 Definition SO
2. 로컬 Presentation Prefab

Presentation Prefab 루트:

- 사고 표현 View
- 실제 공용 인터페이스 구현 컴포넌트
- 인터페이스 신규 작성 시 이름은 `I`로 시작
- 실제 공용 타입 이름은 박한솔 스켈레톤 기준 `[확인 필요]`

필수 자식:

- `PresentationRoot`
- `VfxRoot`
- `AudioRoot`
- `LightRoot` 또는 해당 사고에 불필요하다는 명시
- `HudMarkerSocket` 또는 해당 사고에 불필요하다는 명시

표현 View는 최소 다음 생명주기를 지원한다.

- `Activate`
- 상태 Snapshot 적용
- `Deactivate`
- 재활성화 전 Reset

### 10.4 Collider/Layer

- 사고 피해/수리 판정 Collider는 표현 Prefab과 분리
- Repair Trigger는 `Interactable`
- 피해 Trigger는 `NoPlayerInteract` 또는 전용 직렬화 LayerMask 계약 사용
- VFX 자식은 Collider 없음

### 10.5 네트워크

허용:

- 순수 사고 정의
- 서버 계산에 사용할 무상태 계산 함수
- Snapshot을 받아 재생하는 로컬 View

금지:

- `GameEventManager`를 최종 권한으로 제출
- `EventSchedulerBox`를 최종 권한으로 제출
- 독립 Scheduler
- `NetworkManager`
- Incident별 NetworkObject
- 클라이언트 데미지 적용
- 클라이언트 사건 발생/종료 확정
- Run Phase 또는 Scene Load 변경

### 10.6 필수 증거

- 1–7 사고 Definition ID 중복 없음
- 지원 Anchor 종류와 Accident ID 호환표
- Activate → Active → Repair/Resolve → Deactivate 캡처
- 같은 Prefab을 두 번 재생해 상태가 누적되지 않음
- 실제 피해·수리 로직 없이도 Presentation만 독립 재생 가능
- 최종 Host/Client에서 사고 목록, Zone, HUD 표시가 일치
- Late Join 시 Snapshot으로 현재 표현 복원

---

## 11. Fire 접수 규격

### 11.1 공동 담당 분리

노석민 제출:

- 화재 Definition
- 확산 확률과 순수 계산
- 피해 규칙
- Fire Presentation 콘텐츠
- 관련 SO

탁현재 제출:

- 실제 가연성 Surface
- Zone/Anchor/Patch 공간 배치
- Patch 인접 연결
- Doorway를 통한 Zone 간 연결

박한솔 통합:

- 서버 Tick
- 화재 시작/종료 권한
- Snapshot 복제
- 피해 적용
- Late Join 재구성

### 11.2 제출 경로

- 규칙/표현:
  - `Assets/04. NohSeokMin_Game Event/02_Script/Fire/`
  - `Assets/04. NohSeokMin_Game Event/03_Prefab/Fire/`
  - `Assets/04. NohSeokMin_Game Event/07_UseAssets/FireEffect/`
- 공간/표면 제안:
  - `Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/ShipLayout/Fire/` `[확인 필요]`
- 최종 통합:
  - `Assets/01. MainGame/02. Final_Prefab/PHS_ShipRuntime.prefab`

### 11.3 루트 컴포넌트

Zone:

- `PHSFireZone`

Patch:

- `PHSFirePatch`
- 면적을 가진 `Collider`

연결:

- `PHSFirePatchLink`

필수 Zone 참조:

- `incidentZone`
- `fireAccidentAnchor`
- `patches`
- `damageableLayers`

필수 Patch 데이터:

- `patchId`
- `hazardBounds`
- `presentationRoot`
- `flammability`
- `damageMultiplier`
- `neighbors`
- `visualSockets`

### 11.4 자식 소켓

```text
Patch_<id>
  HazardBounds
  PresentationRoot
    FlameSockets
    SmokeSockets
    LightRoot
    AudioRoot
```

불꽃은 한 점에만 붙이지 않는다. 하나의 Patch는 실제 면적 Collider와 여러 Visual Socket을 가진다. 강도에 따라 켜지는 Socket 조합을 달리하여 같은 점에서만 반복 재생되는 모습을 피한다.

### 11.5 확산·피해 제한

- 한 Zone의 활성 Patch 최대 8
- 확산 Tick 기본 2.5초
- Tick당 최대 2개 인접 후보 시도
- Tick당 최대 1개 신규 점화
- 이웃 Link로만 확산
- Zone 간 연결은 명시적 Doorway Link만 허용
- 피해 Tick 1초
- 같은 Tick에서 같은 대상 중복 피해 금지
- Patch별 독립 NetworkObject 금지
- VFX/Audio는 미리 배치하고 로컬로 켜고 끈다.

### 11.6 Collider/Layer

- `hazardBounds`: Trigger
- 피해 대상: `damageableLayers` 직렬화
- 표면 물리 Collider: 필요 시 `ShipWall`
- Presentation 자식: Collider 없음
- Patch Collider가 점 크기이면 반려
- 서로 완전히 겹치는 Patch는 의도와 우선순위를 Manifest에 기록

### 11.7 네트워크

허용:

- Zone/Patch ID와 인접 그래프
- 순수 확산 가중치 계산
- Snapshot 기반 로컬 강도 표현

금지:

- Patch `NetworkObject`
- Patch별 RPC
- 클라이언트 랜덤 확산
- 클라이언트 피해 적용
- VFX 시작 시 서버 상태 변경

### 11.8 필수 증거

- Patch 면적 Collider Gizmo
- Patch ID와 Link 검증표
- null/self/duplicate Link 없음
- Cross-Zone Link는 Doorway 근거 캡처
- 3회 실행에서 확산 경로가 완전히 동일하지 않되 최대 제한 준수
- 범위 안 대상만 1초 주기로 피해
- 같은 대상 중복 Collider가 있어도 Tick당 한 번만 피해
- 강도별 Visual Socket 변화
- 점화 → 확산 → 진압 → 연기/조명/오디오 정리
- 8 Patch 상한과 성능 증거

---

## 12. Minigame View 접수 규격

### 12.1 담당과 경로

- 제작: 탁현재
- 스크립트: `Assets/05. TakHyunJae_Map & MiniGame/02. Script/01. MiniGame/`
- 프리팹 제안: `Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/MiniGame/` `[확인 필요]`
- 샌드박스: `Assets/05. TakHyunJae_Map & MiniGame/01. Scene/MiniGame/`
- 최종 통합: 실제 Terminal/Device, 공용 HUD

### 12.2 지원 타입

- `Cannon`
- `PowerSync`
- `WireFix`

`DoorKeypad`는 현재 P0 접수 범위에서 제외한다.

### 12.3 루트 컴포넌트

- `RectTransform` 또는 World Space Device Root
- 해당 Minigame View
- 공용 Minigame View 인터페이스 구현

공용 인터페이스의 실제 이름은 박한솔 스켈레톤 기준 `[확인 필요]`다. 신규 인터페이스가 필요하면 이름은 `I`로 시작한다.

필수 자식:

- `InputRoot`
- `PuzzleRoot`
- `ProgressRoot`
- `ResultRoot`
- `OccupiedRoot`
- `CancelRoot`

World Space Device이면 추가:

- `InteractionSocket`
- `CameraSocket`
- `PresentationRoot`

### 12.4 Collider/Layer

- Device 상호작용 Collider: `Interactable`
- UI: `UI`
- World Space UI가 물리 Raycast를 막지 않도록 불필요한 Collider 제거
- 입력 차단용 투명 Graphic은 의도된 `raycastTarget`만 활성

### 12.5 ID와 네트워크

View가 소비하는 값:

- Minigame Type
- Session 상태
- Progress Snapshot
- 남은 시간
- 성공/실패/취소 결과

허용:

- 로컬 입력 수집
- 퍼즐 로컬 표현
- 결과 요청 전송
- Snapshot 표시

금지:

- Terminal 소유권 확정
- Session nonce 생성
- 만료 시간 확정
- 성공 결과 직접 적용
- 보상 지급
- 사건 종료
- 독립 NetworkObject

### 12.6 필수 증거

- Idle → Occupied → Playing → Success/Fail → Reset
- 두 번째 플레이어 입력 시 Busy 표현
- Cancel/거리 이탈/시간 만료 후 입력 잠금 해제
- 같은 결과를 두 번 표시하지 않음
- 1920×1080, 1280×720에서 잘림 없음
- 스크롤/드래그 입력이 배경 UI로 새지 않음
- 최종 Host/Client에서 세션 소유자와 Progress가 일치

---

## 13. Item Held/Dropped/Data 접수 규격

### 13.1 담당과 경로

- 사용/투척/도구 UX: 조한용
- NGO Spawn/Ownership/Canonical Prefab: 박한솔
- 구매 가격/재고/OfferId: Shop 담당자 `[확인 필요]`
- 지갑/보상 수치 승인: 박한솔/사용자

조한용 제출:

- `Assets/06. JoHanYong_PlayerSystem/02. Script/Item/`
- `Assets/06. JoHanYong_PlayerSystem/03. Prefab/` 하위 기능 프리팹 `[확인 필요]`

박한솔 최종 통합:

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Props/Prefabs/Items/Held/`
- 같은 Items 계층의 canonical Dropped Prefab
- 활성 Player Prefab
- Network Prefab List

### 13.2 Data 책임

| 데이터 | 소유 계약 |
|---|---|
| `ItemId` | `UtilityItemPrefabData` |
| Held Prefab | `UtilityItemPrefabData` |
| Dropped Prefab | `UtilityItemPrefabData` |
| 판매 가격 | `UtilityItemPrefabData` |
| 구매 가격 | `ShopProductData` |
| 재고 | `ShopProductData` |
| `OfferId` | `ShopProductData` |
| 지갑/보상 | Economy/Run 권한 |

같은 값을 여러 SO에 중복 저장하지 않는다.

### 13.3 Held Prefab

루트:

- `Transform`
- 로컬 Held View/Use View

필수 매핑:

- Player `TempHoldPoint`
- 도구별 사용 소켓
  - `BatteryThrowOrigin`
  - `GeneralThrowOrigin`
  - `ExtinguisherSprayOrigin`
  - `WrenchAttackPoint`

금지:

- `NetworkObject`
- `NetworkTransform`
- 월드용 Rigidbody
- 월드 충돌 Collider
- `ThrownItemImpact`
- 독립 소유권 로직

### 13.4 Dropped Prefab

팀원 Source 제출 루트:

- Item 기능 컴포넌트
- 상호작용 Collider
- 물리 요구사항 명세
- 시각 Root

최종 canonical Dropped Prefab에는 박한솔이 다음 네트워크 요소를 조립한다.

- `NetworkObject`
- `NetworkTransform`
- Network Item Physics Authority
- Spawn/Despawn/Ownership 계약

팀원 Source Prefab이 이를 중복 보유하면 반려한다.

### 13.5 Collider/Layer

- Dropped 상호작용 Collider: `Interactable`
- 물리 Collider: Trigger 아님
- Held View Collider: 제거 또는 비활성
- 투척 충돌 LayerMask: 직렬화
- 플레이어 자기 충돌 예외와 Ignore 시간은 Manifest에 기록

### 13.6 ID와 네트워크

- `ItemId`는 `lower_snake_case`, 64 bytes 이하
- Held/Dropped/Data의 `ItemId`가 완전히 같아야 한다.
- 서버는 Owner, Scene, 거리, LOS, Catalog, Revision, Request Sequence, Phase, Instance를 검증한다.

허용:

- 로컬 Held 표현
- 순수 입력 요청 데이터
- 순수 사용/충돌 계산
- Local Test Driver

금지:

- `NetworkBehaviour`, RPC, NetworkVariable
- 클라이언트 World Item Despawn
- 클라이언트 Held 확정
- 클라이언트 판매/구매 확정
- Network Prefab List 수정
- 활성 Player Prefab 직접 변경

### 13.7 필수 증거

- Data → Held → Dropped GUID 연결표
- Pickup → Held → Use → Drop → Throw → Repickup
- Extinguisher/Wrench/Battery 도구별 소켓 연결
- 잘못된 ItemId, 거리, Revision 요청 거절
- 투척 후 자기 충돌과 중복 Impact 없음
- Host/Client/Late Join에서 Held와 Dropped 상태 일치

---

## 14. Map Environment/Profile 접수 규격

### 14.1 담당과 경로

- 환경 프리팹/표현: 탁현재
- Map Profile/서버 Spawn/Scene Context: 박한솔
- 제출 제안:
  - `Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/MapEnvironment/` `[확인 필요]`
  - `Assets/05. TakHyunJae_Map & MiniGame/04. Material/`
  - `Assets/05. TakHyunJae_Map & MiniGame/05. Image/`
- 기존 Placeholder:
  - `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Maps/PHS_MapEnvironmentPlaceholder.prefab`
- 최종 통합:
  - `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_Map_ver1.unity`
  - 박한솔 Map Profile Data

### 14.2 루트와 소켓

환경 프리팹 루트:

- `Transform`
- 환경 표현 컴포넌트
- 명시적 Profile Binding용 참조

필수 자식 또는 Manifest의 `notApplicable`:

- `EnvironmentRoot`
- `LightingRoot`
- `VfxRoot`
- `AudioRoot`
- `HazardVolumes`
- `DebrisSpawnVolumes`
- `ShipExteriorSocket`
- `WarpPresentationRoot`

실제 공용 Binding 컴포넌트 이름은 `[확인 필요]`다.

### 14.3 Collider/Layer

- 비상호작용 환경: `Default`
- 함선/고정 구조 충돌: `ShipWall`
- Hazard Volume: Trigger, 직렬화 LayerMask
- Spawn Volume: Trigger, 런타임 물리 충돌 없음
- 장식 Mesh Collider는 필요 근거가 없으면 제거

### 14.4 ID와 네트워크

- Map ID 범위: 8000–8999
- 현재 목표: 8001–8004
- 하나의 공유 `PHS_Map_ver1`에서 Profile로 환경을 교체한다.

허용:

- 로컬 환경/조명/VFX/Audio
- 서버가 전달한 MapId/Profile Snapshot 소비
- 명시적 Spawn Volume

금지:

- Environment Prefab의 NetworkManager
- 로컬 랜덤 Debris Spawn
- Scene Load
- Run Phase 변경
- Map별 복제 통합 씬 제출

### 14.5 필수 증거

- MapId와 Environment Prefab 매핑
- Environment Root 원점/Scale/Bounds
- Spawn/Hazard Volume Gizmo
- Profile 교체 시 이전 환경 완전 비활성
- Warp In/Active/Warp Out 후 잔여 VFX/Audio 없음
- 8001–8004 순환 시 참조 누락 없음
- Host/Client에서 동일 MapId와 환경 표시

---

## 15. Shop/Catalog/Display 접수 규격

### 15.1 담당과 경로

- 신규 Shop/Catalog/Display 담당: `[확인 필요]`
- 기존 경제 자산:
  - `Assets/03. SeoBoGyeong_Game Economy/02. Script/Economy/`
  - `Assets/03. SeoBoGyeong_Game Economy/03. Prefab/`
  - `Assets/03. SeoBoGyeong_Game Economy/04. Data/`
  - `Assets/03. SeoBoGyeong_Game Economy/05. Object/`
- 최종 통합:
  - `PHS_ExteriorShopScene.unity`
  - 박한솔 Shop Catalog Data
  - `PHS_ShopCheckoutCounter.prefab`
  - `PHS_DebrisSellStation.prefab`

담당 확정 전 기존 서보경 경제 자산을 다른 폴더로 이동하지 않는다.

### 15.2 루트 컴포넌트와 소켓

Display 제출 프리팹 루트:

- Display View
- Catalog Entry Binding View
- 가격/품절 표시 View

공용 타입 실제 이름은 `[확인 필요]`다. 신규 인터페이스가 필요하면 `IShopDisplayView`처럼 `I`로 시작한다.

필수 자식:

- `DisplayRoot`
- `ItemVisualSocket`
- `PriceRoot`
- `StockRoot`
- `SoldOutRoot`
- `InteractionSocket`

Checkout/Sell Station은 기존 최종 프리팹을 직접 수정하지 않고, 교체할 View/모듈 프리팹을 제출한다.

### 15.3 Collider/Layer

- 플레이어 직접 상호작용: `Interactable`
- Checkout/Sell 판정 Trigger: 기존 계약대로 `NoPlayerInteract`
- Display 장식 Collider: 제거
- UI: `UI`

### 15.4 ID와 데이터

- `OfferId`: Catalog 안에서 유일
- `ItemId`: `UtilityItemPrefabData`와 동일
- 구매 가격/재고: `ShopProductData`
- 판매 가격: `UtilityItemPrefabData`
- 지갑/보상: Shop Display가 소유하지 않음

가격, 재고, 보상 수치는 다음 상태를 구분한다.

- `PROPOSED`: 담당자 제안
- `APPROVED`: 박한솔/사용자 승인

`PROPOSED` 값으로 최종 ACCEPTED 처리하지 않는다.

### 15.5 네트워크

허용:

- Catalog Snapshot 표시
- 구매/판매 요청 생성
- 가격/재고/품절 로컬 표현

금지:

- 클라이언트 지갑 변경
- 클라이언트 재고 차감
- Display별 NetworkObject
- 자체 NetworkList
- Scene Load
- 승인 전 밸런스 값을 런타임 기준으로 고정

### 15.6 필수 증거

- OfferId/ItemId/Catalog 연결표
- Ready/OutOfStock/InsufficientFunds/Success/Failure 표현
- 연속 클릭 시 중복 구매 요청 방지
- 가격 변경 시 Display 자동 갱신
- 1920×1080과 1280×720 UI
- Host/Client에서 가격·재고·지갑 결과 일치
- 수치 승인 기록

---

## 16. Object Animation 접수 규격

### 16.1 담당과 경로

- 제작: 서보경
- 원본 오브젝트: `Assets/03. SeoBoGyeong_Game Economy/05. Object/`
- 스크립트 제안: `Assets/03. SeoBoGyeong_Game Economy/02. Script/ObjectAnimation/` `[확인 필요]`
- 프리팹 제안: `Assets/03. SeoBoGyeong_Game Economy/03. Prefab/ObjectAnimation/` `[확인 필요]`
- 최종 통합: Ship Device, Incident Presentation, Shop Display, Map Environment의 로컬 자식

### 16.2 루트 컴포넌트

- `Animator`
- Object Animation View
- 필요 시 Reset/State 적용 컴포넌트

공용 계약이 필요하면 박한솔이 `IObjectAnimationView` 형태의 인터페이스를 먼저 확정한다. 서보경이 별도 중복 인터페이스를 만들지 않는다.

필수 자식:

- `ModelRoot`
- `AnimationRoot`
- `PresentationRoot`
- `VfxRoot` 또는 `notApplicable`
- `AudioRoot` 또는 `notApplicable`
- 도메인별 기계 결합 소켓

### 16.3 상태 계약

최소 상태:

- `Dormant`
- `Telegraph`
- `Active`
- `Recover`
- `Disabled`

모든 표현은 눈에 보이게 다음 생명주기를 가져야 한다.

```text
나타남/예고 → 실제 동작 → 종료/정리
```

단순 색상 변경이나 숫자 변경만으로 “작동 애니메이션 완료”로 보지 않는다.

### 16.4 Animation Event 제한

Animation Event에서 허용:

- 로컬 Audio Cue
- 로컬 VFX Cue
- 로컬 View Callback

Animation Event에서 금지:

- 피해 적용
- 아이템 지급
- 지갑 변경
- 사건 시작/종료
- Network Spawn
- RPC
- Scene Load

게임플레이 결과는 서버 상태가 먼저 확정하고 애니메이션은 그 Snapshot을 표현한다.

### 16.5 Collider/Layer

- 애니메이션 자식이 움직여도 게임플레이 Collider 소유권은 Device Root에 둔다.
- 장식 Mesh Collider는 제거한다.
- 움직이는 문/기계의 실제 Collider 동기화가 필요하면 Manifest에 상태별 활성 조건을 기록한다.
- 표현 Root가 Player Interaction Layer를 가로채지 않게 한다.

### 16.6 SO와 ID

- 애니메이션 자체는 신규 네트워크 Wire ID를 만들지 않는다.
- 재사용 Profile이 필요하면 로컬 전용 `animationProfileId`를 `lower_snake_case`로 둔다.
- Profile SO 제안 경로는 `Assets/03. SeoBoGyeong_Game Economy/04. Data/ObjectAnimation/`이며 실제 폴더명은 `[확인 필요]`다.
- `animationProfileId`는 사고, Device, Item, Map의 권한 ID를 대체하지 않는다.
- Manifest에는 `animationProfileId → 통합 대상 Device/Presentation ID` 매핑을 기록한다.

### 16.7 네트워크

허용:

- Snapshot 상태를 Animator Parameter로 변환
- 로컬 시간 보정
- 중간 입장 시 정규화 시간 적용

금지:

- `NetworkObject`
- `NetworkAnimator` 임의 추가
- Animation Event RPC
- 애니메이션 종료를 게임플레이 성공 조건으로 사용

### 16.8 필수 증거

- 상태 전이표
- Animator Controller 캡처
- Dormant → Telegraph → Active → Recover → Dormant
- Active 도중 Disabled 진입
- 두 번 반복 후 위치/회전/Scale Drift 없음
- 비활성화 후 Particle/Light/Audio 잔여 없음
- 중간 상태 Snapshot 적용 시 올바른 시점 표시
- 실제 통합 대상 Device에 붙인 샌드박스 캡처

---

## 17. UI 접수 규격

### 17.1 담당

- Incident/Fire UI 내용: 노석민
- Minigame UI: 탁현재
- Player/Item UI 내용: 조한용
- Shop UI 내용: Shop 담당자 `[확인 필요]`
- Object Animation 연동 UI 표현: 서보경
- 공용 HUD/설정/최종 Canvas 조립: 박한솔

### 17.2 제출 경로

각 담당자 소유 `03. Prefab` 아래 `UI/` 제안 경로를 사용한다. 현재 없는 폴더는 `[확인 필요]`다.

최종 통합:

- `Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab`
- 통합 Lobby/Map/Shop Scene의 기존 Canvas

### 17.3 루트 컴포넌트

일반 패널:

- `RectTransform`
- `CanvasGroup`
- 도메인 View/Presenter 연결용 View

팀 제출 패널에는 다음을 기본적으로 넣지 않는다.

- 새 `EventSystem`
- 새 독립 Screen Space Canvas
- 새 AudioListener
- 세션/지갑/체력 권한 컴포넌트

전체 화면 Root로 승인된 경우만 Canvas를 포함한다.

### 17.4 필수 자식

- `ContentRoot`
- `LoadingRoot`
- `EmptyRoot`
- `ErrorRoot`
- `InputBlocker`
- `CloseButton` 또는 `notApplicable`

스크롤 UI 추가:

- `Viewport`
- `Content`
- `Scrollbar` 또는 `notApplicable`

### 17.5 Layer와 입력

- 모든 UI Graphic: `UI`
- 클릭할 필요 없는 Graphic은 `raycastTarget=false`
- 투명 Image로 전체 화면 입력을 막으면 활성 조건을 명시
- 중첩 ScrollRect는 축과 입력 우선순위를 분리
- Dropdown/ScrollRect가 닫힌 뒤 Raycast 차단 오브젝트를 남기지 않음
- Gamepad/Keyboard Navigation 대상과 기본 선택을 명시

### 17.6 SO와 ID

- UI는 별도 게임플레이 Wire ID를 만들지 않는다.
- 재사용 View 설정 SO가 필요하면 로컬 전용 `viewId`를 `lower_snake_case`로 둔다.
- UI는 원본 `ItemId`, `MapId`, `AccidentId`, `OfferId`, Minigame Type을 읽어 표시만 한다.
- UI용 별칭 ID로 원본 게임플레이 ID를 복제하지 않는다.
- View 설정 SO 경로는 각 담당자 Data/SO 폴더를 사용하며, 정확한 하위 폴더는 `[확인 필요]`다.

### 17.7 네트워크

허용:

- Snapshot 표시
- 로컬 입력 요청
- Pending/Success/Failure 표현

금지:

- UI Button에서 서버 상태 직접 변경
- 지갑/체력/재고/사고 상태 직접 쓰기
- UI 프리팹의 NetworkObject
- UI가 `FindObjectOfType`으로 권한 객체를 자동 생성

### 17.8 필수 증거

- 1920×1080
- 1280×720
- 16:9 최소/최대 Safe Area
- Dropdown 열기/닫기
- Scroll wheel, Drag, Scrollbar가 같은 Content를 정상 제어
- Content가 Viewport보다 작을 때 불필요한 스크롤 이동 없음
- Content가 클 때 끝 항목까지 접근 가능
- 숨긴 패널이 Raycast를 차단하지 않음
- Keyboard/Gamepad Focus가 화면 밖으로 사라지지 않음
- Host/Client Snapshot 표시 일치

---

## 18. Audio/VFX 접수 규격

### 18.1 세부 배정

| 표현 | 제작 담당 |
|---|---|
| Incident/Fire/Enemy Audio·VFX | 노석민 |
| Ship Room/Device/Map/Warp 공간 표현 | 탁현재 |
| Player/Item/Hit/Use 표현 | 조한용 |
| Object Animation 연동 Cue | 서보경 |
| Shop 표현 | Shop 담당자 `[확인 필요]` |
| 최종 Snapshot Binding/Pooling 정책 | 박한솔 |

제출 경로:

- Incident/Fire/Enemy: `Assets/04. NohSeokMin_Game Event/03_Prefab/` 및 `07_UseAssets/`
- Ship/Map/Warp: `Assets/05. TakHyunJae_Map & MiniGame/03. Prefab/`
- Player/Item: `Assets/06. JoHanYong_PlayerSystem/03. Prefab/`
- Object Animation Cue: `Assets/03. SeoBoGyeong_Game Economy/03. Prefab/ObjectAnimation/` `[확인 필요]`
- Shop: 확정 담당자의 소유 Prefab 폴더 `[확인 필요]`

최종 통합:

- Incident/Fire 표현은 `PHS_EventRuntimeSystem.prefab` 또는 `PHS_ShipRuntime.prefab`의 실제 Presentation Root
- Player/Item 표현은 활성 Player와 canonical Held/Dropped Prefab
- Map/Warp 표현은 `PHS_Map_ver1.unity`의 Profile별 Presentation Root
- Shop 표현은 `PHS_ExteriorShopScene.unity`의 Display/Checkout/Sell Presentation Root
- 최종 배치와 Snapshot 연결은 박한솔만 수행

### 18.2 루트 컴포넌트

- Presentation View
- 필요한 `ParticleSystem`, VFX Graph, `AudioSource`, `Light`
- Reset 가능한 생명주기 컴포넌트

필수 자식:

- `VfxRoot`
- `AudioRoot`
- `LightRoot` 또는 `notApplicable`
- 복수 위치 표현이면 명시적 Socket 목록

### 18.3 표현 계약

각 표현은 다음을 지원한다.

- 예고
- 활성
- 강도/진행도 갱신
- 종료
- 강제 취소
- 재사용 전 Reset

3D Audio는 공간 위치, 최소/최대 거리, Loop 여부를 Manifest에 기록한다.

### 18.4 Collider/Layer

- Audio/VFX 자식에는 Collider를 두지 않는다.
- VFX Mesh는 상호작용 Raycast를 가로채지 않는다.
- Light와 Particle의 Culling/Bounds를 확인한다.

### 18.5 SO와 ID

- Audio/VFX는 신규 네트워크 Wire ID를 만들지 않는다.
- 재사용 Presentation Profile이 필요하면 로컬 전용 `presentationId`를 `lower_snake_case`로 둔다.
- `presentationId`는 원본 Accident/Device/Item/Map ID와 Manifest에서 매핑한다.
- Audio Clip, VFX, Light 수치가 SO에 있으면 해당 도메인 담당자의 Data/SO 폴더에 제출한다.
- 실제 SO 하위 폴더명은 `[확인 필요]`다.

### 18.6 네트워크

허용:

- 서버 Snapshot을 받은 로컬 재생
- 미리 배치한 View 활성/비활성
- 로컬 Pool

금지:

- VFX/Audio용 NetworkObject
- Cue별 RPC
- Particle Collision로 서버 피해 확정
- Audio 종료 시 사건 종료 처리

### 18.7 필수 증거

- 나타남 → 작동 → 사라짐 전체 캡처
- 취소 시 즉시 정리
- 반복 재생 시 Audio 중첩 없음
- Loop Audio 종료 확인
- Particle/Light 비활성 확인
- 최대 동시 Incident/Fire 조건에서 프레임과 메모리 증거
- Late Join 시 현재 상태 강도로 시작하고 처음부터 재생하지 않음

---

## 19. 접수 상태 흐름

모든 Bundle은 다음 상태를 순서대로 통과한다.

```text
SUBMITTED
  → STATIC_VALIDATED
  → SANDBOX_PROVEN
  → INTEGRATED
  → NETWORK_VALIDATED
  → ACCEPTED
```

### 19.1 상태 정의

| 상태 | 책임자 | 통과 조건 |
|---|---|---|
| `SUBMITTED` | 제작 담당 | Manifest, 자산, `.meta`, 정적 증거 제출 |
| `STATIC_VALIDATED` | 박한솔 또는 지정 리뷰어 | 컴파일, GUID, Root, Socket, Collider, Layer, ID, 네트워크 금지 요소 검사 |
| `SANDBOX_PROVEN` | 제작 담당 + 리뷰어 | 담당자 샌드박스에서 기능 생명주기와 Reset 증명 |
| `INTEGRATED` | 박한솔 | 잠금 프리팹/씬에 Inspector로 연결하고 Legacy 충돌 제거 |
| `NETWORK_VALIDATED` | 박한솔 | Host/Client/Late Join, 중복 적용, 서버 권한 검증 |
| `ACCEPTED` | 박한솔 + 필요 시 사용자 | 범위 충족, 증거 완료, 수치 승인 완료 |

### 19.2 상태 변경 규칙

- 중간 단계를 건너뛰지 않는다.
- `SUBMITTED`는 작업 중간본이 아니다. 제작 담당 범위에서 GameReady 완성 및 Sandbox 증거까지 끝난 Bundle만 접수한다.
- 제작 담당자는 `SUBMITTED`까지만 직접 설정한다.
- `STATIC_VALIDATED` 이후 자산이 바뀌면 revision을 올리고 영향 단계부터 재검증한다.
- GUID, Root, Network Contract가 바뀌면 `STATIC_VALIDATED`부터 다시 시작한다.
- 표현만 바뀌어도 Sandbox 증거는 다시 제출한다.
- 서버 상태나 ID가 바뀌면 Network 검증 전체를 다시 수행한다.
- `ACCEPTED` 전에는 “최종 통합 완료”라고 부르지 않는다.

### 19.3 Network 검증 최소 조합

- Host + Client 1
- Late Join 1
- 4인 권장 인원
- 최대 8인은 Incident/Fire/Item 동시성 검증 시 수행

Unity Services 프로젝트 연결이 없어 실제 Relay/Lobby 접속이 불가능하면 다음을 분리 기록한다.

- 로컬 NGO Host/Client 결과
- Relay/Lobby 코드·패키지·Inspector 정적 검증
- 실제 서비스 검증 `BLOCKED_EXTERNAL`

---

## 20. 정적 검수 체크리스트

### 20.1 공통

- [ ] Manifest JSON 파싱 성공
- [ ] BundleId와 revision 유효
- [ ] 모든 자산 경로 존재
- [ ] 모든 `.meta` 존재
- [ ] Manifest GUID와 `.meta` GUID 일치
- [ ] 잠금 자산 직접 수정 없음
- [ ] 컴파일 에러 없음
- [ ] Missing Script 없음
- [ ] Root Scale 의도 확인
- [ ] 필수 Root Component 존재
- [ ] 필수 Child Socket 존재
- [ ] Inspector 참조 null 없음
- [ ] 선언한 외부 통합 포트 외 내부 참조 null 0
- [ ] 대표 Prefab만으로 나타남 → 작동 → 종료/취소 → Reset 재현
- [ ] Placeholder/TODO/임시 Cube/통합자 추가 구현 요구 없음
- [ ] Collider Trigger 설정 일치
- [ ] 기존 Layer만 사용
- [ ] ID 형식과 중복 검사 통과
- [ ] 팀원 NetworkObject 없음
- [ ] Network Prefab List 변경 없음
- [ ] 런타임 검색/자동 생성 보강 없음
- [ ] 샌드박스 실행 방법 존재

### 20.2 Incident/Fire 추가

- [ ] Accident ID 1–7 계약 일치
- [ ] Zone/Anchor/Patch ID 유일
- [ ] Anchor와 실제 Device/Surface 연결
- [ ] Fire Patch 면적 Collider
- [ ] Link null/self/duplicate 없음
- [ ] Cross-Zone Link는 Doorway만
- [ ] Legacy Fire Spawn이 최종 권한으로 활성화되지 않음
- [ ] Patch별 NetworkObject 없음

### 20.3 Item 추가

- [ ] ItemId가 Data/Held/Dropped에서 동일
- [ ] Held에 NetworkObject/Rigidbody 없음
- [ ] Dropped Source에 최종 네트워크 컴포넌트 중복 없음
- [ ] 사용 소켓 매핑 존재

### 20.4 UI 추가

- [ ] 중복 EventSystem 없음
- [ ] 불필요한 Canvas 없음
- [ ] 숨은 Graphic의 Raycast 차단 없음
- [ ] ScrollRect Content/Viewport 참조 정상

---

## 21. 반려 사유

반려 시 아래 코드를 Manifest 또는 Review 기록에 남긴다.

| 코드 | 반려 사유 |
|---|---|
| `R01_BUNDLE_INCOMPLETE` | Manifest, README, Evidence 누락 |
| `R02_META_MISSING` | `.meta` 또는 폴더 `.meta` 누락 |
| `R03_GUID_DRIFT` | 승인 없는 GUID 변경/중복 |
| `R04_LOCKED_ASSET_EDIT` | 통합 씬/Final Prefab/활성 Player 직접 수정 |
| `R05_ROOT_CONTRACT` | 루트 컴포넌트 누락 또는 책임 외 컴포넌트 포함 |
| `R06_SOCKET_CONTRACT` | 필수 소켓 누락/이름 변경/매핑 누락 |
| `R07_COLLIDER_LAYER` | Trigger/Layer/LayerMask 계약 위반 |
| `R08_ID_CONTRACT` | ID 형식, 범위, 중복 위반 |
| `R09_NETWORK_AUTHORITY` | 클라이언트 권한 확정 또는 서버 검증 누락 |
| `R10_NETWORK_OBJECT` | 금지된 NetworkObject/NetworkTransform/RPC 포함 |
| `R11_HIDDEN_FALLBACK` | `Find`, 자동 생성, 전역 검색으로 Inspector 누락 은폐 |
| `R12_TEST_EVIDENCE` | 정적/샌드박스/네트워크 증거 부족 |
| `R13_SCENE_DEPENDENCY` | 담당자 테스트 씬에서만 존재하는 숨은 의존성 |
| `R14_LIFECYCLE_RESET` | 재사용/취소/종료 후 상태·Audio·VFX 잔류 |
| `R15_DUPLICATE_SOURCE` | Final/Player/공용 프리팹 복제품 제출 |
| `R16_BALANCE_UNAPPROVED` | 승인 없는 가격/보상/확률을 최종값으로 사용 |
| `R17_PERFORMANCE_LIMIT` | Fire/Incident/VFX 최대 조건에서 제한 위반 |
| `R18_LEGACY_AUTHORITY` | Legacy Manager/Scheduler/Spawn Point가 최종 권한으로 활성 |
| `R19_NOT_GAME_READY` | 내부 기능·참조·표현·Reset이 미완성이라 통합자가 콘텐츠를 추가 구현해야 함 |
| `R20_UNDECLARED_PORT` | Manifest에 없는 외부 참조나 Scene 의존성이 필요함 |

### 21.1 재제출

- 같은 목적이면 `BundleId`는 유지하고 `revision`을 올린다.
- 변경한 파일, 수정한 반려 코드, 재검증 범위를 `CHANGELOG.md`에 기록한다.
- 교체 승인이 없는 한 GUID는 유지한다.
- 반려 원인을 수정하지 않고 우회 Manager, 자동 생성, 추가 NetworkObject로 보강하지 않는다.
- Root 원인과 Inspector 연결을 먼저 고친다.

---

## 22. 최종 통합 규칙

박한솔만 다음을 수행한다.

1. 제출 프리팹을 잠금 씬/Final Prefab에 배치한다.
2. 활성 Player Prefab에 Player/Item 모듈을 부착한다.
3. 하나의 Network Prefab List를 갱신한다.
4. Incident/Fire/Minigame/Item/Map/Shop을 서버 권한 Manager에 연결한다.
5. Legacy Manager/Scheduler/Fire Spawn 중복 실행을 끈다.
6. Inspector 참조를 실제 Scene Device/Anchor/Socket에 연결한다.
7. Host/Client/Late Join을 검증한다.
8. Build Settings와 Standalone Build를 검증한다.
9. 통합 증거를 Bundle의 `Evidence/Network`에 추가한다.
10. Manifest 상태를 `ACCEPTED`로 변경한다.

팀 제출 프리팹은 최종 목적지가 아니다. 최종 목적지는 박한솔이 관리하는 활성 씬, Final Prefab, canonical Data, Network Prefab List다.

조립 허용 범위는 `배치 + 선언 포트 연결 + Network Adapter 부착 + Registry 등록`이다. 이 과정에서 팀 Prefab의 내부 Hierarchy, Animator, Collider, Material, VFX, Audio, 로컬 게임 규칙을 수정해야 하면 통합을 중단하고 원 담당자에게 반려한다.

---

## 23. 통합 목적지 매핑

| 제출물 | 최종 목적지 |
|---|---|
| Player Module | `PHS_CuteWhiteGhost_Player.prefab`의 기존 NetworkObject 아래 |
| Held Item View | 활성 Player의 `TempHoldPoint` 및 도구별 Socket |
| Dropped Item | 02 canonical Dropped Prefab 및 하나의 Network Prefab List |
| Ship Layout/Zone/Device | `PHS_ShipRuntime.prefab`과 `PHS_Map_ver1.unity` |
| Incident Definition | 통합 Event/Incident Registry |
| Incident Presentation | `PHS_EventRuntimeSystem.prefab` 또는 실제 Ship Anchor |
| Fire Zone/Patch | `PHS_ShipRuntime.prefab`의 실제 Zone/Surface |
| Minigame View | 실제 Device/Terminal과 공용 HUD |
| Map Environment | 공유 `PHS_Map_ver1`의 Profile 선택 Root |
| Shop Display | `PHS_ExteriorShopScene.unity`의 Display Slot |
| Shop Catalog | 박한솔 canonical Shop Catalog |
| Object Animation | 실제 Device/Shop/Ship/Map Presentation Root |
| UI Panel | 기존 통합 Canvas/HUD |
| Audio/VFX | 해당 도메인의 preplaced Presentation Root |

---

## 24. 현재 확인이 필요한 항목

다음은 접수 시작 전에 박한솔/팀이 한 번 확정해야 한다.

1. 신규 Shop/Catalog/Display 담당자
2. 서보경 Object Animation의 신규 세부 폴더명
3. 탁현재 ShipLayout/Fire/MapEnvironment/MiniGame 신규 세부 폴더명
4. 조한용 PlayerModules/Item 신규 세부 폴더명
5. 공용 View 인터페이스의 실제 타입명
   - Incident Presentation
   - Minigame View
   - Shop Display
   - Object Animation
6. 대용량 동영상 증거 저장 위치
7. 팀별 Bundle 제출 브랜치/PR 방식
8. Shop 가격·재고·보상 수치 승인 기록 형식

위 항목은 폴더명과 역할의 확정 문제다. 기술 경계는 이미 다음처럼 고정한다.

- 팀원은 자기 폴더에서 독립 제출한다.
- 팀원은 자기 담당 범위의 GameReady 최종 완성품을 제출한다.
- Final Scene/Prefab/Network Prefab List는 박한솔만 수정한다.
- 팀 제출 프리팹과 Script는 NetworkObject/NetworkBehaviour/RPC를 갖지 않는다.
- 모든 Inspector 참조와 소켓은 명시한다.
- `.meta`와 GUID를 보존한다.
- 수치 밸런스는 박한솔/사용자 승인 후 확정한다.
- Object Animation 신규 담당은 서보경이다.

---

## 25. 접수 시작용 최소 안내문

팀원에게는 아래 형식으로 전달한다.

```text
1. 자기 담당 Assets 폴더 안에서만 작업합니다.
2. Final Scene, Final Prefab, 활성 Player Prefab, Network Prefab List는 수정하지 않습니다.
3. 부품이나 구성표가 아니라 자기 구역의 GameReady 최종 완성 Prefab/SO/Script와 .meta를 함께 제출합니다.
4. Docs/Handoffs/<역할>/<BundleId>/에 Manifest, README, Evidence를 둡니다.
5. Root Component, Child Socket, Collider/Layer, ID, 외부 통합 포트, Network 금지 요소를 Manifest에 적습니다.
6. 내부 Inspector 참조를 모두 연결하고 자기 샌드박스에서 나타남 → 작동 → 성공/실패/취소 → Cleanup/Reset까지 증명합니다.
7. 제출 상태는 SUBMITTED로 시작합니다.
8. NetworkObject/NetworkBehaviour/NetworkVariable/RPC는 넣지 않습니다. 이후 배치, 네트워크 Adapter 연결, Host/Client/Late Join 검증은 박한솔이 수행합니다.
9. 반려되면 같은 BundleId의 revision을 올리고 원인을 수정합니다.
10. 박한솔이 내부 기능을 추가 제작해야 하는 제출물은 완성품으로 접수하지 않습니다.
```
