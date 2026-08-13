using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;
using UnityEngine.AI;

namespace SM
{
    public class EnemySpawnEvent : EventBase
    {
        private const float EffectPositionSyncInterval = 0.1f;
        private const int SpawnPlacementAttempts = 16;

        private EnemySpawnDataSO SpawnData { get { return _data as EnemySpawnDataSO; } }

        private ShipSpawnPoint _spawnPoint;
        private GameObject _chosenPrefab;
        private int _spawnedCount;
        private readonly List<EnemyBase> _activeEnemies = new List<EnemyBase>();
        private readonly Dictionary<EnemyBase, uint> _effectInstanceIds = new Dictionary<EnemyBase, uint>();
        private readonly Dictionary<EnemyBase, StatusEffectController> _statusControllers = new Dictionary<EnemyBase, StatusEffectController>();
        private float _spawnTimer;
        private float _effectPositionSyncTimer;
        private byte _chosenVariant;
        private IEventEffectRuntimeBridge _effectRuntimeBridge;
        private IEventEffectFeedbackRuntimeBridge _effectFeedbackRuntimeBridge;

        public override void OnTrigger()
        {
            ChangeState(EventState.InProgress);
            _effectRuntimeBridge = Context?.RuntimeBridge as IEventEffectRuntimeBridge;
            _effectFeedbackRuntimeBridge = Context?.RuntimeBridge as IEventEffectFeedbackRuntimeBridge;
            _effectInstanceIds.Clear();
            _statusControllers.Clear();

            var config = ShipSpawnPointConfig.Peek();
            if (config == null)
            {
                Debug.LogError(
                    $"<color=lime>[{SpawnData.EventName}]</color> ShipSpawnPointConfig 참조가 씬에 없어 발생 취소.");
                OnFail();
                return;
            }

            var point = config.GetRandomFreePoint(HasReachableDeviceTarget);
            if (point == null)
            {
                Debug.LogError(
                    $"PHS_ENEMY_SPAWN_FAILED reason=reachable_device_spawn_point_missing " +
                    $"event={SpawnData.EventName}");
                OnFail();
                return;
            }

            _spawnPoint = point;
            _spawnPoint.Occupy(EventId.EnemySpawn);
            _chosenPrefab = PickRandomPrefab();

            if (_chosenPrefab == null)
            {
                Debug.LogError($"<color=lime>[{SpawnData.EventName}]</color> 선택된 프리팹이 없습니다.");
                _spawnPoint.Release();
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

            if (!TryResolveSpawnPosition(out var spawnPosition))
            {
                Debug.LogError(
                    $"PHS_ENEMY_SPAWN_FAILED reason=separated_navmesh_position_missing event={InstanceId} index={_spawnedCount}");
                return false;
            }

            var enemyUnit = EnemyPool.Instance.Get(
                _chosenPrefab,
                spawnPosition,
                _spawnPoint.transform.rotation);
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
            enemyUnit.OnDamaged += HandleEnemyDamaged;
            BindStatusFeedback(enemyUnit);
            _activeEnemies.Add(enemyUnit);
            _spawnedCount++;
            if (effectInstanceId != 0U)
            {
                _effectInstanceIds[enemyUnit] = effectInstanceId;
                _effectRuntimeBridge.PublishEffectSpawned(
                    InstanceId,
                    effectInstanceId,
                    EventEffectKind.Enemy,
                    spawnPosition,
                    _chosenVariant);
            }

            return true;
        }

        private bool TryResolveSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = default;
            var prefabAgent = _chosenPrefab == null
                ? null
                : _chosenPrefab.GetComponent<NavMeshAgent>();
            if (prefabAgent == null)
            {
                Debug.LogError(
                    $"PHS_ENEMY_SPAWN_FAILED reason=navmesh_agent_missing prefab={_chosenPrefab?.name ?? "none"}");
                return false;
            }

            var minimumSeparation = Mathf.Max(1f, prefabAgent.radius * 2.2f);
            var center = _spawnPoint.transform.position;
            for (var attempt = 0; attempt < SpawnPlacementAttempts; attempt++)
            {
                var candidate = center;
                if (_spawnedCount > 0)
                {
                    var angle = ((_spawnedCount - 1) * SpawnPlacementAttempts + attempt)
                        * (Mathf.PI * 2f / SpawnPlacementAttempts);
                    candidate += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                        * minimumSeparation;
                }

                if (!NavMesh.SamplePosition(
                        candidate,
                        out var navMeshHit,
                        prefabAgent.radius,
                        prefabAgent.areaMask)
                    || _activeEnemies.Exists(enemy =>
                        enemy != null
                        && Vector3.Distance(enemy.transform.position, navMeshHit.position)
                            < minimumSeparation))
                {
                    continue;
                }

                spawnPosition = navMeshHit.position;
                return true;
            }

            return false;
        }

        private static bool HasReachableDeviceTarget(ShipSpawnPoint point)
        {
            return point != null
                && DeviceRegistry.Peek()?.GetNearestDeviceTransform(point.transform.position) != null;
        }

        private void HandleEnemyDeath(EnemyBase unit)
        {
            unit.OnDeath -= HandleEnemyDeath;
            unit.OnDamaged -= HandleEnemyDamaged;
            UnbindStatusFeedback(unit);
            PublishEffectRemoved(unit);
            _activeEnemies.Remove(unit);

            Debug.Log($"<color=lime>[{SpawnData.EventName}]</color> 적 처치. 남은 적: {_activeEnemies.Count}");

            if (_activeEnemies.Count == 0 && _spawnedCount >= SpawnData.enemyCount)
            {
                _spawnPoint?.Release();
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
            _spawnPoint?.Release();
            base.OnFail();
        }

        public override void ForceTerminate()
        {
            ReleaseActiveEnemies();
            _spawnPoint?.Release();
            _spawnedCount = SpawnData.enemyCount;

            base.ForceTerminate();
        }

        private void ReleaseActiveEnemies()
        {
            foreach (var enemy in _activeEnemies)
            {
                enemy.OnDeath -= HandleEnemyDeath;
                enemy.OnDamaged -= HandleEnemyDamaged;
                UnbindStatusFeedback(enemy);
                PublishEffectRemoved(enemy);
                enemy.ForceReturnToPool();
            }

            _activeEnemies.Clear();
            _effectInstanceIds.Clear();
            _statusControllers.Clear();
        }

        private void BindStatusFeedback(EnemyBase enemy)
        {
            if (enemy == null)
            {
                return;
            }

            var controller = enemy.GetComponent<StatusEffectController>();
            if (controller == null)
            {
                Debug.LogError(
                    $"PHS_ENEMY_STATUS_FEEDBACK_BIND_FAILED reason=status_controller_missing enemy={enemy.name}",
                    enemy);
                return;
            }

            controller.StatusEffectStateChanged += HandleEnemyStatusChanged;
            _statusControllers[enemy] = controller;
        }

        private void UnbindStatusFeedback(EnemyBase enemy)
        {
            if (enemy == null
                || !_statusControllers.Remove(enemy, out var controller)
                || controller == null)
            {
                return;
            }

            controller.StatusEffectStateChanged -= HandleEnemyStatusChanged;
        }

        private void HandleEnemyStatusChanged(
            StatusEffectController controller,
            StatusEffectType effectType,
            bool active)
        {
            foreach (var pair in _statusControllers)
            {
                if (pair.Value == controller)
                {
                    PublishEnemyStatusFeedback(pair.Key, effectType, active);
                    return;
                }
            }

            Debug.LogError(
                $"PHS_ENEMY_STATUS_FEEDBACK_FAILED reason=enemy_binding_missing effect={effectType}");
        }

        private void PublishEnemyStatusFeedback(
            EnemyBase enemy,
            StatusEffectType effectType,
            bool active)
        {
            if (enemy == null
                || !_effectInstanceIds.TryGetValue(enemy, out var effectInstanceId))
            {
                Debug.LogError(
                    $"PHS_ENEMY_STATUS_FEEDBACK_FAILED reason=effect_binding_missing event={InstanceId} effect={effectType}");
                return;
            }

            _effectFeedbackRuntimeBridge?.PublishEnemyStatusFeedback(
                InstanceId,
                effectInstanceId,
                effectType,
                active);
        }

        private void HandleEnemyDamaged(EnemyBase enemy)
        {
            if (enemy == null
                || !_effectInstanceIds.TryGetValue(enemy, out var effectInstanceId))
            {
                Debug.LogError($"PHS_ENEMY_HIT_FEEDBACK_FAILED reason=effect_binding_missing event={InstanceId}");
                return;
            }

            _effectFeedbackRuntimeBridge?.PublishEnemyHitFeedback(
                InstanceId,
                effectInstanceId);
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
