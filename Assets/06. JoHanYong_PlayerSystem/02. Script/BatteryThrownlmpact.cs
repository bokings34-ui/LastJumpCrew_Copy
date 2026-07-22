using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Combat;
using LastJumpCrew.ParkHanSol.Multiplayer;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class BatteryThrownImpact : NetworkBehaviour
    {
        [Header("Explosion")]
        [SerializeField, Min(0.1f)] 
        private float explosionRedius = 3f; //첫 충돌 위치 중심 검사 범위

        [SerializeField, Min(0)]
        private int damage = 20; //데미지

        [SerializeField] //검사 할 레이어
        private LayerMask targetLayers;

        [Header("Electric Shock")]
        [SerializeField, Min(0f)]
        private float electricShockDuration = 2f; //감전 지속시간

        private GameObject attacker; //공격자 -> 던진사람

        private bool isAttackThrow; //좌클릭 투척 상태

        private bool hasExploded; //여러번 터지는 걸 막음

        [Header("Impact Effect")]
        [SerializeField]
        private GameObject impactEffectPrefab;
        [SerializeField]
        private float impactEffectLifetime = 3f;

        public void InitializeAttackThrow(GameObject throwAttacker)
        {
            if (!IsServer)
            {
                return;
            }
            attacker = throwAttacker;
            isAttackThrow = true;
            hasExploded = false;

            Debug.Log($"PHS_BATTERY_ATTACK_THROW_ARMED " + $"battery={name} " + $"attacker={(attacker != null ? attacker.name : "null")}");
        }
        private void OnCollisionEnter(Collision collision) //처음 충돌한 Collider -> 자동 호출
        {
            if(!IsServer || !isAttackThrow || hasExploded)
            {
                return;
            }

            hasExploded = true; //충돌만 처리하고 바로 true

            var hitPosition = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position; // 충돌이 없으면 배터리 위치 사용

            Debug.Log($"PHS_BATTERY_FIRST_IMPACT " + $"battery={name} " + $"target={collision.collider.name} " + $"position={hitPosition}");

            Explode(hitPosition);
        }
        private void Explode(Vector3 center)
        {
            PlayImpactEffectClientRpc(center);
            var colliders = Physics.OverlapSphere(center, explosionRedius, targetLayers, QueryTriggerInteraction.Collide); //충돌 지점 범위 대상을 검사

            var processedTargets = new HashSet<GameObject>();

            foreach (var hitCollider in colliders)
            {
                if(hitCollider == null)
                {
                    continue;
                }
                if(!CombatHitResolver.TryResolveCombatTarget(hitCollider, out var targetObject))
                {
                    continue;
                }
                if(CombatHitResolver.IsSameTarget(targetObject, attacker))
                {
                    continue;
                }
                if (!processedTargets.Add(targetObject))
                {
                    continue;
                }
                ApplyBatteryEffect(targetObject);
            }
            Debug.Log($"PHS_BATTERY_EXPLODED " + $"battery={name} " + $"targetCount={processedTargets.Count}");

           
        }
        private void ApplyBatteryEffect(GameObject target) //범위안에 들어온 대상의 종류에 따라 효과를 다르게 적용
        {
            if(target == null)
            {
                return;
            }
            bool isPlayer = target.GetComponentInParent<NetworkPlayerController>() != null;

            if (!isPlayer)
            {
                var damgeable = target.GetComponentInParent<IDamageable>();

                if (damgeable != null && damgeable.IsAlive)
                {
                    damgeable.ApplyDamage(damage, attacker);
                    Debug.Log($"PHS_BATTERY_DAMAGE_APPLIED " + $"target={target.name} " + $"damage={damage}");
                }
            }
            CombatHitResolver.ResolveStatusEffect(target, attacker, StatusEffectType.ElectricShok, electricShockDuration);

        }
        [ClientRpc]
        private void PlayImpactEffectClientRpc(Vector3 position)
        {
            PlayImpactEffectLocal(position);
        }
        private void PlayImpactEffectLocal(Vector3 position)
        {
            if(impactEffectPrefab == null)
            {
                Debug.Log($"PHS_BATTERY_IMPACT_EFFECT_FAILED " + $"reason=prefab_missing " + $"battery={name}");

                return;
            }
            GameObject effectInstance = Instantiate(impactEffectPrefab, position, Quaternion.identity);

            Destroy(effectInstance, impactEffectLifetime);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, explosionRedius);
        }

    }
}