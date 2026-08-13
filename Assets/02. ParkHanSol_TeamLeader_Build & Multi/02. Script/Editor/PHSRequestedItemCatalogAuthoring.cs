using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Shop;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSRequestedItemCatalogAuthoring
    {
        private const string DataRoot =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data";
        private const string ShopCatalogPath =
            DataRoot + "/ShopProducts/PHS_ShopCatalog_0715.asset";
        private const string ItemCatalogPath =
            DataRoot + "/UtilityItems/PHS_UtilityItemCatalog_0717.asset";

        private static readonly ItemSpec[] Specs =
        {
            new("ParkHanSol_AutoRepairKitItemPrefabData.asset", "auto_repair_kit", 200, false, 100,
                (UtilityItemActionKind.DeviceRepair, 100, 0),
                (UtilityItemActionKind.SteamLeakRepair, 100, 0),
                (UtilityItemActionKind.OxygenLeakRepair, 100, 0),
                (UtilityItemActionKind.OxygenGeneratorRepair, 100, 0),
                (UtilityItemActionKind.GravityGeneratorRepair, 100, 0)),
            new("ParkHanSol_FoamSealantGunItemPrefabData.asset", "foam_sealant_gun", 140, false, 100,
                (UtilityItemActionKind.FireSuppression, 100, 0),
                (UtilityItemActionKind.HullBreachRepair, 100, 0)),
            new("ParkHanSol_FuturisticAdjustableWrenchItemPrefabData.asset", "futuristic_adjustable_wrench", 150, true, 150,
                (UtilityItemActionKind.DeviceRepair, 40, 1),
                (UtilityItemActionKind.HullBreachRepair, 40, 1),
                (UtilityItemActionKind.SteamLeakRepair, 40, 1),
                (UtilityItemActionKind.OxygenLeakRepair, 40, 1),
                (UtilityItemActionKind.OxygenGeneratorRepair, 40, 1),
                (UtilityItemActionKind.GravityGeneratorRepair, 40, 1)),
            new("ParkHanSol_FuturisticCanisterItemPrefabData.asset", "futuristic_canister", 80, false, 100,
                (UtilityItemActionKind.PowerRestore, 100, 0)),
            new("ParkHanSol_TripoFireExtinguisherItemPrefabData.asset", "tripo_fire_extinguisher", 170, true, 150,
                (UtilityItemActionKind.FireSuppression, 70, 1))
        };

        private static readonly HashSet<string> VendingOnlyToolIds = new(StringComparer.Ordinal)
        {
            "wrench",
            "futuristic_adjustable_wrench",
            "fire_extinguisher",
            "tripo_fire_extinguisher",
            "battery_pack"
        };

        [MenuItem("Tools/ParkHanSol/Items/Author Requested Shop Items")]
        public static void Author()
        {
            foreach (var spec in Specs)
            {
                ConfigureItem(spec);
            }

            ApplyShopOfferPolicy();

            var itemCatalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(ItemCatalogPath);
            var serializedCatalog = new SerializedObject(itemCatalog);
            var items = serializedCatalog.FindProperty("items");
            var existing = Enumerable.Range(0, items.arraySize)
                .Select(index => items.GetArrayElementAtIndex(index).objectReferenceValue)
                .OfType<UtilityItemDataSO>()
                .ToList();
            foreach (var spec in Specs)
            {
                var item = LoadItem(spec);
                if (!existing.Contains(item))
                {
                    existing.Add(item);
                }
            }

            WriteObjectList(items, existing);
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(itemCatalog);
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Tools/ParkHanSol/Items/Validate Requested Shop Items")]
        public static void Validate()
        {
            var errors = new List<string>();
            foreach (var spec in Specs)
            {
                var item = LoadItem(spec);
                if (item == null || item.ItemId != spec.Id || item.Price != spec.Price
                    || item.UsesDurability != spec.UsesDurability
                    || item.MaxDurability != spec.MaxDurability
                    || item.GetType() != typeof(UtilityItemDataSO))
                {
                    errors.Add($"item_contract:{spec.Id}");
                    continue;
                }

                foreach (var action in spec.Actions)
                {
                    if (!item.TryGetActionProfile(action.Kind, out var profile)
                        || profile.Amount != action.Amount
                        || profile.DurabilityCost != action.Cost)
                    {
                        errors.Add($"action_contract:{spec.Id}:{action.Kind}");
                    }
                }
            }

            var shop = AssetDatabase.LoadAssetAtPath<ShopCatalogSO>(ShopCatalogPath);
            var shopProducts = shop?.Products ?? Array.Empty<ShopProductData>();
            if (shopProducts.Any(product => product == null
                || product.ItemPrefabData == null
                || VendingOnlyToolIds.Contains(product.ItemPrefabData.ItemId)))
            {
                errors.Add("shop_catalog_vending_tool_offer_present");
            }

            var consumableOffers = shopProducts
                .Where(product => product?.ItemPrefabData != null
                    && !product.ItemPrefabData.UsesDurability
                    && product.ItemPrefabData.ItemId is "auto_repair_kit" or "foam_sealant_gun" or "futuristic_canister")
                .ToArray();
            if (consumableOffers.Length < 2
                || consumableOffers.Any(product => product.StockPolicy != ShopStockPolicy.Unlimited))
            {
                errors.Add($"shop_catalog_consumable_offer_policy_invalid count={consumableOffers.Length}");
            }

            var itemCatalog = AssetDatabase.LoadAssetAtPath<UtilityItemCatalogSO>(ItemCatalogPath);
            if (itemCatalog == null || itemCatalog.Items.Count != 21
                || Specs.Any(spec => !itemCatalog.TryGetById(spec.Id, out _)))
            {
                errors.Add($"item_catalog expected=18 actual={itemCatalog?.Items.Count ?? 0}");
            }

            if (errors.Count != 0)
            {
                throw new InvalidOperationException("PHS_REQUESTED_ITEM_VALIDATION_FAILED\n" + string.Join("\n", errors));
            }

            Debug.Log($"PHS_REQUESTED_ITEM_VALIDATION_PASS shop={shopProducts.Count()} utility=21 requested=5 vending_tools=0");
        }

        private static void ApplyShopOfferPolicy()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ShopCatalogSO>(ShopCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("PHS_REQUESTED_ITEM_AUTHOR_FAILED reason=shop_catalog_missing");
            }

            var retained = catalog.Products
                .Where(product => product != null
                    && product.ItemPrefabData != null
                    && !VendingOnlyToolIds.Contains(product.ItemPrefabData.ItemId))
                .ToArray();
            var serialized = new SerializedObject(catalog);
            WriteObjectList(serialized.FindProperty("products"), retained);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureItem(ItemSpec spec)
        {
            var item = LoadItem(spec);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("price").intValue = spec.Price;
            serialized.FindProperty("usesDurability").boolValue = spec.UsesDurability;
            serialized.FindProperty("maxDurability").intValue = spec.MaxDurability;
            serialized.FindProperty("durabilityCostPerUse").intValue = spec.UsesDurability ? 1 : 0;
            var profiles = serialized.FindProperty("actionProfiles");
            profiles.arraySize = spec.Actions.Length;
            for (var index = 0; index < spec.Actions.Length; index++)
            {
                var entry = profiles.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("actionKind").enumValueIndex = (int)spec.Actions[index].Kind;
                entry.FindPropertyRelative("amount").intValue = spec.Actions[index].Amount;
                entry.FindPropertyRelative("durabilityCost").intValue = spec.Actions[index].Cost;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static UtilityItemDataSO LoadItem(ItemSpec spec) =>
            AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(DataRoot + "/UtilityItems/" + spec.AssetName);

        private static void SetCatalogObjects<T>(string catalogPath, string propertyName, IEnumerable<string> paths) where T : UnityEngine.Object
        {
            var catalog = AssetDatabase.LoadMainAssetAtPath(catalogPath);
            var serialized = new SerializedObject(catalog);
            var list = serialized.FindProperty(propertyName);
            WriteObjectList(list, paths.Select(AssetDatabase.LoadAssetAtPath<T>));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void WriteObjectList<T>(SerializedProperty list, IEnumerable<T> objects) where T : UnityEngine.Object
        {
            var values = objects.ToArray();
            list.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private sealed class ItemSpec
        {
            public ItemSpec(string assetName, string id, int price, bool usesDurability, int maxDurability,
                params (UtilityItemActionKind Kind, int Amount, int Cost)[] actions)
            {
                AssetName = assetName; Id = id; Price = price;
                UsesDurability = usesDurability; MaxDurability = maxDurability; Actions = actions;
            }

            public string AssetName { get; }
            public string Id { get; }
            public int Price { get; }
            public bool UsesDurability { get; }
            public int MaxDurability { get; }
            public (UtilityItemActionKind Kind, int Amount, int Cost)[] Actions { get; }
        }
    }
}
