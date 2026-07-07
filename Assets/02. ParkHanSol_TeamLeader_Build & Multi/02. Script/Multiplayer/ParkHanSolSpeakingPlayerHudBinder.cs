using System.Collections.Generic;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class ParkHanSolSpeakingPlayerHudBinder : MonoBehaviour
    {
        [SerializeField] private ProximityVoiceChatSession voiceChatSession;
        [SerializeField] private ParkHanSolPlayHudMockPresenter hudPresenter;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;

        private readonly List<string> currentSpeakingPlayers = new();
        private float nextRefreshTime;

        private void Awake()
        {
            if (hudPresenter == null)
            {
                hudPresenter = GetComponent<ParkHanSolPlayHudMockPresenter>();
            }
        }

        private void OnEnable()
        {
            ProximityVoiceChatSession.ActiveSessionChanged += SetVoiceChatSession;

            if (voiceChatSession == null)
            {
                voiceChatSession = ProximityVoiceChatSession.ActiveSession;
            }

            if (voiceChatSession == null || hudPresenter == null)
            {
                Debug.LogWarning("PHS_SPEAKING_HUD_BIND_FAILED voiceChatSession or hudPresenter missing");
                hudPresenter?.SetSpeakingPlayers(System.Array.Empty<string>());
                return;
            }

            voiceChatSession.SpeakingParticipantsChanged += RefreshSpeakingPlayers;
            RefreshSpeakingPlayers(voiceChatSession.GetSpeakingParticipantNames());
        }

        private void Update()
        {
            if (voiceChatSession == null || hudPresenter == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshSpeakingPlayers(voiceChatSession.GetSpeakingParticipantNames());
        }

        private void OnDisable()
        {
            ProximityVoiceChatSession.ActiveSessionChanged -= SetVoiceChatSession;

            if (voiceChatSession != null)
            {
                voiceChatSession.SpeakingParticipantsChanged -= RefreshSpeakingPlayers;
            }

            currentSpeakingPlayers.Clear();
            hudPresenter?.SetSpeakingPlayers(System.Array.Empty<string>());
        }

        public void SetVoiceChatSession(ProximityVoiceChatSession session)
        {
            if (voiceChatSession == session)
            {
                return;
            }

            if (isActiveAndEnabled && voiceChatSession != null)
            {
                voiceChatSession.SpeakingParticipantsChanged -= RefreshSpeakingPlayers;
            }

            voiceChatSession = session;

            if (isActiveAndEnabled && voiceChatSession != null)
            {
                voiceChatSession.SpeakingParticipantsChanged += RefreshSpeakingPlayers;
                RefreshSpeakingPlayers(voiceChatSession.GetSpeakingParticipantNames());
                return;
            }

            hudPresenter?.SetSpeakingPlayers(System.Array.Empty<string>());
        }

        private void RefreshSpeakingPlayers(IReadOnlyList<string> playerNames)
        {
            if (IsSameSpeakingPlayers(playerNames))
            {
                return;
            }

            currentSpeakingPlayers.Clear();
            if (playerNames != null)
            {
                for (var i = 0; i < playerNames.Count; i++)
                {
                    currentSpeakingPlayers.Add(playerNames[i]);
                }
            }

            hudPresenter?.SetSpeakingPlayers(playerNames);
        }

        private bool IsSameSpeakingPlayers(IReadOnlyList<string> playerNames)
        {
            var playerCount = playerNames?.Count ?? 0;
            if (currentSpeakingPlayers.Count != playerCount)
            {
                return false;
            }

            for (var i = 0; i < playerCount; i++)
            {
                if (currentSpeakingPlayers[i] != playerNames[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
