using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EnemySpawnEvent : EventBase
    {
        private EnemySpawnDataSO SpawnData { get { return _data as EnemySpawnDataSO; } }

        private Transform _spawnPoint;
        private GameObject _chosenPrefab;
        private int _spawnedCount;
        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();
        private float _spawnTimer;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);

            var group = EnemySpawnSetting.Instance.GetRandomPoint();

            if (group == null || group.spawnPoint == null)
            {
                Debug.Log($"<color=lime>[{SpawnData.EventName}]</color> 사용 가능한 스폰 그룹 없음");
                OnFail();
                return;
            }

            _spawnPoint = group.spawnPoint;
            _chosenPrefab = PickRandomPrefab();

            if (_chosenPrefab == null)
            {
                Debug.LogError($"[{SpawnData.EventName}] 선택된 프리팹이 없습니다.");
                OnFail();
                return;
            }

            _spawnedCount = 0;
            _spawnTimer = 0f;

            SpawnOneEnemy();
            Debug.Log($"<color=lime>[{SpawnData.EventName}]</color> 발생! / 수: {SpawnData.enemyCount}");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            foreach (var enemy in _activeEnemies)
            {
                enemy.Tick(deltaTime);
            }

            if (_spawnedCount >= SpawnData.enemyCount) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer >= SpawnData.spawnInterval)
            {
                _spawnTimer = 0f;
                SpawnOneEnemy();
            }
        }
        private GameObject PickRandomPrefab()
        {
            return Random.value < 0.5f
                ? SpawnData.playerAttackEnemyPrefab
                : SpawnData.deviceAttackEnemyPrefab;
        }

        private void SpawnOneEnemy()
        {
            if (_spawnedCount >= SpawnData.enemyCount) return;

            var enemyUnit = EnemyPool.Instance.Get(_chosenPrefab, _spawnPoint.position, _spawnPoint.rotation);
            
            enemyUnit.OnDeath += HandleEnemyDeath;
            _activeEnemies.Add(enemyUnit);
            _spawnedCount++;
        }

        private void HandleEnemyDeath(EnemyBase unit)
        {
            unit.OnDeath -= HandleEnemyDeath;
            _activeEnemies.Remove(unit);

            Debug.Log($"<color=lime>[{SpawnData.EventName}]</color> 적 처치. 남은 적: {_activeEnemies.Count}");

            if (_activeEnemies.Count == 0 && _spawnedCount >= SpawnData.enemyCount)
            {
                OnResolve();
            }
        }

        public override void OnResolve()
        {
            ChangeState(EventState.Resolve);
        }
    }
}