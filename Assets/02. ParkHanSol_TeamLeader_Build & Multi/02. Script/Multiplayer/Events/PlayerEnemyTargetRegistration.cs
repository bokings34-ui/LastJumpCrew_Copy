using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayerLifeState))]
    public sealed class PlayerEnemyTargetRegistration : MonoBehaviour
    {
        private NetworkPlayerLifeState lifeState;
        private bool isRegistered;

        private void Awake()
        {
            lifeState = GetComponent<NetworkPlayerLifeState>();
        }

        private void OnEnable()
        {
            RefreshRegistration();
        }

        private void Update()
        {
            RefreshRegistration();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void RefreshRegistration()
        {
            var shouldRegister = lifeState != null && lifeState.IsAlive;
            var registry = PlayerRegistry.Instance;
            if (shouldRegister && (!isRegistered || !registry.Contains(transform)))
            {
                registry.Register(transform);
                isRegistered = true;
                return;
            }

            if (shouldRegister)
            {
                return;
            }

            Unregister();
        }

        private void Unregister()
        {
            if (!isRegistered)
            {
                return;
            }

            PlayerRegistry.Peek()?.Unregister(transform);
            isRegistered = false;
        }
    }
}
