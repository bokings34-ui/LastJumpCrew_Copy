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

        // 원본 Mesh보다 살짝 크게 복제해서 외곽선처럼 보이게 하는 배율이다.
        [SerializeField, Min(1f)] private float outlineScale = 1.08f;

        // 런타임에 만든 외곽선 오브젝트 목록이다. 초점 상태에 따라 active만 토글한다.
        private readonly List<GameObject> outlineObjects = new();

        // 현재 이 대상이 플레이어 초점을 받고 있는지 여부다.
        private bool isFocused;

        // 외곽선 오브젝트를 이미 만들었는지 기록해서 중복 생성을 막는다.
        private bool isBuilt;

        private void Awake()
        {
            BuildOutlineObjects();
            SetFocused(false);
        }

        public void SetFocused(bool focused)
        {
            // Scanner가 매 프레임 호출하므로 같은 상태면 아무 작업도 하지 않는다.
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
            // 각 MeshFilter마다 같은 Mesh를 쓰는 자식 오브젝트를 만들고 glowMaterial만 입힌다.
            // 원본 Mesh/Material은 건드리지 않는다.
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
                // Mesh가 없는 Transform이나 Renderer가 없는 노드는 외곽선 대상에서 제외한다.
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
        // InteractableFocusGlow가 만든 외곽선 오브젝트를 표시하는 마커 컴포넌트다.
        // 다시 BuildOutlineObjects 대상에 잡히지 않도록 구분용으로만 쓴다.
    }
}
