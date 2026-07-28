using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using LastJumpCrew.ParkHanSol.Multiplayer.Audio;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkPlayerItemRecord))]
    [RequireComponent(typeof(NetworkPlayerItemLifecycle))]
    public sealed class PHSNetworkUtilityFamilyActionController :
        NetworkBehaviour
    {
        [SerializeField, Min(0.25f)] private float wrenchRange = 2.4f;
        [SerializeField, Min(0.1f)] private float wrenchRadius = 1.1f;
        [SerializeField, Min(0.25f)] private float extinguisherRange = 4f;
        [SerializeField, Min(0.1f)] private float extinguisherRadius = 0.45f;
        [SerializeField, Min(0.02f)] private float wrenchInterval = 0.35f;
        [SerializeField, Min(0.02f)] private float extinguisherInterval = 0.16f;
        [Header("Player Knockback")]
        [SerializeField, Min(0f)] private float wrenchPlayerKnockbackForce = 4f;
        [SerializeField, Min(0f)] private float extinguisherPlayerKnockbackForce = 2f;
        [SerializeField] private LayerMask targetLayers = ~0;

        private NetworkPlayerItemRecord itemRecord;
        private NetworkPlayerItemLifecycle itemLifecycle;
        private NetworkPlayerLifeState lifeState;
        private PHSNetworkItemInteractionAudioRelay interactionAudio;
        private uint ownerSequence;
        private uint lastServerSequence;
        private double nextWrenchServerTime;
        private double nextExtinguisherServerTime;

        private void Awake()
        {
            itemRecord = GetComponent<NetworkPlayerItemRecord>();
            itemLifecycle = GetComponent<NetworkPlayerItemLifecycle>();
            lifeState = GetComponent<NetworkPlayerLifeState>();
            interactionAudio = GetComponent<PHSNetworkItemInteractionAudioRelay>();
        }

        public bool CanRequestAction(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemPrefabData itemData)
        {
            return IsSpawned
                && IsOwner
                && itemRecord != null
                && itemRecord.IsSpawned
                && itemRecord.IsOwner
                && lifeState != null
                && lifeState.IsAlive
                && itemData != null
                && itemData.ItemId == itemRecord.HeldItemId
                && HasFamilyProfile(itemData, familyKind);
        }

        public void RequestAction(PHSUtilityFamilyActionKind familyKind)
        {
            if (itemLifecycle == null
                || itemLifecycle.ItemCatalog == null
                || itemRecord == null
                || !itemLifecycle.ItemCatalog.TryGetById(
                    itemRecord.HeldItemId,
                    out var itemData)
                || !CanRequestAction(familyKind, itemData))
            {
                return;
            }

            ownerSequence++;
            if (ownerSequence == 0U)
            {
                ownerSequence = 1U;
            }

            RequestActionServerRpc(
                familyKind,
                new FixedString64Bytes(itemRecord.HeldItemId),
                itemRecord.Revision,
                ownerSequence);
            if (familyKind == PHSUtilityFamilyActionKind.FireExtinguisher)
            {
                interactionAudio?.TryPlayOwnerPredicted(
                    NetworkAudioCue.ExtinguisherSpray);
            }
            else if (familyKind == PHSUtilityFamilyActionKind.Wrench)
            {
                interactionAudio?.TryPlayOwnerPredicted(
                    NetworkAudioCue.WrenchImpact);
            }
        }

        [ServerRpc]
        private void RequestActionServerRpc(
            PHSUtilityFamilyActionKind familyKind,
            FixedString64Bytes expectedItemId,
            uint expectedRevision,
            uint requestSequence,
            ServerRpcParams rpcParams = default)
        {
            var senderClientId = rpcParams.Receive.SenderClientId;
            var itemId = expectedItemId.ToString();
            if (!IsServer
                || senderClientId != OwnerClientId
                || lifeState == null
                || !lifeState.IsAlive
                || itemRecord == null
                || itemLifecycle == null
                || itemRecord.OwnerClientId != senderClientId
                || itemRecord.HeldItemId != itemId
                || itemRecord.Revision != expectedRevision
                || requestSequence == 0U
                || requestSequence <= lastServerSequence
                || itemLifecycle.ItemCatalog == null
                || !itemLifecycle.ItemCatalog.TryGetById(itemId, out var itemData)
                || !HasFamilyProfile(itemData, familyKind)
                || !TryConsumeRateLimit(familyKind))
            {
                return;
            }

            lastServerSequence = requestSequence;
            TryResolveNearestTargetServer(
                familyKind,
                itemData,
                expectedRevision,
                requestSequence);
        }

        private bool TryResolveNearestTargetServer(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemPrefabData itemData,
            uint expectedRevision,
            uint requestSequence)
        {
            var origin = transform.position + Vector3.up * 0.75f;
            var forward = transform.forward;
            var range = familyKind == PHSUtilityFamilyActionKind.Wrench
                ? wrenchRange
                : extinguisherRange;
            var radius = familyKind == PHSUtilityFamilyActionKind.Wrench
                ? wrenchRadius
                : extinguisherRadius;
            var center = familyKind == PHSUtilityFamilyActionKind.Wrench
                ? origin + forward * Mathf.Min(range, 1.2f)
                : origin + forward * (range * 0.5f);
            var colliders = Physics.OverlapSphere(
                center,
                familyKind == PHSUtilityFamilyActionKind.Wrench
                    ? radius
                    : range * 0.5f + radius,
                targetLayers,
                QueryTriggerInteraction.Collide);

            var seen = new HashSet<Component>();
            var candidates = new List<TargetCandidate>();
            foreach (var collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                var closest = collider.ClosestPoint(origin);
                var offset = closest - origin;
                var distance = offset.magnitude;
                if (distance > range
                    || familyKind == PHSUtilityFamilyActionKind.FireExtinguisher
                    && distance > 0.01f
                    && Vector3.Dot(forward, offset / distance) < 0.45f
                    || !TryCreateCandidate(
                        collider,
                        familyKind,
                        out var candidate)
                    || !seen.Add(candidate.Component)
                    || !itemData.TryGetActionProfile(
                        candidate.ActionKind,
                        out _))
                {
                    continue;
                }

                candidate.Distance = distance;
                candidate.AimPosition = closest;
                candidates.Add(candidate);
            }

            candidates.Sort((left, right) =>
                left.Distance.CompareTo(right.Distance));
            foreach (var candidate in candidates)
            {
                if (!HasLineOfSight(origin, candidate))
                {
                    continue;
                }

                if (!itemLifecycle.TryResolveHeldItemActionServer(
                        itemData.ItemId,
                        expectedRevision,
                        candidate.ActionKind,
                        out _))
                {
                    return false;
                }

                if (candidate.TryResolve(itemRecord, requestSequence, gameObject))
                {
                    if (familyKind == PHSUtilityFamilyActionKind.Wrench)
                    {
                        interactionAudio?.TryBroadcastConfirmedServer(
                            NetworkAudioCue.WrenchImpact,
                            requestSequence);
                        if (candidate.IsRepairComplete)
                        {
                            interactionAudio?.TryBroadcastConfirmedServer(
                                NetworkAudioCue.RepairComplete,
                                requestSequence);
                        }
                    }
                    else if (familyKind
                        == PHSUtilityFamilyActionKind.FireExtinguisher
                        && candidate.IsRepairComplete)
                    {
                        interactionAudio?.TryBroadcastConfirmedServer(
                            NetworkAudioCue.ExtinguishComplete,
                            requestSequence);
                    }

                    return true;
                }
            }

            return TryApplyPlayerKnockbackServer(
                familyKind,
                origin,
                forward,
                range,
                colliders);
        }

        private bool TryApplyPlayerKnockbackServer(
            PHSUtilityFamilyActionKind familyKind,
            Vector3 origin,
            Vector3 forward,
            float range,
            Collider[] colliders)
        {
            var force = familyKind == PHSUtilityFamilyActionKind.Wrench
                ? wrenchPlayerKnockbackForce
                : extinguisherPlayerKnockbackForce;
            if (force <= 0f)
            {
                return false;
            }

            IKnockbackable nearestTarget = null;
            Vector3 nearestPoint = default;
            var nearestDistance = float.MaxValue;
            foreach (var collider in colliders)
            {
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                var target = collider.GetComponentInParent<IKnockbackable>();
                if (target == null || !target.CanReceiveKnockback)
                {
                    continue;
                }

                var point = collider.ClosestPoint(origin);
                var offset = point - origin;
                var distance = offset.magnitude;
                if (distance > range
                    || familyKind == PHSUtilityFamilyActionKind.FireExtinguisher
                    && distance > 0.01f
                    && Vector3.Dot(forward, offset / distance) < 0.45f
                    || distance >= nearestDistance)
                {
                    continue;
                }

                nearestTarget = target;
                nearestPoint = point;
                nearestDistance = distance;
            }

            if (nearestTarget == null)
            {
                return false;
            }

            var direction = nearestPoint - origin;
            nearestTarget.ApplyKnockback(
                direction.sqrMagnitude > 0.001f ? direction.normalized : forward,
                force,
                gameObject);
            Debug.Log(
                $"PHS_UTILITY_PLAYER_KNOCKBACK_APPLIED family={familyKind} " +
                $"target={nearestTarget} force={force:F2}",
                this);
            return true;
        }

        private static bool TryCreateCandidate(
            Collider collider,
            PHSUtilityFamilyActionKind familyKind,
            out TargetCandidate candidate)
        {
            foreach (var component in collider.GetComponentsInParent<
                         Component>(true))
            {
                if (component is IEventRepairableEffect eventTarget
                    && UtilityItemRepairActionResolver.TryResolve(
                        eventTarget.EffectKind,
                        out var eventAction)
                    && FamilyAllows(familyKind, eventAction))
                {
                    candidate = TargetCandidate.ForEvent(
                        component,
                        eventTarget,
                        eventAction);
                    return true;
                }

                if (component is IShipAccidentRepairTarget shipTarget
                    && UtilityItemRepairActionResolver.TryResolve(
                        shipTarget.AccidentId,
                        out var shipAction)
                    && FamilyAllows(familyKind, shipAction))
                {
                    candidate = TargetCandidate.ForShip(
                        component,
                        shipTarget,
                        shipAction);
                    return true;
                }

                if (component is PHSFirePatchRuntimeTarget fireTarget
                    && familyKind
                        == PHSUtilityFamilyActionKind.FireExtinguisher)
                {
                    candidate = TargetCandidate.ForUtility(
                        component,
                        fireTarget,
                        UtilityItemActionKind.FireSuppression);
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        private bool TryConsumeRateLimit(
            PHSUtilityFamilyActionKind familyKind)
        {
            var now = NetworkManager.ServerTime.Time;
            if (familyKind == PHSUtilityFamilyActionKind.Wrench)
            {
                if (now < nextWrenchServerTime)
                {
                    return false;
                }

                nextWrenchServerTime = now + wrenchInterval;
                return true;
            }

            if (familyKind == PHSUtilityFamilyActionKind.FireExtinguisher)
            {
                if (now < nextExtinguisherServerTime)
                {
                    return false;
                }

                nextExtinguisherServerTime = now + extinguisherInterval;
                return true;
            }

            return false;
        }

        private static bool HasFamilyProfile(
            UtilityItemPrefabData itemData,
            PHSUtilityFamilyActionKind familyKind)
        {
            if (itemData == null
                || itemData.UtilityFamily != familyKind)
            {
                return false;
            }

            foreach (var profile in itemData.ActionProfiles)
            {
                if (profile.IsValid
                    && FamilyAllows(familyKind, profile.ActionKind))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasLineOfSight(
            Vector3 origin,
            TargetCandidate candidate)
        {
            var offset = candidate.AimPosition - origin;
            var distance = offset.magnitude;
            if (distance <= 0.01f)
            {
                return true;
            }

            var hits = Physics.SphereCastAll(
                origin,
                0.04f,
                offset / distance,
                distance + 0.05f,
                targetLayers,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(
                hits,
                (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.collider == null
                    || hit.collider.transform == transform
                    || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                foreach (var component in hit.collider.GetComponentsInParent<
                             Component>(true))
                {
                    if (component == candidate.Component)
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private static bool FamilyAllows(
            PHSUtilityFamilyActionKind familyKind,
            UtilityItemActionKind actionKind)
        {
            return familyKind switch
            {
                PHSUtilityFamilyActionKind.Wrench =>
                    actionKind is UtilityItemActionKind.DeviceRepair
                        or UtilityItemActionKind.HullBreachRepair
                        or UtilityItemActionKind.SteamLeakRepair
                        or UtilityItemActionKind.OxygenLeakRepair
                        or UtilityItemActionKind.OxygenGeneratorRepair
                        or UtilityItemActionKind.GravityGeneratorRepair,
                PHSUtilityFamilyActionKind.FireExtinguisher =>
                    actionKind == UtilityItemActionKind.FireSuppression,
                _ => false
            };
        }

        private struct TargetCandidate
        {
            private IEventRepairTargetHandle eventTarget;
            private IShipAccidentRepairTarget shipTarget;
            private IUtilityAttackTarget utilityTarget;

            public Component Component;
            public UtilityItemActionKind ActionKind;
            public float Distance;
            public Vector3 AimPosition;

            public bool IsRepairComplete =>
                eventTarget is IEventRepairableEffect repairable
                    && repairable.IsRepairComplete
                || shipTarget != null && shipTarget.IsRepairComplete
                || utilityTarget is PHSFirePatchRuntimeTarget fireTarget
                    && !fireTarget.IsActive;

            public static TargetCandidate ForEvent(
                Component component,
                IEventRepairTargetHandle target,
                UtilityItemActionKind actionKind) =>
                new()
                {
                    Component = component,
                    eventTarget = target,
                    ActionKind = actionKind
                };

            public static TargetCandidate ForShip(
                Component component,
                IShipAccidentRepairTarget target,
                UtilityItemActionKind actionKind) =>
                new()
                {
                    Component = component,
                    shipTarget = target,
                    ActionKind = actionKind
                };

            public static TargetCandidate ForUtility(
                Component component,
                IUtilityAttackTarget target,
                UtilityItemActionKind actionKind) =>
                new()
                {
                    Component = component,
                    utilityTarget = target,
                    ActionKind = actionKind
                };

            public bool TryResolve(
                NetworkPlayerItemRecord record,
                uint sequence,
                GameObject attacker)
            {
                if (eventTarget != null)
                {
                    return NetworkEventCoordinator.Instance != null
                        && NetworkEventCoordinator.Instance.RequestEffectRepair(
                            eventTarget,
                            record,
                            sequence);
                }

                if (shipTarget != null)
                {
                    return PHSNetworkShipAccidentCoordinator.Instance != null
                        && PHSNetworkShipAccidentCoordinator.Instance.RequestRepair(
                            shipTarget,
                            record,
                            sequence);
                }

                return utilityTarget != null
                    && utilityTarget.TryResolveUtilityAttack(
                        new UtilityAttackHit(
                            record.HeldItemId,
                            attacker,
                            sequence));
            }
        }
    }
}
