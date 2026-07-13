using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ZeroGravityDebrisFieldFlow : MonoBehaviour
    {
        [SerializeField] private Transform[] debrisRoots;
        [SerializeField] private Vector3 flowDirection = new Vector3(0.65f, 0.12f, 0.25f);
        [SerializeField, Min(0.01f)] private float flowSpeed = 0.28f;
        [SerializeField, Min(0.01f)] private float velocityFollowSpeed = 0.08f;
        [SerializeField, Min(0f)] private float driftAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float angularSpeed = 0.16f;

        private readonly List<Rigidbody> debrisBodies = new();

        private void Awake()
        {
            if (debrisRoots == null || debrisRoots.Length == 0)
            {
                Debug.LogError($"PHS_DEBRIS_FLOW_SETUP_FAILED reason=debris_roots_missing flow={name}");
                enabled = false;
                return;
            }

            foreach (var debrisRoot in debrisRoots)
            {
                if (debrisRoot == null)
                {
                    Debug.LogError($"PHS_DEBRIS_FLOW_SETUP_FAILED reason=debris_root_missing flow={name}");
                    enabled = false;
                    return;
                }

                debrisBodies.AddRange(debrisRoot.GetComponentsInChildren<Rigidbody>(true));
            }

            if (debrisBodies.Count == 0)
            {
                Debug.LogError($"PHS_DEBRIS_FLOW_SETUP_FAILED reason=rigidbodies_missing flow={name}");
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            var normalizedFlow = flowDirection.sqrMagnitude > 0.001f
                ? flowDirection.normalized
                : Vector3.forward;

            for (var index = 0; index < debrisBodies.Count; index++)
            {
                var body = debrisBodies[index];
                if (body == null || body.isKinematic || body.useGravity)
                {
                    continue;
                }

                var phase = index * 1.618f;
                var drift = new Vector3(
                    Mathf.Sin(Time.time * 0.37f + phase),
                    Mathf.Cos(Time.time * 0.29f + phase * 0.7f),
                    Mathf.Sin(Time.time * 0.23f + phase * 1.3f)) * driftAmplitude;
                var targetVelocity = normalizedFlow * flowSpeed + drift;
                body.linearVelocity = Vector3.MoveTowards(
                    body.linearVelocity,
                    targetVelocity,
                    velocityFollowSpeed * Time.fixedDeltaTime);
                body.angularVelocity = Vector3.MoveTowards(
                    body.angularVelocity,
                    new Vector3(
                        Mathf.Sin(phase),
                        Mathf.Cos(phase * 0.6f),
                        Mathf.Sin(phase * 1.2f)) * angularSpeed,
                    velocityFollowSpeed * Time.fixedDeltaTime);
            }
        }
    }
}
