using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkItemInteractionAudioRelay : NetworkBehaviour
    {
        private const int MaxRememberedKeys = 256;

        [Header("2D owner-predicted use/fire cues")]
        [SerializeField] private MonoBehaviour ownerCuePlayerSource;

        [Header("3D server-confirmed impact/completion cues")]
        [SerializeField] private MonoBehaviour worldCuePlayerSource;

        private readonly HashSet<ulong> broadcastKeys = new();
        private readonly Queue<ulong> broadcastKeyOrder = new();
        private readonly HashSet<ulong> playedKeys = new();
        private readonly Queue<ulong> playedKeyOrder = new();
        private INetworkAudioCuePlayer ownerCuePlayer;
        private IPositionedNetworkAudioCuePlayer worldCuePlayer;

        public bool HasRequiredReferences =>
            ownerCuePlayerSource is INetworkAudioCuePlayer
            && worldCuePlayerSource is IPositionedNetworkAudioCuePlayer;

        private void Awake()
        {
            ownerCuePlayer = ownerCuePlayerSource as INetworkAudioCuePlayer;
            worldCuePlayer = worldCuePlayerSource
                as IPositionedNetworkAudioCuePlayer;
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
            uint sourceSequence,
            Vector3 confirmedPosition)
        {
            if (!IsSpawned
                || !IsServer
                || sourceSequence == 0U
                || !IsFinite(confirmedPosition)
                || !IsServerConfirmedCue(cue))
            {
                return false;
            }

            var key = ((ulong)(byte)cue << 32) | sourceSequence;
            if (!RememberKey(key, broadcastKeys, broadcastKeyOrder))
            {
                return false;
            }

            PlayConfirmedClientRpc(cue, key, confirmedPosition);
            return true;
        }

        [ClientRpc]
        private void PlayConfirmedClientRpc(
            NetworkAudioCue cue,
            ulong key,
            Vector3 confirmedPosition)
        {
            if (!IsServerConfirmedCue(cue)
                || !IsFinite(confirmedPosition)
                || worldCuePlayer == null
                || !RememberKey(key, playedKeys, playedKeyOrder))
            {
                return;
            }

            worldCuePlayer.TryPlayAt(cue, confirmedPosition, out _);
        }

        private static bool IsFinite(Vector3 position)
        {
            return IsFinite(position.x)
                && IsFinite(position.y)
                && IsFinite(position.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
            while (keyOrder.Count > MaxRememberedKeys)
            {
                keys.Remove(keyOrder.Dequeue());
            }

            return true;
        }

        private static bool IsOwnerPredictedCue(NetworkAudioCue cue)
        {
            return cue is NetworkAudioCue.ExtinguisherSpray
                or NetworkAudioCue.FoamShot;
        }

        private static bool IsServerConfirmedCue(NetworkAudioCue cue)
        {
            return cue is NetworkAudioCue.WrenchImpact
                or NetworkAudioCue.RepairComplete
                or NetworkAudioCue.ExtinguishComplete
                or NetworkAudioCue.BatteryInstall
                or NetworkAudioCue.FoamAttach
                or NetworkAudioCue.FoamHarden
                or NetworkAudioCue.FoamSealComplete
                or NetworkAudioCue.FoamFireComplete;
        }
    }
}
