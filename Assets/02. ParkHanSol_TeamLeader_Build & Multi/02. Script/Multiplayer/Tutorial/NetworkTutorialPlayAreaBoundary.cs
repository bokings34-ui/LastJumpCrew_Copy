using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider))]
    public sealed class NetworkTutorialPlayAreaBoundary : NetworkBehaviour
    {
        [SerializeField] private BoxCollider playArea;
        [SerializeField] private Transform returnPoint;
        [SerializeField, Min(0.1f)] private float warningSeconds = 5f;

        private readonly Dictionary<NetworkPlayerController, WarningState>
            warnings = new();
        private bool networkSetupErrorLogged;

        private sealed class WarningState
        {
            public float Deadline;
            public int LastShownSeconds = -1;
            public bool RecoveryFailed;
        }

        private void Awake()
        {
            if (playArea != null
                && returnPoint != null
                && playArea.bounds.Contains(returnPoint.position))
            {
                return;
            }

            Debug.LogError(
                $"PHS_TUTORIAL_BOUNDARY_DISABLED reason=setup_invalid boundary={name}",
                this);
            enabled = false;
        }

        private void Update()
        {
            var networkManager = NetworkManager.Singleton;
            var networkRunning = networkManager != null
                && networkManager.IsListening;
            if (networkRunning && !IsServer)
            {
                return;
            }

            if (networkRunning && !IsSpawned)
            {
                if (!networkSetupErrorLogged)
                {
                    networkSetupErrorLogged = true;
                    Debug.LogError(
                        $"PHS_TUTORIAL_BOUNDARY_DISABLED reason=network_object_not_spawned boundary={name}",
                        this);
                }

                return;
            }

            var players = FindObjectsByType<NetworkPlayerController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (playArea.bounds.Contains(player.transform.position))
                {
                    CancelWarning(player);
                    continue;
                }

                TickWarning(player);
            }
        }

        private void TickWarning(NetworkPlayerController player)
        {
            if (!warnings.TryGetValue(player, out var state))
            {
                state = new WarningState
                {
                    Deadline = Time.unscaledTime + warningSeconds
                };
                warnings.Add(player, state);
            }

            if (state.RecoveryFailed)
            {
                return;
            }

            var remaining = state.Deadline - Time.unscaledTime;
            if (remaining <= 0f)
            {
                if (!player.IsSpawned)
                {
                    player.RequestTestTeleport(
                        returnPoint.position,
                        returnPoint.rotation);
                }
                else if (!player.TryTeleportForRespawn(
                        returnPoint.position,
                        returnPoint.rotation))
                {
                    state.RecoveryFailed = true;
                    Debug.LogError(
                        $"PHS_TUTORIAL_BOUNDARY_RECOVERY_FAILED player={player.name}",
                        player);
                    return;
                }

                ClearWarning(player);
                warnings.Remove(player);
                return;
            }

            var shownSeconds = Mathf.CeilToInt(remaining);
            if (state.LastShownSeconds == shownSeconds)
            {
                return;
            }

            state.LastShownSeconds = shownSeconds;
            ShowWarning(player, shownSeconds);
        }

        private void CancelWarning(NetworkPlayerController player)
        {
            if (!warnings.Remove(player))
            {
                return;
            }

            ClearWarning(player);
        }

        private void ShowWarning(
            NetworkPlayerController player,
            int remainingSeconds)
        {
            var message =
                $"플레이 구역 이탈 - {remainingSeconds}초 후 출입문 앞으로 복귀";
            if (!player.IsSpawned)
            {
                player.ShowLifeStateMessage(message);
                return;
            }

            SetWarningClientRpc(
                new NetworkObjectReference(player.NetworkObject),
                message,
                CreateOwnerClientRpcParams(player.OwnerClientId));
        }

        private void ClearWarning(NetworkPlayerController player)
        {
            if (!player.IsSpawned)
            {
                player.ClearLifeStateMessage();
                return;
            }

            ClearWarningClientRpc(
                new NetworkObjectReference(player.NetworkObject),
                CreateOwnerClientRpcParams(player.OwnerClientId));
        }

        [ClientRpc]
        private void SetWarningClientRpc(
            NetworkObjectReference playerReference,
            string message,
            ClientRpcParams clientRpcParams = default)
        {
            if (!TryResolvePlayer(playerReference, out var player))
            {
                return;
            }

            player.ShowLifeStateMessage(message);
        }

        [ClientRpc]
        private void ClearWarningClientRpc(
            NetworkObjectReference playerReference,
            ClientRpcParams clientRpcParams = default)
        {
            if (!TryResolvePlayer(playerReference, out var player))
            {
                return;
            }

            player.ClearLifeStateMessage();
        }

        private bool TryResolvePlayer(
            NetworkObjectReference playerReference,
            out NetworkPlayerController player)
        {
            player = null;
            if (playerReference.TryGet(out var networkObject)
                && networkObject.TryGetComponent(out player))
            {
                return true;
            }

            Debug.LogError(
                $"PHS_TUTORIAL_BOUNDARY_UI_FAILED reason=player_reference_missing boundary={name}",
                this);
            return false;
        }

        private static ClientRpcParams CreateOwnerClientRpcParams(
            ulong ownerClientId)
        {
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { ownerClientId }
                }
            };
        }
    }
}
