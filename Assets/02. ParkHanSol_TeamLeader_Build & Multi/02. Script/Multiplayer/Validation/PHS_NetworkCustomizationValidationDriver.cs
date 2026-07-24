using System;
using System.Collections;
using LastJumpCrew.ParkHanSol.Multiplayer.Customization;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [DisallowMultipleComponent]
    public sealed class PHS_NetworkCustomizationValidationDriver : MonoBehaviour
    {
        private const string ScenarioFlag = "-phsNetworkCustomizationValidation";
        private const string OwnedItemsPreferenceKey = "PHS_CosmeticOwnedItems_v1";
        private const string HeadPreferenceKey = "PHS_CosmeticHead_v1";
        private const string BackPreferenceKey = "PHS_CosmeticBack_v1";
        private const string ColorPreferenceKey = "PHS_CosmeticColor_v1";
        private const string CreditsPreferenceKey = "PHS_PersonalLobbyCustomizationCredits_v1";
        private const float StepTimeoutSeconds = 20f;
        private static readonly Color32 DefaultBodyColor = new(255, 255, 255, 255);

        private static PHS_NetworkCustomizationValidationDriver instance;
        private ValidationScenario scenario;
        private bool failed;

        private enum ValidationScenario
        {
            None = 0,
            Normal = 1,
            Corrupt = 2,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareScenarioProfile()
        {
            if (!TryReadScenario(out var selectedScenario, out var reason))
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    Debug.LogError(
                        $"PHS_NETWORK_CUSTOMIZATION_VALIDATION FAIL step=argument reason={reason}");
                }

                return;
            }

            if (selectedScenario == ValidationScenario.Normal)
            {
                PlayerPrefs.DeleteKey(OwnedItemsPreferenceKey);
                PlayerPrefs.DeleteKey(HeadPreferenceKey);
                PlayerPrefs.DeleteKey(BackPreferenceKey);
                PlayerPrefs.DeleteKey(ColorPreferenceKey);
                PlayerPrefs.DeleteKey(CreditsPreferenceKey);
            }
            else
            {
                PlayerPrefs.SetString(OwnedItemsPreferenceKey, "invalid_catalog_item");
                PlayerPrefs.SetString(HeadPreferenceKey, "invalid_catalog_item");
                PlayerPrefs.SetString(BackPreferenceKey, string.Empty);
                PlayerPrefs.SetString(ColorPreferenceKey, "corrupt_color");
                PlayerPrefs.SetInt(CreditsPreferenceKey, int.MaxValue);
            }

            PlayerPrefs.Save();
            Debug.Log(
                $"PHS_NETWORK_CUSTOMIZATION_VALIDATION PROFILE_PREPARED scenario={selectedScenario}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!TryReadScenario(out var selectedScenario, out _)
                || instance != null)
            {
                return;
            }

            var driverObject = new GameObject(nameof(PHS_NetworkCustomizationValidationDriver));
            DontDestroyOnLoad(driverObject);
            instance = driverObject.AddComponent<PHS_NetworkCustomizationValidationDriver>();
            instance.scenario = selectedScenario;
        }

        private IEnumerator Start()
        {
            Debug.Log(
                $"PHS_NETWORK_CUSTOMIZATION_VALIDATION START scenario={scenario}",
                this);

            INetworkLobbyCustomizationService service = null;
            yield return WaitForCondition(
                () => TryResolveLocalService(out service),
                "local_service_ready");
            if (failed)
            {
                yield break;
            }

            if (scenario == ValidationScenario.Normal)
            {
                yield return ValidateNormalProfile(service);
            }
            else
            {
                yield return ValidateCorruptProfile(service);
            }

            if (!failed)
            {
                Debug.Log(
                    $"PHS_NETWORK_CUSTOMIZATION_VALIDATION COMPLETE result=PASS scenario={scenario}",
                    this);
            }
        }

        private IEnumerator ValidateNormalProfile(INetworkLobbyCustomizationService service)
        {
            yield return WaitForCondition(
                () => service.IsProfileReady,
                "normal_profile_ready");
            if (failed)
            {
                yield break;
            }

            Require(
                service.BodyColor.Equals(DefaultBodyColor),
                "normal_default_color",
                $"actual={service.BodyColor}");
            Require(
                service.CurrentCredits == 300,
                "normal_starting_credits",
                $"actual={service.CurrentCredits}");
            if (failed)
            {
                yield break;
            }

            var item = FindAffordableItem(service);
            if (item == null)
            {
                Fail("catalog_item", "affordable_item_missing");
                yield break;
            }

            if (!service.TrySelectPreviewItem(item.ItemId, out var reason))
            {
                Fail("preview_select", reason);
                yield break;
            }

            Require(
                item.Slot == CosmeticSlot.Head
                    ? service.PreviewHeadId == item.ItemId
                    : service.PreviewBackId == item.ItemId,
                "preview_selected_state",
                $"item={item.ItemId} slot={item.Slot}");
            if (!service.TryResetPreview(out reason))
            {
                Fail("preview_reset", reason);
                yield break;
            }

            Require(
                service.PreviewHeadId == service.EquippedHeadId
                    && service.PreviewBackId == service.EquippedBackId
                    && service.PreviewBodyColor.Equals(service.BodyColor),
                "preview_reset_state",
                "preview_does_not_match_equipped_state");
            if (failed)
            {
                yield break;
            }

            var creditsBeforePurchase = service.CurrentCredits;
            if (!service.TryRequestPurchase(item.ItemId, out reason))
            {
                Fail("purchase_request", reason);
                yield break;
            }

            yield return WaitForCondition(
                () => service.OwnsItem(item.ItemId)
                    && service.CurrentCredits
                    == creditsBeforePurchase - item.Price,
                "purchase_owned_and_debited");
            if (failed)
            {
                yield break;
            }

            if (!service.TryRequestEquip(item.ItemId, out reason))
            {
                Fail("equip_request", reason);
                yield break;
            }

            yield return WaitForCondition(
                () => item.Slot == CosmeticSlot.Head
                    ? service.EquippedHeadId == item.ItemId
                    : service.EquippedBackId == item.ItemId,
                "equip_synchronized");
            if (failed)
            {
                yield break;
            }

            Require(
                item.Slot == CosmeticSlot.Head
                    ? service.PreviewHeadId == item.ItemId
                    : service.PreviewBackId == item.ItemId,
                "equip_preview_reset",
                $"item={item.ItemId}");
        }

        private IEnumerator ValidateCorruptProfile(INetworkLobbyCustomizationService service)
        {
            yield return WaitForCondition(
                () => !string.IsNullOrWhiteSpace(service.ProfileFailureReason)
                    && !string.IsNullOrWhiteSpace(service.CreditsFailureReason),
                "corrupt_failure_reasons");
            if (failed)
            {
                yield break;
            }

            Require(
                !service.IsProfileReady,
                "corrupt_profile_fail_closed",
                "profile_became_ready");
            Require(
                service.ProfileFailureReason.Contains("saved_color_invalid", StringComparison.Ordinal),
                "corrupt_appearance_reason",
                service.ProfileFailureReason);
            Require(
                service.CreditsFailureReason.Contains(
                    "saved_credits_out_of_range",
                    StringComparison.Ordinal),
                "corrupt_credits_reason",
                service.CreditsFailureReason);
            if (failed)
            {
                yield break;
            }

            var item = FindFirstCatalogItem(service);
            if (item == null)
            {
                Fail("corrupt_catalog_item", "catalog_empty");
                yield break;
            }

            var accepted = service.TryRequestPurchase(item.ItemId, out var reason);
            Require(
                !accepted && reason.StartsWith("profile_failed:", StringComparison.Ordinal),
                "corrupt_action_blocked",
                $"accepted={accepted} reason={reason}");
        }

        private IEnumerator WaitForCondition(Func<bool> predicate, string step)
        {
            var deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail(step, $"timeout_seconds={StepTimeoutSeconds}");
                    yield break;
                }

                yield return null;
            }

            Debug.Log(
                $"PHS_NETWORK_CUSTOMIZATION_VALIDATION PASS step={step}",
                this);
        }

        private void Require(bool condition, string step, string detail)
        {
            if (!condition)
            {
                Fail(step, detail);
                return;
            }

            Debug.Log(
                $"PHS_NETWORK_CUSTOMIZATION_VALIDATION PASS step={step} detail={detail}",
                this);
        }

        private void Fail(string step, string reason)
        {
            failed = true;
            Debug.LogError(
                $"PHS_NETWORK_CUSTOMIZATION_VALIDATION FAIL step={step} reason={reason}",
                this);
        }

        private static bool TryResolveLocalService(
            out INetworkLobbyCustomizationService service)
        {
            service = null;
            var networkManager = Unity.Netcode.NetworkManager.Singleton;
            var playerObject = networkManager != null && networkManager.IsListening
                ? networkManager.LocalClient?.PlayerObject
                : null;
            if (playerObject == null)
            {
                return false;
            }

            service = playerObject.GetComponent<NetworkPlayerCustomization>();
            return service != null;
        }

        private static CosmeticItemData FindAffordableItem(
            INetworkLobbyCustomizationService service)
        {
            CosmeticItemData selected = null;
            var credits = service.CurrentCredits;
            foreach (var item in service.Catalog.Items)
            {
                if (item == null
                    || item.Price > credits
                    || (selected != null && item.Price >= selected.Price))
                {
                    continue;
                }

                selected = item;
            }

            return selected;
        }

        private static CosmeticItemData FindFirstCatalogItem(
            INetworkLobbyCustomizationService service)
        {
            foreach (var item in service.Catalog.Items)
            {
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static bool TryReadScenario(
            out ValidationScenario selectedScenario,
            out string reason)
        {
            selectedScenario = ValidationScenario.None;
            reason = null;
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                string value = null;
                if (string.Equals(arguments[index], ScenarioFlag, StringComparison.Ordinal))
                {
                    if (index + 1 >= arguments.Length)
                    {
                        reason = "scenario_value_missing";
                        return false;
                    }

                    value = arguments[index + 1];
                }
                else if (arguments[index].StartsWith(
                             $"{ScenarioFlag}=",
                             StringComparison.Ordinal))
                {
                    value = arguments[index].Substring(ScenarioFlag.Length + 1);
                }

                if (value == null)
                {
                    continue;
                }

                if (string.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
                {
                    selectedScenario = ValidationScenario.Normal;
                    return true;
                }

                if (string.Equals(value, "corrupt", StringComparison.OrdinalIgnoreCase))
                {
                    selectedScenario = ValidationScenario.Corrupt;
                    return true;
                }

                reason = $"scenario_value_invalid:{value}";
                return false;
            }

            return false;
        }
    }
}
