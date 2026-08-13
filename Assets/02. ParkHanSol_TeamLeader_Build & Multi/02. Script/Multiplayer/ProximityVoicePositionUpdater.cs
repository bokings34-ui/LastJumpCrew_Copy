using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ProximityVoicePositionUpdater : NetworkBehaviour
    {
        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
        [SerializeField, Min(0.1f)] private float positionUpdateInterval = 0.3f;

        private float nextPositionUpdateTime;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            ResolveVoiceChatSession();
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            ResolveVoiceChatSession();
            if (voiceChatSession == null || !voiceChatSession.IsInChannel || Time.time < nextPositionUpdateTime)
            {
                return;
            }

            voiceChatSession.UpdateLocalPosition(gameObject);
            nextPositionUpdateTime = Time.time + positionUpdateInterval;
        }

        private void ResolveVoiceChatSession()
        {
            if (voiceChatSession == null)
            {
                voiceChatSession = FindFirstObjectByType<ProximityVoiceChatSession>();
            }
        }
    }
}
