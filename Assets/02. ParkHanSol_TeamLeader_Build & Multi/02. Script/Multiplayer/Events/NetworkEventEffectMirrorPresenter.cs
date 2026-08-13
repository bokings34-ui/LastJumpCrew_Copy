using System.Collections.Generic;
using LastJumpCrew.Common;
using SM;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class NetworkEventEffectMirrorPresenter : MonoBehaviour
    {
        [Header("Presentation-only Prefabs")]
        [SerializeField] private EventEffectPresentationView firePresentationPrefab;
        [SerializeField] private EventEffectPresentationView oxygenLeakPresentationPrefab;
        [SerializeField] private EventEffectPresentationView hullBreachPresentationPrefab;
        [SerializeField] private EventEffectPresentationView playerAttackEnemyPresentationPrefab;
        [SerializeField] private EventEffectPresentationView deviceAttackEnemyPresentationPrefab;

        [Header("Hierarchy")]
        [SerializeField] private Transform presentationRoot;

        private readonly Dictionary<uint, ActiveMirror> activeMirrors = new();
        private readonly Dictionary<EventEffectPresentationView, Queue<EventEffectPresentationView>> pools = new();
        private readonly Dictionary<uint, byte> enemyStatusMasks = new();
        private readonly HashSet<uint> desiredEffectIds = new();
        private readonly List<uint> removalBuffer = new();

        private readonly struct ActiveMirror
        {
            public ActiveMirror(
                EventEffectPresentationView sourcePrefab,
                EventEffectPresentationView view)
            {
                SourcePrefab = sourcePrefab;
                View = view;
            }

            public EventEffectPresentationView SourcePrefab { get; }
            public EventEffectPresentationView View { get; }
        }

        public int ActiveMirrorCount => activeMirrors.Count;

        public bool ValidateConfiguration()
        {
            var valid = presentationRoot != null;
            valid &= ValidatePresentationPrefab(firePresentationPrefab, "fire");
            valid &= ValidatePresentationPrefab(oxygenLeakPresentationPrefab, "oxygen_leak");
            valid &= ValidatePresentationPrefab(hullBreachPresentationPrefab, "hull_breach");
            valid &= ValidatePresentationPrefab(playerAttackEnemyPresentationPrefab, "enemy_player_attack");
            valid &= ValidatePresentationPrefab(deviceAttackEnemyPresentationPrefab, "enemy_device_attack");
            if (presentationRoot == null)
            {
                Debug.LogError("PHS_EVENT_EFFECT_PRESENTATION_SETUP_FAILED reason=root_missing", this);
            }

            return valid;
        }

        public void Reconcile(IEnumerable<NetworkEventEffectSnapshot> snapshots)
        {
            desiredEffectIds.Clear();
            foreach (var snapshot in snapshots)
            {
                if (!snapshot.IsActive || snapshot.EffectInstanceId == 0U)
                {
                    continue;
                }

                desiredEffectIds.Add(snapshot.EffectInstanceId);
                if (activeMirrors.TryGetValue(snapshot.EffectInstanceId, out var active))
                {
                    active.View.Activate(snapshot);
                    continue;
                }

                var sourcePrefab = ResolvePrefab(snapshot.Kind, snapshot.Variant);
                if (sourcePrefab == null)
                {
                    Debug.LogError(
                        $"PHS_EVENT_EFFECT_PRESENTATION_FAILED reason=prefab_missing effect={snapshot.EffectInstanceId} kind={snapshot.Kind} variant={snapshot.Variant}",
                        this);
                    continue;
                }

                var view = GetFromPool(sourcePrefab);
                view.Activate(snapshot);
                if (snapshot.Kind == EventEffectKind.Enemy
                    && enemyStatusMasks.TryGetValue(snapshot.EffectInstanceId, out var statusMask))
                {
                    view.SetEnemyStatusMask(statusMask);
                }
                activeMirrors.Add(snapshot.EffectInstanceId, new ActiveMirror(sourcePrefab, view));
                Debug.Log(
                    $"PHS_EVENT_EFFECT_MIRROR_SPAWNED effect={snapshot.EffectInstanceId} event={snapshot.EventInstanceId} kind={snapshot.Kind} variant={snapshot.Variant}",
                    this);
            }

            removalBuffer.Clear();
            foreach (var pair in activeMirrors)
            {
                if (!desiredEffectIds.Contains(pair.Key))
                {
                    removalBuffer.Add(pair.Key);
                }
            }

            foreach (var effectInstanceId in removalBuffer)
            {
                ReturnMirror(effectInstanceId);
            }
        }

        public void ClearMirrors()
        {
            removalBuffer.Clear();
            removalBuffer.AddRange(activeMirrors.Keys);
            foreach (var effectInstanceId in removalBuffer)
            {
                ReturnMirror(effectInstanceId);
            }
        }

        public bool TryPlayEnemyHitFeedback(uint effectInstanceId)
        {
            if (!activeMirrors.TryGetValue(effectInstanceId, out var active)
                || active.View == null
                || active.View.EffectKind != EventEffectKind.Enemy)
            {
                return false;
            }

            active.View.PlayEnemyHitFeedback();
            return true;
        }

        public bool TrySetEnemyStatusFeedback(
            uint effectInstanceId,
            StatusEffectType effectType,
            bool active)
        {
            if (effectInstanceId == 0U || !TryGetStatusBit(effectType, out var bit))
            {
                return false;
            }

            enemyStatusMasks.TryGetValue(effectInstanceId, out var mask);
            mask = active
                ? (byte)(mask | bit)
                : (byte)(mask & ~bit);

            if (mask == 0)
            {
                enemyStatusMasks.Remove(effectInstanceId);
            }
            else
            {
                enemyStatusMasks[effectInstanceId] = mask;
            }

            if (activeMirrors.TryGetValue(effectInstanceId, out var mirror)
                && mirror.View != null)
            {
                mirror.View.SetEnemyStatusMask(mask);
            }

            return true;
        }

        private EventEffectPresentationView ResolvePrefab(EventEffectKind kind, byte variant)
        {
            return kind switch
            {
                EventEffectKind.Fire => variant == 0 ? firePresentationPrefab : null,
                EventEffectKind.OxygenLeak => variant == 0 ? oxygenLeakPresentationPrefab : null,
                EventEffectKind.HullBreach => variant == 0 ? hullBreachPresentationPrefab : null,
                EventEffectKind.Enemy => variant switch
                {
                    0 => playerAttackEnemyPresentationPrefab,
                    1 => deviceAttackEnemyPresentationPrefab,
                    _ => null
                },
                _ => null
            };
        }

        private EventEffectPresentationView GetFromPool(EventEffectPresentationView sourcePrefab)
        {
            if (!pools.TryGetValue(sourcePrefab, out var pool))
            {
                pool = new Queue<EventEffectPresentationView>();
                pools.Add(sourcePrefab, pool);
            }

            return pool.Count > 0
                ? pool.Dequeue()
                : Instantiate(sourcePrefab, presentationRoot);
        }

        private void ReturnMirror(uint effectInstanceId)
        {
            if (!activeMirrors.Remove(effectInstanceId, out var active))
            {
                return;
            }

            active.View.Deactivate();
            enemyStatusMasks.Remove(effectInstanceId);
            if (!pools.TryGetValue(active.SourcePrefab, out var pool))
            {
                pool = new Queue<EventEffectPresentationView>();
                pools.Add(active.SourcePrefab, pool);
            }

            pool.Enqueue(active.View);
            Debug.Log($"PHS_EVENT_EFFECT_MIRROR_REMOVED effect={effectInstanceId}", this);
        }

        private static bool TryGetStatusBit(StatusEffectType effectType, out byte bit)
        {
            bit = effectType switch
            {
                StatusEffectType.ElectricShok => 1 << 0,
                StatusEffectType.Freeze => 1 << 1,
                StatusEffectType.Slow => 1 << 2,
                _ => 0
            };
            return bit != 0;
        }

        private bool ValidatePresentationPrefab(
            EventEffectPresentationView prefab,
            string label)
        {
            if (prefab == null)
            {
                Debug.LogError(
                    $"PHS_EVENT_EFFECT_PRESENTATION_SETUP_FAILED reason=prefab_missing kind={label}",
                    this);
                return false;
            }

            if (prefab.GetComponentInChildren<FireEffectInstance>(true) != null
                || prefab.GetComponentInChildren<OxygenLeakEffectInstance>(true) != null
                || prefab.GetComponentInChildren<EnemyBase>(true) != null
                || prefab.GetComponentInChildren<NetworkObject>(true) != null
                || prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<NavMeshAgent>(true) != null)
            {
                Debug.LogError(
                    $"PHS_EVENT_EFFECT_PRESENTATION_SETUP_FAILED reason=gameplay_component_detected kind={label} prefab={prefab.name}",
                    this);
                return false;
            }

            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger
                    || collider.GetComponentInParent<EventEffectPresentationView>(true) != prefab)
                {
                    Debug.LogError(
                        $"PHS_EVENT_EFFECT_PRESENTATION_SETUP_FAILED reason=unsafe_repair_collider kind={label} prefab={prefab.name}",
                        this);
                    return false;
                }
            }

            return true;
        }
    }
}
