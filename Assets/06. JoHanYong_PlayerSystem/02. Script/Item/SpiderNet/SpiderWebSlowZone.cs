using LastJumpCrew.Common;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class SpiderWebSlowZone : NetworkBehaviour
    {
        [Header("Zone Lifetime")]

        // 일단 장판 프리팹 Inspector에서 관리
        [SerializeField, Min(0.1f)]
        private float lifeTime = 5f;

        // 장판 안의 대상에게 Slow를 다시 적용하는 간격
        [SerializeField, Min(0.05f)]
        private float tickInterval = 0.2f;

        private SphereCollider zoneCollider;

        // 실제 상태이상을 받을 대상
        // IStatusEffectReceiver 하나만 저장된다.
        private readonly HashSet<IStatusEffectReceiver>
            targetsInZone = new();

        // 플레이어 한 명에게 Collider가 여러 개 있을 수 있으므로
        // 실제 Trigger 내부에 남아있는 Collider도 별도로 관리한다.
        private readonly HashSet<Collider>
            collidersInZone = new();

        // 투척자
        private GameObject attacker;

        // 거미줄 폭탄 SO
        private UtilityItemDataSO itemData;

        private float nextTickTime;
        private float destroyTime;

        private bool initialized;

        private void Awake()
        {
            zoneCollider = GetComponent<SphereCollider>();

            if (zoneCollider == null)
            {
                Debug.LogError(
                    $"PHS_WEB_ZONE_SETUP_FAILED " +
                    $"reason=sphere_collider_missing zone={name}",
                    this);
                return;
            }

            // 장판 Collider는 물리 충돌이 아니라
            // 범위 감지만 담당한다.
            zoneCollider.isTrigger = true;
        }

        /// <summary>
        /// 폭탄 첫 충돌 시 서버에서 호출한다.
        /// 투척자와 거미줄 폭탄 SO를 전달받는다.
        /// </summary>
        public void InitializeServer(GameObject throwAttacker, UtilityItemDataSO throwItemData)
        {
            if (!IsServer)
            {
                Debug.LogError($"PHS_WEB_ZONE_INIT_FAILED " + $"reason=server_required zone={name}", this);
                return;
            }

            if (throwAttacker == null)
            {
                Debug.LogError($"PHS_WEB_ZONE_INIT_FAILED " + $"reason=attacker_missing zone={name}",this);
                return;
            }

            if (throwItemData == null)
            {
                Debug.LogError($"PHS_WEB_ZONE_INIT_FAILED " + $"reason=item_data_missing zone={name}", this);
                return;
            }

            if (throwItemData.AttackRadius <= 0f)
            {
                Debug.LogError($"PHS_WEB_ZONE_INIT_FAILED " + $"reason=attack_radius_invalid " + $"item={throwItemData.ItemId}", throwItemData);
                return;
            }

            attacker = throwAttacker;
            itemData = throwItemData;

            // SO의 AttackRadius를 장판 크기로 사용
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<SphereCollider>();
            }

            zoneCollider.radius = itemData.AttackRadius;

            initialized = true;

            Debug.Log($"PHS_WEB_ZONE_INITIALIZED " + $"zone={name} " + $"item={itemData.ItemId} " + $"radius={itemData.AttackRadius:F2} " + $"lifeTime={lifeTime:F2}", this);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                return;
            }

            destroyTime = Time.time + lifeTime;
            nextTickTime = Time.time;

            Debug.Log($"PHS_WEB_ZONE_SPAWNED " + $"zone={name} " + $"lifeTime={lifeTime:F2}", this);
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (!initialized)
            {
                return;
            }

            // 장판 유지시간 종료
            if (Time.time >= destroyTime)
            {
                DespawnZone();
                return;
            }

            // 아직 다음 Slow Tick 시간이 안 됨
            if (Time.time < nextTickTime)
            {
                return;
            }

            nextTickTime = Time.time + tickInterval;
            ApplySlowTick();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (!initialized || other == null)
            {
                return;
            }

            // SO에서 지정한 TargetLayers만 장판 대상으로 인정
            if (!IsTargetLayer(other.gameObject.layer))
            {
                return;
            }

            // 같은 Collider가 중복 등록되는 것 방지
            if (!collidersInZone.Add(other))
            {
                return;
            }

            IStatusEffectReceiver receiver = other.GetComponentInParent<IStatusEffectReceiver>();
            if (receiver == null)
            {
                return;
            }
            
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer)
            {
                return;
            }

            if (other == null)
            {
                return;
            }

            collidersInZone.Remove(other);

            IStatusEffectReceiver receiver = other.GetComponentInParent<IStatusEffectReceiver>();
            if (receiver == null)
            {
                return;
            }

            // 같은 플레이어의 다른 Collider가 아직 장판 안에 있는지 검사.
            // 다른 Collider가 남아있다면 target을 제거하면 안됨
            if (HasColliderForReceiver(receiver))
            {
                return;
            }

            targetsInZone.Remove(receiver);

            Debug.Log($"PHS_WEB_ZONE_TARGET_EXITED " + $"zone={name} " + $"target={GetReceiverName(receiver)} " +
                $"targets={targetsInZone.Count}", this);
        }

        private void ApplySlowTick()
        {
            if (itemData == null)
            {
                Debug.LogError($"PHS_WEB_ZONE_TICK_FAILED " + $"reason=item_data_missing zone={name}", this);
                return;
            }

            if (targetsInZone.Count == 0)
            {
                return;
            }

            // foreach 중 HashSet을 수정하지 않도록
            // 삭제할 대상을 별도로 저장한다.
            List<IStatusEffectReceiver> invalidTargets = null;

            foreach (IStatusEffectReceiver receiver in targetsInZone)
            {
                if (receiver == null)
                {
                    invalidTargets ??= new List<IStatusEffectReceiver>();
                    invalidTargets.Add(receiver);
                    continue;
                }

                if (!receiver.CanReceiveStatusEffect(StatusEffectType.Slow))
                {
                    continue;
                }
                ApplySlowFromItemData(receiver);
            }

            if (invalidTargets == null)
            {
                return;
            }
            foreach (IStatusEffectReceiver invalidTarget in invalidTargets)
            {
                targetsInZone.Remove(invalidTarget);
            }
        }

        /// <summary>
        /// UtilityItemDataSO.HitEffects 중에서
        /// Slow 효과를 찾아 대상에게 전달한다.
        /// </summary>
        private void ApplySlowFromItemData(IStatusEffectReceiver receiver)
        {
            if (itemData.HitEffects == null)
            {
                return;
            }

            foreach (ItemEffectData effect in itemData.HitEffects)
            {
                // 상태이상 효과가 아니면 무시
                if (effect.EffectType != ItemEffectType.StatusEffect)
                {
                    continue;
                }

                // Slow가 아니면 무시
                if (effect.StatusEffectType != StatusEffectType.Slow)
                {
                    continue;
                }

                if (effect.Duration <= 0f)
                {
                    continue;
                }

                StatusEffectRequest request = new StatusEffectRequest(StatusEffectType.Slow, effect.Duration, effect.Amount, StatusEffectApplyMode.Refresh,
                        1, attacker);
                receiver.ApplyStatusEffect(request);

                Debug.Log($"PHS_WEB_ZONE_SLOW_APPLIED " + $"zone={name} " + $"target={GetReceiverName(receiver)} " + $"amount={effect.Amount:F2} " + $"duration={effect.Duration:F2}", this);

                // 거미줄 폭탄에는 Slow 효과 하나만 사용한다고 가정.
                // 두 개 이상의 Slow Effect를 허용하려면 break 제거.
                break;
            }
        }

        /// <summary>
        /// 같은 StatusEffectReceiver를 가진 Collider가
        /// 장판 안에 아직 하나라도 남아있는지 검사한다.
        /// </summary>
        private bool HasColliderForReceiver(IStatusEffectReceiver receiver)
        {
            foreach (Collider trackedCollider in collidersInZone)
            {
                if (trackedCollider == null)
                {
                    continue;
                }

                IStatusEffectReceiver trackedReceiver = trackedCollider.GetComponentInParent<IStatusEffectReceiver>();

                if (ReferenceEquals(
                        trackedReceiver,
                        receiver))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Collider Layer가 SO의 TargetLayers에 포함되어 있는지 확인.
        /// </summary>
        private bool IsTargetLayer(int layer)
        {
            if (itemData == null)
            {
                return false;
            }

            int layerMask = 1 << layer;
 
            return (itemData.TargetLayers.value & layerMask) != 0;
        }

        private void DespawnZone()
        {
            targetsInZone.Clear();
            collidersInZone.Clear();

            if (NetworkObject != null &&
                NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static string GetReceiverName(IStatusEffectReceiver receiver)
        {
            Component component =
                receiver as Component;

            return component != null ? component.name : "unknown";
        }

        public override void OnNetworkDespawn()
        {
            targetsInZone.Clear();
            collidersInZone.Clear();

            base.OnNetworkDespawn();
        }

        private void OnDrawGizmosSelected()
        {
            // 실행 전에는 SO가 전달되지 않았으므로
            // SphereCollider 크기를 기준으로 표시
            SphereCollider sphereCollider = zoneCollider != null ? zoneCollider : GetComponent<SphereCollider>();

            if (sphereCollider == null)
            {
                return;
            }

            Gizmos.DrawWireSphere(transform.position, sphereCollider.radius);
        }
    }
}