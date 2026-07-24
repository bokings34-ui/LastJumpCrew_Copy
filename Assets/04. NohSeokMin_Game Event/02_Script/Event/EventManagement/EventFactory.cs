using UnityEngine;

namespace SM
{
    public static class EventFactory
    {
        public static EventBase Create(EventId id)
        {
            switch (id)
            {
                case EventId.Fire:
                    return new FireEvent();
                case EventId.EnemySpawn:
                    return new EnemySpawnEvent();
                case EventId.OxygenLeak:
                    return new OxygenLeakEvent();
                case EventId.MicDestroy:
                    return new MicDestroyEvent();
                case EventId.PowerOff:
                    return new PowerOffEvent();

                // TODO :: EngineBreak 구현 후 추가

                case EventId.MeteorAttack:
                    return new MeteorAttackEvent();
                case EventId.EnemyScout:
                    return new EnemyScoutEvent();
                case EventId.EmpAttack:
                    return new EmpAttackEvent();

                default:
                    Debug.Log($"<color=lime>[EventFactory]</color> {id}에 대한 이벤트가 아직 구현되지 않았습니다.");
                    return null;
            }
        }
    }
}