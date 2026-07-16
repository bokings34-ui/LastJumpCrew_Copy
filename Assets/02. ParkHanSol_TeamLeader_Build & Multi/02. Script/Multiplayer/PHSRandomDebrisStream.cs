using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSRandomDebrisStream : MonoBehaviour
    {
        [SerializeField] private Transform[] debrisRoots;
        [SerializeField, Min(1)] private int minimumDebrisCount = 20;
        [SerializeField, Min(1)] private int maximumDebrisCount = 30;
        [SerializeField] private Vector3 spawnCenter = new(-330f, 6f, -15f);
        [SerializeField] private Vector3 spawnExtents = new(3f, 5f, 12f);
        [SerializeField] private float recycleWorldX = -365f;
        [SerializeField] private bool distributeAcrossPathOnAwake;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2.8f;
        [SerializeField, Min(0f)] private float maximumAngularSpeed = 75f;

        private float[] speeds;
        private Vector3[] angularVelocities;
        private readonly System.Collections.Generic.List<Transform> activeDebris = new();
        private int targetDebrisCount;

        public void SetSimulationEnabled(bool simulationEnabled)
        {
            enabled = simulationEnabled;
            Debug.Log($"PHS_DEBRIS_STREAM_STATE stream={name} enabled={simulationEnabled}", this);
        }

        private void Awake()
        {
            if (debrisRoots == null || debrisRoots.Length == 0)
            {
                Debug.LogError($"PHS_DEBRIS_STREAM_SETUP_FAILED reason=debris_missing stream={name}");
                enabled = false;
                return;
            }

            targetDebrisCount = Random.Range(
                Mathf.Min(minimumDebrisCount, maximumDebrisCount),
                Mathf.Max(minimumDebrisCount, maximumDebrisCount) + 1);
            activeDebris.AddRange(debrisRoots);
            while (activeDebris.Count < targetDebrisCount)
            {
                activeDebris.Add(Instantiate(debrisRoots[activeDebris.Count % debrisRoots.Length], transform.parent));
            }

            speeds = new float[activeDebris.Count];
            angularVelocities = new Vector3[activeDebris.Count];
            for (var index = 0; index < activeDebris.Count; index++)
            {
                ResetDebris(index, distributeAcrossPathOnAwake);
            }
        }

        private void Update()
        {
            for (var index = 0; index < activeDebris.Count; index++)
            {
                var debris = activeDebris[index];
                if (debris == null)
                {
                    debris = Instantiate(debrisRoots[index % debrisRoots.Length], transform.parent);
                    activeDebris[index] = debris;
                    ResetDebris(index, false);
                }
                debris.position += Vector3.left * (speeds[index] * Time.deltaTime);
                debris.Rotate(angularVelocities[index] * Time.deltaTime, Space.Self);

                if (debris.position.x <= recycleWorldX)
                {
                    ResetDebris(index, false);
                }
            }
        }

        private void ResetDebris(int index, bool distributeAcrossPath)
        {
            var debris = activeDebris[index];
            var spawnX = distributeAcrossPath
                ? Random.Range(
                    Mathf.Min(recycleWorldX, spawnCenter.x + spawnExtents.x),
                    Mathf.Max(recycleWorldX, spawnCenter.x + spawnExtents.x))
                : spawnCenter.x + Random.Range(-spawnExtents.x, spawnExtents.x);

            debris.position = new Vector3(
                spawnX,
                spawnCenter.y + Random.Range(-spawnExtents.y, spawnExtents.y),
                spawnCenter.z + Random.Range(-spawnExtents.z, spawnExtents.z));
            debris.rotation = Random.rotation;
            speeds[index] = Random.Range(minimumSpeed, maximumSpeed);
            angularVelocities[index] = Random.insideUnitSphere * maximumAngularSpeed;
        }
    }
}
