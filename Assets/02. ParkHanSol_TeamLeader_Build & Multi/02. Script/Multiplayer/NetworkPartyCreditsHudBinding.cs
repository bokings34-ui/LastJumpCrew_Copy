using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class NetworkPartyCreditsHudBinding : IDisposable
    {
        private readonly ParkHanSolPlayHudMockPresenter playHudPresenter;

        private NetworkRunEconomyLedger boundLedger;
        private bool isRootAvailabilitySubscribed;

        public NetworkPartyCreditsHudBinding(
            ParkHanSolPlayHudMockPresenter playHudPresenter)
        {
            this.playHudPresenter = playHudPresenter
                ? playHudPresenter
                : throw new ArgumentNullException(nameof(playHudPresenter));
        }

        public void Enable()
        {
            SubscribeRootAvailability();
            if (NetworkRunSessionRoot.Instance != null)
            {
                BindRunSessionRoot(NetworkRunSessionRoot.Instance);
            }
        }

        public void Disable()
        {
            UnsubscribeRootAvailability();
            UnbindLedger();
        }

        public void Dispose()
        {
            Disable();
        }

        private void SubscribeRootAvailability()
        {
            if (isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable += HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = true;
        }

        private void UnsubscribeRootAvailability()
        {
            if (!isRootAvailabilitySubscribed)
            {
                return;
            }

            NetworkRunSessionRoot.InstanceAvailable -= HandleRunSessionRootAvailable;
            isRootAvailabilitySubscribed = false;
        }

        private void HandleRunSessionRootAvailable(
            NetworkRunSessionRoot runSessionRoot)
        {
            BindRunSessionRoot(runSessionRoot);
        }

        private void BindRunSessionRoot(NetworkRunSessionRoot runSessionRoot)
        {
            if (runSessionRoot == null || runSessionRoot.Economy == null)
            {
                Debug.LogError(
                    $"PHS_PARTY_CREDITS_NETWORK_HUD_BIND_FAILED reason=economy_ledger_missing binder={playHudPresenter.name}",
                    playHudPresenter);
                return;
            }

            if (boundLedger == runSessionRoot.Economy)
            {
                RefreshCredits(boundLedger.Snapshot);
                return;
            }

            UnbindLedger();
            boundLedger = runSessionRoot.Economy;
            boundLedger.SnapshotChanged += HandleSnapshotChanged;
            RefreshCredits(boundLedger.Snapshot);
        }

        private void UnbindLedger()
        {
            if (boundLedger != null)
            {
                boundLedger.SnapshotChanged -= HandleSnapshotChanged;
            }

            boundLedger = null;
        }

        private void HandleSnapshotChanged(
            NetworkRunEconomySnapshot previous,
            NetworkRunEconomySnapshot current)
        {
            if (previous.Credits != current.Credits)
            {
                RefreshCredits(current);
            }
        }

        private void RefreshCredits(NetworkRunEconomySnapshot snapshot)
        {
            playHudPresenter.SetEconomy(0, snapshot.Credits);
        }
    }
}
