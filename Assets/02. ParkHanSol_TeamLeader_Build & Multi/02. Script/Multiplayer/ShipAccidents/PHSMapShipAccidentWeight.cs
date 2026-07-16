using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [Serializable]
    public sealed class PHSMapShipAccidentWeight
    {
        [SerializeField] private PHSShipAccidentDefinitionSO definition;
        [SerializeField, Min(0.01f)] private float weight = 1f;

        public PHSShipAccidentDefinitionSO Definition => definition;
        public float Weight => weight;

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

            reason = null;
            return true;
        }
    }
}
