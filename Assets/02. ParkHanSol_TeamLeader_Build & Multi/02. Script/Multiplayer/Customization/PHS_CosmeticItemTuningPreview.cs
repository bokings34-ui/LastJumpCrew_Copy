using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PHS_CosmeticItemTuningPreview : MonoBehaviour
    {
        [SerializeField] private CosmeticCatalog catalog;
        [SerializeField] private CosmeticItemData item;
        [SerializeField] private Transform headSlot;
        [SerializeField] private Transform backSlot;
        [SerializeField] private Transform petSlot;
        [SerializeField] private Transform frontSlot;

        private CosmeticItemData renderedItem;
        private GameObject visual;

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();

        private void Update()
        {
            if (!Application.isPlaying) Refresh();
        }

        private void OnDisable() => Clear();

        private void Refresh()
        {
            if (item == null || item.VisualPrefab == null)
            {
                Clear();
                return;
            }

            var slot = GetSlot(item.Slot);
            if (slot == null)
            {
                Debug.LogError($"PHS_COSMETIC_TUNING_SETUP_FAILED reason=slot_missing slot={item.Slot}", this);
                return;
            }

            ClearGeneratedChildren(headSlot, visual);
            ClearGeneratedChildren(backSlot, visual);
            ClearGeneratedChildren(petSlot, visual);
            ClearGeneratedChildren(frontSlot, visual);

            if (visual == null || renderedItem != item)
            {
                Clear();
                visual = Instantiate(item.VisualPrefab, slot, false);
                visual.name = $"Tuning_{item.DisplayName}";
                visual.hideFlags = HideFlags.DontSaveInEditor;
                renderedItem = item;
            }

            visual.transform.SetLocalPositionAndRotation(item.LocalPosition, Quaternion.Euler(item.LocalEulerAngles));
            visual.transform.localScale = item.LocalScale;
        }

        private Transform GetSlot(CosmeticSlot slot) => slot switch
        {
            CosmeticSlot.Head => headSlot,
            CosmeticSlot.Back => backSlot,
            CosmeticSlot.Pet => petSlot,
            CosmeticSlot.Front => frontSlot,
            _ => null
        };

        private void Clear()
        {
            DestroyPreview(visual);
            ClearGeneratedChildren(headSlot);
            ClearGeneratedChildren(backSlot);
            ClearGeneratedChildren(petSlot);
            ClearGeneratedChildren(frontSlot);
            visual = null;
            renderedItem = null;
        }

        private static void ClearGeneratedChildren(Transform slot, GameObject keep = null)
        {
            if (slot == null) return;
            for (var index = slot.childCount - 1; index >= 0; index--)
            {
                var child = slot.GetChild(index).gameObject;
                if (child != keep && child.name.StartsWith("Tuning_")) DestroyPreview(child);
            }
        }

        private static void DestroyPreview(GameObject preview)
        {
            if (preview == null) return;
            if (Application.isPlaying) Destroy(preview);
            else DestroyImmediate(preview);
        }
    }
}
