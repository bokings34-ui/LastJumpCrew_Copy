# BEAVER 2026 시스템 통합 마감 기록

- 작성일: 2026-07-31 KST
- 브랜치: `agent/team-unmerged-integration-20260730`
- Unity: `6000.5.2f1`
- 기준 메인 씬: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_Map_ver1.unity`

## 이번 통합 범위

- 튜토리얼 HUD와 안내 문구의 가독성 및 단일 소스 연결 정리
- 메인 씬 캐릭터, 함선 내부 표시, 외부 데브리 루프 관련 변경 통합
- 맵 배치 authoring/validator 및 런타임 진단 로그 정리
- 게임 이벤트 발생, 스폰, 행동, 종료 로그 보강
- 산소 누출의 실제 파이프 배치, 방 단위 질식 존, 전용 VFX, HUD 경고, 종료 정리 보완

## 산소 누출 최종 구조

### 원인

1. 기존 누출 위치가 공용 `ShipSpawnPoint`에 의존해 파이프가 아닌 임의 지점에 생성될 수 있었다.
2. 메인 씬 산소 존의 월드 Y 범위가 `0~4`여서 실제 플레이어 바닥 높이 약 `-3.7`을 포함하지 않았다.
3. 누출 수리점이 바닥에서 높고 멀어 기본 수리 거리 안에 들어오지 않았다.
4. 산소 누출과 함선 구멍이 같은 보라색 표현을 공유해 사건 구분이 어려웠다.
5. HUD가 산소 누출 경고 문자열을 받았지만 화면에 표시하지 않았다.

### 해결

- 이벤트 담당 코드 `OxygenLeakEvent`는 방에 연결된 `IOxygenLeakZoneProvider`에서 존을 획득한다.
- 이벤트 시작 위치, 수리 타깃, 네트워크 스냅샷 위치를 존의 `RepairPosition`으로 통일했다.
- 성공, 강제 종료, 시작 실패 경로에서 획득한 존을 반드시 반환한다.
- Room A/B/C/D의 수리점을 실제 파이프 표면으로 이동했다.
- 네 방의 질식 존은 월드 Y `-4.0~1.5`를 덮는다.
- 산소 누출은 청록색 압력 분사와 미스트를 사용하고, 함선 구멍의 보라색 표현은 유지했다.
- HUD에 `산소 누출` 경고 문자열을 표시하도록 연결했다.

### 메인 씬 파이프 위치

| 방 | 수리점 월드 좌표 |
|---|---:|
| Room A | `(-21.59, -1.36, 44.37)` |
| Room B | `(16.07, -1.36, 43.83)` |
| Room C | `(0.27, -1.36, 96.94)` |
| Room D | `(8.97, -1.36, 50.38)` |

### 피해 밸런스

- 기존 측정: 약 12.5초 동안 방 안에 머무르면 HP 100 기준 사망
- 조정값: 유예 2.5초, 간격 1.5초, 피해 `2 → 3 → 4 → 5 → 6`, 최대 6
- 목표: 수리 도구를 찾고 접근할 시간을 주면서 약 29.5초 동안 방치하면 치명적

## 담당 경계

- 이벤트 생성과 수명 주기: `Assets/04. NohSeokMin_Game Event/`
- 멀티 연결, 방별 존, HUD, 프리젠테이션, 검증: `Assets/02. ParkHanSol_TeamLeader_Build & Multi/`
- 연결 계약: 이벤트 쪽은 방의 `IOxygenLeakZoneProvider`만 소비한다. 파이프 위치와 질식 범위는 씬/프리팹 Inspector 데이터가 소유한다.

## 검증 결과

| 항목 | 결과 |
|---|---|
| Unity 스크립트 컴파일 | Error 0 |
| `PHS0715IntegrationValidator` | `PHS_0715_VALIDATE_OK errors=0 scenes=4 prefabs=11` |
| 0723 산소 존 프리팹 검증 | 통과, rooms 4 / providers 4 / zones 4 |
| Local Host 산소 이벤트 생성 | `accepted=True`, Room A |
| 런타임 효과 위치 | `(-21.59, -1.36, 44.37)` |
| 런타임 VFX | ParticleSystem 2개 재생, 측정 시 particle 51 |
| 질식 피해 | `100 → 98 → 95 → 91 → 86 → 80 ...` |
| 강제 종료 정리 | lifecycle 0, effect snapshot 0, active effect 0, zone available true |
| 씬 저장 상태 | `PHS_Map_ver1`, Edit Mode, dirty false |

주요 런타임 로그:

- `PHS_OXYGEN_ZONE_ACTIVATED`
- `PHS_OXYGEN_EVENT_STARTED`
- `PHS_OXYGEN_SUFFOCATION_APPLIED`
- `PHS_OXYGEN_ZONE_DEACTIVATED`
- `PHS_EVENT_EFFECT_REMOVED`
- `PHS_EVENT_LIFECYCLE_REMOVED`

## 남은 수동 확인

- 실제 두 프로세스 Host/Client에서 청록색 미러 VFX와 HUD 문구 동기화
- 렌치를 소지한 상태로 실제 접근 및 수리 완료 입력 확인
- 전체 사건 스케줄을 켠 정상 런에서 산소 누출과 다른 함선 사고의 동시 밸런스 확인

위 세 항목은 단일 Editor Host 검증과 프리팹 정적 검증으로 대체하지 않는다.

## Git 포함 원칙

- 이번 통합 브랜치의 기능, 씬, 프리팹, 검증, 문서 변경을 포함한다.
- MCP 로컬 설치물과 Codex 설정은 포함하지 않는다.
- 출처를 확인하지 못한 `Assets/05` 서드파티 대량 삭제와 Unity 자동 ProBuilder 설정은 별도 확인 전까지 포함하지 않는다.
