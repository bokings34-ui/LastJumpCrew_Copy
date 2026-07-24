using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [RequireComponent(typeof(PersonalLobbyCustomizationCreditsWallet))]
    public sealed class NetworkPlayerCustomization :
        NetworkBehaviour,
        INetworkLobbyCustomizationService
    {
        private const string OwnedItemsPreferenceKey = LobbyCustomizationProfileKeys.OwnedItems;
        private const string HeadPreferenceKey = LobbyCustomizationProfileKeys.Head;
        private const string BackPreferenceKey = LobbyCustomizationProfileKeys.Back;
        private const string ColorPreferenceKey = LobbyCustomizationProfileKeys.Color;

        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private PersonalLobbyCustomizationCreditsWallet personalCreditsWallet;
        [SerializeField] private SkinnedMeshRenderer bodyRenderer;
        [SerializeField] private Transform headSlot;
        [SerializeField] private Transform backSlot;

        private readonly NetworkVariable<FixedString64Bytes> equippedHeadId = new();
        private readonly NetworkVariable<FixedString64Bytes> equippedBackId = new();
        private readonly NetworkVariable<Color32> bodyColor = new(new Color32(255, 255, 255, 255));
        private NetworkList<FixedString64Bytes> ownedItemIds;
        private MaterialPropertyBlock bodyMaterialProperties;
        private GameObject headVisual;
        private GameObject backVisual;
        private bool serverProfileLoaded;
        private bool ownerProfileReady;
        private string ownerProfileFailureReason = string.Empty;
        private string previewHeadId = string.Empty;
        private string previewBackId = string.Empty;
        private Color32 previewBodyColor = new(255, 255, 255, 255);

        public event Action StateChanged;
        public event Action PreviewChanged;

        public CosmeticCatalog Catalog => catalog;
        public PersonalLobbyCustomizationCreditsWallet PersonalCreditsWallet =>
            personalCreditsWallet;
        public int CurrentCredits => personalCreditsWallet != null
            ? personalCreditsWallet.CurrentCredits
            : 0;
        public string CreditsFailureReason => personalCreditsWallet != null
            ? personalCreditsWallet.ProfileFailureReason
            : "credits_wallet_missing";
        public bool IsProfileReady => ownerProfileReady
            && personalCreditsWallet != null
            && personalCreditsWallet.IsProfileReady;
        public string ProfileFailureReason => !string.IsNullOrWhiteSpace(ownerProfileFailureReason)
            ? ownerProfileFailureReason
            : personalCreditsWallet != null
                ? personalCreditsWallet.ProfileFailureReason
                : "credits_wallet_missing";
        public string EquippedHeadId => equippedHeadId.Value.ToString();
        public string EquippedBackId => equippedBackId.Value.ToString();
        public Color32 BodyColor => bodyColor.Value;
        public string PreviewHeadId => previewHeadId;
        public string PreviewBackId => previewBackId;
        public Color32 PreviewBodyColor => previewBodyColor;

        private void Awake()
        {
            ownedItemIds = new NetworkList<FixedString64Bytes>();
            bodyMaterialProperties = new MaterialPropertyBlock();
            if (personalCreditsWallet == null)
            {
                personalCreditsWallet = GetComponent<PersonalLobbyCustomizationCreditsWallet>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!ValidateSetup())
            {
                ownerProfileFailureReason = "setup_invalid";
                StateChanged?.Invoke();
                return;
            }
            equippedHeadId.OnValueChanged += HandleAppearanceChanged;
            equippedBackId.OnValueChanged += HandleAppearanceChanged;
            bodyColor.OnValueChanged += HandleColorChanged;
            ownedItemIds.OnListChanged += HandleOwnedItemsChanged;
            personalCreditsWallet.StateChanged += HandleWalletStateChanged;
            ApplyAppearance();
            if (IsOwner)
            {
                if (!TryLoadOwnerProfile(
                        out var savedOwnedIds,
                        out var savedHeadId,
                        out var savedBackId,
                        out var savedColor,
                        out var reason))
                {
                    ownerProfileFailureReason = reason;
                    Debug.LogError(
                        $"PHS_COSMETIC_PROFILE_LOAD_FAILED reason={reason} player={name}",
                        this);
                    return;
                }

                RequestLoadProfileServerRpc(
                    savedOwnedIds,
                    savedHeadId,
                    savedBackId,
                    savedColor);
            }
        }

        public override void OnNetworkDespawn()
        {
            equippedHeadId.OnValueChanged -= HandleAppearanceChanged;
            equippedBackId.OnValueChanged -= HandleAppearanceChanged;
            bodyColor.OnValueChanged -= HandleColorChanged;
            ownedItemIds.OnListChanged -= HandleOwnedItemsChanged;
            if (personalCreditsWallet != null)
            {
                personalCreditsWallet.StateChanged -= HandleWalletStateChanged;
            }
            if (IsOwner && ownerProfileReady) SaveProfile();
        }

        public void RequestPurchase(string itemId)
        {
            if (!TryRequestPurchase(itemId, out var reason))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason={reason} player={name}", this);
            }
        }

        public void RequestEquip(string itemId)
        {
            if (!TryRequestEquip(itemId, out var reason))
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason={reason} player={name}", this);
            }
        }

        public void RequestSetBodyColor(Color32 color)
        {
            if (!TryRequestSetBodyColor(color, out var reason))
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason={reason} player={name}", this);
            }
        }

        public void RequestUnequip(CosmeticSlot slot)
        {
            if (!TryRequestUnequip(slot, out var reason))
            {
                Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason={reason} player={name}", this);
            }
        }

        public bool OwnsItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && Owns(itemId);
        }

        public bool TrySelectPreviewItem(string itemId, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (!catalog.TryGetItem(itemId, out var item))
            {
                reason = $"catalog_item_missing:{itemId}";
                return false;
            }

            if (item.Slot == CosmeticSlot.Head)
            {
                previewHeadId = item.ItemId;
            }
            else if (item.Slot == CosmeticSlot.Back)
            {
                previewBackId = item.ItemId;
            }
            else
            {
                reason = $"slot_invalid:{item.Slot}";
                return false;
            }

            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TrySelectPreviewBodyColor(Color32 color, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            previewBodyColor = color;
            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TryResetPreview(out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            ResetPreviewToEquipped();
            reason = null;
            return true;
        }

        public bool TryRequestPurchase(string itemId, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (!catalog.TryGetItem(itemId, out var item))
            {
                reason = $"catalog_item_missing:{itemId}";
                return false;
            }

            if (Owns(item.ItemId))
            {
                reason = $"already_owned:{item.ItemId}";
                return false;
            }

            RequestPurchaseServerRpc(new FixedString64Bytes(item.ItemId));
            reason = null;
            return true;
        }

        public bool TryRequestEquip(string itemId, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (!catalog.TryGetItem(itemId, out var item))
            {
                reason = $"catalog_item_missing:{itemId}";
                return false;
            }

            if (!Owns(item.ItemId))
            {
                reason = $"item_not_owned:{item.ItemId}";
                return false;
            }

            RequestEquipServerRpc(new FixedString64Bytes(item.ItemId));
            reason = null;
            return true;
        }

        public bool TryRequestUnequip(CosmeticSlot slot, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (slot != CosmeticSlot.Head && slot != CosmeticSlot.Back)
            {
                reason = $"slot_invalid:{slot}";
                return false;
            }

            RequestUnequipServerRpc(slot);
            reason = null;
            return true;
        }

        public bool TryRequestSetBodyColor(Color32 color, out string reason)
        {
            if (!CanUseOwnerProfile(out reason))
            {
                return false;
            }

            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }

            RequestSetBodyColorServerRpc(color);
            reason = null;
            return true;
        }

        [ServerRpc]
        private void RequestLoadProfileServerRpc(FixedString512Bytes ownedIds, FixedString64Bytes headId, FixedString64Bytes backId, Color32 savedColor, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=owner_mismatch player={name}", this);
                return;
            }

            if (serverProfileLoaded)
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=profile_already_submitted player={name}", this);
                RejectProfileLoadServer("profile_already_submitted");
                return;
            }

            serverProfileLoaded = true;
            var validatedOwnedIds = new List<FixedString64Bytes>();
            var uniqueOwnedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ownedIds.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!uniqueOwnedIds.Add(id))
                {
                    Debug.LogError(
                        $"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=owned_item_duplicate item={id} player={name}",
                        this);
                    RejectProfileLoadServer("owned_item_duplicate");
                    return;
                }

                if (!catalog.TryGetItem(id, out _))
                {
                    Debug.LogError(
                        $"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=owned_item_invalid item={id} player={name}",
                        this);
                    RejectProfileLoadServer("owned_item_invalid");
                    return;
                }

                validatedOwnedIds.Add(new FixedString64Bytes(id));
            }

            if (!catalog.IsBodyColorAllowed(savedColor))
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=color_not_allowed color={savedColor} player={name}", this);
                RejectProfileLoadServer("color_not_allowed");
                return;
            }

            if (!TryValidateLoadedEquipment(
                    headId,
                    CosmeticSlot.Head,
                    uniqueOwnedIds,
                    out var equipmentReason)
                || !TryValidateLoadedEquipment(
                    backId,
                    CosmeticSlot.Back,
                    uniqueOwnedIds,
                    out equipmentReason))
            {
                Debug.LogError(
                    $"PHS_COSMETIC_PROFILE_LOAD_FAILED reason={equipmentReason} player={name}",
                    this);
                RejectProfileLoadServer("equipped_item_invalid");
                return;
            }

            for (var index = 0; index < validatedOwnedIds.Count; index++)
            {
                ownedItemIds.Add(validatedOwnedIds[index]);
            }

            bodyColor.Value = savedColor;
            equippedHeadId.Value = headId;
            equippedBackId.Value = backId;
            ConfirmProfileLoadedClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        [ClientRpc]
        private void ConfirmProfileLoadedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerProfileReady = true;
            ownerProfileFailureReason = string.Empty;
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
        }

        [ClientRpc]
        private void RejectProfileLoadClientRpc(
            FixedString128Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner)
            {
                return;
            }

            ownerProfileReady = false;
            ownerProfileFailureReason = reason.ToString();
            StateChanged?.Invoke();
        }

        private void RejectProfileLoadServer(string reason)
        {
            RejectProfileLoadClientRpc(new FixedString128Bytes(reason), new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }

        [ServerRpc]
        private void RequestPurchaseServerRpc(FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId
                || !serverProfileLoaded
                || !catalog.TryGetItem(itemId.ToString(), out var item))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason=owner_or_catalog_invalid player={name}");
                return;
            }
            if (Owns(item.ItemId))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason=already_owned item={item.ItemId} player={name}");
                return;
            }
            if (!personalCreditsWallet.TrySpendCreditsServer(item.Price)) return;
            ownedItemIds.Add(itemId);
            Debug.Log($"PHS_COSMETIC_PURCHASED player={name} item={item.ItemId}");
        }

        [ServerRpc]
        private void RequestEquipServerRpc(FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded)
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason=owner_mismatch player={name}");
                return;
            }
            if (!catalog.TryGetItem(itemId.ToString(), out var item) || !Owns(item.ItemId))
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason=not_owned_or_catalog_invalid player={name}");
                return;
            }
            TryEquipServer(itemId, item.Slot);
        }

        [ServerRpc]
        private void RequestUnequipServerRpc(CosmeticSlot slot, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded)
            {
                Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason=owner_or_profile_invalid player={name}");
                return;
            }

            if (slot == CosmeticSlot.Head)
            {
                equippedHeadId.Value = default;
            }
            else if (slot == CosmeticSlot.Back)
            {
                equippedBackId.Value = default;
            }
            else
            {
                Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason=slot_invalid slot={slot} player={name}");
            }
        }

        [ServerRpc]
        private void RequestSetBodyColorServerRpc(Color32 color, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded)
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason=owner_mismatch player={name}");
                return;
            }
            if (!catalog.IsBodyColorAllowed(color))
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason=color_not_allowed color={color} player={name}");
                return;
            }

            bodyColor.Value = color;
        }

        private void TryEquipServer(FixedString64Bytes itemId, CosmeticSlot expectedSlot)
        {
            if (itemId.IsEmpty) return;
            if (!catalog.TryGetItem(itemId.ToString(), out var item) || item.Slot != expectedSlot || !Owns(item.ItemId)) return;
            if (expectedSlot == CosmeticSlot.Head) equippedHeadId.Value = itemId;
            else equippedBackId.Value = itemId;
        }

        private bool Owns(string itemId)
        {
            foreach (var ownedId in ownedItemIds) if (ownedId.ToString() == itemId) return true;
            return false;
        }

        private bool TryValidateLoadedEquipment(
            FixedString64Bytes itemId,
            CosmeticSlot expectedSlot,
            HashSet<string> validatedOwnedIds,
            out string reason)
        {
            if (itemId.IsEmpty)
            {
                reason = null;
                return true;
            }

            var value = itemId.ToString();
            if (!validatedOwnedIds.Contains(value))
            {
                reason = $"equipped_item_not_owned:{value}";
                return false;
            }

            if (!catalog.TryGetItem(value, out var item)
                || item.Slot != expectedSlot)
            {
                reason = $"equipped_item_slot_invalid:{value}:{expectedSlot}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool CanUseOwnerProfile(out string reason)
        {
            if (!IsSpawned || !IsOwner)
            {
                reason = "owner_required";
                return false;
            }

            if (!ownerProfileReady)
            {
                reason = string.IsNullOrWhiteSpace(ownerProfileFailureReason)
                    ? "profile_not_ready"
                    : $"profile_failed:{ownerProfileFailureReason}";
                return false;
            }

            if (catalog == null)
            {
                reason = "catalog_missing";
                return false;
            }

            reason = null;
            return true;
        }

        private void ResetPreviewToEquipped()
        {
            previewHeadId = EquippedHeadId;
            previewBackId = EquippedBackId;
            previewBodyColor = BodyColor;
            PreviewChanged?.Invoke();
        }

        private void HandleAppearanceChanged(FixedString64Bytes _, FixedString64Bytes __)
        {
            ApplyAppearance();
            SaveOwnerProfileIfReady();
            if (IsOwner && ownerProfileReady)
            {
                ResetPreviewToEquipped();
            }
            StateChanged?.Invoke();
        }

        private void HandleColorChanged(Color32 _, Color32 __)
        {
            ApplyBodyColor();
            SaveOwnerProfileIfReady();
            if (IsOwner && ownerProfileReady)
            {
                ResetPreviewToEquipped();
            }
            StateChanged?.Invoke();
        }

        private void HandleOwnedItemsChanged(NetworkListEvent<FixedString64Bytes> _)
        {
            SaveOwnerProfileIfReady();
            StateChanged?.Invoke();
        }

        private void HandleWalletStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void SaveOwnerProfileIfReady()
        {
            if (IsOwner && ownerProfileReady)
            {
                SaveProfile();
            }
        }

        private void ApplyAppearance()
        {
            ApplyBodyColor();
            ApplyVisual(CosmeticSlot.Head, equippedHeadId.Value.ToString(), ref headVisual, headSlot);
            ApplyVisual(CosmeticSlot.Back, equippedBackId.Value.ToString(), ref backVisual, backSlot);
        }

        private void ApplyBodyColor()
        {
            bodyRenderer.GetPropertyBlock(bodyMaterialProperties);
            bodyMaterialProperties.SetColor("_BaseColor", bodyColor.Value);
            bodyMaterialProperties.SetColor("_Color", bodyColor.Value);
            bodyRenderer.SetPropertyBlock(bodyMaterialProperties);
        }

        private void ApplyVisual(CosmeticSlot slot, string itemId, ref GameObject currentVisual, Transform targetSlot)
        {
            if (currentVisual != null) Destroy(currentVisual);
            currentVisual = null;
            if (string.IsNullOrEmpty(itemId)) return;
            if (!catalog.TryGetItem(itemId, out var item) || item.Slot != slot || item.VisualPrefab == null)
            {
                Debug.LogError($"PHS_COSMETIC_VISUAL_FAILED reason=item_invalid item={itemId} player={name}");
                return;
            }
            currentVisual = Instantiate(item.VisualPrefab, targetSlot);
            currentVisual.transform.SetLocalPositionAndRotation(item.LocalPosition, Quaternion.Euler(item.LocalEulerAngles));
            currentVisual.transform.localScale = item.LocalScale;
        }

        private bool ValidateSetup()
        {
            if (catalog != null && personalCreditsWallet != null && bodyRenderer != null && headSlot != null && backSlot != null) return true;
            Debug.LogError($"PHS_COSMETIC_SETUP_FAILED player={name} catalog={catalog != null} wallet={personalCreditsWallet != null} body={bodyRenderer != null} head={headSlot != null} back={backSlot != null}");
            return false;
        }

        private void SaveProfile()
        {
            var ids = new List<string>();
            foreach (var id in ownedItemIds) ids.Add(id.ToString());
            PlayerPrefs.SetString(OwnedItemsPreferenceKey, string.Join(',', ids));
            PlayerPrefs.SetString(HeadPreferenceKey, equippedHeadId.Value.ToString());
            PlayerPrefs.SetString(BackPreferenceKey, equippedBackId.Value.ToString());
            var color = bodyColor.Value;
            PlayerPrefs.SetString(ColorPreferenceKey, $"{color.r},{color.g},{color.b},{color.a}");
            PlayerPrefs.Save();
        }

        private bool TryLoadOwnerProfile(
            out FixedString512Bytes ownedIds,
            out FixedString64Bytes headId,
            out FixedString64Bytes backId,
            out Color32 savedColor,
            out string reason)
        {
            ownedIds = default;
            headId = default;
            backId = default;
            savedColor = default;

            if (!TryLoadColor(out savedColor, out reason))
            {
                return false;
            }

            try
            {
                ownedIds = new FixedString512Bytes(
                    PlayerPrefs.GetString(OwnedItemsPreferenceKey, string.Empty));
                headId = new FixedString64Bytes(
                    PlayerPrefs.GetString(HeadPreferenceKey, string.Empty));
                backId = new FixedString64Bytes(
                    PlayerPrefs.GetString(BackPreferenceKey, string.Empty));
            }
            catch (Exception exception)
            {
                reason = $"saved_profile_capacity:{exception.GetType().Name}";
                return false;
            }

            reason = null;
            return true;
        }

        private bool TryLoadColor(out Color32 color, out string reason)
        {
            color = new Color32(255, 255, 255, 255);
            if (!PlayerPrefs.HasKey(ColorPreferenceKey))
            {
                if (!catalog.IsBodyColorAllowed(color))
                {
                    reason = "default_color_not_allowed";
                    return false;
                }

                reason = null;
                return true;
            }

            var parts = PlayerPrefs.GetString(ColorPreferenceKey).Split(',');
            if (parts.Length != 4
                || !byte.TryParse(parts[0], out var red)
                || !byte.TryParse(parts[1], out var green)
                || !byte.TryParse(parts[2], out var blue)
                || !byte.TryParse(parts[3], out var alpha))
            {
                reason = "saved_color_invalid";
                return false;
            }

            color = new Color32(red, green, blue, alpha);
            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"saved_color_not_allowed:{color}";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
