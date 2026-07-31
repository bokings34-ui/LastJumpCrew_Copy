# Manager 프리팹 배치 가이드

## 0. 사전 확인 (가장 중요)
- Main Scene의 맵 오브젝트 위치/회전이 테스트 씬과 정확히 동일한지 먼저 확인
  → 다르면 이 프리팹의 NavMesh_Ignore, 기존 SpawnPoints 전부 무효
- 다르다면 배치 전에 알려주세요 (오프셋 보정 로직 추가 필요)

## 1. 프리팹 배치
- Manager 프리팹을 Main Scene에 Position (0,0,0)으로 배치
- Scale은 (1,1,1) 유지

## 2. NavMesh Bake
- Main Scene에 NavMeshSurface 컴포넌트가 없다면 새로 추가
- Bake 실행 (NavMesh_Ignore가 Not Walkable로 자동 반영됨 — 별도 설정 불필요)
- Bake 후 씬 뷰에서 통로/방 전체가 정상적으로 파란색(NavMesh)으로 덮였는지 확인

## 3. 스폰 포인트 재생성 (기존 것 재사용 절대 금지)
1. SpawnPoints_Container의 기존 자식 오브젝트 전부 삭제
2. SpawnPoint_AutoSetting 선택 → 인스펙터에서 generationBounds를 Main Scene 맵 크기에 맞게 조정
3. 우클릭(또는 톱니바퀴) → "Generate Spawn Points on NavMesh" 실행
4. 생성된 스폰 포인트들이 바닥에 정확히 깔렸는지 씬 뷰에서 확인
5. Event_SpawnPoints(ShipSpawnPointConfig) 오브젝트를 인스펙터에서 잠금(Lock) 아이콘 클릭
6. SpawnPoints_Container 열어서 생성된 스폰 포인트 전부 선택 (Shift+클릭)
7. Event_SpawnPoints 인스펙터의 Spawn Points 리스트 영역으로 한번에 드래그
8. 잠금 해제
9. Event_SpawnPoints 우클릭 → "Auto Connect Neighbors" 실행 (Fire 확산용 이웃 연결)

## 4. 참조 확인 (프리팹화 과정에서 끊겼을 수 있음)
- Event_Manager → Registry 필드에 EventRegistrySO 할당 확인
- Event_Zone_Scheduler → Behavior Config 필드에 ZoneBehaviorConfigSO 할당 확인
- Pool_Fire → Effect Prefab 확인
- Pool_OxygenLeak → Effect Prefab 확인
- EventRegistrySO 안에 9개 이벤트(Fire/EnemySpawn/OxygenLeak/PowerOff/EngineBreak/
  MicDestroy/MeteorAttack/EnemyScout/EmpAttack) 데이터가 전부 등록되어 있는지 확인

## 5. 이 프리팹에 안 들어있는, 별도로 붙여야 하는 것들
- EngineRoomConsole 스크립트 → 실제 엔진룸 콘솔 오브젝트에 부착
- PowerOff 신호(OnPowerOff/OnPowerRestored) → 문/배터리 담당 시스템이 구독
- MicDestroy 신호(OnMicDisabled/OnMicRestored) → 마이크 시스템이 구독

## 6. GameManager 연동 호출부
- 스테이지 시작: EventScheduler.Instance.StartScheduler()
                 ZoneEventScheduler.Instance.SetCurrentZone(zone)
                 ZoneEventScheduler.Instance.StartScheduler()
- 스테이지 종료: EventScheduler.Instance.ForceClearAll()
                 ZoneEventScheduler.Instance.StopScheduler()

## 7. 주의사항
- 테스트용 스크립트(EventSchedulerTester 등)는 포함 안 됨, 필요시 별도 요청
- Room은 현재 1개만 등록된 상태(함선 내부) — 구역별 세분화 필요하면 별도 협의
- OxygenLeak의 벽 감지(Linecast) 로직은 알려진 이슈 있음 (같은 구역 안에서도 
  간헐적으로 막힘) — 확인된 버그, 추후 Layer 분리 예정