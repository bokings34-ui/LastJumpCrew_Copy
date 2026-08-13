using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Collider))]
    public sealed class PHSSpiderWebBombImpactEffect : NetworkBehaviour
    {
        private GameObject attacker;
        private UtilityItemDataSO itemData;
        private bool hasImpacted;

        public void InitializeAttackThrow(GameObject throwAttacker, UtilityItemDataSO throwItemData)
        {
            if (IsSpawned && !IsServer)
            {
                return;
            }

            if (throwAttacker == null || throwItemData == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_EFFECT_INIT_FAILED reason=contract bomb={name} " +
                    $"attacker={(throwAttacker == null ? "missing" : "ok")} " +
                    $"item={(throwItemData == null ? "missing" : throwItemData.ItemId)}",
                    this);
                return;
            }

            attacker = throwAttacker;
            itemData = throwItemData;
            hasImpacted = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || hasImpacted || collision == null)
            {
                return;
            }

            hasImpacted = true;
            if (attacker == null || itemData == null)
            {
                Debug.LogError(
                    $"PHS_WEB_BOMB_IMPACT_EFFECT_FAILED reason=not_initialized bomb={name}",
                    this);
                return;
            }

            var direction = collision.relativeVelocity.sqrMagnitude > 0.001f
                ? collision.relativeVelocity.normalized
                : transform.forward;
            var target = collision.collider.gameObject;
            if (!ItemEffectResolver.ApplyEffects(itemData, target, direction, attacker))
            {
                return;
            }

            Debug.Log(
                $"PHS_WEB_BOMB_IMPACT_EFFECT_APPLIED bomb={name} " +
                $"item={itemData.ItemId} target={target.name}",
                this);
        }
    }
}
