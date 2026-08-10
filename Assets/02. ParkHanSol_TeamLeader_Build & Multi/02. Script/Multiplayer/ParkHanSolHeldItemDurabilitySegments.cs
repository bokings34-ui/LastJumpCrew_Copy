using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolHeldItemDurabilitySegments : MonoBehaviour
    {
        [SerializeField] private RectTransform segmentContainer;
        [SerializeField] private GridLayoutGroup segmentGrid;
        [SerializeField] private Image segmentTemplate;
        [SerializeField] private TMP_Text valueText;
        [SerializeField, Min(1)] private int columns = 5;
        [SerializeField, Min(1)] private int maximumDisplaySegments = 5;
        [SerializeField] private Color remainingColor = new(0.16f, 0.9f, 0.42f, 1f);
        [SerializeField] private Color consumedColor = new(0.1f, 0.13f, 0.16f, 0.8f);

        private readonly List<Image> segments = new();
        private int currentSegmentCount = -1;

        private void Awake()
        {
            if (segmentTemplate != null)
            {
                segmentTemplate.gameObject.SetActive(false);
            }
        }

        public void SetDurability(int currentDurability, int maximumDurability, int durabilityCost)
        {
            if (segmentContainer == null || segmentGrid == null || segmentTemplate == null)
            {
                Debug.LogError($"PHS_HELD_ITEM_DURABILITY_SEGMENTS_FAILED reason=reference_missing target={name}", this);
                return;
            }

            if (maximumDurability <= 0 || durabilityCost <= 0)
            {
                Debug.LogError(
                    $"PHS_HELD_ITEM_DURABILITY_SEGMENTS_FAILED reason=invalid_contract target={name} max={maximumDurability} cost={durabilityCost}",
                    this);
                return;
            }

            var totalUses = Mathf.CeilToInt((float)maximumDurability / durabilityCost);
            var remainingUses = Mathf.Clamp(
                Mathf.CeilToInt((float)Mathf.Max(0, currentDurability) / durabilityCost),
                0,
                totalUses);
            var displayedSegmentCount = Mathf.Min(totalUses, maximumDisplaySegments);
            var remainingDisplayedSegments = Mathf.Clamp(
                Mathf.CeilToInt((float)remainingUses / totalUses * displayedSegmentCount),
                0,
                displayedSegmentCount);
            EnsureSegments(displayedSegmentCount);
            ResizeCells(displayedSegmentCount);
            for (var index = 0; index < segments.Count; index++)
            {
                segments[index].color = index < remainingDisplayedSegments
                    ? remainingColor
                    : consumedColor;
            }

            if (valueText != null)
            {
                valueText.text = string.Empty;
                valueText.gameObject.SetActive(false);
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            if (valueText != null)
            {
                valueText.gameObject.SetActive(false);
                valueText.text = string.Empty;
            }
        }

        private void EnsureSegments(int requiredCount)
        {
            if (currentSegmentCount == requiredCount)
            {
                return;
            }

            for (var index = 0; index < segments.Count; index++)
            {
                Destroy(segments[index].gameObject);
            }

            segments.Clear();
            for (var index = 0; index < requiredCount; index++)
            {
                var segment = Instantiate(segmentTemplate, segmentContainer);
                segment.name = $"Durability Segment {index + 1:000}";
                segment.gameObject.SetActive(true);
                segments.Add(segment);
            }

            currentSegmentCount = requiredCount;
        }

        private void ResizeCells(int segmentCount)
        {
            var safeColumns = Mathf.Max(1, Mathf.Min(columns, segmentCount));
            var rows = Mathf.CeilToInt((float)segmentCount / safeColumns);
            var spacing = segmentGrid.spacing;
            var cellWidth =
                (segmentContainer.rect.width - spacing.x * (safeColumns - 1)) / safeColumns;
            var cellHeight =
                (segmentContainer.rect.height - spacing.y * (rows - 1)) / rows;
            segmentGrid.childAlignment = TextAnchor.MiddleCenter;
            segmentGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            segmentGrid.constraintCount = safeColumns;
            segmentGrid.cellSize = new Vector2(cellWidth, cellHeight);
        }
    }
}
