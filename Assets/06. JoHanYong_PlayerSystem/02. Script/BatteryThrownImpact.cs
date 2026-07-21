using LastJumpCrew.Common;
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
            var colliders = Physics.OverlapSphere(center, explosionRedius, targetLayers, QueryTriggerInteraction.Collide); //충돌 지점 범위 대상을 검사

            var processedTargets = new HashSet<GameObject>();

            foreach (var hitCollider in colliders)
            {
                if(hitCollider == null)
                {
                    continue;
                }
                var targetRoot = hitCollider.transform.root.gameObject;

                if (!processedTargets.Add(targetRoot))
                {
                    continue;
                }
                ApplyBatteryEffect(targetRoot);
            }
            Debug.Log($"PHS_BATTERY_EXPLODED " + $"battery={name} " + $"targetCount={processedTargets.Count}");

           
        }
        private void ApplyBatteryEffect(GameObject target) //범위안에 들어온 대상의 종류에 따라 효과를 다르게 적용
        {
            if(target == null)
            {
                return;
            }
            var playerTarget = target.GetComponentInParent<NetworkPlayerController>() != null;

            var effectReciver = target.GetComponentInParent<IStatusEffectReceiver>(); //상태이상 컴포넌트 찾기

            if (playerTarget)
            {
                if (effectReciver != null && effectReciver.CanReceiveStatusEffect(StatusEffectType.ElectricShok))
                {
                    effectReciver.ApplyStatusEffect(StatusEffectType.ElectricShok, electricShockDuration, attacker);

                    Debug.Log($"PHS_BATTERY_PLAYER_SHOCKED" + $"target={target.name}");
                }

                return;
            }
            var damageable = target.GetComponentInParent<IDamageable>();

            if(damageable != null && damageable.IsAlive)
            {
                damageable.ApplyDamage(damage, attacker);

                Debug.Log($"PHS_BATTERY_DAMAGE_APPLIED " + $"target={target.name} " + $"damage={damage}");
            }
            if(effectReciver != null && effectReciver.CanReceiveStatusEffect(StatusEffectType.ElectricShok))
            {
                effectReciver.ApplyStatusEffect(StatusEffectType.ElectricShok, electricShockDuration, attacker);

                Debug.Log($"PHS_BATTERY_ENEMY_SHOCKED " + $"target={target.name}");
            }

        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, explosionRedius);
        }

    }
}