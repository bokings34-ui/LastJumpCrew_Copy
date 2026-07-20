using LastJumpCrew.SeoBoGyeong;
using LastJumpCrew.ParkHanSol.Multiplayer;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    public sealed class ShopRunFlowAdapter : MonoBehaviour, IShopRunFlowService
    {
        private IGameStateProvider state;
        private IGameCommands commands;

        public bool IsReady => state != null && commands != null;
        public bool IsShopVisitRequired => IsReady
            && (state.Phase == GamePhase.Shop
                || NetworkRunFlowCoordinator.Instance != null
                && NetworkRunFlowCoordinator.Instance.IsFinalShopPending);

        private void Start()
        {
            var gameCore = GameCore.Instance;
            if (gameCore == null || gameCore.Services == null)
            {
                Debug.LogError($"PHS_SHOP_RUN_FLOW_BIND_FAILED reason=game_core_missing adapter={name}", this);
                return;
            }

            state = gameCore.Services.Get<IGameStateProvider>();
            commands = gameCore.Services.Get<IGameCommands>();
            if (!IsReady)
            {
                Debug.LogError($"PHS_SHOP_RUN_FLOW_BIND_FAILED reason=services_missing adapter={name}", this);
            }
        }

        public bool CanEnterShop(out string reason)
        {
            if (!IsReady)
            {
                reason = "run_flow_not_ready";
                return false;
            }

            var networkRunFlow = NetworkRunFlowCoordinator.Instance;
            var isFinalShop = networkRunFlow != null
                && networkRunFlow.IsFinalShopPending
                && state.Phase == GamePhase.GameClear;
            if (state.Phase != GamePhase.Shop && !isFinalShop)
            {
                reason = $"phase_{state.Phase}";
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryCompleteShop(out string reason)
        {
            if (!IsReady)
            {
                reason = "run_flow_not_ready";
                return false;
            }

            var networkRunFlow = NetworkRunFlowCoordinator.Instance;
            if (networkRunFlow != null && networkRunFlow.IsFinalShopPending)
            {
                return networkRunFlow.TryCompleteFinalShop(out reason);
            }

            // 메인 씬에서 직접 방문한 상점은 진행 상태를 바꾸지 않고 돌아간다.
            // 정규 상점 회차(GamePhase.Shop)만 CloseShop 명령으로 다음 구역 선택 단계에 진입한다.
            if (state.Phase == GamePhase.ZoneSelect || state.Phase == GamePhase.Play)
            {
                reason = null;
                Debug.Log($"PHS_SHOP_RUN_FLOW_DIRECT_RETURN adapter={name}");
                return true;
            }

            if (state.Phase != GamePhase.Shop)
            {
                reason = $"phase_{state.Phase}";
                return false;
            }

            commands.CloseShop();
            if (state.Phase != GamePhase.ZoneSelect)
            {
                reason = $"close_rejected_phase_{state.Phase}";
                Debug.LogError($"PHS_SHOP_RUN_FLOW_CLOSE_FAILED reason={reason} adapter={name}", this);
                return false;
            }

            reason = null;
            Debug.Log($"PHS_SHOP_RUN_FLOW_CLOSED adapter={name} clearedZones={state.ClearedZoneCount}");
            return true;
        }
    }
}
