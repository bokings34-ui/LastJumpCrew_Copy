using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkItemInteractionAudioRelay : NetworkBehaviour
    {
        private const int RememberedConfirmationLimit = 256;

        [Header("2D owner-predicted use/fire cues")]
        [SerializeField] private MonoBehaviour ownerCuePlayerSource;

        [Header("3D server-confirmed impact/completion cues")]
        [SerializeField] private MonoBehaviour worldCuePlayerSource;

        private readonly HashSet<ulong> broadcastKeys = new();
        private readonly Queue<ulong> broadcastKeyOrder = new();
        private readonly HashSet<ulong> playedKeys = new();
        private readonly Queue<ulong> playedKeyOrder = new();
        private INetworkAudioCuePlayer ownerCuePlayer;
        private INetworkAudioCuePlayer worldCuePlayer;

        public bool HasRequiredReferences =>
            ownerCuePlayerSource is INetworkAudioCuePlayer
            && worldCuePlayerSource is INetworkAudioCuePlayer;

        private void Awake()
        {
            ownerCuePlayer = ownerCuePlayerSource as INetworkAudioCuePlayer;
            worldCuePlayer = worldCuePlayerSource as INetworkAudioCuePlayer;
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    $"PHS_ITEM_INTERACTION_AUDIO_SETUP_FAILED reason=cue_player_missing player={name}",
                    this);
            }
        }

        public bool TryPlayOwnerPredicted(NetworkAudioCue cue)
        {
            if (!IsSpawned
                || !IsOwner
                || ownerCuePlayer == null
                || !IsOwnerPredictedCue(cue))
            {
                return false;
            }

            return ownerCuePlayer.TryPlay(cue, out _);
        }

        public bool TryBroadcastConfirmedServer(
            NetworkAudioCue cue,
            uint sourceSequence)
        {
            if (!IsSpawned
                || !IsServer
                || sourceSequence == 0U
                || !IsServerConfirmedCue(cue))
            {
                return false;
            }

            var key = ((ulong)(byte)cue << 32) | sourceSequence;
            if (!RememberKey(key, broadcastKeys, broadcastKeyOrder))
            {
                return false;
            }

            PlayConfirmedClientRpc(cue, key);
            return true;
        }

        [ClientRpc]
        private void PlayConfirmedClientRpc(NetworkAudioCue cue, ulong key)
        {
            if (!IsServerConfirmedCue(cue)
                || cue == NetworkAudioCue.WrenchImpact && IsOwner
                || worldCuePlayer == null
                || !RememberKey(key, playedKeys, playedKeyOrder))
            {
                return;
            }

            worldCuePlayer.TryPlay(cue, out _);
        }

        private static bool RememberKey(
            ulong key,
            ISet<ulong> keys,
            Queue<ulong> keyOrder)
        {
            if (!keys.Add(key))
            {
                return false;
            }

            keyOrder.Enqueue(key);
            while (keyOrder.Count > RememberedConfirmationLimit)
            {
                keys.Remove(keyOrder.Dequeue());
            }

            return true;
        }

        private static bool IsOwnerPredictedCue(NetworkAudioCue cue)
        {
            return cue is NetworkAudioCue.ExtinguisherSpray
                or NetworkAudioCue.FoamShot
                or NetworkAudioCue.WrenchImpact;
        }

        private static bool IsServerConfirmedCue(NetworkAudioCue cue)
        {
            return cue is NetworkAudioCue.WrenchImpact
                or NetworkAudioCue.RepairComplete
                or NetworkAudioCue.ExtinguisherSpray
                or NetworkAudioCue.ExtinguishComplete
                or NetworkAudioCue.BatteryInstall
                or NetworkAudioCue.FoamAttach
                or NetworkAudioCue.FoamHarden
                or NetworkAudioCue.FoamSealComplete
                or NetworkAudioCue.FoamFireComplete;
        }
    }
}
