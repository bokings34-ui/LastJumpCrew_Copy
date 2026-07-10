namespace SM
{
    public interface IEnemyState
    {
        void Enter(EnemyBase owner);
        void Tick(EnemyBase owner, float deltaTime);
        void Exit(EnemyBase owner);
    }
}