using System.Collections.Generic;
using LastJumpCrew.SeoBoGyeong;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class NetworkGameOverSequencePresenter : MonoBehaviour, IGameOverSequencePresentation
    {
        [Header("Cinematic Hierarchy")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private Transform playerShipRoot;
        [SerializeField] private Transform enemyFleetRoot;

        [Header("Downloaded Effect Roots")]
        [SerializeField] private GameObject fleetArrivalEffectRoot;
        [SerializeField] private GameObject barrageEffectRoot;
        [SerializeField] private GameObject explosionEffectRoot;

        [Header("Motion")]
        [SerializeField] private Vector3 playerShipDrift = new(0f, -1.4f, 7f);
        [SerializeField] private Vector3 enemyFleetApproach = new(0f, 0f, 38f);
        [SerializeField, Min(0f)] private float cameraShakePosition = 0.32f;
        [SerializeField, Min(0f)] private float cameraShakeRotation = 1.15f;

        private NetworkGameOverSequenceCoordinator sequence;
        private NetworkGameOverSequenceSnapshot activeSnapshot;
        private Vector3 playerShipStart;
        private Vector3 enemyFleetStart;
        private Vector3 cameraStart;
        private Quaternion cameraRotationStart;
        private bool barrageStarted;
        private bool explosionStarted;
        private readonly List<Canvas> hiddenCanvases = new();

        public bool IsPresenting { get; private set; }
        public uint PresentedRevision { get; private set; }

        private void Awake()
        {
            CacheStartPose();
            ResetPresentation();
        }

        private void OnEnable()
        {
            NetworkRunSessionRoot.InstanceAvailable += HandleRunSessionRootAvailable;
            if (NetworkRunSessionRoot.Instance != null)
            {
                Bind(NetworkRunSessionRoot.Instance.GameOverSequence);
            }
        }

        private void OnDisable()
        {
            NetworkRunSessionRoot.InstanceAvailable -= HandleRunSessionRootAvailable;
            Unbind();
        }

        private void Update()
        {
            if (!IsPresenting || activeSnapshot.State != NetworkGameOverSequenceState.Playing)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            var duration = activeSnapshot.CompletesServerTime - activeSnapshot.StartedServerTime;
            if (duration <= 0d)
            {
                return;
            }

            var elapsed = networkManager.ServerTime.Time - activeSnapshot.StartedServerTime;
            var normalized = Mathf.Clamp01((float)(elapsed / duration));
            TickPresentation(normalized, (float)elapsed, activeSnapshot.Reason);
        }

        public void Present(NetworkGameOverSequenceSnapshot snapshot)
        {
            if (snapshot.State != NetworkGameOverSequenceState.Playing
                || snapshot.Revision == 0U
                || PresentedRevision == snapshot.Revision)
            {
                return;
            }

            ResetPose();
            activeSnapshot = snapshot;
            PresentedRevision = snapshot.Revision;
            IsPresenting = true;
            visualRoot.SetActive(true);
            cinematicCamera.enabled = true;
            HideGameplayCanvases();
            SetEffectActive(fleetArrivalEffectRoot, snapshot.Reason == GameOverReason.TimeOver);
            SetEffectActive(barrageEffectRoot, false);
            SetEffectActive(explosionEffectRoot, false);
            barrageStarted = false;
            explosionStarted = false;
            Debug.Log(
                $"PHS_GAME_OVER_PRESENTATION_STARTED reason={snapshot.Reason} revision={snapshot.Revision}",
                this);
        }

        public void Complete(NetworkGameOverSequenceSnapshot snapshot)
        {
            if (snapshot.Revision == 0U || snapshot.Revision != PresentedRevision)
            {
                return;
            }

            activeSnapshot = snapshot;
            if (!explosionStarted)
            {
                StartExplosion();
            }

            IsPresenting = false;
            RestoreGameplayCanvases();
            Debug.Log(
                $"PHS_GAME_OVER_PRESENTATION_COMPLETED reason={snapshot.Reason} revision={snapshot.Revision}",
                this);
        }

        public void ResetPresentation()
        {
            RestoreGameplayCanvases();
            activeSnapshot = default;
            PresentedRevision = 0U;
            IsPresenting = false;
            barrageStarted = false;
            explosionStarted = false;
            ResetPose();
            SetEffectActive(fleetArrivalEffectRoot, false);
            SetEffectActive(barrageEffectRoot, false);
            SetEffectActive(explosionEffectRoot, false);
            if (cinematicCamera != null)
            {
                cinematicCamera.enabled = false;
            }

            if (visualRoot != null)
            {
                visualRoot.SetActive(false);
            }
        }

        private void HandleRunSessionRootAvailable(NetworkRunSessionRoot root)
        {
            Bind(root != null ? root.GameOverSequence : null);
        }

        private void Bind(NetworkGameOverSequenceCoordinator nextSequence)
        {
            if (sequence == nextSequence)
            {
                return;
            }

            Unbind();
            sequence = nextSequence;
            if (sequence == null)
            {
                return;
            }

            sequence.SequenceChanged += HandleSequenceChanged;
            ApplySnapshot(sequence.Snapshot);
        }

        private void Unbind()
        {
            if (sequence != null)
            {
                sequence.SequenceChanged -= HandleSequenceChanged;
            }

            sequence = null;
        }

        private void HandleSequenceChanged(
            NetworkGameOverSequenceSnapshot previous,
            NetworkGameOverSequenceSnapshot current)
        {
            ApplySnapshot(current);
        }

        private void ApplySnapshot(NetworkGameOverSequenceSnapshot snapshot)
        {
            switch (snapshot.State)
            {
                case NetworkGameOverSequenceState.Idle:
                    ResetPresentation();
                    break;
                case NetworkGameOverSequenceState.Playing:
                    Present(snapshot);
                    break;
                case NetworkGameOverSequenceState.Completed:
                    if (PresentedRevision == 0U)
                    {
                        Present(new NetworkGameOverSequenceSnapshot(
                            NetworkGameOverSequenceState.Playing,
                            snapshot.Reason,
                            snapshot.Revision,
                            snapshot.StartedServerTime,
                            snapshot.CompletesServerTime));
                    }

                    Complete(snapshot);
                    break;
            }
        }

        private void TickPresentation(float normalized, float elapsed, GameOverReason reason)
        {
            var pursuitEnd = reason == GameOverReason.TimeOver ? 0.36f : 0f;
            var explosionStart = reason == GameOverReason.TimeOver ? 0.76f : 0.56f;

            if (playerShipRoot != null)
            {
                playerShipRoot.localPosition = playerShipStart + playerShipDrift * normalized;
                playerShipRoot.localRotation = Quaternion.Euler(
                    Mathf.Sin(elapsed * 3.7f) * 2.3f,
                    Mathf.Sin(elapsed * 1.9f) * 4f,
                    Mathf.Sin(elapsed * 4.4f) * 3.2f);
            }

            if (enemyFleetRoot != null && reason == GameOverReason.TimeOver)
            {
                var approach = pursuitEnd <= 0f ? 1f : Mathf.Clamp01(normalized / pursuitEnd);
                enemyFleetRoot.localPosition = enemyFleetStart
                    + enemyFleetApproach * Mathf.SmoothStep(0f, 1f, approach);
            }

            if (!barrageStarted && normalized >= pursuitEnd)
            {
                barrageStarted = true;
                SetEffectActive(barrageEffectRoot, true);
            }

            if (!explosionStarted && normalized >= explosionStart)
            {
                StartExplosion();
            }

            ApplyCameraShake(elapsed, normalized >= pursuitEnd ? 1f : 0.35f);
        }

        private void StartExplosion()
        {
            explosionStarted = true;
            SetEffectActive(explosionEffectRoot, true);
            if (playerShipRoot != null)
            {
                playerShipRoot.gameObject.SetActive(false);
            }
        }

        private void ApplyCameraShake(float elapsed, float intensity)
        {
            if (cinematicCamera == null)
            {
                return;
            }

            var x = (Mathf.PerlinNoise(elapsed * 18f, 0.37f) - 0.5f) * 2f;
            var y = (Mathf.PerlinNoise(0.71f, elapsed * 21f) - 0.5f) * 2f;
            cinematicCamera.transform.localPosition = cameraStart
                + new Vector3(x, y, 0f) * cameraShakePosition * intensity;
            cinematicCamera.transform.localRotation = cameraRotationStart
                * Quaternion.Euler(y * cameraShakeRotation * intensity, x * cameraShakeRotation * intensity, 0f);
        }

        private void CacheStartPose()
        {
            playerShipStart = playerShipRoot != null ? playerShipRoot.localPosition : Vector3.zero;
            enemyFleetStart = enemyFleetRoot != null ? enemyFleetRoot.localPosition : Vector3.zero;
            cameraStart = cinematicCamera != null ? cinematicCamera.transform.localPosition : Vector3.zero;
            cameraRotationStart = cinematicCamera != null
                ? cinematicCamera.transform.localRotation
                : Quaternion.identity;
        }

        private void ResetPose()
        {
            if (playerShipRoot != null)
            {
                playerShipRoot.localPosition = playerShipStart;
                playerShipRoot.localRotation = Quaternion.identity;
                playerShipRoot.gameObject.SetActive(true);
            }

            if (enemyFleetRoot != null)
            {
                enemyFleetRoot.localPosition = enemyFleetStart;
            }

            if (cinematicCamera != null)
            {
                cinematicCamera.transform.localPosition = cameraStart;
                cinematicCamera.transform.localRotation = cameraRotationStart;
            }
        }

        private static void SetEffectActive(GameObject root, bool active)
        {
            if (root == null)
            {
                return;
            }

            root.SetActive(active);
            if (!active)
            {
                return;
            }

            foreach (var particleSystem in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Clear(true);
                particleSystem.Play(true);
            }
        }

        private void HideGameplayCanvases()
        {
            hiddenCanvases.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas == null
                    || !canvas.enabled
                    || canvas.transform.IsChildOf(transform))
                {
                    continue;
                }

                canvas.enabled = false;
                hiddenCanvases.Add(canvas);
            }
        }

        private void RestoreGameplayCanvases()
        {
            foreach (var canvas in hiddenCanvases)
            {
                if (canvas != null)
                {
                    canvas.enabled = true;
                }
            }

            hiddenCanvases.Clear();
        }
    }
}
