using LastJumpCrew.ParkHanSol.Multiplayer;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerItemAudioFeedback : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerItemRecord itemRecord;
        [SerializeField] private NetworkObject networkObject;
        [SerializeField] private MonoBehaviour ownerCuePlayerSource;
        [SerializeField] private MonoBehaviour worldCuePlayerSource;

        private INetworkAudioCuePlayer ownerCuePlayer;
        private INetworkAudioCuePlayer worldCuePlayer;
        private string previousItemId;
        private bool isSubscribed;
        private bool hasBaseline;

        public bool HasRequiredReferences =>
            itemRecord != null
            && networkObject != null
            && ownerCuePlayerSource is INetworkAudioCuePlayer
            && worldCuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            ownerCuePlayer = ownerCuePlayerSource as INetworkAudioCuePlayer;
            worldCuePlayer = worldCuePlayerSource as INetworkAudioCuePlayer;
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_AUDIO_SETUP_FAILED reason=inspector_reference_missing player={name}",
                    this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            previousItemId = itemRecord.HeldItemId ?? string.Empty;
            hasBaseline = true;
            Subscribe();
        }

        private void OnEnable()
        {
            if (!hasBaseline || itemRecord == null)
            {
                return;
            }

            previousItemId = itemRecord.HeldItemId ?? string.Empty;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (isSubscribed || itemRecord == null)
            {
                return;
            }

            itemRecord.HeldItemChanged += HandleHeldItemChanged;
            isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!isSubscribed || itemRecord == null)
            {
                return;
            }

            itemRecord.HeldItemChanged -= HandleHeldItemChanged;
            isSubscribed = false;
        }

        private void HandleHeldItemChanged(string currentItemId)
        {
            currentItemId ??= string.Empty;
            if (!hasBaseline)
            {
                previousItemId = currentItemId;
                hasBaseline = true;
                return;
            }

            if (string.Equals(
                    previousItemId,
                    currentItemId,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            var previousWasEmpty = string.IsNullOrEmpty(previousItemId);
            var currentIsEmpty = string.IsNullOrEmpty(currentItemId);
            var cue = previousWasEmpty
                ? NetworkAudioCue.ItemPickup
                : currentIsEmpty
                    ? NetworkAudioCue.ItemDrop
                    : NetworkAudioCue.ItemSwap;
            previousItemId = currentItemId;

            var cuePlayer = networkObject.IsSpawned && !networkObject.IsOwner
                ? worldCuePlayer
                : ownerCuePlayer;
            if (cuePlayer == null)
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_AUDIO_PLAY_FAILED reason=cue_player_missing player={name} cue={cue}",
                    this);
                return;
            }

            if (!cuePlayer.TryPlay(cue, out var reason)
                && reason != "cue_cooldown")
            {
                Debug.LogError(
                    $"PHS_NETWORK_ITEM_AUDIO_PLAY_FAILED reason={reason} player={name} cue={cue}",
                    this);
            }
        }
    }
}
