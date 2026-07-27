using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [RequireComponent(typeof(PersonalLobbyCustomizationCreditsWallet))]
    public sealed class NetworkPlayerCustomization : NetworkBehaviour
    {
        private const string OwnedItemsPreferenceKey = "PHS_CosmeticOwnedItems_v1";
        private const string HeadPreferenceKey = "PHS_CosmeticHead_v1";
        private const string BackPreferenceKey = "PHS_CosmeticBack_v1";
        private const string ColorPreferenceKey = "PHS_CosmeticColor_v1";

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

        public event Action StateChanged;

        public CosmeticCatalog Catalog => catalog;
        public PersonalLobbyCustomizationCreditsWallet PersonalCreditsWallet => personalCreditsWallet;
        public bool IsProfileReady => ownerProfileReady;
        public string EquippedHeadId => equippedHeadId.Value.ToString();
        public string EquippedBackId => equippedBackId.Value.ToString();
        public Color32 BodyColor => bodyColor.Value;

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
            if (!ValidateSetup()) return;
            equippedHeadId.OnValueChanged += HandleAppearanceChanged;
            equippedBackId.OnValueChanged += HandleAppearanceChanged;
            bodyColor.OnValueChanged += HandleColorChanged;
            ownedItemIds.OnListChanged += HandleOwnedItemsChanged;
            ApplyAppearance();
            if (IsOwner)
            {
                RequestLoadProfileServerRpc(new FixedString512Bytes(PlayerPrefs.GetString(OwnedItemsPreferenceKey, string.Empty)),
                    new FixedString64Bytes(PlayerPrefs.GetString(HeadPreferenceKey, string.Empty)),
                    new FixedString64Bytes(PlayerPrefs.GetString(BackPreferenceKey, string.Empty)),
                    LoadColor());
            }
        }

        public override void OnNetworkDespawn()
        {
            equippedHeadId.OnValueChanged -= HandleAppearanceChanged;
            equippedBackId.OnValueChanged -= HandleAppearanceChanged;
            bodyColor.OnValueChanged -= HandleColorChanged;
            ownedItemIds.OnListChanged -= HandleOwnedItemsChanged;
            if (IsOwner && ownerProfileReady) SaveProfile();
        }

        public void RequestPurchase(string itemId)
        {
            if (!IsOwner || !ownerProfileReady || string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"PHS_COSMETIC_PURCHASE_FAILED reason=owner_or_item_invalid player={name}");
                return;
            }
            RequestPurchaseServerRpc(new FixedString64Bytes(itemId));
        }

        public void RequestEquip(string itemId)
        {
            if (!IsOwner || !ownerProfileReady || string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogError($"PHS_COSMETIC_EQUIP_FAILED reason=owner_or_item_invalid player={name}");
                return;
            }
            RequestEquipServerRpc(new FixedString64Bytes(itemId));
        }

        public void RequestSetBodyColor(Color32 color)
        {
            if (!IsOwner || !ownerProfileReady)
            {
                Debug.LogError($"PHS_COSMETIC_COLOR_FAILED reason=owner_required player={name}");
                return;
            }
            RequestSetBodyColorServerRpc(color);
        }

        public void RequestUnequip(CosmeticSlot slot)
        {
            if (!IsOwner || !ownerProfileReady)
            {
                Debug.LogError($"PHS_COSMETIC_UNEQUIP_FAILED reason=owner_or_profile_not_ready player={name}");
                return;
            }

            RequestUnequipServerRpc(slot);
        }

        public bool OwnsItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && Owns(itemId);
        }

        [ServerRpc]
        private void RequestLoadProfileServerRpc(FixedString512Bytes ownedIds, FixedString64Bytes headId, FixedString64Bytes backId, Color32 savedColor, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId || serverProfileLoaded)
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=owner_mismatch_or_loaded player={name}");
                return;
            }
            serverProfileLoaded = true;
            foreach (var id in ownedIds.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (catalog.TryGetItem(id, out _)) ownedItemIds.Add(new FixedString64Bytes(id));
            }
            if (catalog.IsBodyColorAllowed(savedColor))
            {
                bodyColor.Value = savedColor;
            }
            else
            {
                Debug.LogError($"PHS_COSMETIC_PROFILE_LOAD_FAILED reason=color_not_allowed color={savedColor} player={name}");
            }
            TryEquipServer(headId, CosmeticSlot.Head);
            TryEquipServer(backId, CosmeticSlot.Back);
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
            StateChanged?.Invoke();
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

        private void HandleAppearanceChanged(FixedString64Bytes _, FixedString64Bytes __)
        {
            ApplyAppearance();
            SaveOwnerProfileIfReady();
            StateChanged?.Invoke();
        }

        private void HandleColorChanged(Color32 _, Color32 __)
        {
            ApplyBodyColor();
            SaveOwnerProfileIfReady();
            StateChanged?.Invoke();
        }

        private void HandleOwnedItemsChanged(NetworkListEvent<FixedString64Bytes> _)
        {
            SaveOwnerProfileIfReady();
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

        private static Color32 LoadColor()
        {
            var parts = PlayerPrefs.GetString(ColorPreferenceKey, "255,255,255,255").Split(',');
            return parts.Length == 4 && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b) && byte.TryParse(parts[3], out var a)
                ? new Color32(r, g, b, a) : new Color32(255, 255, 255, 255);
        }
    }
}
