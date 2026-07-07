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
