using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    [Serializable]
    public sealed class PHSFirePatchLink
    {
        [SerializeField] private PHSFirePatch target;
        [SerializeField, Min(0f)] private float spreadWeight = 1f;
        [SerializeField] private byte minimumSourceIntensity = 2;
        [SerializeField] private bool oneWay;

        public PHSFirePatch Target => target;
        public float SpreadWeight => spreadWeight;
        public byte MinimumSourceIntensity => minimumSourceIntensity;
        public bool OneWay => oneWay;

        public bool IsEligible(byte sourceIntensity)
        {
            return sourceIntensity >= minimumSourceIntensity;
        }

        public bool TryValidate(
            PHSFirePatch source,
            out string reason)
        {
            if (source == null)
            {
                reason = "source_patch_missing";
                return false;
            }

            if (target == null)
            {
                reason = "target_patch_missing";
                return false;
            }

            if (target == source)
            {
                reason = $"target_patch_self_reference:{source.PatchId}";
                return false;
            }

            if (spreadWeight <= 0f
                || float.IsNaN(spreadWeight)
                || float.IsInfinity(spreadWeight))
            {
                reason = $"spread_weight_invalid:{spreadWeight}";
                return false;
            }

            if (minimumSourceIntensity == 0)
            {
                reason = "minimum_source_intensity_invalid:0";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
