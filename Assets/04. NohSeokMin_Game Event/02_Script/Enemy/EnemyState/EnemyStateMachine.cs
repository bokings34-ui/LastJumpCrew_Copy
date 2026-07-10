using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EnemyStateMachine
    {
        private readonly Dictionary<EnemyStateType, IEnemyState> _states = new Dictionary<EnemyStateType, IEnemyState>();
        private IEnemyState _currentState;

        public EnemyStateType CurrentType { get; private set; }

        public void Register(EnemyStateType type, IEnemyState state)
        {
            _states[type] = state;
        }

        public void ChangeState(EnemyBase owner, EnemyStateType nextType)
        {
            if (_currentState != null && CurrentType == nextType) return;

            _currentState?.Exit(owner);

            if (!_states.TryGetValue(nextType, out var nextState))
            {
                Debug.LogError($"<color=lime>[EnemyStateMachine]</color> {nextType} 상태가 등록되지 않음.");
                return;
            }

            CurrentType = nextType;
            _currentState = nextState;
            _currentState.Enter(owner);
        }

        public void Tick(EnemyBase owner, float deltaTime)
        {
            _currentState?.Tick(owner, deltaTime);
        }
    }
}