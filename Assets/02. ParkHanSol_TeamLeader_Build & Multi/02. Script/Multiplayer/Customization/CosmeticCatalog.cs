using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    // 모든 판매/장착 가능 코스메틱의 단일 조회표다.
    // 런타임은 ScriptableObject 자체가 아닌 itemId만 저장하거나 네트워크로 주고받는다.
    [CreateAssetMenu(
        fileName = "PHS_CosmeticCatalog",
        menuName = "LastJumpCrew/ParkHanSol/Customization/Cosmetic Catalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        [SerializeField] private List<CosmeticItemData> items = new List<CosmeticItemData>();

        private Dictionary<string, CosmeticItemData> itemsById;

        public IReadOnlyList<CosmeticItemData> Items => items;

        public bool TryGetItem(string itemId, out CosmeticItemData item)
        {
            item = null;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"[{nameof(CosmeticCatalog)}] Item lookup failed. itemId is empty on catalog '{name}'.", this);
                return false;
            }

            EnsureLookup();
            if (itemsById.TryGetValue(itemId, out item))
            {
                return true;
            }

            Debug.LogError($"[{nameof(CosmeticCatalog)}] Item lookup failed. Unknown itemId '{itemId}' in catalog '{name}'.", this);
            return false;
        }

        private void OnValidate()
        {
            itemsById = null;
        }

        private void EnsureLookup()
        {
            if (itemsById != null)
            {
                return;
            }

            itemsById = new Dictionary<string, CosmeticItemData>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    Debug.LogError($"[{nameof(CosmeticCatalog)}] Item index {i} is empty in catalog '{name}'.", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.ItemId))
                {
                    Debug.LogError($"[{nameof(CosmeticCatalog)}] Item '{item.name}' has empty itemId in catalog '{name}'.", item);
                    continue;
                }

                if (itemsById.ContainsKey(item.ItemId))
                {
                    Debug.LogError($"[{nameof(CosmeticCatalog)}] Duplicate itemId '{item.ItemId}' in catalog '{name}'.", item);
                    continue;
                }

                itemsById.Add(item.ItemId, item);
            }
        }
    }
}
