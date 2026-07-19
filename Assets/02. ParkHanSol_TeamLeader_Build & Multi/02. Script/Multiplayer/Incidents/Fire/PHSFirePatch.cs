using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [DisallowMultipleComponent]
    public sealed class PHSFirePatch : MonoBehaviour
    {
        [Header("Patch Identity")]
        [SerializeField] private ushort patchId;

        [Header("Hazard And Presentation")]
        [SerializeField] private Collider hazardBounds;
        [SerializeField] private Transform presentationRoot;
        [SerializeField, Min(0f)] private float flammability = 1f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        [Header("Spread Graph")]
        [SerializeField] private PHSFirePatchLink[] neighbors =
            Array.Empty<PHSFirePatchLink>();

        [Header("Local Visual Variation")]
        [SerializeField] private Transform[] visualSockets =
            Array.Empty<Transform>();

        public ushort PatchId => patchId;
        public Collider HazardBounds => hazardBounds;
        public Transform PresentationRoot => presentationRoot;
        public float Flammability => flammability;
        public float DamageMultiplier => damageMultiplier;
        public IReadOnlyList<PHSFirePatchLink> Neighbors =>
            neighbors ?? Array.Empty<PHSFirePatchLink>();
        public IReadOnlyList<Transform> VisualSockets =>
            visualSockets ?? Array.Empty<Transform>();

        public bool TryValidate(out string reason)
        {
            if (patchId == 0)
            {
                reason = "patch_id_invalid:0";
                return false;
            }

            if (hazardBounds == null)
            {
                reason = "hazard_bounds_missing";
                return false;
            }

            if (!hazardBounds.isTrigger)
            {
                reason = "hazard_bounds_not_trigger";
                return false;
            }

            if (presentationRoot == null)
            {
                reason = "presentation_root_missing";
                return false;
            }

            if (flammability <= 0f
                || float.IsNaN(flammability)
                || float.IsInfinity(flammability))
            {
                reason = $"flammability_invalid:{flammability}";
                return false;
            }

            if (damageMultiplier <= 0f
                || float.IsNaN(damageMultiplier)
                || float.IsInfinity(damageMultiplier))
            {
                reason = $"damage_multiplier_invalid:{damageMultiplier}";
                return false;
            }

            var uniqueTargets = new HashSet<PHSFirePatch>();
            foreach (var link in Neighbors)
            {
                if (link == null)
                {
                    reason = "neighbor_link_missing";
                    return false;
                }

                if (!link.TryValidate(this, out var linkReason))
                {
                    reason = $"neighbor_link_invalid:{linkReason}";
                    return false;
                }

                if (!uniqueTargets.Add(link.Target))
                {
                    reason = $"neighbor_link_duplicate:{link.Target.PatchId}";
                    return false;
                }
            }

            if (visualSockets == null || visualSockets.Length == 0)
            {
                reason = "visual_sockets_empty";
                return false;
            }

            var uniqueSockets = new HashSet<Transform>();
            foreach (var socket in visualSockets)
            {
                if (socket == null)
                {
                    reason = "visual_socket_missing";
                    return false;
                }

                if (!uniqueSockets.Add(socket))
                {
                    reason = $"visual_socket_duplicate:{socket.name}";
                    return false;
                }

                if (socket != presentationRoot
                    && !socket.IsChildOf(presentationRoot))
                {
                    reason = $"visual_socket_outside_presentation_root:{socket.name}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
