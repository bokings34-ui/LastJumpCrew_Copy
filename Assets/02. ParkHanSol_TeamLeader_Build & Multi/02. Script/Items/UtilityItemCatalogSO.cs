using LastJumpCrew.Common;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    [CreateAssetMenu(
        fileName = "PHS_UtilityItemCatalog",
        menuName = "LastJumpCrew/ParkHanSol/Utility Item Catalog")]
    public sealed class UtilityItemCatalogSO : ScriptableObject
    {
        [SerializeField] private List<UtilityItemDataSO> items = new();

        public IReadOnlyList<UtilityItemDataSO> Items => items;

        public bool TryGetById(string itemId, out UtilityItemDataSO item)
        {
            item = null;
            if (items == null || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            foreach (var candidate in items)
            {
                if (candidate != null
                    && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
                {
                    item = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool Contains(UtilityItemDataSO item)
        {
            return item != null && items != null && items.Contains(item);
        }
    }
}
