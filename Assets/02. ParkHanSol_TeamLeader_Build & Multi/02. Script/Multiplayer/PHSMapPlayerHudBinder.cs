using LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DefaultExecutionOrder(-100)]
    public sealed class PHSMapPlayerHudBinder : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [SerializeField] private ShipGravityZoneController shipGravityController;

        private NetworkPlayerController boundOwner;
        private NetworkPlayerLifeState boundLifeState;

        private void OnEnable()
        {
            if (shipGravityController == null)
            {
                shipGravityController = FindFirstObjectByType<ShipGravityZoneController>();
            }

            if (shipGravityController != null)
            {
                shipGravityController.GravityStateChanged += HandleGravityStateChanged;
                HandleGravityStateChanged(shipGravityController.IsGravityEnabled);
            }
        }

        private void OnDisable()
        {
            UnbindLifeState();
            boundOwner = null;
            if (shipGravityController != null)
            {
                shipGravityController.GravityStateChanged -= HandleGravityStateChanged;
            }
        }

        private void Update()
        {
            if (playHudPresenter == null)
            {
                return;
            }

            var activeNetworkManager = NetworkManager.Singleton;
            if (activeNetworkManager != null
                && activeNetworkManager.IsListening
                && networkManager != activeNetworkManager)
            {
                networkManager = activeNetworkManager;
            }

            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            foreach (var player in FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None))
            {
                if (!player.IsSpawned || !player.IsOwner || player == boundOwner)
                {
                    continue;
                }

                player.BindPlayHudPresenter(playHudPresenter);
                player.GetComponent<TempPlayerItemHolder>()?.BindPlayHudPresenter(playHudPresenter);
                player.GetComponent<TempPlayerInteractionScanner>()?.BindPlayHudPresenter(playHudPresenter);
                boundOwner = player;
                BindLifeState(player.GetComponent<NetworkPlayerLifeState>());
                return;
            }
        }

        private void BindLifeState(NetworkPlayerLifeState lifeState)
        {
            UnbindLifeState();
            boundLifeState = lifeState;
            if (boundLifeState == null)
            {
                Debug.LogError("PHS_PLAYER_HEALTH_HUD_BIND_FAILED reason=life_state_missing", this);
                return;
            }

            boundLifeState.HealthChanged += HandleHealthChanged;
            HandleHealthChanged(boundLifeState.CurrentHealth, boundLifeState.MaximumHealth);
        }

        private void UnbindLifeState()
        {
            if (boundLifeState != null)
            {
                boundLifeState.HealthChanged -= HandleHealthChanged;
                boundLifeState = null;
            }
        }

        private void HandleHealthChanged(int current, int maximum)
        {
            playHudPresenter?.SetHealth(current, maximum);
        }

        private void HandleGravityStateChanged(bool isEnabled)
        {
            playHudPresenter?.SetGravityWarning(!isEnabled);
        }
    }
}
