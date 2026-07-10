namespace SM
{
    public class EnemyDeadState : IEnemyState
    {
        private float _timer;

        // 사망 연출 대기 시간
        private const float DeathDelay = 1f; 

        public void Enter(EnemyBase owner)
        {
            _timer = 0f;
            owner.Agent.isStopped = true;
            owner.SetColliderEnabled(false);
        }

        public void Tick(EnemyBase owner, float deltaTime)
        {
            _timer += deltaTime;

            if (_timer >= DeathDelay)
            {
                owner.CompleteDeath();
            }
        }

        public void Exit(EnemyBase owner)
        {
            owner.SetColliderEnabled(true);
        }
    }
}