using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Multiplayer;
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
        private float explosionRedius = 3f;

        [SerializeField]
        private LayerMask targetLayers;

        [Header("Electric Shock")]
        [SerializeField, Min(0f)]
        private float electricShockDuration = 2f;

        [SerializeField, Min(0f)]
        private float playerKnockbackForce = 4f;

        [Header("Impact Visual Effect")]
        [SerializeField] private GameObject lightningBallEffectPrefab;
        [SerializeField] private GameObject lightningRingEffectPrefab;
        [SerializeField, Min(0.01f)] private float lightningBallScale = 0.35f;
        [SerializeField, Min(0.01f)] private float lightningRingScale = 1.25f;
        [SerializeField, Min(0.05f)] private float impactEffectLifetime = 0.8f;

        private GameObject attacker;
        private int attackDamage;
        private bool isAttackThrow;
        private bool hasExploded;

        public bool WasAttackThrow => isAttackThrow;
        public bool HasExploded => hasExploded;

        private void Awake()
        {
            ValidateImpactEffectPrefab(lightningBallEffectPrefab, "lightning_ball");
            ValidateImpactEffectPrefab(lightningRingEffectPrefab, "lightning_ring");
        }

        public void InitializeAttackThrow(
            GameObject throwAttacker,
            int damage)
        {
            if (!IsServer)
            {
                return;
            }

            if (throwAttacker == null || damage <= 0)
            {
                Debug.LogError(
                    $"PHS_BATTERY_ATTACK_THROW_FAILED reason=contract attacker={(throwAttacker == null ? "null" : throwAttacker.name)} damage={damage}",
                    this);
                return;
            }

            attacker = throwAttacker;
            attackDamage = damage;
            isAttackThrow = true;
            hasExploded = false;
            Debug.Log(
                $"PHS_BATTERY_ATTACK_THROW_ARMED battery={name} attacker={(attacker != null ? attacker.name : "null")}",
                this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !isAttackThrow || hasExploded)
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
            PlayImpactEffectClientRpc(center);

            var colliders = Physics.OverlapSphere(
                center,
                explosionRedius,
                targetLayers,
                QueryTriggerInteraction.Collide);
            var processedTargets = new HashSet<GameObject>();
            var acceptedTargetPositions = new List<Vector3>();

            foreach (var hitCollider in colliders)
            {
                if (hitCollider == null)
                {
                    continue;
                }

                var targetRoot = hitCollider.transform.root.gameObject;
                if (!processedTargets.Add(targetRoot)
                    || !TryApplyBatteryEffect(
                        targetRoot,
                        center,
                        out var reaction))
                {
                    continue;
                }

                var feedbackPosition = hitCollider.ClosestPoint(center);
                acceptedTargetPositions.Add(feedbackPosition);
                Debug.Log(
                    $"PHS_ITEM_TARGET_REACTION item=battery_pack target={targetRoot.name} reaction={reaction} result=accepted position={feedbackPosition}",
                    targetRoot);
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
                    explosionRedius,
                    0f,
                    acceptedTargetPositions.ToArray());
            }
            else
            {
                Debug.LogError(
                    $"PHS_BATTERY_FEEDBACK_FAILED reason=controller_missing battery={name}",
                    this);
            }

            Debug.Log(
                $"PHS_BATTERY_EXPLODED battery={name} radius={explosionRedius:F2} candidates={processedTargets.Count} acceptedTargets={acceptedTargetPositions.Count}",
                this);
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

            var playerTarget =
                target.GetComponentInParent<NetworkPlayerController>() != null;
            var effectReceiver =
                target.GetComponentInParent<IStatusEffectReceiver>();

            if (playerTarget)
            {
                var playerShockApplied = effectReceiver != null
                    && effectReceiver.CanReceiveStatusEffect(
                        StatusEffectType.ElectricShok);
                if (playerShockApplied)
                {
                    effectReceiver.ApplyStatusEffect(
                        StatusEffectType.ElectricShok,
                        electricShockDuration,
                        attacker);
                }

                var knockbackable = target.GetComponentInParent<IKnockbackable>();
                var playerKnockbackApplied = knockbackable != null
                    && knockbackable.CanReceiveKnockback;
                if (playerKnockbackApplied)
                {
                    var direction = target.transform.position - explosionCenter;
                    if (direction.sqrMagnitude <= 0.001f)
                    {
                        direction = Vector3.up;
                    }

                    direction = (direction.normalized + Vector3.up * 0.2f)
                        .normalized;
                    knockbackable.ApplyKnockback(
                        direction,
                        playerKnockbackForce,
                        attacker);
                }

                if (!playerShockApplied && !playerKnockbackApplied)
                {
                    Debug.LogError(
                        $"PHS_BATTERY_PLAYER_REACTION_FAILED " +
                        $"reason=receivers_missing target={target.name}",
                        target);
                    return false;
                }

                reaction = playerShockApplied && playerKnockbackApplied
                    ? "player_shock_and_knockback"
                    : playerShockApplied
                        ? "player_shock"
                        : "player_knockback";
                Debug.Log(
                    $"PHS_BATTERY_PLAYER_REACTED target={target.name} " +
                    $"reaction={reaction}",
                    target);
                return true;
            }

            var damageable = target.GetComponentInParent<IDamageable>();
            var damageApplied = false;
            var shockApplied = false;
            if (damageable != null && damageable.IsAlive)
            {
                damageable.ApplyDamage(attackDamage, attacker);
                damageApplied = true;
                Debug.Log(
                    $"PHS_BATTERY_DAMAGE_APPLIED target={target.name} damage={attackDamage}",
                    target);
            }

            if (effectReceiver != null
                && effectReceiver.CanReceiveStatusEffect(
                    StatusEffectType.ElectricShok))
            {
                effectReceiver.ApplyStatusEffect(
                    StatusEffectType.ElectricShok,
                    electricShockDuration,
                    attacker);
                shockApplied = true;
                Debug.Log($"PHS_BATTERY_ENEMY_SHOCKED target={target.name}", target);
            }

            reaction = damageApplied && shockApplied
                ? "damage_and_shock"
                : damageApplied
                    ? "damage"
                    : shockApplied
                        ? "shock"
                        : null;
            return damageApplied || shockApplied;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(transform.position, explosionRedius);
        }
    }
}
