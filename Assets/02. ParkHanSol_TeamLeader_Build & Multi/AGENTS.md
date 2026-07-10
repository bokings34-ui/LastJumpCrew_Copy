# ParkHanSol Folder Guide

## Scope

- 이 폴더는 ParkHanSol 담당 빌드/멀티/프롭 작업 영역이다.
- 작업 범위는 기본적으로 `Assets/02. ParkHanSol_TeamLeader_Build & Multi/` 안에서 끝낸다.
- 공용 패키지/빌드 설정 변경이 필요할 때만 루트 설정 파일을 건드린다.
- 요청 범위를 넘는 기능, 추상화, 리팩터링은 하지 않는다.
- 완성 작업품 조립은 `Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/` 쪽에서만 한다.
- 이 폴더의 프리팹/데이터는 작업 원본, 임시 검증, 담당자 관리용으로 둔다.

## Props Folder Rule

- 기능이 붙은 완성 프리팹만 `03. Prefab/Props/Prefabs/` 아래에 둔다.
- 아이템 프리팹은 `03. Prefab/Props/Prefabs/Items/`에 둔다.
- 자판기 프리팹은 `03. Prefab/Props/Prefabs/UtilityVendingMachines/`에 둔다.
- 기능 없는 모델, 재질, 텍스처, 검토용 이미지는 `03. Prefab/Props/SourceAssets/` 아래에 둔다.
- 아이템 원본 에셋은 `03. Prefab/Props/SourceAssets/Items/` 아래에 분류한다.
- 자판기 원본 에셋은 `03. Prefab/Props/SourceAssets/UtilityVendingMachines/` 아래에 분류한다.
- 실제 런타임 데이터에 물리는 ScriptableObject와 최종 아이콘만 `04. Data/` 아래에 둔다.
- 검토용 아이콘 후보 이미지는 데이터가 아니므로 `04. Data/`에 두지 않고 `SourceAssets`로 분류한다.
- 메인 씬에 배치할 완성본은 `Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/` 아래의 프리팹을 사용한다.
- Final 프리팹은 가능하면 Final 내부 `Data/`, `Prefab/Props/SourceAssets/`만 참조하게 만든다.
- Final 조립본이 이 폴더의 작업용 SC 데이터나 원본 모델을 직접 참조하지 않도록 확인한다.

## Current Utility Item Structure

- 아이템 프리팹:
  - `03. Prefab/Props/Prefabs/Items/ParkHanSol_FuturisticBatteryPack.prefab`
  - `03. Prefab/Props/Prefabs/Items/ParkHanSol_FireExtinguisher.prefab`
  - `03. Prefab/Props/Prefabs/Items/ParkHanSol_Wrench.prefab`
- 자판기 프리팹:
  - `03. Prefab/Props/Prefabs/UtilityVendingMachines/ParkHanSol_BatteryChargingStation.prefab`
  - `03. Prefab/Props/Prefabs/UtilityVendingMachines/ParkHanSol_FireExtinguisherVendingMachine.prefab`
  - `03. Prefab/Props/Prefabs/UtilityVendingMachines/ParkHanSol_WrenchVendingMachine.prefab`
- 아이템 데이터:
  - `04. Data/UtilityItems/`
- 자판기 데이터:
  - `04. Data/UtilityVendingMachines/`
- 최종 아이콘:
  - `04. Data/UtilityItems/Icons/`
- 검토용 아이콘:
  - `03. Prefab/Props/SourceAssets/Items/IconReview/`

## Scene / Player Rule

- ParkHanSol 테스트 씬의 플레이어는 최신 `03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab`을 기준으로 배치한다.
- 씬에 직접 만든 임시 플레이어가 필요한 경우에도 최신 플레이어 프리팹과 입력/카메라/아이템 홀더 차이를 먼저 확인한다.
- 프로젝트 입력 설정은 New Input System 기준이다. 플레이어 조작 스크립트에서 구 `Input.GetAxisRaw`/`Input.GetKey`에 의존하지 않는다.
- Shop/Utility 같은 테스트 씬에서 최신 네트워크 플레이어를 그냥 배치하면 네트워크 소유권/씬명 조건 때문에 조작이 꺼질 수 있다. 작동하지 않으면 먼저 입력 컴포넌트, `NetworkObject` 스폰 여부, 카메라 활성, Inspector 참조를 확인한다.

## Utility Interaction Rule

- `TempPlayerInteractionScanner` 입력 규칙은 다음을 기본으로 둔다.
  - `F`: 바라보는 `IInteractable` 실행
  - 좌클릭: 툴박스 슬롯을 바라보면 보관/꺼내기/교체 상호작용, 아니면 현재 든 아이템 사용
  - 우클릭: 현재 든 아이템 내려놓기
- 툴박스 보관 슬롯은 `UtilityToolBoxStorageSlotInteractable`의 공용 `CanInteract`/`Interact` 규칙을 사용한다.
- 툴박스 슬롯에 새 기능을 붙일 때는 별도 우회 로직을 만들지 말고 기존 보관/꺼내기/교체 계약을 먼저 확장한다.

## Asset Import Rule

- ParkHanSol 폴더 안 FBX meta의 `materials.materialLocation`은 Unity 6 경고가 나지 않게 `1`을 사용한다.
- `materialLocation: 0`은 `MaterialLocation.External is obsolete` 경고 원인이므로 새 FBX import 후 확인한다.
- FBX import 경고를 고칠 때는 모델/프리팹 참조를 바꾸기 전에 `.fbx.meta` import 설정부터 확인한다.

## Implementation Rule

- Unity 런타임 스크립트는 `02. Script/` 아래에 둔다.
- 인터페이스 스크립트 이름은 항상 `I`로 시작한다.
- OOP를 지킨다.
- Inspector 참조가 드러나게 프리팹/씬에서 연결한다.
- 코드에서 누락 참조를 임의 보강하지 않는다.
- fallback 기능은 만들지 않는다. 필요한 참조가 없으면 로그로 오류를 드러낸다.
- 작동하지 않을 때는 보강 전에 코드 구조, 참조, Inspector 연결 포인트부터 원인 분석한다.

## Validation

- `03. Prefab/Props/Prefabs/` 안 프리팹은 기능 컴포넌트를 가져야 한다.
- 아이템 프리팹은 `UtilityItemObject`를 가진다.
- 자판기 프리팹은 `UtilityVendingMachineInteractable`을 가진다.
- SC 데이터는 현재 사용할 아이템/자판기 프리팹과 아이콘을 Inspector 참조로 가진다.
- Unity compile error가 없어야 한다.
- ParkHanSol 씬 작업 뒤에는 해당 씬의 missing reference/missing script가 없어야 한다.
- 경고 정리 작업 뒤에는 Unity Console warning/error와 CompilationPipeline 결과를 분리해서 확인한다.
