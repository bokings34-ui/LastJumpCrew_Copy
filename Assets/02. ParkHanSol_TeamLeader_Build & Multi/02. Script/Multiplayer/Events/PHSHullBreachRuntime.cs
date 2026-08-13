using System.Collections.Generic;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class PHSHullBreachRuntime : MonoBehaviour
    {
        [SerializeField] private NetworkEventCoordinator coordinator;
        [SerializeField] private PHSHullBreachRepairSite sitePrefab;
        [SerializeField] private float visualHeightOffset = 0.12f;

        private readonly Dictionary<ulong, ActiveSite> activeSites = new();

        private sealed class ActiveSite
        {
            public ShipSpawnPoint SpawnPoint;
            public PHSHullBreachRepairSite Site;
            public PHSHullBreachRepairTarget Target;
            public uint EffectInstanceId;
        }

        public bool IsConfigured => coordinator != null && sitePrefab != null;

        public bool TryValidate(out string reason)
        {
            if (coordinator == null)
            {
                reason = "coordinator_missing";
                return false;
            }

            if (sitePrefab == null)
            {
                reason = "site_prefab_invalid";
                return false;
            }

            if (!sitePrefab.TryValidate(out reason))
            {
                reason ??= "site_prefab_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryStartServer(ulong eventInstanceId, out string reason)
        {
            if (!TryValidate(out reason) || coordinator == null || !coordinator.IsAuthoritative)
            {
                reason ??= "server_required";
                return false;
            }

            if (activeSites.ContainsKey(eventInstanceId))
            {
                reason = null;
                return true;
            }

            var config = ShipSpawnPointConfig.Peek();
            var point = config == null ? null : config.GetRandomFreePoint();
            if (point == null)
            {
                reason = "hull_breach_spawn_point_missing";
                return false;
            }

            var instance = Instantiate(sitePrefab, point.transform.position + Vector3.up * visualHeightOffset,
                Quaternion.identity);
            instance.name = $"PHS_HullBreachRuntime_{eventInstanceId}";
            if (!instance.TryActivate(out reason))
            {
                Destroy(instance.gameObject);
                return false;
            }

            var effectInstanceId = coordinator.AllocateEffectInstanceId(eventInstanceId);
            if (effectInstanceId == 0U)
            {
                instance.Deactivate();
                Destroy(instance.gameObject);
                reason = "effect_id_missing";
                return false;
            }

            var target = instance.gameObject.AddComponent<PHSHullBreachRepairTarget>();
            if (!target.TryConfigure(coordinator, eventInstanceId, effectInstanceId, instance, out reason)
                || !coordinator.RegisterRepairTarget(target))
            {
                instance.Deactivate();
                Destroy(instance.gameObject);
                return false;
            }

            // Presentation is snapshot-mirrored for every client. Keep the
            // authoritative repair site invisible so Host does not render it twice.
            instance.SetPresentationVisible(false);

            point.Occupy(EventId.HullBreach);
            activeSites.Add(eventInstanceId, new ActiveSite
            {
                SpawnPoint = point,
                Site = instance,
                Target = target,
                EffectInstanceId = effectInstanceId
            });
            coordinator.PublishEffectSpawned(
                eventInstanceId,
                effectInstanceId,
                EventEffectKind.HullBreach,
                instance.RepairPosition,
                0);
            Debug.Log($"PHS_HULL_BREACH_RUNTIME_STARTED event={eventInstanceId} effect={effectInstanceId} position={instance.RepairPosition}", this);
            reason = null;
            return true;
        }

        public void StopServer(ulong eventInstanceId)
        {
            if (!activeSites.Remove(eventInstanceId, out var active))
            {
                return;
            }

            coordinator?.UnregisterRepairTarget(eventInstanceId, active.EffectInstanceId);
            coordinator?.PublishEffectRemoved(eventInstanceId, active.EffectInstanceId);
            active.SpawnPoint?.Release();
            if (active.Site != null)
            {
                active.Site.Deactivate();
                Destroy(active.Site.gameObject);
            }

            Debug.Log($"PHS_HULL_BREACH_RUNTIME_STOPPED event={eventInstanceId} effect={active.EffectInstanceId}", this);
        }

        private void OnDestroy()
        {
            foreach (var eventInstanceId in new List<ulong>(activeSites.Keys))
            {
                StopServer(eventInstanceId);
            }
        }
    }
}
