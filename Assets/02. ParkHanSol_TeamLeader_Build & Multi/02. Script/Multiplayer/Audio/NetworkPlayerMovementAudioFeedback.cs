using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerMovementAudioFeedback : NetworkBehaviour
    {
        [SerializeField] private NetworkObject networkObject;
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerControlInput playerControlInput;
        [SerializeField] private MonoBehaviour ownerCuePlayerSource;
        [SerializeField] private MonoBehaviour worldCuePlayerSource;
        [SerializeField, Min(0.05f)] private float walkStepInterval = 0.44f;
        [SerializeField, Min(0.05f)] private float runStepInterval = 0.28f;
        [SerializeField, Min(0.01f)] private float minimumMovementSpeed = 0.1f;

        private INetworkAudioCuePlayer ownerCuePlayer;
        private INetworkAudioCuePlayer worldCuePlayer;
        private float nextStepTime;
        private Vector3 previousPosition;
        private bool hasPositionBaseline;

        public bool HasRequiredReferences =>
            networkObject != null
            && playerController != null
            && characterController != null
            && playerControlInput != null
            && ownerCuePlayerSource is INetworkAudioCuePlayer
            && worldCuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            ownerCuePlayer = ownerCuePlayerSource as INetworkAudioCuePlayer;
            worldCuePlayer = worldCuePlayerSource as INetworkAudioCuePlayer;
            if (HasRequiredReferences)
            {
                return;
            }

            Debug.LogError(
                $"PHS_MOVEMENT_AUDIO_SETUP_FAILED player={name} " +
                $"networkObject={networkObject != null} controller={playerController != null} " +
                $"characterController={characterController != null} input={playerControlInput != null} " +
                $"ownerCuePlayer={ownerCuePlayer != null} worldCuePlayer={worldCuePlayer != null}",
                this);
            enabled = false;
        }

        private void Update()
        {
            var currentPosition = transform.position;
            var movedSpeed = 0f;
            if (hasPositionBaseline && Time.deltaTime > 0f)
            {
                var delta = currentPosition - previousPosition;
                delta.y = 0f;
                movedSpeed = delta.magnitude / Time.deltaTime;
            }

            previousPosition = currentPosition;
            hasPositionBaseline = true;

            if (networkObject.IsSpawned && !networkObject.IsOwner)
            {
                return;
            }

            if (!playerController.CanAcceptLocalInput
                || playerController.GravityMode != NetworkPlayerGravityMode.ShipGravity)
            {
                nextStepTime = Time.time;
                return;
            }

            var grounded = characterController.isGrounded || playerController.IsGrounded;
            if (playerControlInput.JumpPressedThisFrame && grounded)
            {
                Play(NetworkAudioCue.PlayerJump);
                nextStepTime = Time.time + runStepInterval;
                return;
            }

            if (!grounded
                || !playerController.HasMoveInput
                || movedSpeed < minimumMovementSpeed)
            {
                nextStepTime = Time.time;
                return;
            }

            if (Time.time < nextStepTime)
            {
                return;
            }

            var cue = playerController.IsRunning
                ? NetworkAudioCue.FootstepRun
                : NetworkAudioCue.FootstepWalk;
            Play(cue);
            nextStepTime = Time.time + (playerController.IsRunning
                ? runStepInterval
                : walkStepInterval);
        }

        private void Play(NetworkAudioCue cue)
        {
            if (!ownerCuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_MOVEMENT_AUDIO_PLAY_FAILED reason={reason} player={name} cue={cue}",
                    this);
            }

            if (networkObject.IsSpawned && networkObject.IsOwner)
            {
                BroadcastMovementCueServerRpc(cue);
            }
        }

        [ServerRpc]
        private void BroadcastMovementCueServerRpc(NetworkAudioCue cue)
        {
            if (!IsMovementCue(cue))
            {
                Debug.LogError(
                    $"PHS_MOVEMENT_AUDIO_BROADCAST_FAILED reason=cue_invalid player={name} cue={cue}",
                    this);
                return;
            }

            BroadcastMovementCueClientRpc(cue);
        }

        [ClientRpc]
        private void BroadcastMovementCueClientRpc(NetworkAudioCue cue)
        {
            if (IsOwner)
            {
                return;
            }

            if (!worldCuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_MOVEMENT_AUDIO_REMOTE_PLAY_FAILED reason={reason} player={name} cue={cue}",
                    this);
            }
        }

        private static bool IsMovementCue(NetworkAudioCue cue)
        {
            return cue == NetworkAudioCue.FootstepWalk
                || cue == NetworkAudioCue.FootstepRun
                || cue == NetworkAudioCue.PlayerJump;
        }
    }
}
