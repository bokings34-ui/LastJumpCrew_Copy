using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class MudPrototypeVolume : MonoBehaviour
    {
        private static readonly int[,] Tetrahedrons =
        {
            { 0, 5, 1, 6 },
            { 0, 1, 2, 6 },
            { 0, 2, 3, 6 },
            { 0, 3, 7, 6 },
            { 0, 7, 4, 6 },
            { 0, 4, 5, 6 }
        };

        private static readonly Vector3Int[] CubeCorners =
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(1, 0, 1),
            new(0, 0, 1),
            new(0, 1, 0),
            new(1, 1, 0),
            new(1, 1, 1),
            new(0, 1, 1)
        };

        [SerializeField] private List<MudPrototypeSphereBrush> brushes = new();
        [SerializeField] private Vector3 boundsSize = new(2.4f, 2.8f, 1.8f);
        [SerializeField, Range(6, 48)] private int resolutionX = 28;
        [SerializeField, Range(6, 48)] private int resolutionY = 32;
        [SerializeField, Range(6, 48)] private int resolutionZ = 22;
        [SerializeField, Range(0.01f, 2f)] private float isoLevel = 0.42f;
        [SerializeField] private bool rebuildOnStart;

        private MeshFilter meshFilter;

        private void Awake()
        {
            if (rebuildOnStart)
            {
                Regenerate();
            }
        }

        [ContextMenu("Regenerate Mesh")]
        public void Regenerate()
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_BUILD_FAILED reason=mesh_filter_missing target={name}");
                return;
            }

            if (brushes == null || brushes.Count == 0)
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_BUILD_FAILED reason=brushes_missing target={name}");
                return;
            }

            for (var i = 0; i < brushes.Count; i++)
            {
                if (brushes[i] == null)
                {
                    Debug.LogError($"PHS_MUD_PROTOTYPE_BUILD_FAILED reason=brush_reference_missing index={i} target={name}");
                    return;
                }
            }

            var values = SampleField();
            var vertices = new List<Vector3>(resolutionX * resolutionY * resolutionZ);
            var triangles = new List<int>(resolutionX * resolutionY * resolutionZ * 3);

            for (var x = 0; x < resolutionX; x++)
            {
                for (var y = 0; y < resolutionY; y++)
                {
                    for (var z = 0; z < resolutionZ; z++)
                    {
                        PolygoniseCube(values, x, y, z, vertices, triangles);
                    }
                }
            }

            if (triangles.Count == 0)
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_BUILD_FAILED reason=empty_surface target={name}");
                return;
            }

            WeldVertices(vertices, triangles, out var weldedVertices, out var weldedTriangles);

            var mesh = new Mesh
            {
                name = $"{name}_GeneratedMesh",
                indexFormat = weldedVertices.Count > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16
            };

            mesh.SetVertices(weldedVertices);
            mesh.SetTriangles(weldedTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;

            Debug.Log($"PHS_MUD_PROTOTYPE_BUILD_OK target={name} vertices={weldedVertices.Count} triangles={weldedTriangles.Count / 3}");
        }

        private float[,,] SampleField()
        {
            var values = new float[resolutionX + 1, resolutionY + 1, resolutionZ + 1];
            for (var x = 0; x <= resolutionX; x++)
            {
                for (var y = 0; y <= resolutionY; y++)
                {
                    for (var z = 0; z <= resolutionZ; z++)
                    {
                        var localPosition = GridToLocalPosition(x, y, z);
                        var worldPosition = transform.TransformPoint(localPosition);
                        values[x, y, z] = EvaluateBrushes(worldPosition);
                    }
                }
            }

            return values;
        }

        private float EvaluateBrushes(Vector3 worldPosition)
        {
            var density = 0f;
            foreach (var brush in brushes)
            {
                density += brush.Evaluate(worldPosition);
            }

            return density;
        }

        private Vector3 GridToLocalPosition(int x, int y, int z)
        {
            return new Vector3(
                Mathf.Lerp(-boundsSize.x * 0.5f, boundsSize.x * 0.5f, x / (float)resolutionX),
                Mathf.Lerp(-boundsSize.y * 0.5f, boundsSize.y * 0.5f, y / (float)resolutionY),
                Mathf.Lerp(-boundsSize.z * 0.5f, boundsSize.z * 0.5f, z / (float)resolutionZ));
        }

        private void PolygoniseCube(float[,,] values, int x, int y, int z, List<Vector3> vertices, List<int> triangles)
        {
            var positions = new Vector3[8];
            var cubeValues = new float[8];

            for (var i = 0; i < CubeCorners.Length; i++)
            {
                var corner = CubeCorners[i];
                var gx = x + corner.x;
                var gy = y + corner.y;
                var gz = z + corner.z;
                positions[i] = GridToLocalPosition(gx, gy, gz);
                cubeValues[i] = values[gx, gy, gz];
            }

            for (var i = 0; i < Tetrahedrons.GetLength(0); i++)
            {
                var tetraPositions = new Vector3[4];
                var tetraValues = new float[4];

                for (var j = 0; j < 4; j++)
                {
                    var index = Tetrahedrons[i, j];
                    tetraPositions[j] = positions[index];
                    tetraValues[j] = cubeValues[index];
                }

                PolygoniseTetra(tetraPositions, tetraValues, vertices, triangles);
            }
        }

        private void PolygoniseTetra(Vector3[] positions, float[] values, List<Vector3> vertices, List<int> triangles)
        {
            var inside = new List<int>(4);
            var outside = new List<int>(4);

            for (var i = 0; i < 4; i++)
            {
                if (values[i] >= isoLevel)
                {
                    inside.Add(i);
                }
                else
                {
                    outside.Add(i);
                }
            }

            if (inside.Count == 0 || inside.Count == 4)
            {
                return;
            }

            if (inside.Count == 1)
            {
                AddTriangle(
                    Interpolate(positions, values, inside[0], outside[0]),
                    Interpolate(positions, values, inside[0], outside[1]),
                    Interpolate(positions, values, inside[0], outside[2]),
                    vertices,
                    triangles);
                return;
            }

            if (inside.Count == 3)
            {
                AddTriangle(
                    Interpolate(positions, values, outside[0], inside[0]),
                    Interpolate(positions, values, outside[0], inside[2]),
                    Interpolate(positions, values, outside[0], inside[1]),
                    vertices,
                    triangles);
                return;
            }

            var p0 = Interpolate(positions, values, inside[0], outside[0]);
            var p1 = Interpolate(positions, values, inside[0], outside[1]);
            var p2 = Interpolate(positions, values, inside[1], outside[0]);
            var p3 = Interpolate(positions, values, inside[1], outside[1]);

            AddTriangle(p0, p1, p2, vertices, triangles);
            AddTriangle(p2, p1, p3, vertices, triangles);
        }

        private Vector3 Interpolate(Vector3[] positions, float[] values, int from, int to)
        {
            var valueRange = values[to] - values[from];
            var t = Mathf.Approximately(valueRange, 0f) ? 0.5f : Mathf.Clamp01((isoLevel - values[from]) / valueRange);
            return Vector3.Lerp(positions[from], positions[to], t);
        }

        private static void AddTriangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> vertices, List<int> triangles)
        {
            var index = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
        }

        private static void WeldVertices(
            List<Vector3> sourceVertices,
            List<int> sourceTriangles,
            out List<Vector3> weldedVertices,
            out List<int> weldedTriangles)
        {
            var indexByPosition = new Dictionary<Vector3Int, int>(sourceVertices.Count);
            var remap = new int[sourceVertices.Count];
            weldedVertices = new List<Vector3>(sourceVertices.Count);
            weldedTriangles = new List<int>(sourceTriangles.Count);

            for (var i = 0; i < sourceVertices.Count; i++)
            {
                var key = Quantize(sourceVertices[i]);
                if (!indexByPosition.TryGetValue(key, out var index))
                {
                    index = weldedVertices.Count;
                    indexByPosition.Add(key, index);
                    weldedVertices.Add(sourceVertices[i]);
                }

                remap[i] = index;
            }

            foreach (var triangleIndex in sourceTriangles)
            {
                weldedTriangles.Add(remap[triangleIndex]);
            }
        }

        private static Vector3Int Quantize(Vector3 position)
        {
            const float scale = 10000f;
            return new Vector3Int(
                Mathf.RoundToInt(position.x * scale),
                Mathf.RoundToInt(position.y * scale),
                Mathf.RoundToInt(position.z * scale));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.9f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, boundsSize);
        }
    }
}
