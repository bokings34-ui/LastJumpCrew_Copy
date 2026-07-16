using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [Serializable]
    public sealed class PHSMapShipAccidentWeight
    {
        [SerializeField] private PHSShipAccidentDefinitionSO definition;
        [SerializeField, Min(0.01f)] private float weight = 1f;
        [SerializeField, Range(0f, 1f)] private float warpChargeMultiplier = 0.8f;

        public PHSShipAccidentDefinitionSO Definition => definition;
        public float Weight => weight;
        public float WarpChargeMultiplier => warpChargeMultiplier;

        public bool TryValidate(out string reason)
        {
            if (definition == null)
            {
                reason = "definition_missing";
                return false;
            }

            if (!definition.TryValidate(out var definitionReason))
            {
                reason = $"definition_invalid:{definitionReason}";
                return false;
            }

            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
            {
                reason = $"weight_invalid:value={weight}";
                return false;
            }

            if (warpChargeMultiplier < 0f || warpChargeMultiplier > 1f
                || float.IsNaN(warpChargeMultiplier)
                || float.IsInfinity(warpChargeMultiplier))
            {
                reason = $"warp_charge_multiplier_invalid:value={warpChargeMultiplier}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
