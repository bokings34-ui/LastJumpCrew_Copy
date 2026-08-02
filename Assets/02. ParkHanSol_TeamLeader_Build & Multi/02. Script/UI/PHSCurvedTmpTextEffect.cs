using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class PHSCurvedTmpTextEffect : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.2f)]
        private float curvature = 0.085f;

        [SerializeField]
        private RectTransform referenceRect;

        private TMP_Text targetText;

        public void Configure(RectTransform rect, float strength)
        {
            referenceRect = rect;
            curvature = Mathf.Clamp(strength, 0f, 0.2f);
            CacheText();
            targetText?.SetVerticesDirty();
        }

        private void OnEnable()
        {
            CacheText();
            targetText.OnPreRenderText -= ApplyCurvature;
            targetText.OnPreRenderText += ApplyCurvature;
            targetText.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (targetText != null)
            {
                targetText.OnPreRenderText -= ApplyCurvature;
            }
        }

        private void CacheText()
        {
            if (targetText == null)
            {
                targetText = GetComponent<TMP_Text>();
            }
        }

        private void ApplyCurvature(TMP_TextInfo textInfo)
        {
            if (referenceRect == null)
            {
                return;
            }

            Rect rect = referenceRect.rect;
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            if (halfWidth <= Mathf.Epsilon || halfHeight <= Mathf.Epsilon)
            {
                return;
            }

            for (int meshIndex = 0; meshIndex < textInfo.meshInfo.Length; meshIndex++)
            {
                Vector3[] vertices = textInfo.meshInfo[meshIndex].vertices;
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 worldPosition = transform.TransformPoint(vertices[vertexIndex]);
                    Vector3 referencePosition = referenceRect.InverseTransformPoint(worldPosition);
                    float normalizedX = (referencePosition.x - rect.center.x) / halfWidth;
                    float normalizedY = (referencePosition.y - rect.center.y) / halfHeight;
                    float radiusSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                    float scale = Mathf.Max(0.72f, 1f - curvature * radiusSquared);

                    referencePosition.x = rect.center.x + normalizedX * scale * halfWidth;
                    referencePosition.y = rect.center.y + normalizedY * scale * halfHeight;

                    worldPosition = referenceRect.TransformPoint(referencePosition);
                    vertices[vertexIndex] = transform.InverseTransformPoint(worldPosition);
                }
            }
        }
    }
}
