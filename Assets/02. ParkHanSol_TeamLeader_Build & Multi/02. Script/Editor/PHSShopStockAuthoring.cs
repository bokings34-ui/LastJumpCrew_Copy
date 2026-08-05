using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    public static class PHSShopStockAuthoring
    {
        private const int MinimumDisplayCount = 12;
        private const int MaximumDisplayCount = 12;
        private const string HudPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/UI/ParkHanSol_PlayHudUI.prefab";
        private const string ShopScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/BEAVER_2026/PHS_ExteriorShopScene.unity";
        private const string ShopDisplayPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/PHS_ShopDisplayDesk_Shared.prefab";
        private const string TutorialDisplayPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Tutorial/PHS_NetworkTutorialDisplayDesk.prefab";
        private const string CheckoutPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Shop/PHS_NetworkShopCheckoutCounter.prefab";

        [MenuItem("Tools/ParkHanSol/BEAVER/Author Server Shop Stock And Local HUD")]
        public static void Author()
        {
            ConfigureDisplayPriceTag(ShopDisplayPrefabPath);
            ConfigureDisplayPriceTag(TutorialDisplayPrefabPath);
            ConfigureLocalPickupHud();
            ConfigureCheckoutPriceStack();
            ConfigureShopScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            Debug.Log(
                "PHS_SHOP_STOCK_AUTHORING_OK stock=server_dropped_prefab " +
                "price=world_object_tag refs=explicit");
        }

        [MenuItem("Tools/ParkHanSol/BEAVER/Validate Server Shop Stock And Local HUD")]
        public static void ValidateFromMenu()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateHudPrefab(errors);
            ValidateDisplayPriceTag(ShopDisplayPrefabPath, errors);
            ValidateDisplayPriceTag(TutorialDisplayPrefabPath, errors);
            ValidateCheckoutPriceStack(errors);
            ValidateShopScene(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PHS_SHOP_STOCK_VALIDATION_FAILED\n- " +
                    string.Join("\n- ", errors));
            }

            Debug.Log(
                "PHS_SHOP_STOCK_VALIDATION_OK stock=server_dropped_prefab " +
                "price=world_object_tag refs=explicit");
        }

        private static void ConfigureShopScene()
        {
            WithShopScene(scene =>
            {
                var displayController = FindExactlyOneInScene<
                    ShopRandomDisplayController>(scene, "display_controller");
                var controllerData = new SerializedObject(displayController);
                var slots = FindAllInScene<ShopDisplaySlot>(scene)
                    .OrderBy(slot => GetHierarchyPath(slot.transform), StringComparer.Ordinal)
                    .ToArray();
                if (slots.Length != 12)
                {
                    throw new InvalidOperationException(
                        $"PHS_SHOP_STOCK_AUTHORING_FAILED reason=scene_display_slot_count_invalid actual={slots.Length}");
                }

                SetArray(controllerData, "displaySlots", slots);
                SetInt(controllerData, "minimumDisplayCount", MinimumDisplayCount);
                SetInt(controllerData, "maximumDisplayCount", MaximumDisplayCount);
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(displayController);
                var purchaseService = controllerData
                    .FindProperty("purchaseServiceSource")?.objectReferenceValue as MonoBehaviour
                    ?? throw new InvalidOperationException(
                        "PHS_SHOP_STOCK_AUTHORING_FAILED reason=purchase_service_missing");
                var registry = displayController.GetComponent<NetworkShopStockRegistry>()
                    ?? displayController.gameObject.AddComponent<NetworkShopStockRegistry>();
                var localHudPresenter = FindExactlyOneInScene<
                    ShopLocalProductHudPresenter>(scene, "local_hud_presenter");
                var registryData = new SerializedObject(registry);
                SetArray(registryData, "displaySlots", slots);
                SetReference(registryData, "purchaseServiceSource", purchaseService);
                SetReference(registryData, "localHudPresenter", localHudPresenter);
                registryData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(registry);

                foreach (var slot in slots)
                {
                    var slotData = new SerializedObject(slot);
                    SetReference(slotData, "stockRegistry", registry);
                    slotData.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(slot);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                {
                    throw new InvalidOperationException(
                        "PHS_SHOP_STOCK_AUTHORING_FAILED reason=shop_scene_save_failed");
                }
            });
        }

        private static void ConfigureDisplayPriceTag(string prefabPath)
        {
            var priceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    PHSUIFontPaths.SuiteBold)
                ?? throw new InvalidOperationException(
                    "PHS_SHOP_PRICE_TAG_AUTHORING_FAILED reason=bold_font_missing");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var slot in root.GetComponentsInChildren<ShopDisplaySlot>(true))
                {
                    var slotData = new SerializedObject(slot);
                    var label = slotData.FindProperty("productLabel")
                        ?.objectReferenceValue as TMP_Text
                        ?? throw new InvalidOperationException(
                            $"PHS_SHOP_PRICE_TAG_AUTHORING_FAILED reason=label_missing prefab={prefabPath} slot={slot.name}");
                    label.gameObject.name = "ItemPriceTag";
                    if (label.GetComponent<ShopPriceTagBillboard>() == null)
                    {
                        label.gameObject.AddComponent<ShopPriceTagBillboard>();
                    }
                    label.text = string.Empty;
                    label.fontSize = 0.42f;
                    label.font = priceFont;
                    label.fontSharedMaterial = priceFont.material;
                    label.fontStyle = FontStyles.Bold;
                    label.color = new Color(0.12f, 1f, 0.32f, 1f);
                    label.alignment = TextAlignmentOptions.Center;
                    label.enableWordWrapping = false;
                    label.raycastTarget = false;
                    label.gameObject.SetActive(false);
                    var rect = label.rectTransform;
                    rect.SetParent(slot.PresentationAnchor, false);
                    rect.anchoredPosition = new Vector2(0f, 0.35f);
                    rect.sizeDelta = new Vector2(3.2f, 0.8f);
                    rect.localScale = Vector3.one;
                    EditorUtility.SetDirty(label);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureLocalPickupHud()
        {
            var root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                var presenter = RequireSingle<ShopLocalProductHudPresenter>(
                    root,
                    "shop_local_hud_presenter");
                var data = new SerializedObject(presenter);
                var panel = data.FindProperty("productPanel")
                    ?.objectReferenceValue as CanvasGroup
                    ?? throw new InvalidOperationException(
                        "PHS_SHOP_PICKUP_HUD_AUTHORING_FAILED reason=panel_missing");
                var nameText = data.FindProperty("productNameText")
                    ?.objectReferenceValue as TMP_Text;
                var priceText = data.FindProperty("priceText")
                    ?.objectReferenceValue as TMP_Text;
                var promptText = data.FindProperty("pickupPromptText")
                    ?.objectReferenceValue as TMP_Text
                    ?? throw new InvalidOperationException(
                        "PHS_SHOP_PICKUP_HUD_AUTHORING_FAILED reason=prompt_missing");

                if (nameText != null)
                {
                    nameText.text = string.Empty;
                    nameText.gameObject.SetActive(false);
                }

                if (priceText != null)
                {
                    priceText.text = string.Empty;
                    priceText.gameObject.SetActive(false);
                }

                presenter.enabled = false;
                panel.alpha = 0f;
                panel.interactable = false;
                panel.blocksRaycasts = false;
                var panelRect = panel.GetComponent<RectTransform>();
                panelRect.sizeDelta = new Vector2(260f, 64f);
                panelRect.anchoredPosition = new Vector2(0f, -128f);
                priceText.rectTransform.anchoredPosition = Vector2.zero;
                priceText.rectTransform.sizeDelta = new Vector2(240f, 54f);
                priceText.fontSize = 36f;
                PHSUIFontPaths.Apply(priceText, PHSUIFontRole.Control);
                priceText.alignment = TextAlignmentOptions.Center;
                promptText.gameObject.SetActive(false);
                promptText.rectTransform.anchoredPosition = Vector2.zero;
                promptText.rectTransform.sizeDelta = new Vector2(170f, 32f);
                promptText.fontSize = 18f;
                PHSUIFontPaths.Apply(promptText, PHSUIFontRole.Control);
                promptText.alignment = TextAlignmentOptions.Center;
                EditorUtility.SetDirty(presenter);
                EditorUtility.SetDirty(panel);
                EditorUtility.SetDirty(promptText);
                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureCheckoutPriceStack()
        {
            var root = PrefabUtility.LoadPrefabContents(CheckoutPrefabPath);
            try
            {
                var checkout = RequireSingle<LastJumpCrew.ParkHanSol.Interaction.ShopCheckoutZone>(
                    root,
                    "shop_checkout_zone");
                var data = new SerializedObject(checkout);
                var priceText = data.FindProperty("priceText")
                    ?.objectReferenceValue as TMP_Text
                    ?? throw new InvalidOperationException(
                        "PHS_SHOP_CHECKOUT_UI_AUTHORING_FAILED reason=price_missing");
                var unavailableText = data.FindProperty("purchaseUnavailableText")
                    ?.objectReferenceValue as TMP_Text
                    ?? throw new InvalidOperationException(
                        "PHS_SHOP_CHECKOUT_UI_AUTHORING_FAILED reason=unavailable_missing");

                var priceRect = priceText.rectTransform;
                priceRect.localScale = Vector3.one;
                priceText.fontSize = 1.15f;
                PHSUIFontPaths.Apply(priceText, PHSUIFontRole.Control);
                priceText.alignment = TextAlignmentOptions.Center;
                priceText.raycastTarget = false;

                var unavailableRect = unavailableText.rectTransform;
                unavailableRect.SetParent(priceRect, false);
                unavailableRect.anchorMin = new Vector2(0.5f, 0.5f);
                unavailableRect.anchorMax = new Vector2(0.5f, 0.5f);
                unavailableRect.pivot = new Vector2(0.5f, 0.5f);
                unavailableRect.anchoredPosition = new Vector2(0f, 1.05f);
                unavailableRect.sizeDelta = new Vector2(6f, 0.8f);
                unavailableRect.localRotation = Quaternion.identity;
                unavailableRect.localScale = Vector3.one;
                unavailableText.fontSize = 0.55f;
                PHSUIFontPaths.Apply(unavailableText, PHSUIFontRole.Emphasis);
                unavailableText.alignment = TextAlignmentOptions.Center;
                unavailableText.raycastTarget = false;
                EditorUtility.SetDirty(priceText);
                EditorUtility.SetDirty(unavailableText);
                PrefabUtility.SaveAsPrefabAsset(root, CheckoutPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateHudPrefab(ICollection<string> errors)
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Require(hud != null, "shop_hud_prefab_missing", errors);
            if (hud == null)
            {
                return;
            }

            var presenters = hud.GetComponentsInChildren<
                ShopLocalProductHudPresenter>(true);
            Require(
                presenters.Length == 1,
                $"shop_hud_presenter_count_invalid actual={presenters.Length}",
                errors);
            if (presenters.Length != 1)
            {
                return;
            }

            var data = new SerializedObject(presenters[0]);
            foreach (var propertyName in new[]
                     {
                         "productPanel",
                         "productNameText",
                         "priceText",
                         "pickupPromptText"
                     })
            {
                Require(
                    data.FindProperty(propertyName)?.objectReferenceValue != null,
                    $"shop_hud_reference_missing property={propertyName}",
                    errors);
            }

            var presenterData = new SerializedObject(presenters[0]);
            var panel = presenterData.FindProperty("productPanel")
                ?.objectReferenceValue as CanvasGroup;
            var nameText = presenterData.FindProperty("productNameText")
                ?.objectReferenceValue as TMP_Text;
            var priceText = presenterData.FindProperty("priceText")
                ?.objectReferenceValue as TMP_Text;
            var promptText = presenterData.FindProperty("pickupPromptText")
                ?.objectReferenceValue as TMP_Text;
            Require(
                nameText != null && !nameText.gameObject.activeSelf,
                "shop_hud_product_name_should_be_hidden",
                errors);
            Require(
                !presenters[0].enabled,
                "shop_hud_proximity_presenter_should_be_disabled",
                errors);
            Require(
                priceText != null,
                "shop_hud_proximity_price_reference_missing",
                errors);
            Require(
                promptText != null && !promptText.gameObject.activeSelf,
                "shop_hud_duplicate_pickup_prompt_should_be_hidden",
                errors);
            if (panel != null)
            {
                Require(
                    Mathf.Approximately(panel.alpha, 0f)
                        && !panel.interactable
                        && !panel.blocksRaycasts,
                    "shop_hud_proximity_panel_should_be_hidden",
                    errors);
            }
        }

        private static void ValidateDisplayPriceTag(
            string prefabPath,
            ICollection<string> errors)
        {
            var priceFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                PHSUIFontPaths.SuiteBold);
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Require(root != null, $"shop_display_prefab_missing path={prefabPath}", errors);
            if (root == null)
            {
                return;
            }

            var slots = root.GetComponentsInChildren<ShopDisplaySlot>(true);
            Require(
                slots.Length == 2,
                $"shop_display_slot_count_invalid path={prefabPath} actual={slots.Length}",
                errors);
            foreach (var slot in slots)
            {
                var data = new SerializedObject(slot);
                var label = data.FindProperty("productLabel")
                    ?.objectReferenceValue as TMP_Text;
                Require(
                    label != null,
                    $"shop_price_tag_missing path={prefabPath} slot={slot.name}",
                    errors);
                if (label == null)
                {
                    continue;
                }

                Require(
                    label.gameObject.name == "ItemPriceTag",
                    $"shop_price_tag_name_invalid path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.transform.parent == slot.PresentationAnchor,
                    $"shop_price_tag_not_attached_to_item_anchor path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.GetComponent<ShopPriceTagBillboard>() != null,
                    $"shop_price_tag_billboard_missing path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.fontSize <= 0.43f && label.color.g >= 0.95f,
                    $"shop_price_tag_style_invalid path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.font == priceFont,
                    $"shop_price_tag_font_invalid path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.fontStyle == FontStyles.Bold,
                    $"shop_price_tag_font_weight_invalid path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    label.rectTransform.sizeDelta.x <= 3.21f,
                    $"shop_price_tag_size_invalid path={prefabPath} slot={slot.name}",
                    errors);
                Require(
                    !label.gameObject.activeSelf,
                    $"shop_world_price_tag_should_be_hidden path={prefabPath} slot={slot.name}",
                    errors);
            }
        }

        private static void ValidateCheckoutPriceStack(ICollection<string> errors)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(CheckoutPrefabPath);
            Require(root != null, "shop_checkout_prefab_missing", errors);
            if (root == null)
            {
                return;
            }

            var checkout = root.GetComponentInChildren<
                LastJumpCrew.ParkHanSol.Interaction.ShopCheckoutZone>(true);
            Require(checkout != null, "shop_checkout_zone_missing", errors);
            if (checkout == null)
            {
                return;
            }

            var data = new SerializedObject(checkout);
            var priceText = data.FindProperty("priceText")
                ?.objectReferenceValue as TMP_Text;
            var unavailableText = data.FindProperty("purchaseUnavailableText")
                ?.objectReferenceValue as TMP_Text;
            Require(priceText != null, "shop_checkout_price_missing", errors);
            Require(unavailableText != null, "shop_checkout_unavailable_missing", errors);
            if (priceText == null || unavailableText == null)
            {
                return;
            }

            Require(
                unavailableText.transform.parent == priceText.transform,
                "shop_checkout_unavailable_not_above_price_hierarchy",
                errors);
            Require(
                unavailableText.rectTransform.anchoredPosition.y > 0.5f,
                "shop_checkout_unavailable_not_above_price",
                errors);
            Require(
                priceText.rectTransform.localScale == Vector3.one
                    && Mathf.Approximately(priceText.fontSize, 1.15f),
                "shop_checkout_price_original_size_not_restored",
                errors);
        }

        private static void ValidateShopScene(ICollection<string> errors)
        {
            WithShopScene(scene =>
            {
                var controllers = FindAllInScene<ShopRandomDisplayController>(scene);
                var registries = FindAllInScene<NetworkShopStockRegistry>(scene);
                var presenters = FindAllInScene<ShopLocalProductHudPresenter>(scene);
                Require(
                    controllers.Length == 1,
                    $"shop_display_controller_count_invalid actual={controllers.Length}",
                    errors);
                Require(
                    registries.Length == 1,
                    $"shop_stock_registry_count_invalid actual={registries.Length}",
                    errors);
                Require(
                    presenters.Length == 1,
                    $"shop_local_hud_presenter_count_invalid actual={presenters.Length}",
                    errors);
                if (controllers.Length != 1
                    || registries.Length != 1
                    || presenters.Length != 1)
                {
                    return;
                }

                var controllerData = new SerializedObject(controllers[0]);
                var slots = ReadSlots(controllerData);
                var registry = registries[0];
                Require(
                    registry.gameObject == controllers[0].gameObject,
                    "shop_stock_registry_owner_invalid",
                    errors);
                var registryData = new SerializedObject(registry);
                var registrySlots = registryData.FindProperty("displaySlots");
                Require(
                    registrySlots != null && registrySlots.arraySize == slots.Length,
                    $"shop_stock_registry_slot_count_invalid actual={registrySlots?.arraySize ?? -1}",
                    errors);
                Require(
                    registryData.FindProperty("purchaseServiceSource")
                        ?.objectReferenceValue != null,
                    "shop_stock_registry_purchase_service_missing",
                    errors);
                Require(
                    registryData.FindProperty("localHudPresenter")
                        ?.objectReferenceValue == presenters[0],
                    "shop_stock_registry_local_hud_reference_invalid",
                    errors);
                foreach (var slot in slots)
                {
                    var slotData = new SerializedObject(slot);
                    Require(
                        slotData.FindProperty("stockRegistry")?.objectReferenceValue == registry,
                        $"shop_slot_registry_reference_invalid slot={slot.name}",
                        errors);
                }
            });
        }

        private static ShopDisplaySlot[] ReadSlots(SerializedObject controllerData)
        {
            var property = controllerData.FindProperty("displaySlots")
                ?? throw new InvalidOperationException(
                    "PHS_SHOP_STOCK_AUTHORING_FAILED reason=display_slots_property_missing");
            var slots = new ShopDisplaySlot[property.arraySize];
            for (var index = 0; index < property.arraySize; index++)
            {
                slots[index] = property.GetArrayElementAtIndex(index)
                    .objectReferenceValue as ShopDisplaySlot
                    ?? throw new InvalidOperationException(
                        $"PHS_SHOP_STOCK_AUTHORING_FAILED reason=display_slot_missing index={index}");
            }

            return slots;
        }

        private static void SetArray(
            SerializedObject target,
            string propertyName,
            IReadOnlyList<ShopDisplaySlot> values)
        {
            var property = target.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_SHOP_STOCK_AUTHORING_FAILED reason=property_missing property={propertyName}");
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetReference(
            SerializedObject target,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = target.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_SHOP_STOCK_AUTHORING_FAILED reason=property_missing property={propertyName}");
            property.objectReferenceValue = value;
        }

        private static void SetInt(
            SerializedObject target,
            string propertyName,
            int value)
        {
            var property = target.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"PHS_SHOP_STOCK_AUTHORING_FAILED reason=property_missing property={propertyName}");
            property.intValue = value;
        }

        private static string GetHierarchyPath(Transform target)
        {
            var names = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static T RequireSingle<T>(GameObject root, string role)
            where T : Component
        {
            var components = root.GetComponentsInChildren<T>(true);
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_SHOP_STOCK_AUTHORING_FAILED reason={role}_count_invalid actual={components.Length}");
            }

            return components[0];
        }

        private static T FindExactlyOneInScene<T>(Scene scene, string role)
            where T : Component
        {
            var components = FindAllInScene<T>(scene);
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_SHOP_STOCK_AUTHORING_FAILED reason={role}_count_invalid actual={components.Length}");
            }

            return components[0];
        }

        private static T[] FindAllInScene<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void WithShopScene(Action<Scene> action)
        {
            var scene = SceneManager.GetSceneByPath(ShopScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(
                    ShopScenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void Require(
            bool condition,
            string error,
            ICollection<string> errors)
        {
            if (!condition)
            {
                errors.Add(error);
            }
        }
    }
}
