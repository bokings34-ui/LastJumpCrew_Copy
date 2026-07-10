/// <summary>
/// 모든 정적 데이터의 공통 규약. Id(int)로 조회할 수 있게 한다.
/// ItemData / EventData / ZoneData 등 ScriptableObject가 이 인터페이스를 구현한다.
/// (정적 데이터는 게임 중 불변이므로 네트워크 동기화 대상이 아니다.)
/// </summary>
public interface IGameData
{
    int Id { get; }
}
