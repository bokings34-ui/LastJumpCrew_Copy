using System.Collections.Generic;
using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DisallowMultipleComponent]
    public sealed class PHSShipAccidentHazardVolume : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float radius = 2.5f;
        [SerializeField, Min(1)] private int playerDamage = 6;
        [SerializeField, Min(0.1f)] private float damageIntervalSeconds = 1.25f;
        [SerializeField] private LayerMask playerLayers = ~0;

        private readonly Collider[] overlapBuffer = new Collider[32];
        private readonly HashSet<IDamageable> processedTargets = new();
        private float nextDamageTime;

        private void OnEnable()
        {
            nextDamageTime = Time.time;
        }

        private void Update()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer
                || Time.time < nextDamageTime)
            {
                return;
            }

            nextDamageTime = Time.time + damageIntervalSeconds;
            ApplyAreaDamage();
        }

        private void ApplyAreaDamage()
        {
            var hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                overlapBuffer,
                playerLayers,
                QueryTriggerInteraction.Collide);
            processedTargets.Clear();

            for (var index = 0; index < hitCount; index++)
            {
                var overlap = overlapBuffer[index];
                overlapBuffer[index] = null;
                if (overlap == null)
                {
                    continue;
                }

                var target = overlap.GetComponentInParent<IDamageable>();
                if (target == null
                    || !target.IsAlive
                    || target is not NetworkPlayerLifeState
                    || !processedTargets.Add(target))
                {
                    continue;
                }

                target.ApplyDamage(playerDamage, gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
