using TinyGiantStudio.Text;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PHSModular3DTextMirror : MonoBehaviour
    {
        [SerializeField] private TMP_Text sourceText;
        [SerializeField] private Modular3DText extrusionText;

        private string lastText;
        private bool referencesValid;
        private bool boundsFitPending;
        private int boundsFitFramesUntilReady;

        private void Awake()
        {
            referencesValid = ValidateReferences();
            if (referencesValid)
                RefreshNow();
        }

        private void OnEnable()
        {
            referencesValid = ValidateReferences();
            if (referencesValid)
                RefreshNow();
        }

        private void OnValidate()
        {
            if (sourceText == null || extrusionText == null)
                return;

            referencesValid = true;
            RefreshNow();
        }

        private void LateUpdate()
        {
            if (!referencesValid)
                return;

            var shouldRender = sourceText.enabled && sourceText.color.a > 0.001f;
            if (extrusionText.gameObject.activeSelf != shouldRender)
                extrusionText.gameObject.SetActive(shouldRender);

            if (lastText != sourceText.text)
                RefreshNow();

            if (boundsFitPending)
            {
                if (boundsFitFramesUntilReady > 0)
                {
                    boundsFitFramesUntilReady--;
                }
                else
                {
                    FitExtrusionToSourceBounds();
                }
            }
        }

        public void RefreshNow()
        {
            if (!ValidateReferences())
                return;

            lastText = sourceText.text ?? string.Empty;
            sourceText.ForceMeshUpdate();
            extrusionText.UpdateText(sourceText.GetParsedText());
            boundsFitPending = true;
            boundsFitFramesUntilReady = 1;
        }

        private void FitExtrusionToSourceBounds()
        {
            if (string.IsNullOrEmpty(sourceText.text))
            {
                boundsFitPending = false;
                return;
            }

            if (!TryGetSourceBounds(out var sourceBounds)
                || !TryGetExtrusionBounds(out var extrusionBounds)
                || extrusionBounds.size.x <= 0.0001f
                || extrusionBounds.size.y <= 0.0001f)
            {
                boundsFitPending = false;
                return;
            }

            var extrusionTransform = extrusionText.transform;
            var currentScale = extrusionTransform.localScale;
            extrusionTransform.localScale = new Vector3(
                currentScale.x * sourceBounds.size.x / extrusionBounds.size.x,
                currentScale.y * sourceBounds.size.y / extrusionBounds.size.y,
                currentScale.z);

            if (!TryGetExtrusionBounds(out extrusionBounds))
            {
                boundsFitPending = false;
                return;
            }

            var parent = extrusionTransform.parent;
            if (parent == null)
            {
                boundsFitPending = false;
                return;
            }

            var worldOffset = sourceBounds.center - extrusionBounds.center;
            var localOffset = parent.InverseTransformVector(worldOffset);
            extrusionTransform.localPosition += new Vector3(
                localOffset.x,
                localOffset.y,
                0f);
            boundsFitPending = false;
        }

        private bool TryGetSourceBounds(out Bounds bounds)
        {
            bounds = default;
            var mesh = sourceText.mesh;
            if (mesh == null || mesh.vertexCount == 0)
                return false;

            return TryGetWorldBounds(mesh.bounds, sourceText.transform, out bounds);
        }

        private bool TryGetExtrusionBounds(out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var filter in extrusionText.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null
                    || !TryGetWorldBounds(
                        filter.sharedMesh.bounds,
                        filter.transform,
                        out var meshBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = meshBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshBounds);
                }
            }

            return hasBounds;
        }

        private static bool TryGetWorldBounds(
            Bounds localBounds,
            Transform targetTransform,
            out Bounds worldBounds)
        {
            worldBounds = default;
            var corners = new[]
            {
                new Vector3(localBounds.min.x, localBounds.min.y, localBounds.min.z),
                new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z),
                new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z),
                new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z),
                new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z),
                new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z),
                new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z),
                new Vector3(localBounds.max.x, localBounds.max.y, localBounds.max.z)
            };
            worldBounds = new Bounds(targetTransform.TransformPoint(corners[0]), Vector3.zero);
            for (var index = 1; index < corners.Length; index++)
                worldBounds.Encapsulate(targetTransform.TransformPoint(corners[index]));

            return true;
        }

        private bool ValidateReferences()
        {
            if (sourceText != null && extrusionText != null)
                return true;

            Debug.LogError($"PHS_M3D_TEXT_MIRROR_FAILED reason=reference_missing object={name}", this);
            return false;
        }
    }
}
