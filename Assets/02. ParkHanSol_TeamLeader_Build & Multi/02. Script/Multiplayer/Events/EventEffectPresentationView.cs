using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using SM;
using UnityEngine;
using PHSItemHolder = LastJumpCrew.ParkHanSol.Interaction.IItemHolder;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    public sealed class EventEffectPresentationView : MonoBehaviour, IEventRepairTargetHandle
    {
        private const string FireExtinguisherItemId = "fire_extinguisher";
        private const string WrenchItemId = "wrench";
        private const string FoamSealantGunItemId = "foam_sealant_gun";

        [Header("Enemy Hit Presentation")]
        [SerializeField] private Animator enemyAnimator;
        [SerializeField] private ParticleSystem enemyHitEffect;

        private NetworkEventEffectSnapshot snapshot;

        public ulong EventInstanceId => snapshot.EventInstanceId;
        public uint EffectInstanceId => snapshot.EffectInstanceId;
        public EventEffectKind EffectKind => snapshot.Kind;
        public bool IsActiveEffect => snapshot.IsActive
            && snapshot.EventInstanceId != 0UL
            && snapshot.EffectInstanceId != 0U;
        public string RequiredItemId => snapshot.Kind switch
        {
            EventEffectKind.Fire => FireExtinguisherItemId,
            EventEffectKind.OxygenLeak => WrenchItemId,
            EventEffectKind.HullBreach => FoamSealantGunItemId,
            _ => string.Empty
        };
        public string InteractionPrompt => snapshot.Kind switch
        {
            EventEffectKind.Fire => "소화기 필요",
            EventEffectKind.OxygenLeak => "렌치 필요",
            EventEffectKind.HullBreach => "실란트 건 필요",
            _ => string.Empty
        };

        public void Activate(NetworkEventEffectSnapshot effectSnapshot)
        {
            snapshot = effectSnapshot;
            transform.position = effectSnapshot.WorldPosition;
            gameObject.SetActive(true);
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Play(true);
            }

            if (snapshot.Kind == EventEffectKind.OxygenLeak)
            {
                PlayOxygenLeakVisual();
            }
        }

        private void LateUpdate()
        {
            if (snapshot.Kind == EventEffectKind.OxygenLeak
                && IsActiveEffect)
            {
                PlayOxygenLeakVisual();
            }
        }

        public void Deactivate()
        {
            if (snapshot.Kind == EventEffectKind.OxygenLeak)
            {
                foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
                {
                    particle.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            snapshot = default;
            gameObject.SetActive(false);
        }

        private void PlayOxygenLeakVisual()
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = true;
                main.startColor = new Color(0.12f, 0.78f, 1f, 0.9f);
                main.startLifetime = 1.15f;
                main.startSpeed = 4.25f;
                main.startSize = 0.3f;
                var shape = particle.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.radius = 0.14f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                particle.Play(true);
            }
        }

        public bool TryGetVisualRepairPoint(
            Vector3 observerPosition,
            out Vector3 repairPoint)
        {
            repairPoint = default;
            if (!IsActiveEffect)
            {
                return false;
            }

            var renderers = GetComponentsInChildren<Renderer>(false);
            var hasRenderer = false;
            var nearestDistanceSquared = float.PositiveInfinity;
            foreach (var visualRenderer in renderers)
            {
                if (visualRenderer == null || !visualRenderer.enabled)
                {
                    continue;
                }

                hasRenderer = true;
                var candidate = visualRenderer.bounds.ClosestPoint(
                    observerPosition);
                var distanceSquared = (candidate - observerPosition)
                    .sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                repairPoint = candidate;
            }

            if (!hasRenderer)
            {
                Debug.LogError(
                    $"PHS_EVENT_PRESENTATION_TARGET_FAILED reason=visual_renderer_missing effect={snapshot.EffectInstanceId}",
                    this);
            }

            return hasRenderer;
        }

        public void PlayEnemyHitFeedback()
        {
            if (snapshot.Kind != EventEffectKind.Enemy)
            {
                Debug.LogError($"PHS_ENEMY_HIT_PRESENTATION_FAILED reason=kind_invalid kind={snapshot.Kind}", this);
                return;
            }

            if (enemyAnimator == null || enemyHitEffect == null)
            {
                Debug.LogError($"PHS_ENEMY_HIT_PRESENTATION_FAILED reason=reference_missing effect={snapshot.EffectInstanceId}", this);
                return;
            }

            if (!enemyAnimator.HasState(0, EnemyAnimData.TakeDamage))
            {
                Debug.LogError(
                    $"PHS_ENEMY_HIT_PRESENTATION_FAILED reason=take_damage_state_missing effect={snapshot.EffectInstanceId}",
                    this);
                return;
            }

            enemyAnimator.Play(EnemyAnimData.TakeDamage, -1, 0f);
            enemyHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            enemyHitEffect.Play(true);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return snapshot.IsActive
                && itemHolder != null
                && itemHolder.HasItem
                && itemHolder.CurrentItem != null
                && itemHolder is PHSItemHolder phsItemHolder
                && phsItemHolder.CurrentItemPrefabData != null
                && phsItemHolder.CurrentItemPrefabData.ItemId
                    == itemHolder.CurrentItem.ItemId
                && UtilityItemRepairActionResolver.TryResolve(
                    snapshot.Kind,
                    out var actionKind)
                && phsItemHolder.CurrentItemPrefabData.TryGetActionProfile(
                    actionKind,
                    out _);
        }

        public void Interact(IItemHolder itemHolder)
        {
        }
    }
}
