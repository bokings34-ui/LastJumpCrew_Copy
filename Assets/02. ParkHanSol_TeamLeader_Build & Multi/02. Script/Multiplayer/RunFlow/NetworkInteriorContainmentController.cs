using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkInteriorContainmentController : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float breachConfirmationSeconds = 0.2f;

        private readonly HashSet<NetworkInteriorContainmentVolume> registeredVolumes = new();
        private readonly Dictionary<ulong, HashSet<NetworkInteriorContainmentVolume>> occupiedVolumes = new();
        private readonly Dictionary<ulong, float> pendingBreachDeadlines = new();
        private readonly List<ulong> readyClientIds = new();
        private bool setupValid;

        private void Awake()
        {
            setupValid = true;
        }

        private void Start()
        {
            if (registeredVolumes.Count > 0)
            {
                return;
            }

            setupValid = false;
            Debug.LogError(
                $"PHS_INTERIOR_CONTAINMENT_SETUP_FAILED reason=registered_volumes_missing controller={name}",
                this);
        }

        private void Update()
        {
            if (!setupValid || !IsServerRunning() || pendingBreachDeadlines.Count == 0)
            {
                return;
            }

            readyClientIds.Clear();
            foreach (var pair in pendingBreachDeadlines)
            {
                if (Time.time >= pair.Value)
                {
                    readyClientIds.Add(pair.Key);
                }
            }

            foreach (var clientId in readyClientIds)
            {
                pendingBreachDeadlines.Remove(clientId);
                ConfirmContainmentBreach(clientId);
            }
        }

        private void OnDisable()
        {
            occupiedVolumes.Clear();
            pendingBreachDeadlines.Clear();
            readyClientIds.Clear();
        }

        internal void RegisterVolume(NetworkInteriorContainmentVolume volume)
        {
            if (volume == null || volume.Controller != this)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_REGISTER_FAILED reason=controller_mismatch " +
                    $"volume={(volume != null ? volume.name : "missing")} controller={name}",
                    this);
                return;
            }

            registeredVolumes.Add(volume);
            setupValid = true;
        }

        internal void NotifyPlayerEntered(
            NetworkInteriorContainmentVolume volume,
            NetworkPlayerController player)
        {
            if (!setupValid || !IsServerRunning() || !ValidateNotification(volume, player))
            {
                return;
            }

            if (!occupiedVolumes.TryGetValue(player.OwnerClientId, out var playerVolumes))
            {
                playerVolumes = new HashSet<NetworkInteriorContainmentVolume>();
                occupiedVolumes.Add(player.OwnerClientId, playerVolumes);
            }

            playerVolumes.Add(volume);
            if (pendingBreachDeadlines.Remove(player.OwnerClientId))
            {
                Debug.Log(
                    $"PHS_INTERIOR_CONTAINMENT_REENTERED clientId={player.OwnerClientId} volume={volume.name}",
                    player);
            }
        }

        internal void NotifyPlayerExited(
            NetworkInteriorContainmentVolume volume,
            NetworkPlayerController player)
        {
            if (!setupValid || !IsServerRunning() || !ValidateNotification(volume, player))
            {
                return;
            }

            if (occupiedVolumes.TryGetValue(player.OwnerClientId, out var playerVolumes))
            {
                playerVolumes.Remove(volume);
                if (playerVolumes.Count > 0)
                {
                    return;
                }
            }
            else
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_EXIT_FAILED reason=entry_missing " +
                    $"clientId={player.OwnerClientId} volume={volume.name}",
                    player);
                return;
            }

            var sectorState = player.GetComponent<NetworkPlayerSectorState>();
            if (sectorState == null)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_EXIT_FAILED reason=sector_state_missing clientId={player.OwnerClientId}",
                    player);
                return;
            }

            if (sectorState.CurrentSector != NetworkPlayerSector.Interior)
            {
                pendingBreachDeadlines.Remove(player.OwnerClientId);
                Debug.Log(
                    $"PHS_INTERIOR_CONTAINMENT_EXIT_AUTHORIZED clientId={player.OwnerClientId} " +
                    $"sector={sectorState.CurrentSector}",
                    player);
                return;
            }

            var confirmationDelay = Mathf.Max(0.05f, breachConfirmationSeconds);
            pendingBreachDeadlines[player.OwnerClientId] = Time.time + confirmationDelay;
            Debug.LogWarning(
                $"PHS_INTERIOR_CONTAINMENT_EXIT_PENDING clientId={player.OwnerClientId} " +
                $"seconds={confirmationDelay:0.00}",
                player);
        }

        internal void NotifyVolumeDisabled(NetworkInteriorContainmentVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            registeredVolumes.Remove(volume);
            foreach (var playerVolumes in occupiedVolumes.Values)
            {
                playerVolumes.Remove(volume);
            }
        }

        private void ConfirmContainmentBreach(ulong clientId)
        {
            if (occupiedVolumes.TryGetValue(clientId, out var playerVolumes) && playerVolumes.Count > 0)
            {
                Debug.Log($"PHS_INTERIOR_CONTAINMENT_BREACH_CANCELLED clientId={clientId} reason=reentered", this);
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.ConnectedClients.TryGetValue(clientId, out var client)
                || client.PlayerObject == null)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_BREACH_FAILED reason=player_missing clientId={clientId}",
                    this);
                occupiedVolumes.Remove(clientId);
                return;
            }

            var player = client.PlayerObject.GetComponent<NetworkPlayerController>();
            var sectorState = client.PlayerObject.GetComponent<NetworkPlayerSectorState>();
            var lifeState = client.PlayerObject.GetComponent<INetworkPlayerLifeState>();
            if (player == null || sectorState == null || lifeState == null)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_BREACH_FAILED reason=player_contract_missing clientId={clientId} " +
                    $"player={player != null} sector={sectorState != null} life={lifeState != null}",
                    client.PlayerObject);
                return;
            }

            if (!lifeState.IsAlive)
            {
                Debug.Log($"PHS_INTERIOR_CONTAINMENT_BREACH_CANCELLED clientId={clientId} reason=player_dead", player);
                return;
            }

            if (sectorState.CurrentSector != NetworkPlayerSector.Interior)
            {
                Debug.Log(
                    $"PHS_INTERIOR_CONTAINMENT_BREACH_CANCELLED clientId={clientId} " +
                    $"reason=authorized sector={sectorState.CurrentSector}",
                    player);
                return;
            }

            Debug.LogError(
                $"PHS_INTERIOR_CONTAINMENT_BREACH_CONFIRMED clientId={clientId} pos={player.transform.position}",
                player);
            lifeState.KillForContainmentBreach();
        }

        private bool ValidateNotification(
            NetworkInteriorContainmentVolume volume,
            NetworkPlayerController player)
        {
            if (volume == null || player == null || !player.IsSpawned)
            {
                Debug.LogError(
                    $"PHS_INTERIOR_CONTAINMENT_NOTIFICATION_FAILED volume={volume != null} " +
                    $"player={player != null} spawned={player != null && player.IsSpawned}",
                    this);
                return false;
            }

            if (volume.Controller == this && registeredVolumes.Contains(volume))
            {
                return true;
            }

            Debug.LogError(
                $"PHS_INTERIOR_CONTAINMENT_NOTIFICATION_FAILED reason=volume_not_registered volume={volume.name}",
                volume);
            return false;
        }

        private static bool IsServerRunning()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null && networkManager.IsListening && networkManager.IsServer;
        }
    }
}
