# ParkHanSol Legacy Archive

현재 빌드와 직접 연결되지 않는 과거 작업 보관소다.

## 2026-07-26 이동 범위

- `Scenes/Dated`: 0714, 0715, 0723 씬 스냅샷
- `Scenes/Retired`: 폐기된 DebrisCollection 씬
- `Scenes/TestArchive`: Lobby UI, InGame UI Motion, MiniGame 과거 검토 씬
- `Scripts/TestSupport`: 위 테스트 씬 전용 드라이버와 미사용 중력 테스트 스위처
- `Integration/0715`: 참조가 없는 OxygenLeak prefab과 ZoneBehaviorConfig
- `Data`: 임시 TravelMap 4개와 PriceText backdrop 테스트 재질
- `SourceAssets`: 활성 Tripo 원본과 중복된 미사용 MiniGameDevices 원본 3종
- `EmptyBackups`: 비어 있던 Scene, PlayerPrefab, UI 백업 폴더

## 보관 규칙

- Build Settings, 활성 씬, Catalog, NetworkPrefab에서 이 폴더를 참조하지 않는다.
- 복구 또는 재사용 시 Unity `AssetDatabase.MoveAsset`으로 이동해 GUID를 유지한다.
- `.meta`를 삭제하거나 새 GUID로 복사하지 않는다.
- `MudPrototype`, `GravityTestBall`, `Validation` 문서는 참조 또는 인수인계 가치가 남아 있어 이번 이동에서 제외했다.
