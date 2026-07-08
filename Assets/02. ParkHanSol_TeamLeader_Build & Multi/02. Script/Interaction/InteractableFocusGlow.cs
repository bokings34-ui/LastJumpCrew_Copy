using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    // 플레이어가 집을 수 있는 아이템을 조준했을 때 형광색 외곽선을 보여주는 테스트용 시각 피드백이다.
    // 실제 선택 판정은 TempPlayerInteractionScanner가 하고, 이 컴포넌트는 표시만 담당한다.
    public sealed class InteractableFocusGlow : MonoBehaviour
    {
        // glowMaterial은 Inspector에서 연결한다. 누락되면 대체 재질을 만들지 않고 로그로 드러낸다.
        [SerializeField] private Material glowMaterial;
        [SerializeField, Min(1f)] private float outlineScale = 1.08f;

        private readonly List<GameObject> outlineObjects = new();
        private bool isFocused;
        private bool isBuilt;

        private void Awake()
        {
            BuildOutlineObjects();
            SetFocused(false);
        }

        public void SetFocused(bool focused)
        {
            if (isFocused == focused)
            {
                return;
            }

            isFocused = focused;
            if (!isBuilt)
            {
                BuildOutlineObjects();
            }

            foreach (var outlineObject in outlineObjects)
            {
                if (outlineObject != null)
                {
                    outlineObject.SetActive(isFocused);
                }
            }
        }

        private void BuildOutlineObjects()
        {
            if (isBuilt)
            {
                return;
            }

            isBuilt = true;

            if (glowMaterial == null)
            {
                Debug.LogError($"PHS_INTERACT_GLOW_SETUP_FAILED reason=glowMaterial_missing target={name}");
                return;
            }

            var meshFilters = GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters == null || meshFilters.Length == 0)
            {
                Debug.LogError($"PHS_INTERACT_GLOW_SETUP_FAILED reason=mesh_missing target={name}");
                return;
            }

            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                if (meshFilter.GetComponentInParent<InteractableFocusGlowOutline>() != null)
                {
                    // 런타임에 생성한 외곽선 오브젝트를 다시 외곽선 대상으로 잡지 않도록 막는다.
                    continue;
                }

                var sourceRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer == null)
                {
                    continue;
                }

                var outlineObject = new GameObject($"{meshFilter.name}_FocusGlow");
                outlineObject.transform.SetParent(meshFilter.transform, false);
                outlineObject.transform.localPosition = Vector3.zero;
                outlineObject.transform.localRotation = Quaternion.identity;
                outlineObject.transform.localScale = Vector3.one * outlineScale;
                outlineObject.AddComponent<InteractableFocusGlowOutline>();

                var outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
                outlineMeshFilter.sharedMesh = meshFilter.sharedMesh;

                var outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
                outlineRenderer.sharedMaterial = glowMaterial;
                outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;

                outlineObject.SetActive(false);
                outlineObjects.Add(outlineObject);
            }
        }
    }

    public sealed class InteractableFocusGlowOutline : MonoBehaviour
    {
    }
}
