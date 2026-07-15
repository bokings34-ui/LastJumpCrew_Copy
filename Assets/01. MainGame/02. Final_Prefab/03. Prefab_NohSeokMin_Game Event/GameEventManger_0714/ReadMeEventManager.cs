/*

빨간 박스 = 불 이펙트 / 노란 캡슐 = 적 / 보라색 구체 = 산소 유출 이펙트(함선 구멍)

1.
0710_Map_Ver1 기준으로 GameEventManager(빈 오브젝트) 의 Transform 위치가 (X:-378.6103 , Y:0 , Z:-14.3925) 면
SpawnPoint가 완벽히 맞긴 합니다.

2.
산소 유출 이벤트 발생 시 플레이어가 이펙트 범위 안에 들어오면
벽 뒤에 있어도 끌려가는데 안끌려가게 맵에 있는 벽 무시 설정 필요.
벽만 따로 Layer 설정이 안되길래 Map 자체의 Layer를 바꾸면 해결 되긴 합니다.
Test_OxygenLeakEffect 프리팹 -> 벽 Layer 따로 설정 해야함

3.
맵에 NavMesh Surface 필요. (Bake 까지)

4.
이벤트스케줄러박스 상호작용도 열어 뒀고 숫자 키로도 UI On/off 따로 설정해놨습니다.

[5] : 이벤트 UI On/off (IInteractable 로 Box에 상호작용 해도 됨)
[6] : 이벤트 스케줄러 시작 (30초마다 이벤트 발생)
[7] : 이벤트 스케줄러 강제 종료 (초기화)
[8] : 화재 이벤트 강제 발생 
[9] : 적 침투 이벤트 강제 발생
[0] : 산소 유출 이벤트 강제 발생

5.
이벤트 스케줄러 박스에 UI Panel 연결은
EventUIPanel 연결하시면 됩니다.

6.
혹시 GameManager를 따로 구현하신다면 이벤트 스케줄러 박스 필요 없고
스테이지 시작 시 해당 우주 환경에 맞는 Zone만 세팅해주시고 아래 코드 넣어주시면
알아서 5분동안 스케줄러 돌아갑니다. (30초 마다 내부사고 1 , 40~70초마다 우주 환경 이벤트 발생(미니게임 대응)

// *해당 환경 목록(ZoneType.PatrolZone, MeteorZone, NebulaZone, PlanetZone)

스테이지 시작 시 
ZoneEventScheduler.Instance.SetCurrentZone(*해당 환경); 
EventScheduler.Instance.StartScheduler();
ZoneEventScheduler.Instance.StartScheduler();

스테이지 종료 시
EventScheduler.Instance.ForceClearAll();
ZoneEventScheduler.Instance.StopScheduler();

7.
장치에 붙어있는 스크립트 IDevice 상속 필요 (위치만 열어주시고 등록/해제 해주시면 됩니다.)

    // public Transform Transform => transform;
    // private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    // private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }

 */