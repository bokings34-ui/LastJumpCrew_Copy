# Common Script Contract

## Purpose

Shared scripts define contracts only. Feature scripts should depend on these interfaces instead of direct references to another member's concrete class.

## Source Basis

- Notion: Last Jump Crew development hub
- Notion: main design document
- Google Drive: `2팀_LastJumpCrew_기획발표.pptx`
- Google Sheet: `2팀 스페이스크루 일정관리`
- Git branches checked:
  - `origin/feature/(2)seobogyeong`
  - `origin/feature/(3)nohseokmin`
  - `origin/feature/(4)takhyunjae`
  - `origin/feature/(5)johanyong`
  - `origin/feature/takhyunjae`

## Current Branch Findings

- `origin/feature/(2)seobogyeong` has data structure work:
  - `IGameData`
  - `DataRepository<T>`
  - `DataManager`
  - `ZoneData`
- `origin/feature/takhyunjae` has `MissionBase`, but no mini-game target contract yet.
- `origin/feature/(3)nohseokmin`, `origin/feature/(4)takhyunjae`, and `origin/feature/(5)johanyong` are still initial-code level in the fetched remote state.

## ParkHanSol Prefab Check

Checked prefab assets:

- `ParkHanSol_WrenchVendingMachine.prefab`
- `ParkHanSol_FireExtinguisherVendingMachine.prefab`
- `ParkHanSol_BatteryChargingStation.prefab`
- `ParkHanSol_Wrench.prefab`
- `ParkHanSol_FireExtinguisher.prefab`
- `ParkHanSol_FuturisticBatteryPack.prefab`

Current adapter status:

- `UtilityVendingMachineInteractable` keeps the temporary ParkHanSol interaction contract and also implements `LastJumpCrew.Common.IInteractable`.
- `UtilityItemObject` now implements `LastJumpCrew.Common.IHoldableItem`.
- `TempPlayerItemHolder` keeps the temporary ParkHanSol holder contract and also implements `LastJumpCrew.Common.IItemHolder`.
- Existing prefab components do not need replacement for this step.
- The temporary scanner still uses `E`. Final project rule is `F` for interaction and `E` for pickup/drop, so input replacement is a later integration step.

## Interface Roles

| Interface | Owner Use | Role |
| --- | --- | --- |
| `IGameData` | Data, item, event, zone | Static ScriptableObject lookup by `Id` |
| `IInteractable` | Player, devices, shop, doors | F-key interaction contract |
| `IItemHolder` | Player | Held item slot contract |
| `IHoldableItem` | Items | Pick up and drop contract |
| `IUsableItem` | Tools, weapons, consumables | Use held item on target |
| `IRequireHeldItem` | Devices, repair targets | Required held item condition only |
| `IDamageable` | Player, enemy, device, ship parts | Damage receiver contract |
| `IStatusEffectReceiver` | Player, enemy, devices | Timed status receiver contract |
| `IEffectable` | Electric/fire/simple one-shot effects | Immediate effect receiver contract |
| `IKnockbackable` | Player, enemy, physics targets | Knockback separated from status effects |
| `IMiniGameTarget` | Devices | Mini-game result receiver contract |

## Merge Notes

- Move or adapt `origin/feature/(2)seobogyeong` global `IGameData` to `LastJumpCrew.Common.IGameData`.
- Update `DataRepository<T>` constraint to use `LastJumpCrew.Common.IGameData`.
- Update `ZoneData` to expose `public int Id => id;` or rename serialized field through a controlled migration.
- Do not wire concrete feature classes to each other directly. Use these contracts at scene/prefab connection points.
- If a reference is missing, log a clear error and stop the action. Do not create fallback behavior.

## Validation

- Compile error count is zero.
- No duplicate global/common interface confusion after branch merge.
- Scene/prefab Inspector references are visible and intentionally assigned.
- Interactions use `F`; pickup/drop uses `E`.
- Mini-game devices call `IMiniGameTarget.OnMiniGameSucceeded` or `OnMiniGameFailed`.
