using LastJumpCrew.ParkHanSol.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSMapPlayerHudBinder : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private ParkHanSolPlayHudMockPresenter playHudPresenter;
        [SerializeField] private ShipGravityZoneController shipGravityController;

        private NetworkPlayerController boundOwner;

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
                return;
            }
        }

        private void HandleGravityStateChanged(bool isEnabled)
        {
            playHudPresenter?.SetGravityWarning(!isEnabled);
        }
    }
}
