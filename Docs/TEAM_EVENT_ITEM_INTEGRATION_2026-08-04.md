# 이벤트·아이템 통합 기준 2026-08-04

## 단일 기준

- 내부 이벤트: `SM.EventScheduler` → `SM.EventManager` → `SM.RoomRegistry` / `SM.ShipSpawnPointConfig`.
- 외부 이벤트: `SM.ZoneEventScheduler`.
- PHS 네트워크 코드는 생성기가 아니라 서버 동기화 어댑터다. 같은 이벤트를 PHS 스케줄러가 다시 뽑지 않는다.
- 아이템 원본: 경제 `Assets/03. SeoBoGyeong_Game Economy`, 사용/프리팹 `Assets/06. JoHanYong_PlayerSystem`.
- PHS 아이템 코드는 네트워크·상점 브리지다. 원본 아이템 계약을 대체하지 않는다.

## 이벤트 목록과 등록 위치

| 분류 | 이벤트 | 등록 위치 |
|---|---|---|
| 내부 기본 | Fire, EnemySpawn, OxygenLeak, PowerOff, EngineBreak, MicDestroy | `SM.EventScheduler` |
| 내부 추가 | HullBreach, SteamLeak, OxygenGeneratorFailure, GravityGeneratorFailure | `SM.EventScheduler` |
| 외부 추가 | EnemyScout, MeteorAttack, EmpAttack | `SM.ZoneEventScheduler` |

각 추가 이벤트는 EventData, Prefab, Room/SpawnPointConfig 실제 좌표가 모두 있어야 풀에 넣는다. `SpawnEvent` 실패 시 활성 이벤트 수를 올리면 안 된다.

## 상점·아이템 확정

| 구분 | 항목 | 상태 |
|---|---|---|
| 기본 도구 | 렌치(2101), 소화기(2102), 배터리(2103) | 상점 제외. 플레이/이벤트 대응용 원본 데이터 유지 |
| 고급 도구 | 고급 렌치(2104), 고급 소화기(2105) | 상점 판매 |
| 제외 | 고급 배터리 | 만들지 않음 |
| 수리 도구 | AutoRepairKit(2106) | 렌치와 동일한 수리·넉백·사용 연출 계열 |
| 소모품 | Canister(2107), FoamSealantGun(2108) | 각 원본 사용 계약·대상 연결을 별도 점검 |
| 기존 복구 | 함선 최대 체력 증가 | 상점 연결·서버 적용 확인 |
| 기존 복구 | 플레이어 최대 체력 증가 | 상점 연결·서버 적용 확인 |
| 기존 복구 | 훅 성능 강화 | 상점 연결·서버 적용 확인 |
| 기존 복구 | 함선 체력 회복 | 상점 연결·서버 적용 확인 |

## 검증 기준

1. Unity 컴파일 오류 0.
2. Host에서 내부/외부 이벤트 각각 1회 생성, 실패 생성이 활성 카운트를 증가시키지 않음.
3. Host/Client에서 AutoRepairKit 사용 뒤 렌치 내구도 회복과 소모가 동일하게 동기화됨.
4. 기본 3종이 상점 목록에 없고, 고급 배터리 항목이 없음.
5. 네 가지 기존 업그레이드가 구매 후 서버 상태에 적용됨.
6. 바탕화면 Windows 빌드 실행·로그에 치명 오류 없음.
