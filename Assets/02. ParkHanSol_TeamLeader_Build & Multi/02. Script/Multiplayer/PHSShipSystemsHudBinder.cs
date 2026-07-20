using System.Text;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class PHSShipSystemsHudBinder : MonoBehaviour
    {
        [SerializeField] private ParkHanSolPlayHudMockPresenter presenter;
        [SerializeField] private TMP_Text optionalModuleStatusText;

        private NetworkShipSystemsState boundState;
        private float nextBindAttemptTime;

        private void OnEnable()
        {
            TryBind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            if (boundState != null || Time.unscaledTime < nextBindAttemptTime)
            {
                return;
            }

            nextBindAttemptTime = Time.unscaledTime + 0.25f;
            TryBind();
        }

        private void TryBind()
        {
            if (presenter == null)
            {
                Debug.LogError($"PHS_SHIP_HUD_BIND_FAILED reason=presenter_missing binder={name}", this);
                enabled = false;
                return;
            }

            var state = NetworkShipSystemsState.Instance;
            if (state == null || !state.IsSpawned)
            {
                return;
            }

            if (boundState == state)
            {
                Refresh();
                return;
            }

            Unbind();
            boundState = state;
            boundState.StateChanged += Refresh;
            Refresh();
            Debug.Log($"PHS_SHIP_HUD_BOUND revision={boundState.Revision}", this);
        }

        private void Unbind()
        {
            if (boundState != null)
            {
                boundState.StateChanged -= Refresh;
                boundState = null;
            }
        }

        private void Refresh()
        {
            if (boundState == null)
            {
                return;
            }

            presenter.SetShipHp(boundState.CurrentShipHp, boundState.MaximumShipHp);
            if (optionalModuleStatusText == null)
            {
                return;
            }

            var builder = new StringBuilder(128);
            builder.Append("LAST DAMAGE ");
            builder.Append(boundState.LastDamageCause.ToUpperInvariant());
            for (var index = 0; index < boundState.ModuleCount; index++)
            {
                var module = boundState.GetModuleSnapshotAt(index);
                builder.AppendLine();

                builder.Append(module.ModuleId.ToString().ToUpperInvariant());
                builder.Append(' ');
                builder.Append(module.CurrentHp);
                builder.Append('/');
                builder.Append(module.MaximumHp);
                builder.Append(' ');
                builder.Append(module.RepairCondition.ToString().ToUpperInvariant());
                builder.Append(" CAUSE=");
                builder.Append(module.LastDamageCause.ToString().ToUpperInvariant());
            }

            optionalModuleStatusText.text = builder.ToString();
        }
    }
}
