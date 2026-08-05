using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [RequireComponent(typeof(PersonalLobbyCustomizationCreditsWallet))]
    public sealed class NetworkPlayerCustomization : NetworkBehaviour, INetworkLobbyCustomizationService
    {
        // v2 deliberately starts without the removed primitive-item profile data.
        private const string OwnedItemsPreferenceKey = "PHS_CosmeticOwnedItems_v2";
        private const string HeadPreferenceKey = "PHS_CosmeticHead_v2";
        private const string BackPreferenceKey = "PHS_CosmeticBack_v2";
        private const string PetPreferenceKey = "PHS_CosmeticPet_v2";
        private const string FrontPreferenceKey = "PHS_CosmeticFront_v2";
        private const string ColorPreferenceKey = "PHS_CosmeticColor_v2";

        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private PersonalLobbyCustomizationCreditsWallet personalCreditsWallet;
        [SerializeField] private SkinnedMeshRenderer bodyRenderer;
        [SerializeField] private Transform headSlot;
        [SerializeField] private Transform backSlot;
        [SerializeField] private Transform petSlot;
        [SerializeField] private Transform frontSlot;

        private readonly NetworkVariable<FixedString64Bytes> equippedHeadId = new();
        private readonly NetworkVariable<FixedString64Bytes> equippedBackId = new();
        private readonly NetworkVariable<FixedString64Bytes> equippedPetId = new();
        private readonly NetworkVariable<FixedString64Bytes> equippedFrontId = new();
        private readonly NetworkVariable<Color32> bodyColor = new(new Color32(255, 255, 255, 255));
        private NetworkList<FixedString64Bytes> ownedItemIds;
        private MaterialPropertyBlock bodyMaterialProperties;
        private GameObject headVisual;
        private GameObject backVisual;
        private GameObject petVisual;
        private GameObject frontVisual;
        private bool serverProfileLoaded;
        private bool ownerProfileReady;
        private string ownerProfileFailureReason = string.Empty;
        private string previewHeadId = string.Empty;
        private string previewBackId = string.Empty;
        private string previewPetId = string.Empty;
        private string previewFrontId = string.Empty;
        private Color32 previewBodyColor = new(255, 255, 255, 255);

        public event Action StateChanged;
        public event Action PreviewChanged;
        public CosmeticCatalog Catalog => catalog;
        public PersonalLobbyCustomizationCreditsWallet PersonalCreditsWallet => personalCreditsWallet;
        public int CurrentCredits => personalCreditsWallet == null ? 0 : personalCreditsWallet.CurrentCredits;
        public bool IsProfileReady => ownerProfileReady && personalCreditsWallet != null && personalCreditsWallet.IsProfileReady;
        public string ProfileFailureReason => !string.IsNullOrWhiteSpace(ownerProfileFailureReason)
            ? ownerProfileFailureReason
            : personalCreditsWallet != null ? personalCreditsWallet.ProfileFailureReason : "credits_wallet_missing";
        public string EquippedHeadId => equippedHeadId.Value.ToString();
        public string EquippedBackId => equippedBackId.Value.ToString();
        public string EquippedPetId => equippedPetId.Value.ToString();
        public string EquippedFrontId => equippedFrontId.Value.ToString();
        public Color32 BodyColor => bodyColor.Value;
        public string PreviewHeadId => previewHeadId;
        public string PreviewBackId => previewBackId;
        public string PreviewPetId => previewPetId;
        public string PreviewFrontId => previewFrontId;
        public Color32 PreviewBodyColor => previewBodyColor;

        private void Awake()
        {
            ownedItemIds = new NetworkList<FixedString64Bytes>();
            bodyMaterialProperties = new MaterialPropertyBlock();
            personalCreditsWallet ??= GetComponent<PersonalLobbyCustomizationCreditsWallet>();
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
            equippedPetId.OnValueChanged += HandleAppearanceChanged;
            equippedFrontId.OnValueChanged += HandleAppearanceChanged;
            bodyColor.OnValueChanged += HandleColorChanged;
            ownedItemIds.OnListChanged += HandleOwnedItemsChanged;
            personalCreditsWallet.StateChanged += HandleWalletStateChanged;
            ApplyAppearance();
            if (!IsOwner) return;

            if (!TryLoadOwnerProfile(out var savedOwnedIds, out var headId, out var backId, out var petId, out var frontId, out var savedColor, out var reason))
            {
                ownerProfileFailureReason = reason;
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason={reason} player={name}", this);
                return;
            }
            RequestLoadProfileServerRpc(savedOwnedIds, headId, backId, petId, frontId, savedColor);
        }

        public override void OnNetworkDespawn()
        {
            equippedHeadId.OnValueChanged -= HandleAppearanceChanged;
            equippedBackId.OnValueChanged -= HandleAppearanceChanged;
            equippedPetId.OnValueChanged -= HandleAppearanceChanged;
            equippedFrontId.OnValueChanged -= HandleAppearanceChanged;
            bodyColor.OnValueChanged -= HandleColorChanged;
            ownedItemIds.OnListChanged -= HandleOwnedItemsChanged;
            if (personalCreditsWallet != null) personalCreditsWallet.StateChanged -= HandleWalletStateChanged;
            if (IsOwner && ownerProfileReady) SaveProfile();
        }

        public void RequestPurchase(string itemId) { if (!TryRequestPurchase(itemId, out var reason)) Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason={reason} player={name}", this); }
        public void RequestEquip(string itemId) { if (!TryRequestEquip(itemId, out var reason)) Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason={reason} player={name}", this); }
        public void RequestSetBodyColor(Color32 color) { if (!TryRequestSetBodyColor(color, out var reason)) Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason={reason} player={name}", this); }
        public void RequestUnequip(CosmeticSlot slot) { if (!TryRequestUnequip(slot, out var reason)) Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason={reason} player={name}", this); }
        public bool OwnsItem(string itemId) => !string.IsNullOrWhiteSpace(itemId) && Owns(itemId);

        public bool TrySelectPreviewItem(string itemId, out string reason)
        {
            if (!CanUseOwnerProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
                return false;
            }
            SetPreviewId(item.Slot, item.ItemId);
            ApplyPreviewVisual(item.Slot, item.ItemId);
            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TrySelectPreviewBodyColor(Color32 color, out string reason)
        {
            if (!CanUseOwnerProfile(out reason)) return false;
            if (!catalog.IsBodyColorAllowed(color))
            {
                reason = $"color_not_allowed:{color}";
                return false;
            }
            previewBodyColor = color;
            ApplyPreviewBodyColor();
            reason = null;
            PreviewChanged?.Invoke();
            return true;
        }

        public bool TryResetPreview(out string reason)
        {
            if (!CanUseOwnerProfile(out reason)) return false;
            ResetPreviewToEquipped();
            ApplyAppearance();
            reason = null;
            return true;
        }

        public bool TryRequestPurchase(string itemId, out string reason)
        {
            if (!CanUseOwnerProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
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
            if (!CanUseOwnerProfile(out reason) || !catalog.TryGetItem(itemId, out var item))
            {
                reason ??= $"catalog_item_missing:{itemId}";
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
            if (!CanUseOwnerProfile(out reason)) return false;
            if (!IsSupportedSlot(slot))
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
            if (!CanUseOwnerProfile(out reason)) return false;
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
        private void RequestLoadProfileServerRpc(FixedString512Bytes ownedIds, FixedString64Bytes headId, FixedString64Bytes backId, FixedString64Bytes petId, FixedString64Bytes frontId, Color32 savedColor, ServerRpcParams rpcParams = default)
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
                if (!uniqueOwnedIds.Add(id) || !catalog.TryGetItem(id, out _))
                {
                    Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=owned_item_invalid item={id} player={name}", this);
                    RejectProfileLoadServer("owned_item_invalid");
                    return;
                }
                validatedOwnedIds.Add(new FixedString64Bytes(id));
            }
            if (!catalog.IsBodyColorAllowed(savedColor))
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=color_not_allowed player={name}", this);
                RejectProfileLoadServer("color_not_allowed");
                return;
            }
            if (!TryValidateLoadedEquipment(headId, CosmeticSlot.Head, uniqueOwnedIds, out var equipmentReason)
                || !TryValidateLoadedEquipment(backId, CosmeticSlot.Back, uniqueOwnedIds, out equipmentReason)
                || !TryValidateLoadedEquipment(petId, CosmeticSlot.Pet, uniqueOwnedIds, out equipmentReason)
                || !TryValidateLoadedEquipment(frontId, CosmeticSlot.Front, uniqueOwnedIds, out equipmentReason))
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason={equipmentReason} player={name}", this);
                RejectProfileLoadServer("equipped_item_invalid");
                return;
            }
            foreach (var id in validatedOwnedIds) ownedItemIds.Add(id);
            bodyColor.Value = savedColor;
            equippedHeadId.Value = headId;
            equippedBackId.Value = backId;
            equippedPetId.Value = petId;
            equippedFrontId.Value = frontId;
            ConfirmProfileLoadedClientRpc(TargetOwner());
        }

        [ClientRpc]
        private void ConfirmProfileLoadedClientRpc(ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner) return;
            ownerProfileReady = true;
            ownerProfileFailureReason = string.Empty;
            ResetPreviewToEquipped();
            StateChanged?.Invoke();
        }

        [ClientRpc]
        private void RejectProfileLoadClientRpc(FixedString128Bytes reason, ClientRpcParams clientRpcParams = default)
        {
            if (!IsOwner) return;
            ownerProfileReady = false;
            ownerProfileFailureReason = reason.ToString();
            StateChanged?.Invoke();
        }

        private void RejectProfileLoadServer(string reason) => RejectProfileLoadClientRpc(new FixedString128Bytes(reason), TargetOwner());
        private ClientRpcParams TargetOwner() => new() { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };

        [ServerRpc]
        private void RequestPurchaseServerRpc(FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded || !catalog.TryGetItem(itemId.ToString(), out var item))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason=owner_or_catalog_invalid player={name}", this);
                return;
            }
            if (Owns(item.ItemId)
                || item.Price < 0
                || (item.Price > 0 && !personalCreditsWallet.TrySpendCreditsServer(item.Price))) return;
            ownedItemIds.Add(itemId);
            Debug.Log($"PHS_COSMETIC_PURCHASED player={name} item={item.ItemId}", this);
        }

        [ServerRpc]
        private void RequestEquipServerRpc(FixedString64Bytes itemId, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded || !catalog.TryGetItem(itemId.ToString(), out var item) || !Owns(item.ItemId))
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason=owner_or_item_invalid player={name}", this);
                return;
            }
            SetEquippedIdServer(item.Slot, GetEquippedId(item.Slot) == item.ItemId ? default : itemId);
        }

        [ServerRpc]
        private void RequestUnequipServerRpc(CosmeticSlot slot, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded || !IsSupportedSlot(slot))
            {
                Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason=owner_or_slot_invalid player={name}", this);
                return;
            }
            SetEquippedIdServer(slot, default);
        }

        [ServerRpc]
        private void RequestSetBodyColorServerRpc(Color32 color, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || !serverProfileLoaded || !catalog.IsBodyColorAllowed(color))
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason=owner_or_color_invalid player={name}", this);
                return;
            }
            bodyColor.Value = color;
        }

        private bool Owns(string itemId)
        {
            foreach (var ownedId in ownedItemIds) if (ownedId.ToString() == itemId) return true;
            return false;
        }

        private bool TryValidateLoadedEquipment(FixedString64Bytes itemId, CosmeticSlot expectedSlot, HashSet<string> ownedIds, out string reason)
        {
            if (itemId.IsEmpty)
            {
                reason = null;
                return true;
            }
            var value = itemId.ToString();
            if (!ownedIds.Contains(value))
            {
                reason = $"equipped_item_not_owned:{value}";
                return false;
            }
            if (!catalog.TryGetItem(value, out var item) || item.Slot != expectedSlot)
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
                reason = string.IsNullOrWhiteSpace(ownerProfileFailureReason) ? "profile_not_ready" : $"profile_failed:{ownerProfileFailureReason}";
                return false;
            }
            reason = catalog == null ? "catalog_missing" : null;
            return reason == null;
        }

        private void ResetPreviewToEquipped()
        {
            previewHeadId = EquippedHeadId;
            previewBackId = EquippedBackId;
            previewPetId = EquippedPetId;
            previewFrontId = EquippedFrontId;
            previewBodyColor = BodyColor;
            PreviewChanged?.Invoke();
        }

        private void HandleAppearanceChanged(FixedString64Bytes _, FixedString64Bytes __)
        {
            ApplyAppearance();
            SaveOwnerProfileIfReady();
            if (IsOwner && ownerProfileReady) ResetPreviewToEquipped();
            StateChanged?.Invoke();
        }

        private void HandleColorChanged(Color32 _, Color32 __)
        {
            ApplyBodyColor();
            SaveOwnerProfileIfReady();
            if (IsOwner && ownerProfileReady) ResetPreviewToEquipped();
            StateChanged?.Invoke();
        }

        private void HandleOwnedItemsChanged(NetworkListEvent<FixedString64Bytes> _) { SaveOwnerProfileIfReady(); StateChanged?.Invoke(); }
        private void HandleWalletStateChanged() => StateChanged?.Invoke();
        private void SaveOwnerProfileIfReady() { if (IsOwner && ownerProfileReady) SaveProfile(); }

        private void ApplyAppearance()
        {
            ApplyBodyColor();
            ApplyVisual(CosmeticSlot.Head, EquippedHeadId, ref headVisual, headSlot);
            ApplyVisual(CosmeticSlot.Back, EquippedBackId, ref backVisual, backSlot);
            ApplyVisual(CosmeticSlot.Pet, EquippedPetId, ref petVisual, petSlot);
            ApplyVisual(CosmeticSlot.Front, EquippedFrontId, ref frontVisual, frontSlot);
        }

        private void ApplyBodyColor()
        {
            ApplyBodyColor(bodyColor.Value);
        }

        private void ApplyPreviewBodyColor()
        {
            ApplyBodyColor(previewBodyColor);
        }

        private void ApplyBodyColor(Color32 color)
        {
            bodyMaterialProperties ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(bodyMaterialProperties);
            bodyMaterialProperties.SetColor("_BaseColor", color);
            bodyMaterialProperties.SetColor("_Color", color);
            bodyRenderer.SetPropertyBlock(bodyMaterialProperties);
        }

        private void ApplyPreviewVisual(CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Head: ApplyVisual(slot, itemId, ref headVisual, headSlot); break;
                case CosmeticSlot.Back: ApplyVisual(slot, itemId, ref backVisual, backSlot); break;
                case CosmeticSlot.Pet: ApplyVisual(slot, itemId, ref petVisual, petSlot); break;
                case CosmeticSlot.Front: ApplyVisual(slot, itemId, ref frontVisual, frontSlot); break;
            }
        }

        private void ApplyVisual(CosmeticSlot slot, string itemId, ref GameObject currentVisual, Transform targetSlot)
        {
            if (currentVisual != null) Destroy(currentVisual);
            currentVisual = null;
            if (IsOwner && slot != CosmeticSlot.Pet) return;
            if (string.IsNullOrEmpty(itemId)) return;
            if (!catalog.TryGetItem(itemId, out var item) || item.Slot != slot || item.VisualPrefab == null)
            {
                Debug.LogError($"PHS_COSMETIC_VISUAL_FAILED reason=item_invalid item={itemId} player={name}", this);
                return;
            }
            currentVisual = Instantiate(item.VisualPrefab, targetSlot, false);
            currentVisual.transform.SetLocalPositionAndRotation(item.LocalPosition, Quaternion.Euler(item.LocalEulerAngles));
            currentVisual.transform.localScale = item.LocalScale;
            DisableCosmeticPhysics(currentVisual);
        }

        private static void DisableCosmeticPhysics(GameObject visual)
        {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }
        }

        private string GetEquippedId(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Head => EquippedHeadId,
            CosmeticSlot.Back => EquippedBackId,
            CosmeticSlot.Pet => EquippedPetId,
            CosmeticSlot.Front => EquippedFrontId,
            _ => string.Empty
        };

        private void SetPreviewId(CosmeticSlot slot, string itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Head: previewHeadId = itemId; break;
                case CosmeticSlot.Back: previewBackId = itemId; break;
                case CosmeticSlot.Pet: previewPetId = itemId; break;
                case CosmeticSlot.Front: previewFrontId = itemId; break;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private void SetEquippedIdServer(CosmeticSlot slot, FixedString64Bytes itemId)
        {
            switch (slot)
            {
                case CosmeticSlot.Head: equippedHeadId.Value = itemId; break;
                case CosmeticSlot.Back: equippedBackId.Value = itemId; break;
                case CosmeticSlot.Pet: equippedPetId.Value = itemId; break;
                case CosmeticSlot.Front: equippedFrontId.Value = itemId; break;
                default: Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason=slot_invalid slot={slot} player={name}", this); break;
            }
        }

        private static bool IsSupportedSlot(CosmeticSlot slot) => slot is CosmeticSlot.Head or CosmeticSlot.Back or CosmeticSlot.Pet or CosmeticSlot.Front;

        private bool ValidateSetup()
        {
            if (catalog != null && personalCreditsWallet != null && bodyRenderer != null && headSlot != null && backSlot != null && petSlot != null && frontSlot != null) return true;
            Debug.LogError($"PHS_COSMETIC_SETUP_FAILED player={name} catalog={catalog != null} wallet={personalCreditsWallet != null} body={bodyRenderer != null} head={headSlot != null} back={backSlot != null} pet={petSlot != null} front={frontSlot != null}", this);
            return false;
        }

        private void SaveProfile()
        {
            var ids = new List<string>();
            foreach (var id in ownedItemIds) ids.Add(id.ToString());
            PlayerPrefs.SetString(OwnedItemsPreferenceKey, string.Join(',', ids));
            PlayerPrefs.SetString(HeadPreferenceKey, EquippedHeadId);
            PlayerPrefs.SetString(BackPreferenceKey, EquippedBackId);
            PlayerPrefs.SetString(PetPreferenceKey, EquippedPetId);
            PlayerPrefs.SetString(FrontPreferenceKey, EquippedFrontId);
            var color = BodyColor;
            PlayerPrefs.SetString(ColorPreferenceKey, $"{color.r},{color.g},{color.b},{color.a}");
            PlayerPrefs.Save();
        }

        private bool TryLoadOwnerProfile(out FixedString512Bytes ownedIds, out FixedString64Bytes headId, out FixedString64Bytes backId, out FixedString64Bytes petId, out FixedString64Bytes frontId, out Color32 savedColor, out string reason)
        {
            ownedIds = default;
            headId = default;
            backId = default;
            petId = default;
            frontId = default;
            if (!TryLoadColor(out savedColor, out reason)) return false;
            try
            {
                ownedIds = new FixedString512Bytes(PlayerPrefs.GetString(OwnedItemsPreferenceKey, string.Empty));
                headId = new FixedString64Bytes(PlayerPrefs.GetString(HeadPreferenceKey, string.Empty));
                backId = new FixedString64Bytes(PlayerPrefs.GetString(BackPreferenceKey, string.Empty));
                petId = new FixedString64Bytes(PlayerPrefs.GetString(PetPreferenceKey, string.Empty));
                frontId = new FixedString64Bytes(PlayerPrefs.GetString(FrontPreferenceKey, string.Empty));
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
                reason = catalog.IsBodyColorAllowed(color) ? null : "default_color_not_allowed";
                return reason == null;
            }
            var parts = PlayerPrefs.GetString(ColorPreferenceKey).Split(',');
            if (parts.Length != 4 || !byte.TryParse(parts[0], out var red) || !byte.TryParse(parts[1], out var green) || !byte.TryParse(parts[2], out var blue) || !byte.TryParse(parts[3], out var alpha))
            {
                reason = "saved_color_invalid";
                return false;
            }
            color = new Color32(red, green, blue, alpha);
            reason = catalog.IsBodyColorAllowed(color) ? null : $"saved_color_not_allowed:{color}";
            return reason == null;
        }
    }
}
