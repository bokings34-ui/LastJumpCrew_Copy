using System.Collections.Generic;
using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ItemGravityReceiver : MonoBehaviour, IGravityAffectable
    {
        [SerializeField] private GravityMode defaultGravityMode = GravityMode.ZeroGravity;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField, Min(0f)] private float zeroGravityLinearDamping = 0.08f;
        [SerializeField, Min(0f)] private float zeroGravityAngularDamping = 0.08f;
        [SerializeField, Min(0f)] private float gravityLinearDamping = 0.02f;
        [SerializeField, Min(0f)] private float gravityAngularDamping = 0.05f;
        [SerializeField, Min(0f)] private float driftTorque = 0.015f;

        private readonly List<IGravitySource> gravitySources = new();
        private GravityState currentState;

        private void Awake()
        {
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody>();
            }

            currentState = CreateDefaultState();
        }

        private void OnEnable()
        {
            ApplyCurrentState();
        }

        private void FixedUpdate()
        {
            if (targetRigidbody == null || targetRigidbody.isKinematic)
            {
                return;
            }

            ApplyCurrentState();
            if (currentState.Mode == GravityMode.ShipGravity)
            {
                targetRigidbody.AddForce(
                    currentState.GravityDirection * currentState.GravityStrength,
                    ForceMode.Acceleration);
                return;
            }

            if (driftTorque > 0f)
            {
                targetRigidbody.AddTorque(transform.up * driftTorque, ForceMode.Acceleration);
            }
        }

        public void EnterGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null || gravitySources.Contains(gravitySource))
            {
                return;
            }

            gravitySources.Add(gravitySource);
            currentState = GetHighestPriorityState();
            ApplyCurrentState();
        }

        public void ExitGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null)
            {
                return;
            }

            gravitySources.Remove(gravitySource);
            currentState = GetHighestPriorityState();
            ApplyCurrentState();
        }

        public void RefreshGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null || !gravitySources.Contains(gravitySource))
            {
                return;
            }

            currentState = GetHighestPriorityState();
            ApplyCurrentState();
        }

        private void ApplyCurrentState()
        {
            if (targetRigidbody == null)
            {
                return;
            }

            targetRigidbody.useGravity = false;
            if (currentState.Mode == GravityMode.ShipGravity)
            {
                targetRigidbody.linearDamping = gravityLinearDamping;
                targetRigidbody.angularDamping = gravityAngularDamping;
                return;
            }

            targetRigidbody.linearDamping = zeroGravityLinearDamping;
            targetRigidbody.angularDamping = zeroGravityAngularDamping;
        }

        private GravityState GetHighestPriorityState()
        {
            var selectedState = CreateDefaultState();
            foreach (var source in gravitySources)
            {
                if (source == null)
                {
                    continue;
                }

                var state = source.CurrentGravityState;
                if (state.Priority >= selectedState.Priority)
                {
                    selectedState = state;
                }
            }

            return selectedState;
        }

        private GravityState CreateDefaultState()
        {
            return defaultGravityMode == GravityMode.Spacewalk
                ? GravityState.Spacewalk()
                : new GravityState(defaultGravityMode, int.MinValue, Vector3.down, 0f);
        }
    }
}
