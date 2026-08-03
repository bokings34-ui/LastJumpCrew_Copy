using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [DisallowMultipleComponent]
    public sealed class LobbyCustomizationPreviewPresenter :
        MonoBehaviour,
        IDragHandler,
        IScrollHandler
    {
        private const int RequiredRenderTextureSize = 1024;
        private static readonly Vector3 FrontEulerAngles = new(0f, 180f, 0f);

        [SerializeField] private Transform previewRigRoot;
        [SerializeField] private Transform rotationRoot;
        [SerializeField] private SkinnedMeshRenderer bodyRenderer;
        [SerializeField] private Transform headSlot;
        [SerializeField] private Transform backSlot;
        [SerializeField] private Transform petSlot;
        [SerializeField] private Transform frontSlot;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private RawImage previewImage;
        [SerializeField, Min(0.01f)] private float rotationDegreesPerPixel = 0.35f;
        [SerializeField, Min(0.1f)] private float zoomDegreesPerScroll = 2.5f;
        [SerializeField, Range(1f, 179f)] private float minimumFieldOfView = 24f;
        [SerializeField, Range(1f, 179f)] private float maximumFieldOfView = 45f;

        private MaterialPropertyBlock bodyMaterialProperties;
        private ILobbyCustomizationService service;
        private GameObject headVisual;
        private GameObject backVisual;
        private GameObject petVisual;
        private GameObject frontVisual;
        private string renderedHeadId = string.Empty;
        private string renderedBackId = string.Empty;
        private string renderedPetId = string.Empty;
        private string renderedFrontId = string.Empty;

        private void Awake()
        {
            EnsureBodyMaterialProperties();
        }

        public bool TryBind(
            ILobbyCustomizationService customizationService,
            out string reason)
        {
            if (!ResolveAdditionalSlots(out reason)
                || !ValidateSetup(out reason)
                || customizationService == null)
            {
                reason ??= "service_missing";
                return false;
            }

            if (!ValidateCatalog(customizationService.Catalog, out reason))
            {
                return false;
            }

            rotationRoot.localRotation = Quaternion.Euler(FrontEulerAngles);
            service = customizationService;
            return TryRefresh(out reason);
        }

        public void ClearBinding()
        {
            service = null;
            renderedHeadId = string.Empty;
            renderedBackId = string.Empty;
            renderedPetId = string.Empty;
            renderedFrontId = string.Empty;
            DestroyVisual(ref headVisual);
            DestroyVisual(ref backVisual);
            DestroyVisual(ref petVisual);
            DestroyVisual(ref frontVisual);
        }

        public bool TryRefresh(out string reason)
        {
            if (service == null)
            {
                reason = "service_missing";
                return false;
            }

            if (!TryApplyVisual(
                    service.PreviewHeadId,
                    CosmeticSlot.Head,
                    headSlot,
                    ref headVisual,
                    ref renderedHeadId,
                    out reason)
                || !TryApplyVisual(
                    service.PreviewBackId,
                    CosmeticSlot.Back,
                    backSlot,
                    ref backVisual,
                    ref renderedBackId,
                    out reason)
                || !TryApplyVisual(
                    service.PreviewPetId,
                    CosmeticSlot.Pet,
                    petSlot,
                    ref petVisual,
                    ref renderedPetId,
                    out reason)
                || !TryApplyVisual(
                    service.PreviewFrontId,
                    CosmeticSlot.Front,
                    frontSlot,
                    ref frontVisual,
                    ref renderedFrontId,
                    out reason))
            {
                return false;
            }

            EnsureBodyMaterialProperties();
            bodyRenderer.GetPropertyBlock(bodyMaterialProperties);
            bodyMaterialProperties.SetColor("_BaseColor", service.PreviewBodyColor);
            bodyMaterialProperties.SetColor("_Color", service.PreviewBodyColor);
            bodyRenderer.SetPropertyBlock(bodyMaterialProperties);
            reason = null;
            return true;
        }

        public bool ValidateCatalog(CosmeticCatalog catalog, out string reason)
        {
            if (catalog == null)
            {
                reason = "catalog_missing";
                return false;
            }

            for (var index = 0; index < catalog.Items.Count; index++)
            {
                var item = catalog.Items[index];
                if (item == null || item.VisualPrefab == null)
                {
                    reason = $"visual_prefab_missing:index={index}";
                    return false;
                }

                if (ContainsNetworking(item.VisualPrefab))
                {
                    reason = $"visual_prefab_networked:item={item.ItemId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || rotationRoot == null)
            {
                return;
            }

            rotationRoot.Rotate(
                Vector3.up,
                -eventData.delta.x * rotationDegreesPerPixel,
                Space.World);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null || previewCamera == null)
            {
                return;
            }

            previewCamera.fieldOfView = Mathf.Clamp(
                previewCamera.fieldOfView
                - eventData.scrollDelta.y * zoomDegreesPerScroll,
                minimumFieldOfView,
                maximumFieldOfView);
        }

        private void OnDestroy()
        {
            ClearBinding();
        }

        private bool TryApplyVisual(
            string itemId,
            CosmeticSlot expectedSlot,
            Transform targetSlot,
            ref GameObject currentVisual,
            ref string renderedItemId,
            out string reason)
        {
            itemId ??= string.Empty;
            if (renderedItemId == itemId)
            {
                reason = null;
                return true;
            }

            DestroyVisual(ref currentVisual);
            renderedItemId = string.Empty;
            if (string.IsNullOrEmpty(itemId))
            {
                reason = null;
                return true;
            }

            if (!service.Catalog.TryGetItem(itemId, out var item)
                || item.Slot != expectedSlot
                || item.VisualPrefab == null)
            {
                reason = $"preview_item_invalid:item={itemId}:slot={expectedSlot}";
                return false;
            }

            if (ContainsNetworking(item.VisualPrefab))
            {
                reason = $"preview_item_networked:item={itemId}";
                return false;
            }

            currentVisual = Instantiate(item.VisualPrefab, targetSlot, false);
            currentVisual.transform.SetLocalPositionAndRotation(
                item.LocalPosition,
                Quaternion.Euler(item.LocalEulerAngles));
            currentVisual.transform.localScale = item.LocalScale;
            renderedItemId = itemId;
            reason = null;
            return true;
        }

        private bool ValidateSetup(out string reason)
        {
            if (previewRigRoot == null
                || rotationRoot == null
                || bodyRenderer == null
                || headSlot == null
                || backSlot == null
                || petSlot == null
                || frontSlot == null
                || previewCamera == null
                || previewImage == null)
            {
                reason = "preview_reference_missing";
                return false;
            }

            if (previewRigRoot.GetComponentsInChildren<NetworkObject>(true).Length > 0
                || previewRigRoot.GetComponentsInChildren<NetworkBehaviour>(true).Length > 0)
            {
                reason = "preview_rig_networked";
                return false;
            }

            var renderTexture = previewCamera.targetTexture;
            if (renderTexture == null
                || previewImage.texture != renderTexture
                || renderTexture.width != RequiredRenderTextureSize
                || renderTexture.height != RequiredRenderTextureSize)
            {
                reason = "preview_render_texture_invalid";
                return false;
            }

            if (minimumFieldOfView > maximumFieldOfView)
            {
                reason = "preview_zoom_range_invalid";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ResolveAdditionalSlots(out string reason)
        {
            if (petSlot == null && rotationRoot != null)
            {
                petSlot = rotationRoot.Find("PetSlot");
            }

            if (frontSlot == null && rotationRoot != null)
            {
                frontSlot = rotationRoot.Find("FrontSlot");
            }

            if (petSlot == null || frontSlot == null)
            {
                reason = "preview_additional_slot_missing";
                Debug.LogError($"PHS_COSMETIC_PREVIEW_SETUP_FAILED reason={reason}", this);
                return false;
            }

            reason = null;
            return true;
        }

        private static bool ContainsNetworking(GameObject prefab)
        {
            return prefab.GetComponentInChildren<NetworkObject>(true) != null
                || prefab.GetComponentInChildren<NetworkBehaviour>(true) != null;
        }

        private void EnsureBodyMaterialProperties()
        {
            bodyMaterialProperties ??= new MaterialPropertyBlock();
        }

        private static void DestroyVisual(ref GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(visual);
            visual = null;
        }
    }
}
