#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace SM
{
    public class SpawnPointAutoGenerator : MonoBehaviour
    {
        [SerializeField] private float gridSpacing = 4f;
        [SerializeField] private float verticalSpacing = 3f;
        [SerializeField] private Bounds generationBounds = new Bounds(Vector3.zero, new Vector3(200, 20, 200));
        [SerializeField] private GameObject spawnPointPrefab;
        [SerializeField] private Transform parentContainer;
        [SerializeField] private float sampleMaxDistance = 1.5f;

        [Header("바닥 높이 필터 (각 층의 바닥 Y값 리스트, 허용 오차 포함)")]
        [SerializeField] private List<float> floorHeights = new List<float> { 0f, 3f, 6f }; // 각 층 바닥 Y값
        [SerializeField] private float floorHeightTolerance = 0.5f; // 이 범위 안이면 바닥으로 인정

        [ContextMenu("Generate Spawn Points on NavMesh")]
        public void Generate()
        {
            for (int i = parentContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parentContainer.GetChild(i).gameObject);
            }

            var min = generationBounds.min;
            var max = generationBounds.max;
            int count = 0;
            var placedPositions = new List<Vector3>();

            for (float x = min.x; x <= max.x; x += gridSpacing)
            {
                for (float z = min.z; z <= max.z; z += gridSpacing)
                {
                    for (float y = min.y; y <= max.y; y += verticalSpacing)
                    {
                        Vector3 testPos = new Vector3(x, y, z);

                        if (NavMesh.SamplePosition(testPos, out NavMeshHit hit, sampleMaxDistance, NavMesh.AllAreas))
                        {
                            if (!IsNearFloorHeight(hit.position.y)) continue;
                            if (!IsPositionSafelyOnNavMesh(hit.position)) continue;

                            bool duplicate = false;
                            foreach (var p in placedPositions)
                            {
                                if (Vector3.Distance(p, hit.position) < gridSpacing * 0.5f)
                                {
                                    duplicate = true;
                                    break;
                                }
                            }

                            if (!duplicate)
                            {
                                var obj = Instantiate(spawnPointPrefab, hit.position, Quaternion.identity, parentContainer);
                                obj.name = $"SpawnPoint_{count}";
                                placedPositions.Add(hit.position);
                                count++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"[SpawnPointAutoGenerator] {count}개 스폰 포인트 생성 완료.");
        }

        private bool IsNearFloorHeight(float y)
        {
            foreach (var floorY in floorHeights)
            {
                if (Mathf.Abs(y - floorY) <= floorHeightTolerance)
                    return true;
            }
            return false;
        }

        private bool IsPositionSafelyOnNavMesh(Vector3 position, float safetyMargin = 0.5f)
        {
            // 안전 마진만큼 떨어진 몇 개 지점도 전부 NavMesh 위인지 확인
            Vector3[] checkOffsets = {
        Vector3.zero,
        new Vector3(safetyMargin, 0, 0),
        new Vector3(-safetyMargin, 0, 0),
        new Vector3(0, 0, safetyMargin),
        new Vector3(0, 0, -safetyMargin)
    };

            foreach (var offset in checkOffsets)
            {
                Vector3 checkPos = position + offset;
                if (!NavMesh.SamplePosition(checkPos, out NavMeshHit hit, 0.3f, NavMesh.AllAreas))
                {
                    return false; // 주변 중 하나라도 NavMesh 밖이면 안전하지 않음 (경계 근처)
                }

                // 샘플링된 위치가 원래 확인하려던 위치와 너무 멀면 (즉 실제로는 NavMesh가 없는데 억지로 당겨온 것)
                if (Vector3.Distance(hit.position, checkPos) > safetyMargin)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawCube(generationBounds.center, generationBounds.size);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(generationBounds.center, generationBounds.size);
        }
    }
}
#endif
