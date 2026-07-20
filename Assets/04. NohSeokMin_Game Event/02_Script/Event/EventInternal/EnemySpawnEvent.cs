using System.Collections.Generic;
using UnityEngine;

namespace SM
{
    public class EnemySpawnEvent : EventBase
    {
        private const float EffectPositionSyncInterval = 0.1f;

        private EnemySpawnDataSO SpawnData { get { return _data as EnemySpawnDataSO; } }

        private Transform _spawnPoint;
        private GameObject _chosenPrefab;
        private int _spawnedCount;
        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();
        private readonly Dictionary<EnemyBase, uint> _effectInstanceIds = new Dictionary<EnemyBase, uint>();
        private float _spawnTimer;
        private float _effectPositionSyncTimer;
        private byte _chosenVariant;
        private IEventEffectRuntimeBridge _effectRuntimeBridge;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _effectRuntimeBridge = Context?.RuntimeBridge as IEventEffectRuntimeBridge;
            _effectInstanceIds.Clear();

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
            _effectPositionSyncTimer = 0f;

            if (!SpawnOneEnemy())
            {
                OnFail();
            }
            //Debug.Log($"<color=lime>[{SpawnData.EventName}]</color> 발생!");
        }

        public override void OnTick(float deltaTime)
        {
            if (State != EventState.InProgress) return;

            for (int i = _activeEnemies.Count - 1; i >= 0; i--)
            {
                _activeEnemies[i].Tick(deltaTime);
            }

            PublishEnemyPositions(deltaTime);

            if (_spawnedCount >= SpawnData.enemyCount) return;

            _spawnTimer += deltaTime;

            if (_spawnTimer >= SpawnData.spawnInterval)
            {
                _spawnTimer = 0f;
                if (!SpawnOneEnemy())
                {
                    OnFail();
                }
            }
        }
        private GameObject PickRandomPrefab()
        {
            if (Random.value < 0.5f)
            {
                _chosenVariant = 0;
                return SpawnData.playerAttackEnemyPrefab;
            }

            _chosenVariant = 1;
            return SpawnData.deviceAttackEnemyPrefab;
        }

        private bool SpawnOneEnemy()
        {
            if (_spawnedCount >= SpawnData.enemyCount) return true;

            var enemyUnit = EnemyPool.Instance.Get(_chosenPrefab, _spawnPoint.position, _spawnPoint.rotation);
            var effectInstanceId = _effectRuntimeBridge == null
                ? 0U
                : _effectRuntimeBridge.AllocateEffectInstanceId(InstanceId);
            if (_effectRuntimeBridge != null && effectInstanceId == 0U)
            {
                enemyUnit.ForceReturnToPool();
                Debug.LogError($"PHS_ENEMY_EFFECT_SPAWN_FAILED reason=effect_id_missing event={InstanceId}");
                return false;
            }

            enemyUnit.OnDeath += HandleEnemyDeath;
            _activeEnemies.Add(enemyUnit);
            _spawnedCount++;
            if (effectInstanceId != 0U)
            {
                _effectInstanceIds[enemyUnit] = effectInstanceId;
                _effectRuntimeBridge.PublishEffectSpawned(
                    InstanceId,
                    effectInstanceId,
                    EventEffectKind.Enemy,
                    _spawnPoint.position,
                    _chosenVariant);
            }

            return true;
        }

        private void HandleEnemyDeath(EnemyBase unit)
        {
            unit.OnDeath -= HandleEnemyDeath;
            PublishEffectRemoved(unit);
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

        public override void OnFail()
        {
            ReleaseActiveEnemies();
            base.OnFail();
        }

        public override void ForceTerminate()
        {
            ReleaseActiveEnemies();
            _spawnedCount = SpawnData.enemyCount;

            base.ForceTerminate();
        }

        private void ReleaseActiveEnemies()
        {
            foreach (var enemy in _activeEnemies)
            {
                enemy.OnDeath -= HandleEnemyDeath;
                PublishEffectRemoved(enemy);
                enemy.ForceReturnToPool();
            }

            _activeEnemies.Clear();
            _effectInstanceIds.Clear();
        }

        private void PublishEffectRemoved(EnemyBase enemy)
        {
            if (!_effectInstanceIds.Remove(enemy, out var effectInstanceId))
            {
                return;
            }

            _effectRuntimeBridge?.PublishEffectRemoved(InstanceId, effectInstanceId);
        }

        private void PublishEnemyPositions(float deltaTime)
        {
            if (_effectRuntimeBridge == null)
            {
                return;
            }

            _effectPositionSyncTimer += deltaTime;
            if (_effectPositionSyncTimer < EffectPositionSyncInterval)
            {
                return;
            }

            _effectPositionSyncTimer = 0f;
            foreach (var pair in _effectInstanceIds)
            {
                if (pair.Key != null && pair.Key.gameObject.activeInHierarchy)
                {
                    _effectRuntimeBridge.PublishEffectPositionChanged(
                        InstanceId,
                        pair.Value,
                        pair.Key.transform.position);
                }
            }
        }
    }
}
