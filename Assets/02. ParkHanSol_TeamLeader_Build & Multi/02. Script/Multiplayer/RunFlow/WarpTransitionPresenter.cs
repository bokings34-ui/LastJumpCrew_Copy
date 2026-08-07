using System.Collections;
using LastJumpCrew.ParkHanSol.Multiplayer.Maps;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class WarpTransitionPresenter :
        MonoBehaviour,
        IWarpTransitionView,
        IMapPresentationConfigurator
    {
        [Header("Run Flow")]
        [SerializeField] private NetworkRunFlowCoordinator runFlowCoordinator;

        [Header("Presentation")]
        [SerializeField] private CanvasGroup transitionCanvasGroup;
        [SerializeField] private GameObject warpVisualRoot;
        [SerializeField] private GameObject warpStatusCardRoot;
        [SerializeField] private TMP_Text warpStatusText;
        [SerializeField] private GameObject safeZoneStatusRoot;
        [SerializeField] private TMP_Text safeZoneStatusText;
        [SerializeField] private GameObject safeZoneShipWarpEffect;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;

        [Header("Environment")]
        [SerializeField] private Material normalSkybox;
        [SerializeField] private Material warpSkybox;
        [SerializeField] private Material arrivalSkybox;

        private Coroutine transitionRoutine;
        private bool setupValid;
        private float nextBindAttemptTime;
        private float bindStartedAtTime;
        private bool bindErrorLogged;

        private void Awake()
        {
            setupValid = ValidateSetup();
            if (!setupValid)
            {
                enabled = false;
                return;
            }

            ApplyIdleState();
        }

        private void OnEnable()
        {
            if (!setupValid)
            {
                return;
            }

            bindStartedAtTime = Time.unscaledTime;
            TryBindCoordinator();
        }

        private void OnDisable()
        {
            if (!setupValid)
            {
                return;
            }

            UnbindCoordinator();
            StopTransitionRoutine();
            ApplyIdleState();
        }

        private void Update()
        {
            if (!setupValid || runFlowCoordinator != null)
            {
                return;
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.IsListening
                || networkManager.ShutdownInProgress)
            {
                bindStartedAtTime = Time.unscaledTime;
                bindErrorLogged = false;
                return;
            }

            if (Time.unscaledTime < nextBindAttemptTime)
            {
                return;
            }

            nextBindAttemptTime = Time.unscaledTime + 0.25f;
            TryBindCoordinator();
        }

        public void EnterWarp()
        {
            if (!RequireSetup(nameof(EnterWarp)))
            {
                return;
            }

            StopTransitionRoutine();
            SetPlayerInputBlocked(true);
            transitionCanvasGroup.blocksRaycasts = true;
            transitionCanvasGroup.interactable = false;
            warpVisualRoot.SetActive(true);
            SetSafeZoneShipWarpEffect(false);
            HideSafeZoneStatus();
            ShowWarpStatusCard("WARP IN PROGRESS");
            ApplySkybox(warpSkybox);
            transitionRoutine = StartCoroutine(FadeCanvas(transitionCanvasGroup.alpha, 1f));
        }

        public void ExitWarp()
        {
            if (!RequireSetup(nameof(ExitWarp)))
            {
                return;
            }

            StopTransitionRoutine();
            SetPlayerInputBlocked(true);
            transitionCanvasGroup.alpha = 1f;
            transitionCanvasGroup.blocksRaycasts = true;
            transitionCanvasGroup.interactable = false;
            warpVisualRoot.SetActive(true);
            SetSafeZoneShipWarpEffect(false);
            HideSafeZoneStatus();
            ShowWarpStatusCard("ARRIVING");
            ApplySkybox(arrivalSkybox);
            transitionRoutine = StartCoroutine(PlayArrival());
        }

        public bool TryConfigureMapPresentation(
            Material gameplaySkybox,
            Material mapArrivalSkybox,
            out string reason)
        {
            if (gameplaySkybox == null)
            {
                reason = "gameplay_skybox_missing";
                return false;
            }

            if (mapArrivalSkybox == null)
            {
                reason = "arrival_skybox_missing";
                return false;
            }

            normalSkybox = gameplaySkybox;
            arrivalSkybox = mapArrivalSkybox;
            if (runFlowCoordinator == null
                || runFlowCoordinator.Phase != NetworkRunPhase.Warping
                && runFlowCoordinator.Phase != NetworkRunPhase.WarpArrival)
            {
                ApplySkybox(normalSkybox);
            }

            reason = null;
            return true;
        }

        private void HandlePhaseChanged(NetworkRunPhase previousPhase, NetworkRunPhase currentPhase)
        {
            if (currentPhase == NetworkRunPhase.WarpSafe)
            {
                EnterSafeZone("안전구역");
                return;
            }

            if (currentPhase == NetworkRunPhase.Shop
                || currentPhase == NetworkRunPhase.FinalShop)
            {
                EnterSafeZone("상점 도착");
                return;
            }

            if (currentPhase == NetworkRunPhase.Warping)
            {
                EnterWarp();
                return;
            }

            if (currentPhase == NetworkRunPhase.WarpArrival)
            {
                ExitWarp();
                return;
            }

            ApplyIdleState();
        }

        private void TryBindCoordinator()
        {
            if (runFlowCoordinator == null)
            {
                var networkManager = NetworkManager.Singleton;
                if (networkManager == null
                    || !networkManager.IsListening
                    || networkManager.ShutdownInProgress)
                {
                    bindStartedAtTime = Time.unscaledTime;
                    bindErrorLogged = false;
                    return;
                }

                runFlowCoordinator = NetworkRunFlowCoordinator.Instance;
            }

            if (runFlowCoordinator == null)
            {
                ApplyIdleState();
                if (!bindErrorLogged && Time.unscaledTime - bindStartedAtTime >= 5f)
                {
                    bindErrorLogged = true;
                    Debug.LogError("PHS_WARP_TRANSITION_BIND_FAILED reason=run_flow_missing", this);
                }

                return;
            }

            bindErrorLogged = false;
            runFlowCoordinator.PhaseChanged -= HandlePhaseChanged;
            runFlowCoordinator.PhaseChanged += HandlePhaseChanged;
            if (runFlowCoordinator.Phase == NetworkRunPhase.Warping)
            {
                EnterWarp();
            }
            else if (runFlowCoordinator.Phase == NetworkRunPhase.WarpSafe)
            {
                EnterSafeZone("안전구역");
            }
            else if (runFlowCoordinator.Phase == NetworkRunPhase.Shop
                || runFlowCoordinator.Phase == NetworkRunPhase.FinalShop)
            {
                EnterSafeZone("상점 도착");
            }
            else if (runFlowCoordinator.Phase == NetworkRunPhase.WarpArrival)
            {
                ExitWarp();
            }
            else
            {
                ApplyIdleState();
            }
        }

        private void UnbindCoordinator()
        {
            if (runFlowCoordinator != null)
            {
                runFlowCoordinator.PhaseChanged -= HandlePhaseChanged;
                runFlowCoordinator = null;
            }
        }

        private IEnumerator PlayArrival()
        {
            yield return FadeCanvas(transitionCanvasGroup.alpha, 0f);
            warpVisualRoot.SetActive(false);
            SetSafeZoneShipWarpEffect(false);
            HideWarpStatusCard();
            HideSafeZoneStatus();
            transitionCanvasGroup.blocksRaycasts = false;
            SetPlayerInputBlocked(false);
            transitionRoutine = null;
        }

        private IEnumerator FadeCanvas(float startAlpha, float targetAlpha)
        {
            var elapsed = 0f;
            transitionCanvasGroup.alpha = startAlpha;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                transitionCanvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }

            transitionCanvasGroup.alpha = targetAlpha;
        }

        private void ApplyIdleState()
        {
            StopTransitionRoutine();
            transitionCanvasGroup.alpha = 0f;
            transitionCanvasGroup.blocksRaycasts = false;
            transitionCanvasGroup.interactable = false;
            warpVisualRoot.SetActive(false);
            SetSafeZoneShipWarpEffect(false);
            HideWarpStatusCard();
            ApplySkybox(normalSkybox);
            SetPlayerInputBlocked(false);
        }

        private static void SetPlayerInputBlocked(bool blocked)
        {
            foreach (var player in FindObjectsByType<NetworkPlayerController>(FindObjectsSortMode.None))
            {
                player.SetWarpInputBlocked(blocked);
            }
        }

        private static void ApplySkybox(Material skybox)
        {
            RenderSettings.skybox = skybox;
            DynamicGI.UpdateEnvironment();
        }

        private void StopTransitionRoutine()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        private void EnterSafeZone(string statusText)
        {
            StopTransitionRoutine();
            transitionCanvasGroup.alpha = 1f;
            transitionCanvasGroup.blocksRaycasts = false;
            transitionCanvasGroup.interactable = false;
            warpVisualRoot.SetActive(false);
            SetSafeZoneShipWarpEffect(true);
            HideWarpStatusCard();
            safeZoneStatusText.text = statusText;
            safeZoneStatusRoot.SetActive(true);
            ApplySkybox(warpSkybox);
            SetPlayerInputBlocked(false);
        }

        private void ShowWarpStatusCard(string message)
        {
            warpStatusText.text = message;
            warpStatusCardRoot.SetActive(true);
        }

        private void HideWarpStatusCard()
        {
            warpStatusCardRoot.SetActive(false);
        }

        private void HideSafeZoneStatus()
        {
            safeZoneStatusRoot.SetActive(false);
        }

        private void SetSafeZoneShipWarpEffect(bool active)
        {
            if (safeZoneShipWarpEffect != null)
            {
                safeZoneShipWarpEffect.SetActive(active);
            }
        }

        private bool RequireSetup(string operation)
        {
            if (setupValid)
            {
                return true;
            }

            Debug.LogError($"PHS_WARP_TRANSITION_FAILED reason=setup_invalid operation={operation}", this);
            return false;
        }

        private bool ValidateSetup()
        {
            var valid = true;
            valid &= RequireReference(transitionCanvasGroup, nameof(transitionCanvasGroup));
            valid &= RequireReference(warpVisualRoot, nameof(warpVisualRoot));
            valid &= RequireReference(warpStatusCardRoot, nameof(warpStatusCardRoot));
            valid &= RequireReference(warpStatusText, nameof(warpStatusText));
            valid &= RequireReference(safeZoneStatusRoot, nameof(safeZoneStatusRoot));
            valid &= RequireReference(safeZoneStatusText, nameof(safeZoneStatusText));
            valid &= RequireReference(normalSkybox, nameof(normalSkybox));
            valid &= RequireReference(warpSkybox, nameof(warpSkybox));
            valid &= RequireReference(arrivalSkybox, nameof(arrivalSkybox));
            return valid;
        }

        private bool RequireReference(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"PHS_WARP_TRANSITION_SETUP_FAILED reason=reference_missing field={fieldName}", this);
            return false;
        }
    }
}
