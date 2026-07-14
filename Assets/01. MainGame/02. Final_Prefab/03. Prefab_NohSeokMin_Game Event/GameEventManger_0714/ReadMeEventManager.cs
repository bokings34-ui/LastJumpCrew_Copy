/*

빨간 박스 = 불 이펙트 / 노란 캡슐 = 적 / 보라색 구체 = 산소 유출 이펙트(함선 구멍)

1. 
산소 유출 이벤트 발생 시 플레이어가 이펙트 범위 안에 들어오면
벽 뒤에 있어도 끌려가는데 안끌려가게 맵에 있는 벽 무시 설정 필요.
벽만 따로 Layer 설정이 안되길래 Map 자체의 Layer를 바꾸면 해결 되긴 합니다.
Test_OxygenLeakEffect 프리팹 -> 벽 Layer 따로 설정 해야함

2. 
맵에 NavMesh Surface 필요. (Bake 까지)

3.
Rooms 안에 Room 마다 FireSpawnPoint 가 따로 설정되어 있는데
배치하실 때 Map에 맞춰서 위치 조정 필요할 것 같습니다.

4. 
이벤트스케줄러박스 상호작용도 열어 뒀고 숫자 키로도 UI On/off 따로 설정해놨습니다.

[0] : 이벤트 UI On/off (IInteractable 로 Box에 상호작용 해도 됨)
[1] : 이벤트 스케줄러 시작 (30초마다 이벤트 발생)
[2] : 이벤트 스케줄러 강제 종료 (초기화)
[3] : 화재 이벤트 강제 발생 
[4] : 적 침투 이벤트 강제 발생
[5] : 산소 유출 이벤트 강제 발생

5.
이벤트 스케줄러 박스에 UI Panel 연결은
EventUIPanel 연결하시면 됩니다.

6.
장치에 붙어있는 스크립트 IDevice 상속 필요 (위치만 열어주시고 등록/해제 해주시면 됩니다.)

    // public Transform Transform => transform;
    // private void OnEnable() { DeviceRegistry.Instance.Register(this); }
    // private void OnDisable() { DeviceRegistry.Peek()?.Unregister(this); }

 */