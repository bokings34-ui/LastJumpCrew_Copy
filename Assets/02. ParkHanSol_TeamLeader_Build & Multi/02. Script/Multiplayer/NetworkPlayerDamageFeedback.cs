using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkPlayerLifeState))]
    public sealed class NetworkPlayerDamageFeedback : MonoBehaviour
    {
        [SerializeField] private NetworkPlayerLifeState lifeState;
        [SerializeField] private NetworkPlayerSquishyVisualFeedback squishyFeedback;
        [SerializeField] private ParticleSystem hitEffect;
        [SerializeField] private AudioSource hitAudio;

        private bool healthSnapshotReady;

        private void Awake()
        {
            if (lifeState == null
                || squishyFeedback == null
                || hitEffect == null
                || hitAudio == null)
            {
                Debug.LogError(
                    $"PHS_PLAYER_DAMAGE_FEEDBACK_SETUP_FAILED player={name} life={lifeState != null} squishy={squishyFeedback != null} vfx={hitEffect != null} audio={hitAudio != null}",
                    this);
            }
        }

        private void OnEnable()
        {
            if (lifeState != null)
            {
                lifeState.HealthChangedOnAllClients += HandleHealthChanged;
            }
        }

        private void Start()
        {
            healthSnapshotReady = lifeState != null && lifeState.IsSpawned;
        }

        private void OnDisable()
        {
            if (lifeState != null)
            {
                lifeState.HealthChangedOnAllClients -= HandleHealthChanged;
            }
        }

        private void HandleHealthChanged(int previousHealth, int currentHealth)
        {
            if (!healthSnapshotReady)
            {
                healthSnapshotReady = true;
                return;
            }

            if (currentHealth >= previousHealth)
            {
                return;
            }

            squishyFeedback.PlayDamageImpact();
            hitEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            hitEffect.Play(true);
            hitAudio.Play();

            if (lifeState.IsOwner)
            {
                var hud = FindFirstObjectByType<PHSHudFeedbackController>();
                if (hud == null)
                {
                    Debug.LogError(
                        $"PHS_PLAYER_DAMAGE_FEEDBACK_UI_FAILED reason=hud_missing player={name}",
                        this);
                }
                else
                {
                    hud.PlayPlayerDamageFeedback();
                }
            }

            Debug.Log(
                $"PHS_PLAYER_DAMAGE_FEEDBACK_APPLIED player={name} previous={previousHealth} current={currentHealth} owner={lifeState.IsOwner}",
                this);
        }
    }
}
