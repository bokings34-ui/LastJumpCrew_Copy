using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class PHSCurvedHudMeshEffect : BaseMeshEffect
    {
        [SerializeField, Range(0f, 0.2f)]
        private float curvature = 0.085f;

        [SerializeField]
        private RectTransform referenceRect;

        public void Configure(RectTransform rect, float strength)
        {
            referenceRect = rect;
            curvature = Mathf.Clamp(strength, 0f, 0.2f);
            graphic?.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || referenceRect == null || vertexHelper.currentVertCount == 0)
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

            UIVertex vertex = default;
            for (int index = 0; index < vertexHelper.currentVertCount; index++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, index);

                Vector3 worldPosition = transform.TransformPoint(vertex.position);
                Vector3 referencePosition = referenceRect.InverseTransformPoint(worldPosition);
                float normalizedX = (referencePosition.x - rect.center.x) / halfWidth;
                float normalizedY = (referencePosition.y - rect.center.y) / halfHeight;
                float radiusSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                float scale = Mathf.Max(0.72f, 1f - curvature * radiusSquared);

                referencePosition.x = rect.center.x + normalizedX * scale * halfWidth;
                referencePosition.y = rect.center.y + normalizedY * scale * halfHeight;

                worldPosition = referenceRect.TransformPoint(referencePosition);
                vertex.position = transform.InverseTransformPoint(worldPosition);
                vertexHelper.SetUIVertex(vertex, index);
            }
        }
    }
}
