#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Shop;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSCanonicalSpecialItemAuthoring
    {
        private const string Root = "Assets/02. ParkHanSol_TeamLeader_Build & Multi";
        private const string ItemDataRoot = Root + "/04. Data/UtilityItems";
        private const string ProductRoot = Root + "/04. Data/ShopProducts";
        private const string PrefabRoot = Root + "/03. Prefab/Props/Prefabs/Items/Special";
        private const string ItemCatalogPath = ItemDataRoot + "/PHS_UtilityItemCatalog_0717.asset";
        private const string ShopCatalogPath = ProductRoot + "/PHS_ShopCatalog_0715.asset";
        private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

        private static readonly Spec[] Specs =
        {
            new("freeze_sprayer", "Freeze Sprayer", 180, ItemUseType.Spray, PrimitiveType.Cylinder, false),
            new("spider_web_bomb", "Spider Web Bomb", 160, ItemUseType.Throwable, PrimitiveType.Sphere, false),
            new("hammer", "Hammer", 140, ItemUseType.Melee, PrimitiveType.Cube, true)
        };

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Canonical Special Items")]
        public static void Author()
        {
            EnsureFolder(PrefabRoot);
            var items = new Dictionary<string, UtilityItemDataSO>();
            foreach (var spec in Specs)
            {
                items.Add(spec.Id, GetOrCreateItem(spec));
            }

            var spiderZoneRoot = CreateSpiderZonePrefab();
            var spiderZone = spiderZoneRoot.GetComponent<SpiderWebSlowZone>();
            if (spiderZone == null)
            {
                throw new InvalidOperationException(
                    "PHS_SPECIAL_ITEM_AUTHOR_FAILED reason=spider_zone_component_missing");
            }
            foreach (var spec in Specs)
            {
                var item = items[spec.Id];
                var hand = CreateItemPrefab(spec, item, "Hand", false, spiderZone);
                var dropped = CreateItemPrefab(spec, item, "Dropped", true, spiderZone);
                ConfigureItem(item, spec, hand, dropped, null);
                CreateOrConfigureProduct(item, spec);
            }

            AppendCatalogItems(items.Values);
            AppendCatalogProducts(items.Values);
            UnregisterNetworkPrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath("spider_web_bomb", "Thrown")));
            RegisterNetworkPrefabs(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("freeze_sprayer", "Dropped")),
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("hammer", "Dropped")),
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("spider_web_bomb", "Dropped")),
                spiderZoneRoot);
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("PHS_CANONICAL_SPECIAL_ITEMS_AUTHOR_OK items=3 products=3 network_prefabs=4 spider=rebuilt");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Canonical Special Items")]
        public static void Validate()
        {
            var itemCatalog = Require<UtilityItemCatalogSO>(ItemCatalogPath);
            var shopCatalog = Require<ShopCatalogSO>(ShopCatalogPath);
            var expectedNetwork = new HashSet<GameObject>();
            foreach (var spec in Specs)
            {
                var item = Require<UtilityItemDataSO>(ItemPath(spec.Id));
                if (!itemCatalog.TryGetById(spec.Id, out var catalogItem) || catalogItem != item)
                    throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=item_catalog_missing item={spec.Id}");
                if (!shopCatalog.TryGetByItemData(item, out var product) || product == null)
                    throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=shop_product_missing item={spec.Id}");
                if (item.Icon == null)
                    Debug.LogWarning($"PHS_SPECIAL_ITEM_ICON_BLOCKER item={spec.Id} reason=team_sprite_missing");
                ValidateEffects(item, spec.Id);
                ValidatePrefab(PrefabPath(spec.Id, "Hand"), false);
                ValidatePrefab(PrefabPath(spec.Id, "Dropped"), true);
                ValidateTeamVisual(spec, PrefabPath(spec.Id, "Hand"));
                ValidateTeamVisual(spec, PrefabPath(spec.Id, "Dropped"));
                if (spec.Id != "spider_web_bomb") expectedNetwork.Add(Require<GameObject>(PrefabPath(spec.Id, "Dropped")));
            }
            var spiderZone = Require<GameObject>(PrefabRoot + "/PHS_SpiderWebSlowZone.prefab");
            ValidatePrefab(PrefabPath("spider_web_bomb", "Hand"), false);
            ValidatePrefab(PrefabPath("spider_web_bomb", "Dropped"), true);
            ValidatePrefab(PrefabRoot + "/PHS_SpiderWebSlowZone.prefab", true);
            var spiderDropped = Require<GameObject>(PrefabPath("spider_web_bomb", "Dropped"));
            if (spiderDropped.GetComponent<SpiderWebBombImpact>() == null || spiderZone.GetComponent<SpiderWebSlowZone>() == null)
                throw new InvalidOperationException("PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=spider_status_component_missing");
            if (spiderDropped.GetComponent<PHSSpiderWebBombImpactEffect>() == null)
                throw new InvalidOperationException("PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=spider_impact_effect_missing");
            expectedNetwork.Add(spiderDropped); expectedNetwork.Add(spiderZone);
            var list = Require<NetworkPrefabsList>(NetworkPrefabsPath);
            foreach (var prefab in expectedNetwork)
            {
                var count = 0;
                foreach (var entry in list.PrefabList) if (entry != null && entry.Prefab == prefab) count++;
                if (count != 1) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=network_prefab_count prefab={prefab.name} count={count}");
            }

            ValidateServerOnlyStatusEffects();
            Debug.Log("PHS_CANONICAL_SPECIAL_ITEMS_VALIDATE_OK data=3 shop=3 network_prefabs=4 source_prefab_refs=0");
        }

        private static void ValidateServerOnlyStatusEffects()
        {
            var actor = PrefabUtility.LoadPrefabContents(
                PrefabPath("spider_web_bomb", "Dropped"));
            try
            {
                actor.SetActive(false);
                var presentation = new GameObject("ElectricEffect");
                presentation.transform.SetParent(actor.transform, false);
                var controller = actor.AddComponent<StatusEffectController>();
                var controllerData = new SerializedObject(controller);
                controllerData.FindProperty("electricShockEffectRoot").objectReferenceValue = presentation;
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                actor.SetActive(true);

                foreach (var spec in Specs)
                {
                    var item = Require<UtilityItemDataSO>(ItemPath(spec.Id));
                    if (!ItemEffectResolver.ApplyEffects(item, actor, Vector3.forward, actor))
                    {
                        throw new InvalidOperationException(
                            $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=status_apply_rejected item={spec.Id}");
                    }
                }

                if (!controller.IsFrozen || !controller.IsSlowed)
                {
                    throw new InvalidOperationException(
                        $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=server_status_state_invalid " +
                        $"freeze={controller.IsFrozen} slow={controller.IsSlowed}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }

        private static UtilityItemDataSO GetOrCreateItem(Spec spec)
        {
            var path = ItemPath(spec.Id);
            var item = AssetDatabase.LoadAssetAtPath<UtilityItemDataSO>(path);
            if (item != null) return item;
            item = ScriptableObject.CreateInstance<UtilityItemDataSO>();
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        private static GameObject CreateItemPrefab(
            Spec spec,
            UtilityItemDataSO item,
            string suffix,
            bool networked,
            SpiderWebSlowZone spiderZone)
        {
            var path = PrefabPath(spec.Id, suffix);
            var root = new GameObject($"PHS_{spec.Id}_{suffix}");
            try
            {
                if (networked) root.AddComponent<NetworkObject>();
                if (networked) root.AddComponent<Rigidbody>();
                if (networked) root.AddComponent<BoxCollider>();
                var itemObject = root.AddComponent<UtilityItemObject>();
                if (!networked && spec.Id == "freeze_sprayer")
                {
                    root.AddComponent<FireExtinguisherItemUse>();
                }
                else if (!networked && spec.Id == "hammer")
                {
                    root.AddComponent<LastJumpCrew.ParkHanSol.Item.WrenchItemUse>();
                }
                if (networked)
                {
                    root.AddComponent<NetworkTransform>();
                    root.AddComponent<NetworkItemPhysicsAuthority>();
                    root.AddComponent<ThrownItemImpact>();
                    if (spec.Id == "spider_web_bomb")
                    {
                        root.AddComponent<PHSSpiderWebBombImpactEffect>();
                        var spiderImpact = root.AddComponent<SpiderWebBombImpact>();
                        var spiderData = new SerializedObject(spiderImpact);
                        spiderData.FindProperty("slowZonePrefab").objectReferenceValue = spiderZone;
                        spiderData.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (spec.Durability)
                    {
                        var durability = root.AddComponent<NetworkUtilityItemDurabilityState>();
                        var durabilityData = new SerializedObject(durability);
                        durabilityData.FindProperty("itemObject").objectReferenceValue = itemObject;
                        durabilityData.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                AttachTeamVisualIfAvailable(root.transform, spec, suffix);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject CreateSpiderZonePrefab()
        {
            var path = PrefabRoot + "/PHS_SpiderWebSlowZone.prefab";
            var root = new GameObject("PHS_SpiderWebSlowZone");
            try
            {
                root.AddComponent<NetworkObject>();
                var collider = root.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = 2f;
                root.AddComponent<SpiderWebSlowZone>();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void ConfigureItem(UtilityItemDataSO item, Spec spec, GameObject hand, GameObject dropped, GameObject thrown)
        {
            var data = new SerializedObject(item);
            data.FindProperty("itemId").stringValue = spec.Id; data.FindProperty("displayName").stringValue = spec.Name;
            data.FindProperty("price").intValue = spec.Price; data.FindProperty("useType").enumValueIndex = (int)spec.UseType;
            data.FindProperty("handPrefab").objectReferenceValue = hand; data.FindProperty("droppedPrefab").objectReferenceValue = dropped; data.FindProperty("thrownPrefab").objectReferenceValue = thrown;
            data.FindProperty("usesDurability").boolValue = spec.Durability; data.FindProperty("maxDurability").intValue = 100; data.FindProperty("durabilityCostPerUse").intValue = spec.Durability ? 1 : 0;
            data.FindProperty("attackRange").floatValue = spec.Id == "freeze_sprayer" ? 5f : spec.Id == "spider_web_bomb" ? 12f : 1.2f;
            data.FindProperty("attackRadius").floatValue = spec.Id == "hammer" ? 1.5f : 1f;
            data.FindProperty("targetLayers").intValue = ~0;
            var effects = data.FindProperty("hitEffects");
            effects.arraySize = spec.Id == "freeze_sprayer" ? 2 : spec.Id == "hammer" ? 3 : 2;
            if (spec.Id == "freeze_sprayer")
            {
                ConfigureEffect(effects.GetArrayElementAtIndex(0), ItemEffectType.Damage, 8f, StatusEffectType.None, 0f);
                ConfigureEffect(effects.GetArrayElementAtIndex(1), ItemEffectType.StatusEffect, 0f, StatusEffectType.Freeze, 3f);
            }
            else if (spec.Id == "hammer")
            {
                ConfigureEffect(effects.GetArrayElementAtIndex(0), ItemEffectType.Damage, 25f, StatusEffectType.None, 0f);
                ConfigureEffect(effects.GetArrayElementAtIndex(1), ItemEffectType.Knockback, 15f, StatusEffectType.None, 0f);
                ConfigureEffect(effects.GetArrayElementAtIndex(2), ItemEffectType.StatusEffect, 0f, StatusEffectType.Freeze, 3f);
            }
            else
            {
                ConfigureEffect(effects.GetArrayElementAtIndex(0), ItemEffectType.Damage, 20f, StatusEffectType.None, 0f);
                ConfigureEffect(effects.GetArrayElementAtIndex(1), ItemEffectType.StatusEffect, 0f, StatusEffectType.Slow, 3f);
            }
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item);
            ConfigureItemObject(hand, item); ConfigureItemObject(dropped, item);
        }

        private static void ConfigureItemObject(GameObject prefab, UtilityItemDataSO item)
        {
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try { var objectData = new SerializedObject(root.GetComponent<UtilityItemObject>()); objectData.FindProperty("itemData").objectReferenceValue = item; objectData.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(prefab)); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureEffect(
            SerializedProperty effect,
            ItemEffectType type,
            float amount,
            StatusEffectType status,
            float duration)
        {
            effect.FindPropertyRelative("effectType").enumValueIndex = (int)type;
            effect.FindPropertyRelative("targetType").enumValueIndex = (int)EffectTargetType.EnemyOnly;
            effect.FindPropertyRelative("amount").floatValue = amount;
            effect.FindPropertyRelative("statusEffectType").enumValueIndex = (int)status;
            effect.FindPropertyRelative("duration").floatValue = duration;
        }

        private static void ValidateEffects(UtilityItemDataSO item, string itemId)
        {
            var data = new SerializedObject(item);
            var effects = data.FindProperty("hitEffects");
            var expectedCount = itemId == "freeze_sprayer" ? 2 : itemId == "hammer" ? 3 : 2;
            if (effects == null || effects.arraySize != expectedCount)
            {
                throw new InvalidOperationException(
                    $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=effect_count item={itemId}");
            }

            var expectedStatus = itemId == "spider_web_bomb" ? StatusEffectType.Slow : StatusEffectType.Freeze;
            var statusIndex = itemId == "hammer" ? 2 : 1;
            var status = effects.GetArrayElementAtIndex(statusIndex);
            if (status.FindPropertyRelative("effectType").enumValueIndex != (int)ItemEffectType.StatusEffect
                || status.FindPropertyRelative("targetType").enumValueIndex != (int)EffectTargetType.EnemyOnly
                || status.FindPropertyRelative("statusEffectType").enumValueIndex != (int)expectedStatus
                || status.FindPropertyRelative("duration").floatValue < 3f)
            {
                throw new InvalidOperationException(
                    $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=status_effect_invalid item={itemId}");
            }
        }

        private static void AttachTeamVisualIfAvailable(Transform destination, Spec spec, string suffix)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                SourceVisualPath(spec.Id, suffix));
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SPECIAL_ITEM_AUTHOR_FAILED reason=team_visual_missing item={spec.Id} variant={suffix}");
            }

            var visual = PrefabUtility.InstantiatePrefab(source, destination) as GameObject;
            if (visual == null)
            {
                throw new InvalidOperationException(
                    $"PHS_SPECIAL_ITEM_AUTHOR_FAILED reason=team_visual_instantiate_failed item={spec.Id}");
            }

            visual.name = "TeamVisual";
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
        }

        private static void CreateOrConfigureProduct(UtilityItemDataSO item, Spec spec)
        {
            var path = ProductRoot + "/PHS_" + spec.Id + "_ShopProductData.asset";
            var product = AssetDatabase.LoadAssetAtPath<ShopProductData>(path) ?? ScriptableObject.CreateInstance<ShopProductData>();
            if (AssetDatabase.GetAssetPath(product).Length == 0) AssetDatabase.CreateAsset(product, path);
            var data = new SerializedObject(product); data.FindProperty("offerId").stringValue = spec.Id; data.FindProperty("itemPrefabData").objectReferenceValue = item; data.FindProperty("purchasePrice").intValue = spec.Price; data.FindProperty("isDisplayed").boolValue = true; data.FindProperty("stockPolicy").enumValueIndex = (int)ShopStockPolicy.Unlimited; data.FindProperty("shopDescription").stringValue = spec.Name; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(product);
        }

        private static void AppendCatalogItems(IEnumerable<UtilityItemDataSO> additions) { var catalog = Require<UtilityItemCatalogSO>(ItemCatalogPath); AppendObjectReferences(catalog, "items", additions); }
        private static void AppendCatalogProducts(IEnumerable<UtilityItemDataSO> additions) { var catalog = Require<ShopCatalogSO>(ShopCatalogPath); var products = new List<ShopProductData>(); foreach (var item in additions) products.Add(Require<ShopProductData>(ProductRoot + "/PHS_" + item.ItemId + "_ShopProductData.asset")); AppendObjectReferences(catalog, "products", products); }
        private static void AppendObjectReferences(UnityEngine.Object owner, string propertyName, IEnumerable<UnityEngine.Object> additions) { var data = new SerializedObject(owner); var list = data.FindProperty(propertyName); var values = new List<UnityEngine.Object>(); for (var i=0;i<list.arraySize;i++) values.Add(list.GetArrayElementAtIndex(i).objectReferenceValue); foreach(var value in additions) if(!values.Contains(value)) values.Add(value); list.arraySize=values.Count; for(var i=0;i<values.Count;i++) list.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(owner); }
        private static void UnregisterNetworkPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            var list = Require<NetworkPrefabsList>(NetworkPrefabsPath);
            for (var index = list.PrefabList.Count - 1; index >= 0; index--)
            {
                if (list.PrefabList[index] != null && list.PrefabList[index].Prefab == prefab)
                    list.Remove(list.PrefabList[index]);
            }
            EditorUtility.SetDirty(list);
        }
        private static void RegisterNetworkPrefabs(params GameObject[] prefabs) { var list=Require<NetworkPrefabsList>(NetworkPrefabsPath); foreach(var prefab in prefabs){ var matches=new List<NetworkPrefab>(); foreach(var entry in list.PrefabList) if(entry!=null && entry.Prefab==prefab) matches.Add(entry); for(var i=matches.Count-1;i>=1;i--) list.Remove(matches[i]); if(matches.Count==0) list.Add(new NetworkPrefab{Override=NetworkPrefabOverride.None,Prefab=prefab}); else {matches[0].Override=NetworkPrefabOverride.None;matches[0].SourcePrefabToOverride=null;matches[0].OverridingTargetPrefab=null;matches[0].SourceHashToOverride=0U;} } EditorUtility.SetDirty(list); }
        private static void ValidatePrefab(string path, bool networked) { var root=PrefabUtility.LoadPrefabContents(path); try { var isZone=path.Contains("SlowZone"); if(networked && root.GetComponent<NetworkObject>()==null) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=network_object_missing path={path}"); if(networked && !isZone && (root.GetComponent<NetworkTransform>()==null || root.GetComponent<NetworkItemPhysicsAuthority>()==null)) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=network_physics_contract_missing path={path}"); if(networked && path.Contains("Dropped") && root.GetComponent<ThrownItemImpact>()==null) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=impact_missing path={path}"); if(!networked && path.Contains("freeze_sprayer") && root.GetComponent<FireExtinguisherItemUse>()==null) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=spray_use_missing path={path}"); if(!networked && path.Contains("hammer") && root.GetComponent<LastJumpCrew.ParkHanSol.Item.WrenchItemUse>()==null) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=melee_use_missing path={path}"); foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true)) if(component==null) throw new InvalidOperationException($"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=missing_component path={path}"); } finally { PrefabUtility.UnloadPrefabContents(root); } }
        private static void ValidateTeamVisual(Spec spec, string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.transform.Find("TeamVisual");
                if (spec.Id == "spider_web_bomb")
                {
                    if (visual == null || visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=spider_visual_missing item={spec.Id} path={path} required=team_renderer");
                    }

                    return;
                }

                if (visual == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_SPECIAL_ITEM_VALIDATE_FAILED reason=team_visual_missing item={spec.Id} path={path}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static string ItemPath(string id) => ItemDataRoot + "/PHS_" + id + ".asset";
        private static string PrefabPath(string id, string suffix) => PrefabRoot + "/PHS_" + id + "_" + suffix + ".prefab";
        private static string SourceVisualPath(string id, string suffix) => id switch
        {
            "freeze_sprayer" => "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Freeze_sprayer/ParkHanSol_FreezeSprayerItem_00_" + suffix + ".prefab",
            "hammer" => "Assets/06. JoHanYong_PlayerSystem/03. Prefab/Item/Hammer/ParHanSol_HammerItem_00_" + suffix + ".prefab",
            "spider_web_bomb" => "Assets/03. SeoBoGyeong_Game Economy/05. Object/SourceAsset/GrenadeModule/WebGranade.fbx",
            _ => string.Empty
        };
        private static T Require<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"PHS_SPECIAL_ITEM_AUTHOR_FAILED reason=asset_missing path={path}");
        private static void EnsureFolder(string path) { var parts=path.Split('/'); var current=parts[0]; for(var i=1;i<parts.Length;i++){ var next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next; } }
        private readonly struct Spec { public Spec(string id,string name,int price,ItemUseType useType,PrimitiveType primitive,bool durability){Id=id;Name=name;Price=price;UseType=useType;Primitive=primitive;Durability=durability;} public string Id{get;} public string Name{get;} public int Price{get;} public ItemUseType UseType{get;} public PrimitiveType Primitive{get;} public bool Durability{get;} }
    }
}
#endif
