using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SM
{
    // GameReady. Location 확정 후 호출받는 진입점.
    // 위치 선택/원장 등록/피해 확정/네트워크는 포함하지 않음.
    public class EnemySpawnRuntime : MonoBehaviour
    {
        [SerializeField] private EnemySpawnDataSO spawnData;
        
        public event Action OnResolved;
        public event Action OnFailed;
        public event Action OnCancelled;

        private Transform _spawnPoint;
        private GameObject _chosenPrefab;
        private int _spawnedCount;
        private float _spawnTimer;
        private bool _isActive;

        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();

        public void Telegraph(Transform confirmedLocation)
        {
            _spawnPoint = confirmedLocation;
            // TODO :: Telegraph 연출(경고 이펙트 등) 필요 시 여기에 추가
        }

        public void Activate(Transform confirmedLocation)
        {
            _spawnPoint = confirmedLocation;

            if (_spawnPoint == null)
            {
                Debug.LogError("[EnemySpawnRuntime] 확정된 위치가 없어 발생 취소.");
                OnFailed?.Invoke();
                return;
            }

            _chosenPrefab = PickRandomPrefab();
            _spawnedCount = 0;
            _spawnTimer = 0f;
            _isActive = true;

            SpawnOneEnemy();
        }

        private void Update()
        {
            if (!_isActive) return;

            _spawnTimer += Time.deltaTime;
            if (_spawnedCount < spawnData.enemyCount && _spawnTimer >= spawnData.spawnInterval)
            {
                _spawnTimer = 0f;
                SpawnOneEnemy();
            }

            foreach (var enemy in _activeEnemies)
            {
                enemy.Tick(Time.deltaTime);
            }
        }

        private GameObject PickRandomPrefab()
        {
            return UnityEngine.Random.value < 0.5f
                ? spawnData.playerAttackEnemyPrefab
                : spawnData.deviceAttackEnemyPrefab;
        }

        private void SpawnOneEnemy()
        {
            var enemyUnit = EnemyPool.Instance.Get(_chosenPrefab, _spawnPoint.position, _spawnPoint.rotation);
            enemyUnit.OnDeath += HandleEnemyDeath;
            _activeEnemies.Add(enemyUnit);
            _spawnedCount++;
        }

        private void HandleEnemyDeath(EnemyBase unit)
        {
            unit.OnDeath -= HandleEnemyDeath;
            _activeEnemies.Remove(unit);

            if (_activeEnemies.Count == 0 && _spawnedCount >= spawnData.enemyCount)
            {
                _isActive = false;
                OnResolved?.Invoke();
            }
        }

        // 외부(박한솔님 계층)에서 강제 취소가 필요할 때 호출
        public void Cancel()
        {
            _isActive = false;

            foreach (var enemy in _activeEnemies)
            {
                enemy.OnDeath -= HandleEnemyDeath;
                enemy.ForceReturnToPool();
            }
            _activeEnemies.Clear();

            OnCancelled?.Invoke();
        }

        // 재사용 전 반드시 호출
        public void Cleanup()
        {
            _isActive = false;
            _spawnPoint = null;
            _chosenPrefab = null;
            _spawnedCount = 0;
            _spawnTimer = 0f;

            foreach (var enemy in _activeEnemies)
            {
                enemy.OnDeath -= HandleEnemyDeath;
                enemy.ForceReturnToPool();
            }
            _activeEnemies.Clear();
        }
    }
}