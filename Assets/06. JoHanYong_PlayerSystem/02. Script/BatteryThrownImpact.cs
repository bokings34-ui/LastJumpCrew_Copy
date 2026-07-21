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

        [SerializeField, Min(0)]
        private int damage = 20;

        [SerializeField]
        private LayerMask targetLayers;

        [Header("Electric Shock")]
        [SerializeField, Min(0f)]
        private float electricShockDuration = 2f;

        private GameObject attacker;
        private bool isAttackThrow;
        private bool hasExploded;

        public void InitializeAttackThrow(GameObject throwAttacker)
        {
            if (!IsServer)
            {
                return;
            }

            attacker = throwAttacker;
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
                    || !TryApplyBatteryEffect(targetRoot, out var reaction))
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

        private bool TryApplyBatteryEffect(GameObject target, out string reaction)
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
                if (effectReceiver == null
                    || !effectReceiver.CanReceiveStatusEffect(
                        StatusEffectType.ElectricShok))
                {
                    return false;
                }

                effectReceiver.ApplyStatusEffect(
                    StatusEffectType.ElectricShok,
                    electricShockDuration,
                    attacker);
                Debug.Log($"PHS_BATTERY_PLAYER_SHOCKED target={target.name}", target);
                reaction = "player_shock";
                return true;
            }

            var damageable = target.GetComponentInParent<IDamageable>();
            var damageApplied = false;
            var shockApplied = false;
            if (damageable != null && damageable.IsAlive)
            {
                damageable.ApplyDamage(damage, attacker);
                damageApplied = true;
                Debug.Log(
                    $"PHS_BATTERY_DAMAGE_APPLIED target={target.name} damage={damage}",
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
