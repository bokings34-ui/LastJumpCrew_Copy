using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Combat;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{

    // 플레이어 공격 요청과 서버 판정을 담당한다.
    // 실제 OverlapSphere, 데미지, 넉백 판정은 서버가 수행한다.

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerCombatController : NetworkBehaviour
    {
        [Header("Wrench Attack")]

        [SerializeField]
        private Transform wrenchAttackPoint;

        [SerializeField, Min(0.1f)]
        private float wrenchAttackRadius = 1.2f; //렌치 공격의 구형 판정 범위

        [SerializeField, Min(0)]
        private int wrenchDamage = 15; //몬스터한테 적용되는 데미지

        [SerializeField, Min(0f)]
        private float wrenchKnockback = 4f; //넉백 세기 => 몬스터

        [SerializeField, Min(0.01f)]
        private float wrenchCooldown = 0.5f; //공격 간격

        [Header("Fire Extinguisher Spray")]
        [SerializeField]
        private Transform extinguisherSprayOrigin; //분사 시작위치

        [SerializeField, Min(0.05f)]
        private float extinguisherSprayRadius = 0.6f; //분사 범위

        [SerializeField, Min(0.1f)]
        private float extinguisherSprayDistance = 4f; //분사 도달하는 최대거리

        [SerializeField, Min(0)]
        private int extinguisherDamagePerTick = 2; //데미지

        [SerializeField, Min(0f)]
        private float extinguisherKnockback = 2f; //넉백 세기
        [SerializeField, Min(0.05f)]
        private float extinguisherDamageInterval = 0.5f; //분사 판정을 실행하는 시간 간격

        [SerializeField]
        private LayerMask extinguisherTargetLayers; //분사로 감지하는 레이어 플레이어 몬스터

        private float nextExtinguisherDamgeTime; //서버가 관리하는 다음 분사판정 가능 시간

        [SerializeField]
        private LayerMask wrenchTargetLayers; //몬스터 플레이어 레이어 판정

        private readonly HashSet<GameObject> processedTargets = new();

        private float nextWrenchAttackTime;

        public void RequestWrenchAttack()
        {
            if (!IsSpawned)
            {
                PerformWrenchAttack();
                return;
            }
            if (!IsOwner)
            {
                return;
            }
            RequestWrenchAttackServerRpc();
        }
        [ServerRpc]
        private void RequestWrenchAttackServerRpc() //서버에서 실제 렌치 공격 판정 실행
        {
            PerformWrenchAttack();
        }
        private void PerformWrenchAttack()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }
            if (wrenchAttackPoint == null)
            {
                Debug.LogError($"PHS_WRENCH_ATTACK_FAILED " +
                $"reason=attack_point_missing " +
                $"player={name}");

                return;
            }
            if (Time.time < nextWrenchAttackTime) //서버 기준 쿨타임 검사
            {
                return;
            }
            nextWrenchAttackTime = Time.time + wrenchCooldown;

            var hits = Physics.OverlapSphere(wrenchAttackPoint.position, wrenchAttackRadius, wrenchTargetLayers, QueryTriggerInteraction.Collide);

            processedTargets.Clear();

            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                var targetRoot = hit.transform.root.gameObject;

                if (targetRoot == transform.gameObject)//자기 자신은 공격하지 않는다.
                {
                    continue;
                }

                if (!processedTargets.Add(targetRoot)) //콜라이드가 여러개 있어도 한번 만 처리
                {
                    continue;
                }
                var knockbackDirection = targetRoot.transform.position - wrenchAttackPoint.position;

                CombatHitResolver.ResolveDamageAndKnockback(targetRoot, gameObject, wrenchDamage, knockbackDirection, wrenchKnockback);
            }
            Debug.Log($"PHS_WRENCH_ATTACK " + $"player={name} " + $"hitCount={processedTargets.Count}");
        }
        public void RequestExtinguisherSpray() //자기 플레이어만 소화기 사용 요청을 보낼 수 있음
        {
            if (!IsSpawned)
            {
                PerformExtinguisherSpray();
                return;
            }
            if (!IsOwner)
            {
                return;
            }
            RequestExtinguisherSprayServerRpc();
        }
        [ServerRpc]
        private void RequestExtinguisherSprayServerRpc()
        {
            PerformExtinguisherSpray();
        }
        private void PerformExtinguisherSpray()
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }//네트워크 플레이 중에는 서버만 공격판정 수행

            if (extinguisherSprayOrigin == null)
            {
                Debug.LogError($"PHS_EXTINGUISHER_SPRAY_FAILED" + $"reason=spray_origin_missing " + $"player={name}");

                return;
            }

            //서버 판정 간격 검사
            if (Time.time < nextExtinguisherDamgeTime)
            {
                return;
            }
            nextExtinguisherDamgeTime = Time.time + extinguisherDamageInterval;

            //분사 범위 판정
            var hits = Physics.SphereCastAll(extinguisherSprayOrigin.position, extinguisherSprayRadius, extinguisherSprayOrigin.forward, extinguisherSprayDistance, extinguisherTargetLayers, QueryTriggerInteraction.Collide);

            processedTargets.Clear();

            foreach (var hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }
                var targetRoot = hit.collider.transform.root.gameObject;

                //소화기 사용하는 자신은 안 맞음
                if (targetRoot == transform.root.gameObject)
                {
                    continue;
                }
                if (!processedTargets.Add(targetRoot))//Collider가 여러 개 검출돼고 한번만 데미지 넉백 처리용
                {
                    continue;
                }
                var sprayDirection = extinguisherSprayOrigin.forward; //넉백 방향 

                CombatHitResolver.ResolveDamageAndKnockback(targetRoot, gameObject, extinguisherDamagePerTick, sprayDirection, extinguisherKnockback);
            }
            Debug.Log($"PHS_EXTINGUISHER_SPRAY " + $"player={name} " + $"hitCount={processedTargets.Count}");
        }
        private void OnDrawGizmosSelected()
        {
            if (wrenchAttackPoint == null)
            {
                return;
            }
            Gizmos.DrawWireSphere(wrenchAttackPoint.position, wrenchAttackRadius);

            // 소화기 분사 범위
            

            if (extinguisherSprayOrigin != null)
            {
                var endPosition =
                    extinguisherSprayOrigin.position
                    + extinguisherSprayOrigin.forward
                    * extinguisherSprayDistance;

                // 분사 시작 지점
                Gizmos.DrawWireSphere(
                    extinguisherSprayOrigin.position,
                    extinguisherSprayRadius);

                // 분사 끝 지점
                Gizmos.DrawWireSphere(
                    endPosition,
                    extinguisherSprayRadius);

                // 분사 방향
                Gizmos.DrawLine(
                    extinguisherSprayOrigin.position,
                    endPosition);
            }
        }
    } 
}
