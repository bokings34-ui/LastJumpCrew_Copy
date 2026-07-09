using UnityEngine;

namespace LastJumpCrew.ParkHanSol.PlayerPrefab
{
    public sealed class PHS_CuteWhiteGhostRagdollLineupHitDriver : MonoBehaviour
    {
        [SerializeField] private PHS_CuteWhiteGhostRagdollStateController[] targets;
        [SerializeField] private Transform paddle;
        [SerializeField] private Vector3 paddleStartPosition = new(-4f, -0.05f, 1.15f);
        [SerializeField] private Vector3 paddleEndPosition = new(4f, -0.05f, 1.15f);
        [SerializeField, Min(0.05f)] private float waitSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float travelSeconds = 2.2f;
        [SerializeField] private Vector3 launchVelocity = new(3.8f, 5.4f, 0f);
        [SerializeField] private Vector3 launchImpulse = new(0.8f, 0.9f, 0f);
        [SerializeField] private Vector3 launchTorque = new(0f, 0f, -3.2f);
        [SerializeField] private bool autoStart = true;

        private bool setupComplete;
        private bool setupErrorReported;
        private bool running;
        private float elapsed;
        private bool[] hitTargets;

        private void Awake()
        {
            setupComplete = ValidateSetup();
            if (!setupComplete)
            {
                return;
            }

            hitTargets = new bool[targets.Length];
            ResetLineup();
        }

        private void OnEnable()
        {
            if (!setupComplete)
            {
                setupComplete = ValidateSetup();
            }

            if (setupComplete && autoStart)
            {
                StartLineupHit();
            }
        }

        private void Update()
        {
            if (!setupComplete || !running)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < waitSeconds)
            {
                return;
            }

            var t = Mathf.Clamp01((elapsed - waitSeconds) / travelSeconds);
            paddle.position = Vector3.Lerp(paddleStartPosition, paddleEndPosition, t);

            for (var i = 0; i < targets.Length; i++)
            {
                if (hitTargets[i] || targets[i] == null)
                {
                    continue;
                }

                var targetPosition = targets[i].transform.position;
                if (paddleStartPosition.x <= paddleEndPosition.x
                    ? paddle.position.x >= targetPosition.x
                    : paddle.position.x <= targetPosition.x)
                {
                    targets[i].EnterDown(launchVelocity, launchImpulse, launchTorque);
                    hitTargets[i] = true;
                    Debug.Log($"PHS_RAGDOLL_LINEUP_TARGET_HIT target={targets[i].name} index={i}");
                }
            }

            if (t >= 1f)
            {
                running = false;
            }
        }

        [ContextMenu("Start Lineup Hit")]
        public void StartLineupHit()
        {
            if (!ValidateSetup())
            {
                return;
            }

            ResetLineup();
            running = true;
        }

        [ContextMenu("Reset Lineup")]
        public void ResetLineup()
        {
            if (!ValidateSetup())
            {
                return;
            }

            elapsed = 0f;
            running = false;
            paddle.position = paddleStartPosition;

            for (var i = 0; i < targets.Length; i++)
            {
                hitTargets[i] = false;
                targets[i].RestoreFromDown();
            }
        }

        private bool ValidateSetup()
        {
            if (targets == null || targets.Length == 0)
            {
                LogSetupError("targets_missing");
                return false;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    LogSetupError($"target_missing index={i}");
                    return false;
                }
            }

            if (paddle == null)
            {
                LogSetupError("paddle_missing");
                return false;
            }

            return true;
        }

        private void LogSetupError(string reason)
        {
            if (setupErrorReported)
            {
                return;
            }

            setupErrorReported = true;
            Debug.LogError($"PHS_RAGDOLL_LINEUP_HIT_SETUP_FAILED reason={reason} target={name}");
        }
    }
}
