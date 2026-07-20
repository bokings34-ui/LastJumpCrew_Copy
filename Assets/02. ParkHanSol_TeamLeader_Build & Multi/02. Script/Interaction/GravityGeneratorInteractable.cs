using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class GravityGeneratorInteractable : NetworkBehaviour, IInteractable
    {
        [Header("Existing Scene Reference")]
        [SerializeField] private ShipGravityZoneController gravityController;
        [SerializeField] private string interactionPrompt = "Disable Ship Gravity";

        [Header("Server Validation")]
        [SerializeField, Min(0.1f)] private float serverInteractionDistance = 4f;

        private bool requestPending;

        public string InteractionPrompt => interactionPrompt;
        public float ServerInteractionDistance => serverInteractionDistance;

        public bool CanInteract(IItemHolder itemHolder)
        {
            if (requestPending || gravityController == null || !IsSpawned
                || itemHolder is not Component holderComponent
                || holderComponent.GetComponent<NetworkPlayerController>() is not { } player
                || !player.IsSpawned || !player.IsOwner)
            {
                return false;
            }

            var shipState = NetworkShipSystemsState.Instance;
            return shipState != null
                && shipState.IsSpawned
                && shipState.IsGravityEnabled;
        }

        public void Interact(IItemHolder itemHolder)
        {
            if (!CanInteract(itemHolder))
            {
                Debug.LogWarning(
                    $"PHS_GRAVITY_GENERATOR_REQUEST_FAILED reason=interaction_unavailable generator={name}",
                    this);
                return;
            }

            var player = ((Component)itemHolder).GetComponent<NetworkPlayerController>();
            requestPending = true;
            if (IsServer)
            {
                CompleteServerRequest(player.OwnerClientId);
                return;
            }

            RequestDisableGravityServerRpc();
        }

        public bool IsServerRequestInRange(Vector3 playerPosition)
        {
            return (playerPosition - transform.position).sqrMagnitude
                <= serverInteractionDistance * serverInteractionDistance;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDisableGravityServerRpc(ServerRpcParams rpcParams = default)
        {
            CompleteServerRequest(rpcParams.Receive.SenderClientId);
        }

        private void CompleteServerRequest(ulong senderClientId)
        {
            var success = TryDisableGravityOnServer(senderClientId, out var reason);
            SendResult(senderClientId, success, reason);
        }

        private bool TryDisableGravityOnServer(ulong senderClientId, out string reason)
        {
            reason = null;
            if (!IsSpawned || !IsServer)
            {
                reason = "server_required";
                return false;
            }

            if (gravityController == null)
            {
                reason = "gravity_controller_missing";
                return false;
            }

            if (NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(senderClientId, out var client)
                || client.PlayerObject == null)
            {
                reason = "player_missing";
                return false;
            }

            if (!IsServerRequestInRange(client.PlayerObject.transform.position))
            {
                reason = "player_too_far";
                return false;
            }

            var shipState = NetworkShipSystemsState.Instance;
            if (shipState == null || !shipState.IsSpawned)
            {
                reason = "ship_state_missing";
                return false;
            }

            if (!shipState.TrySetGravityEnabled(false, out reason))
            {
                return false;
            }

            Debug.Log(
                $"PHS_GRAVITY_GENERATOR_DISABLED generator={name} clientId={senderClientId} revision={shipState.Revision}",
                this);
            return true;
        }

        private void SendResult(ulong targetClientId, bool success, string reason)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetClientId }
                }
            };
            CompleteRequestClientRpc(
                success,
                new FixedString64Bytes(reason ?? string.Empty),
                clientRpcParams);
        }

        [ClientRpc]
        private void CompleteRequestClientRpc(
            bool success,
            FixedString64Bytes reason,
            ClientRpcParams clientRpcParams = default)
        {
            requestPending = false;
            if (!success)
            {
                Debug.LogWarning(
                    $"PHS_GRAVITY_GENERATOR_REQUEST_FAILED reason={reason} generator={name}",
                    this);
            }
        }
    }
}
