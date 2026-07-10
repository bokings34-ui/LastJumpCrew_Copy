using System.Collections.Generic;
using LastJumpCrew.Common;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PlayerGravityReceiver : MonoBehaviour, IGravityAffectable
    {
        [SerializeField] private GravityMode defaultGravityMode = GravityMode.Spacewalk;

        private readonly List<IGravitySource> gravitySources = new();
        private NetworkPlayerController playerController;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
        }

        private void OnEnable()
        {
            ApplyCurrentGravity();
        }

        public void EnterGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null || gravitySources.Contains(gravitySource))
            {
                return;
            }

            gravitySources.Add(gravitySource);
            ApplyCurrentGravity();
        }

        public void ExitGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null)
            {
                return;
            }

            gravitySources.Remove(gravitySource);
            ApplyCurrentGravity();
        }

        public void RefreshGravitySource(IGravitySource gravitySource)
        {
            if (gravitySource == null || !gravitySources.Contains(gravitySource))
            {
                return;
            }

            ApplyCurrentGravity();
        }

        private void ApplyCurrentGravity()
        {
            if (playerController == null)
            {
                return;
            }

            playerController.ApplyGravityState(GetHighestPriorityState());
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
