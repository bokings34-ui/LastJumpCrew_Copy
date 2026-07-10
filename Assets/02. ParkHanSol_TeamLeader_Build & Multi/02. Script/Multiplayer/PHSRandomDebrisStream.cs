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
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float maximumSpeed = 2.8f;
        [SerializeField, Min(0f)] private float maximumAngularSpeed = 75f;

        private float[] speeds;
        private Vector3[] angularVelocities;
        private readonly System.Collections.Generic.List<Transform> activeDebris = new();
        private int targetDebrisCount;

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
            for (var index = 0; index < activeDebris.Count; index++) ResetDebris(index);
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
                    ResetDebris(index);
                }
                debris.position += Vector3.left * (speeds[index] * Time.deltaTime);
                debris.Rotate(angularVelocities[index] * Time.deltaTime, Space.Self);

                if (debris.position.x <= recycleWorldX)
                {
                    ResetDebris(index);
                }
            }
        }

        private void ResetDebris(int index)
        {
            var debris = activeDebris[index];
            debris.position = spawnCenter + new Vector3(
                Random.Range(-spawnExtents.x, spawnExtents.x),
                Random.Range(-spawnExtents.y, spawnExtents.y),
                Random.Range(-spawnExtents.z, spawnExtents.z));
            debris.rotation = Random.rotation;
            speeds[index] = Random.Range(minimumSpeed, maximumSpeed);
            angularVelocities[index] = Random.insideUnitSphere * maximumAngularSpeed;
        }
    }
}
