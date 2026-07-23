using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class PHSHullBreachSuctionVolume : MonoBehaviour
    {
        [SerializeField] private Transform suctionCenter;
        [SerializeField, Min(0.1f)] private float pullRadius = 8f;
        [SerializeField, Min(0f)] private float stopDistance = 0.85f;
        [SerializeField, Min(0.1f)] private float pullAcceleration = 36f;
        [SerializeField, Min(0.1f)] private float maximumPullSpeed = 3.2f;
        [SerializeField] private LayerMask playerLayers;
        [SerializeField] private LayerMask obstructionLayers;

        private readonly Collider[] overlapBuffer = new Collider[256];
        private readonly HashSet<NetworkPlayerController> processedPlayers = new();
        private readonly HashSet<ulong> loggedPlayerIds = new();
        private bool isConfigurationValid;
        private bool overlapCapacityErrorLogged;

        private void OnEnable()
        {
            isConfigurationValid = ValidateConfiguration();
        }

        private void OnDisable()
        {
            processedPlayers.Clear();
            loggedPlayerIds.Clear();
            overlapCapacityErrorLogged = false;
        }

        private void Update()
        {
            if (!isConfigurationValid)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsListening
                || !networkManager.IsServer)
            {
                return;
            }

            ApplySuction(Time.deltaTime);
        }

        private void ApplySuction(float deltaTime)
        {
            var centerPosition = suctionCenter.position;
            var hitCount = Physics.OverlapSphereNonAlloc(
                centerPosition,
                pullRadius,
                overlapBuffer,
                playerLayers,
                QueryTriggerInteraction.Collide);
            if (hitCount >= overlapBuffer.Length && !overlapCapacityErrorLogged)
            {
                overlapCapacityErrorLogged = true;
                Debug.LogError(
                    $"PHS_HULL_SUCTION_FAILED reason=overlap_capacity_exceeded capacity={overlapBuffer.Length}",
                    this);
            }

            processedPlayers.Clear();

            for (var index = 0; index < hitCount; index++)
            {
                var overlap = overlapBuffer[index];
                overlapBuffer[index] = null;
                if (overlap == null)
                {
                    continue;
                }

                var player = overlap.GetComponentInParent<NetworkPlayerController>();
                if (player == null || !processedPlayers.Add(player))
                {
                    continue;
                }

                var lifeState = player.GetComponent<NetworkPlayerLifeState>();
                if (lifeState == null)
                {
                    Debug.LogError(
                        $"PHS_HULL_SUCTION_FAILED reason=life_state_missing player={player.name}",
                        player);
                    continue;
                }

                if (!lifeState.IsAlive
                    || Physics.Linecast(
                        centerPosition,
                        player.transform.position,
                        obstructionLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                player.ApplyGrapplePull(
                    centerPosition,
                    pullAcceleration,
                    maximumPullSpeed,
                    stopDistance,
                    deltaTime);

                if (loggedPlayerIds.Add(player.OwnerClientId))
                {
                    Debug.Log(
                        $"PHS_HULL_SUCTION_APPLIED client={player.OwnerClientId} distance={Vector3.Distance(centerPosition, player.transform.position):F2}",
                        this);
                }
            }
        }

        private bool ValidateConfiguration()
        {
            if (suctionCenter == null)
            {
                Debug.LogError("PHS_HULL_SUCTION_SETUP_FAILED reason=center_missing", this);
                return false;
            }

            if (playerLayers.value == 0)
            {
                Debug.LogError("PHS_HULL_SUCTION_SETUP_FAILED reason=player_layers_empty", this);
                return false;
            }

            if (obstructionLayers.value == 0)
            {
                Debug.LogError("PHS_HULL_SUCTION_SETUP_FAILED reason=obstruction_layers_empty", this);
                return false;
            }

            if (pullRadius <= stopDistance)
            {
                Debug.LogError(
                    $"PHS_HULL_SUCTION_SETUP_FAILED reason=invalid_distances radius={pullRadius:F2} stop={stopDistance:F2}",
                    this);
                return false;
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (suctionCenter == null)
            {
                return;
            }

            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireSphere(suctionCenter.position, pullRadius);
        }
    }
}
