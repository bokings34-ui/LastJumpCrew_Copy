using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class RunFlowHudBinder : MonoBehaviour
    {
        [SerializeField] private ParkHanSolPlayHudMockPresenter presenter;

        private float nextRefreshTime;
        private bool setupErrorLogged;

        private void Awake()
        {
            if (presenter == null && !setupErrorLogged)
            {
                setupErrorLogged = true;
                Debug.LogError($"PHS_RUN_FLOW_HUD_SETUP_FAILED reason=presenter_missing binder={name}", this);
            }
        }

        private void Update()
        {
            if (presenter == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.1f;
            var coordinator = NetworkRunFlowCoordinator.Instance;
            if (coordinator != null)
            {
                presenter.SetWarpGauge(coordinator.WarpChargeNormalized);
            }
        }
    }
}
