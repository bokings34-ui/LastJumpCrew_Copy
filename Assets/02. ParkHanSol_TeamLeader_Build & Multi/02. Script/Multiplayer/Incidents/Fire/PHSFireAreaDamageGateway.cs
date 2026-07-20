using System;
using System.Collections.Generic;
using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSFireAreaDamageGateway :
        MonoBehaviour,
        IFireAreaDamageGateway
    {
        private const int MaximumOverlapCapacity = 1024;

        [Header("Server Damage Guard")]
        [SerializeField, Min(1)]
        private int maximumDamagePerTargetPerTick = 12;

        private readonly Dictionary<EntityId, DamageCandidate>
            candidatesByTargetId =
            new();
        private readonly List<EntityId> orderedTargetIds = new();
        private readonly HashSet<EntityId> sampleTargetIds = new();
        private readonly HashSet<string> reportedSampleFailures =
            new(StringComparer.Ordinal);
        private Collider[] overlapBuffer = new Collider[64];
        private bool overlapCapacityWarningReported;

        public int MaximumDamagePerTargetPerTick =>
            maximumDamagePerTargetPerTick;

        public bool TryValidate(out string reason)
        {
            if (maximumDamagePerTargetPerTick <= 0)
            {
                reason =
                    $"maximum_damage_per_target_invalid:" +
                    $"{maximumDamagePerTargetPerTick}";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryApplyDamageServer(
            IReadOnlyList<FireAreaDamageSample> samples,
            out int damagedTargetCount,
            out string reason)
        {
            damagedTargetCount = 0;
            candidatesByTargetId.Clear();
            orderedTargetIds.Clear();

            if (!HasServerAuthority())
            {
                reason = "server_required";
                return false;
            }

            if (samples == null)
            {
                reason = "samples_missing";
                return false;
            }

            if (!TryValidate(out reason))
            {
                return false;
            }

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (!TryValidateSample(
                        sample,
                        index,
                        out var sampleReason))
                {
                    ReportSampleFailure(sampleReason);
                    continue;
                }

                TryCalculateDamage(sample, out var damage);
                CollectDamageCandidates(sample, damage);
            }

            orderedTargetIds.Sort();
            foreach (var targetId in orderedTargetIds)
            {
                var candidate = candidatesByTargetId[targetId];
                if (candidate.Target is not Component targetComponent
                    || targetComponent == null
                    || !candidate.Target.IsAlive)
                {
                    continue;
                }

                candidate.Target.ApplyDamage(candidate.Damage, gameObject);
                damagedTargetCount++;
            }

            candidatesByTargetId.Clear();
            orderedTargetIds.Clear();
            reason = null;
            return true;
        }

        private static bool HasServerAuthority()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null
                && networkManager.IsListening
                && networkManager.IsServer;
        }

        private static bool TryValidateSample(
            FireAreaDamageSample sample,
            int index,
            out string reason)
        {
            if (sample.Patch == null)
            {
                reason = $"sample_invalid:{index}:patch_missing";
                return false;
            }

            if (sample.Intensity == PHSFireIntensity.None
                || !PHSFireIntensityUtility.IsDefined(
                    sample.Intensity))
            {
                reason = $"sample_invalid:{index}:intensity_zero";
                return false;
            }

            if (sample.BaseDamagePerTick <= 0)
            {
                reason =
                    $"sample_invalid:{index}:base_damage_not_positive";
                return false;
            }

            if (sample.DamageableLayers.value == 0)
            {
                reason =
                    $"sample_invalid:{index}:damageable_layers_empty";
                return false;
            }

            var hazardBounds = sample.Patch.HazardBounds;
            if (hazardBounds == null)
            {
                reason =
                    $"sample_invalid:{index}:hazard_bounds_missing";
                return false;
            }

            if (!hazardBounds.enabled
                || !hazardBounds.gameObject.activeInHierarchy)
            {
                reason =
                    $"sample_invalid:{index}:hazard_bounds_inactive";
                return false;
            }

            if (!hazardBounds.isTrigger)
            {
                reason =
                    $"sample_invalid:{index}:hazard_bounds_not_trigger";
                return false;
            }

            if (!TryCalculateDamage(sample, out _))
            {
                reason =
                    $"sample_invalid:{index}:calculated_damage_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private void CollectDamageCandidates(
            FireAreaDamageSample sample,
            int damage)
        {
            sampleTargetIds.Clear();
            var overlapCount = CollectOverlaps(
                sample.Patch.HazardBounds,
                sample.DamageableLayers);
            for (var overlapIndex = 0;
                 overlapIndex < overlapCount;
                 overlapIndex++)
            {
                var overlap = overlapBuffer[overlapIndex];
                overlapBuffer[overlapIndex] = null;
                if (overlap == null)
                {
                    continue;
                }

                var target = overlap.GetComponentInParent<IDamageable>();
                if (target is not Component targetComponent
                    || targetComponent == null
                    || !target.IsAlive)
                {
                    continue;
                }

                var targetId = targetComponent.GetEntityId();
                if (!sampleTargetIds.Add(targetId))
                {
                    continue;
                }

                if (candidatesByTargetId.TryGetValue(
                        targetId,
                        out var existing))
                {
                    var combinedDamage = Math.Min(
                        maximumDamagePerTargetPerTick,
                        (long)existing.Damage + damage);
                    candidatesByTargetId[targetId] =
                        new DamageCandidate(
                            target,
                            (int)combinedDamage);

                    continue;
                }

                candidatesByTargetId.Add(
                    targetId,
                    new DamageCandidate(
                        target,
                        Math.Min(
                            maximumDamagePerTargetPerTick,
                            damage)));
                orderedTargetIds.Add(targetId);
            }
        }

        private static bool TryCalculateDamage(
            FireAreaDamageSample sample,
            out int damage)
        {
            damage = 0;
            var multiplier = sample.Patch == null
                ? 0f
                : sample.Patch.DamageMultiplier;
            if (multiplier <= 0f
                || float.IsNaN(multiplier)
                || float.IsInfinity(multiplier))
            {
                return false;
            }

            var scaledDamage =
                sample.BaseDamagePerTick
                * (double)(byte)sample.Intensity
                * multiplier;
            if (double.IsNaN(scaledDamage)
                || double.IsInfinity(scaledDamage)
                || scaledDamage <= 0d)
            {
                return false;
            }

            if (scaledDamage >= int.MaxValue)
            {
                damage = int.MaxValue;
                return true;
            }

            damage = Math.Max(
                1,
                (int)Math.Round(
                    scaledDamage,
                    MidpointRounding.AwayFromZero));
            return true;
        }

        private int CollectOverlaps(
            Collider hazardBounds,
            LayerMask damageableLayers)
        {
            while (true)
            {
                var overlapCount = hazardBounds switch
                {
                    BoxCollider boxCollider =>
                        CollectBoxOverlaps(
                            boxCollider,
                            damageableLayers),
                    SphereCollider sphereCollider =>
                        CollectSphereOverlaps(
                            sphereCollider,
                            damageableLayers),
                    CapsuleCollider capsuleCollider =>
                        CollectCapsuleOverlaps(
                            capsuleCollider,
                            damageableLayers),
                    _ => Physics.OverlapBoxNonAlloc(
                        hazardBounds.bounds.center,
                        hazardBounds.bounds.extents,
                        overlapBuffer,
                        Quaternion.identity,
                        damageableLayers.value,
                        QueryTriggerInteraction.Collide)
                };
                if (overlapCount < overlapBuffer.Length
                    || overlapBuffer.Length
                        >= MaximumOverlapCapacity)
                {
                    if (overlapCount >= overlapBuffer.Length
                        && !overlapCapacityWarningReported)
                    {
                        overlapCapacityWarningReported = true;
                        Debug.LogError(
                            $"PHS_FIRE_DAMAGE_OVERLAP_TRUNCATED " +
                            $"capacity={overlapBuffer.Length}",
                            this);
                    }

                    return overlapCount;
                }

                Array.Resize(
                    ref overlapBuffer,
                    Math.Min(
                        MaximumOverlapCapacity,
                        overlapBuffer.Length * 2));
            }
        }

        private int CollectBoxOverlaps(
            BoxCollider boxCollider,
            LayerMask damageableLayers)
        {
            var colliderTransform = boxCollider.transform;
            var scale = Abs(colliderTransform.lossyScale);
            var halfExtents = Vector3.Scale(
                boxCollider.size * 0.5f,
                scale);
            return Physics.OverlapBoxNonAlloc(
                colliderTransform.TransformPoint(boxCollider.center),
                halfExtents,
                overlapBuffer,
                colliderTransform.rotation,
                damageableLayers.value,
                QueryTriggerInteraction.Collide);
        }

        private int CollectSphereOverlaps(
            SphereCollider sphereCollider,
            LayerMask damageableLayers)
        {
            var colliderTransform = sphereCollider.transform;
            var scale = Abs(colliderTransform.lossyScale);
            var radius = sphereCollider.radius
                * Mathf.Max(scale.x, scale.y, scale.z);
            return Physics.OverlapSphereNonAlloc(
                colliderTransform.TransformPoint(sphereCollider.center),
                radius,
                overlapBuffer,
                damageableLayers.value,
                QueryTriggerInteraction.Collide);
        }

        private int CollectCapsuleOverlaps(
            CapsuleCollider capsuleCollider,
            LayerMask damageableLayers)
        {
            var colliderTransform = capsuleCollider.transform;
            var scale = Abs(colliderTransform.lossyScale);
            GetCapsuleAxis(
                capsuleCollider.direction,
                scale,
                out var localAxis,
                out var axisScale,
                out var radiusScale);

            var center =
                colliderTransform.TransformPoint(capsuleCollider.center);
            var worldAxis =
                colliderTransform.TransformDirection(localAxis).normalized;
            var radius = capsuleCollider.radius * radiusScale;
            var height = Mathf.Max(
                capsuleCollider.height * axisScale,
                radius * 2f);
            var halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            var pointOffset = worldAxis * halfSegment;
            return Physics.OverlapCapsuleNonAlloc(
                center - pointOffset,
                center + pointOffset,
                radius,
                overlapBuffer,
                damageableLayers.value,
                QueryTriggerInteraction.Collide);
        }

        private void ReportSampleFailure(string reason)
        {
            if (reportedSampleFailures.Add(reason))
            {
                Debug.LogError(
                    $"PHS_FIRE_DAMAGE_SAMPLE_SKIPPED " +
                    $"reason={reason}",
                    this);
            }
        }

        private static void GetCapsuleAxis(
            int direction,
            Vector3 scale,
            out Vector3 localAxis,
            out float axisScale,
            out float radiusScale)
        {
            switch (direction)
            {
                case 0:
                    localAxis = Vector3.right;
                    axisScale = scale.x;
                    radiusScale = Mathf.Max(scale.y, scale.z);
                    return;
                case 2:
                    localAxis = Vector3.forward;
                    axisScale = scale.z;
                    radiusScale = Mathf.Max(scale.x, scale.y);
                    return;
                default:
                    localAxis = Vector3.up;
                    axisScale = scale.y;
                    radiusScale = Mathf.Max(scale.x, scale.z);
                    return;
            }
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }

        private readonly struct DamageCandidate
        {
            public DamageCandidate(
                IDamageable target,
                int damage)
            {
                Target = target;
                Damage = damage;
            }

            public IDamageable Target { get; }
            public int Damage { get; }
        }
    }
}
