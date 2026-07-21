using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [CreateAssetMenu(
        fileName = "PHS_ShipAccidentCatalog",
        menuName = "LastJumpCrew/ParkHanSol/Ship Accident Catalog")]
    public sealed class PHSShipAccidentCatalogSO : ScriptableObject
    {
        [SerializeField] private List<PHSShipAccidentDefinitionSO> definitions = new();

        private readonly Dictionary<PHSShipAccidentId, PHSShipAccidentDefinitionSO> index = new();

        public bool TryResolve(
            PHSShipAccidentId id,
            out PHSShipAccidentDefinitionSO definition)
        {
            if (!TryBuildIndex(out var reason))
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_CATALOG_INVALID asset={name} reason={reason}", this);
                definition = null;
                return false;
            }

            return index.TryGetValue(id, out definition);
        }

        public bool TryValidate(out string reason)
        {
            return TryBuildIndex(out reason);
        }

        private void OnEnable()
        {
            if (!TryBuildIndex(out var reason))
            {
                Debug.LogError($"PHS_SHIP_ACCIDENT_CATALOG_INVALID asset={name} reason={reason}", this);
            }
        }

        private bool TryBuildIndex(out string reason)
        {
            index.Clear();
            if (definitions == null || definitions.Count == 0)
            {
                reason = "definitions_missing";
                return false;
            }

            for (var indexValue = 0; indexValue < definitions.Count; indexValue++)
            {
                var definition = definitions[indexValue];
                if (definition == null)
                {
                    reason = $"definition_missing:index={indexValue}";
                    return false;
                }

                if (!definition.TryValidate(out var definitionReason))
                {
                    reason = $"definition_invalid:index={indexValue}:{definitionReason}";
                    return false;
                }

                if (!index.TryAdd(definition.Id, definition))
                {
                    reason = $"definition_duplicate:id={definition.Id}";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
