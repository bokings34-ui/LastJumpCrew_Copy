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
        [Header("Impact Visual Effect")]
        [SerializeField] private GameObject lightningBallEffectPrefab;
        [SerializeField] private GameObject lightningRingEffectPrefab;
        [SerializeField, Min(0.01f)] private float lightningBallScale = 0.35f;
        [SerializeField, Min(0.01f)] private float lightningRingScale = 1.25f;
        [SerializeField, Min(0.05f)] private float impactEffectLifetime = 0.8f;

        private GameObject attacker;
        private UtilityItemDataSO itemData; //투척 당시 배터리 SO 저장
        private bool isAttackThrow;
        private bool hasExploded;

        public bool WasAttackThrow => isAttackThrow;
        public bool HasExploded => hasExploded; //둘 다 검증 코드에서 사용

        private void Awake()
        {
            ValidateImpactEffectPrefab(lightningBallEffectPrefab, "lightning_ball");
            ValidateImpactEffectPrefab(lightningRingEffectPrefab, "lightning_ring");
        }

        public void InitializeAttackThrow(
            GameObject throwAttacker,
            UtilityItemDataSO throwItemData) //투척 당시 배터리SO 전달
        {
            if (!IsServer)
            {
                return;
            }

            if (throwAttacker == null || throwItemData == null)
            {
                Debug.LogError($"PHS_BATTERY_ATTACK_THROW_FAILED " + $"reason=contract " + $"attacker={(throwAttacker == null ? "null" : throwAttacker.name)} " +
                    $"item = {(throwItemData == null ? "null" :  throwItemData.ItemId)} ", this);

                return;
            }
            if(throwItemData.UseType != ItemUseType.Throwable)
            {
                Debug.LogError($"PHS_BATTERY_ATTACK_THROW_FAILED " + $"reason=use_type_invalid " + $"battery={name} " + $"item={throwItemData.ItemId} " +
                    $"useType={throwItemData.UseType}", throwItemData);

                return;
            }
            if (throwItemData.AttackRadius <= 0f)
            {
                Debug.LogError($"PHS_BATTERY_ATTACK_THROW_FAILED " + $"reason=attack_radius_invalid " + $"battery={name} " +
                    $"item={throwItemData.ItemId} " + $"radius={throwItemData.AttackRadius:F2}", throwItemData);

                return;
            }
            if(throwItemData.HitEffects ==null || throwItemData.HitEffects.Count == 0)
            {
                Debug.LogError($"PHS_BATTERY_ATTACK_THROW_FAILED " + $"reason=hit_effects_missing " + $"battery={name} " +
                     $"item={throwItemData.ItemId}", throwItemData);

                return;
            }

            attacker = throwAttacker;
            itemData = throwItemData;
            isAttackThrow = true;
            hasExploded = false;
            Debug.Log(
                $"PHS_BATTERY_ATTACK_THROW_ARMED" + $"battery = {name}" + $"attacker = {attacker.name}" + $"item = {itemData.ItemId}", this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !isAttackThrow || hasExploded)
            {
                return;
            }
            if(collision == null)
            {
                return;
            }


            hasExploded = true;
            var hitPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            Debug.Log(
                $"PHS_BATTERY_FIRST_IMPACT battery={name} target={collision.collider.name} position={hitPosition}",
                this);
            Explode(hitPosition);
        }

        private void Explode(Vector3 center)
        {
            if(itemData == null)
            {
                Debug.LogError($"PHS_BATTERY_EXPLOSION_FAILED " + $"reason=item_data_missing " + $"battery={name}", this); 
                return;
            }
            if(attacker  == null)
            {
                Debug.LogError($"PHS_BATTERY_EXPLOSION_FAILED " + $"reason=attacker_missing " + $"battery={name} " + $"item={itemData.ItemId}", this);
                return;
            }
            if(itemData.AttackRadius <= 0f)
            {
                Debug.LogError($"PHS_BATTERY_EXPLOSION_FAILED " + $"reason=attack_radius_invalid " + $"battery={name} " 
                    + $"item={itemData.ItemId} " + $"radius={itemData.AttackRadius:F2}", this);

                return;
            }
            PlayImpactEffectClientRpc(center);

            var colliders = Physics.OverlapSphere(
                center,
                itemData.AttackRadius, //SO 폭발 반경
                itemData.TargetLayers, //SO 대상 레이어
                QueryTriggerInteraction.Collide);
            var processedTargets = new HashSet<GameObject>();
            var acceptedTargetPositions = new List<Vector3>();

            foreach (var hitCollider in colliders)
            {
                if (hitCollider == null)
                {
                    continue;
                }

                var targetObject = CombatHitResolver.ResolveTargetObject(hitCollider.gameObject);
                if (targetObject == null)
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
                if(!TryApplyBatteryEffect(targetObject,center,out var reaction))
                {
                    continue;
                }
            
                var feedbackPosition = hitCollider.ClosestPoint(center);
                acceptedTargetPositions.Add(feedbackPosition);
                Debug.Log(
                    $"PHS_ITEM_TARGET_REACTION " + $"item={itemData.ItemId} " + $"target={targetObject.name} " + $"reaction={reaction} " + $"result=accepted " +
                    $"position={feedbackPosition}");
            }

            var feedback = attacker == null
                ? null
                : attacker.GetComponent<PHSNetworkItemUseFeedbackController>();
            if (feedback != null)
            {
                feedback.PublishServerFeedback(
                    PHSItemUseFeedbackKind.Battery,
                    PHSItemUseFeedbackShape.Sphere,
                    center,
                    Vector3.up,
                    itemData.AttackRadius,
                    0f,
                    acceptedTargetPositions.ToArray());
            }
            else
            {
                Debug.LogError(
                    $"PHS_BATTERY_FEEDBACK_FAILED reason=controller_missing battery={name}",
                    this);
            }

            Debug.Log($"PHS_BATTERY_EXPLODED " + $"battery={name} " + $"item={itemData.ItemId} " + $"radius={itemData.AttackRadius:F2} " +
                $"candidates={processedTargets.Count} " + $"acceptedTargets={acceptedTargetPositions.Count}", this);
        }

        [ClientRpc]
        private void PlayImpactEffectClientRpc(Vector3 center)
        {
            SpawnImpactEffect(
                lightningBallEffectPrefab,
                center,
                lightningBallScale,
                "lightning_ball");
            SpawnImpactEffect(
                lightningRingEffectPrefab,
                center,
                lightningRingScale,
                "lightning_ring");
        }

        private void SpawnImpactEffect(
            GameObject effectPrefab,
            Vector3 center,
            float scaleMultiplier,
            string effectName)
        {
            if (!ValidateImpactEffectPrefab(effectPrefab, effectName))
            {
                return;
            }

            var effectInstance = Instantiate(
                effectPrefab,
                center,
                Quaternion.identity);
            effectInstance.transform.localScale =
                effectPrefab.transform.localScale * scaleMultiplier;
            foreach (var particle in effectInstance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }

            Destroy(effectInstance, impactEffectLifetime);
            Debug.Log(
                $"PHS_BATTERY_IMPACT_EFFECT effect={effectName} center={center} lifetime={impactEffectLifetime:F2}",
                this);
        }

        private bool ValidateImpactEffectPrefab(
            GameObject effectPrefab,
            string effectName)
        {
            if (effectPrefab == null)
            {
                Debug.LogError(
                    $"PHS_BATTERY_IMPACT_EFFECT_FAILED reason=prefab_missing effect={effectName}",
                    this);
                return false;
            }

            if (effectPrefab.GetComponentsInChildren<Collider>(true).Length > 0)
            {
                Debug.LogError(
                    $"PHS_BATTERY_IMPACT_EFFECT_FAILED reason=collider_present effect={effectName}",
                    effectPrefab);
                return false;
            }

            if (effectPrefab.GetComponentsInChildren<ParticleSystem>(true).Length > 0)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_BATTERY_IMPACT_EFFECT_FAILED reason=particles_missing effect={effectName}",
                effectPrefab);
            return false;
        }

        private bool TryApplyBatteryEffect(
            GameObject target,
            Vector3 explosionCenter,
            out string reaction)
        {
            reaction = null;
            if (target == null)
            {
                return false;
            }
            if(itemData == null)
            {
                Debug.LogError($"PHS_BATTERY_EFFECT_FAILED" + $"reason=item_data_missing " + $"battery={name}", this);
                return false;
            }
            var effectDirection = target.transform.position - explosionCenter; //폭발 중심에서 대상 바깥쪽으로 넉백 방향 계산

            if(effectDirection.sqrMagnitude <= 0.001f)
            {
                effectDirection = Vector3.up;
            }

            effectDirection = (effectDirection.normalized + Vector3.up * 0.2f).normalized;

            bool effectApplied = ItemEffectResolver.ApplyEffects(itemData, target, effectDirection, attacker);

            if (!effectApplied)
            {
                return false;
            }

            reaction = "item_effects_applied"; //실제 효과 처리는 ItemEffectResolver가 담당

            Debug.Log($"PHS_BATTERY_TARGET_REACTED " + $"item={itemData.ItemId} " + $"target={target.name} " + $"reaction={reaction}", target);
            return true;

        }

        private void OnDrawGizmosSelected()
        {
            if(itemData == null)
            {
                return;
            }
            Gizmos.DrawWireSphere(transform.position, itemData.AttackRadius);
        }
    }
}
