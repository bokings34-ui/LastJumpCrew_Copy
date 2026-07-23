using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.External
{
    [DisallowMultipleComponent]
    public sealed class PHSExternalEventPresentationBinder : MonoBehaviour
    {
        [Header("Inspector References")]
        [SerializeField] private NetworkEventCoordinator coordinatorOverride;
        [SerializeField] private MonoBehaviour presentationViewBehaviour;

        [Header("Online Event Contract")]
        [SerializeField, Min(0)] private int eventIdValue;
        [SerializeField, Min(0.1f)] private float bindRetrySeconds = 0.5f;
        [SerializeField, Min(0.1f)] private float terminalVisibleSeconds = 1.4f;

        private readonly List<NetworkEventLifecycleSnapshot> snapshotBuffer =
            new();

        private IExternalEventPresentationView presentationView;
        private NetworkEventCoordinator boundCoordinator;
        private ulong appliedInstanceId;
        private uint appliedRevision;
        private float nextBindAttemptTime;
        private float terminalClearAt;
        private bool terminalHoldActive;
        private bool restorePresentationOnNextRefresh;

        public int EventIdValue => eventIdValue;
        public bool HasViewWiring =>
            presentationViewBehaviour is IExternalEventPresentationView;

        private void Awake()
        {
            presentationView =
                presentationViewBehaviour as IExternalEventPresentationView;
        }

        private void OnEnable()
        {
            TryBindCoordinator();
        }

        private void Update()
        {
            if (terminalHoldActive
                && Time.unscaledTime >= terminalClearAt)
            {
                terminalHoldActive = false;
                presentationView?.Cleanup();
            }

            if (boundCoordinator == null
                && Time.unscaledTime >= nextBindAttemptTime)
            {
                nextBindAttemptTime =
                    Time.unscaledTime + bindRetrySeconds;
                TryBindCoordinator();
            }
        }

        private void OnDisable()
        {
            restorePresentationOnNextRefresh =
                appliedInstanceId != 0UL && appliedRevision != 0U;
            UnbindCoordinator();
            presentationView?.Cleanup();
            terminalHoldActive = false;
        }

        private void TryBindCoordinator()
        {
            if (presentationView == null || eventIdValue <= 0)
            {
                return;
            }

            var candidate = coordinatorOverride != null
                ? coordinatorOverride
                : NetworkEventCoordinator.Instance;
            if (candidate == null || !candidate.IsSpawned)
            {
                return;
            }

            if (boundCoordinator == candidate)
            {
                RefreshFromCoordinator();
                return;
            }

            UnbindCoordinator();
            boundCoordinator = candidate;
            boundCoordinator.LifecycleSnapshotsChanged +=
                RefreshFromCoordinator;
            RefreshFromCoordinator();
        }

        private void UnbindCoordinator()
        {
            if (boundCoordinator != null)
            {
                boundCoordinator.LifecycleSnapshotsChanged -=
                    RefreshFromCoordinator;
                boundCoordinator = null;
            }

            snapshotBuffer.Clear();
        }

        private void RefreshFromCoordinator()
        {
            if (boundCoordinator == null || presentationView == null)
            {
                return;
            }

            boundCoordinator.CopySnapshotsTo(snapshotBuffer);
            if (!TrySelectNewestSnapshot(out var selected))
            {
                if (!terminalHoldActive)
                {
                    presentationView.Cleanup();
                    appliedInstanceId = 0UL;
                    appliedRevision = 0U;
                    restorePresentationOnNextRefresh = false;
                }

                return;
            }

            var isSameRevision =
                selected.InstanceId == appliedInstanceId
                && selected.Revision == appliedRevision;
            if (isSameRevision && !restorePresentationOnNextRefresh)
            {
                return;
            }

            var isSameRevisionReactivation =
                isSameRevision && restorePresentationOnNextRefresh;
            restorePresentationOnNextRefresh = false;
            appliedInstanceId = selected.InstanceId;
            appliedRevision = selected.Revision;
            ApplySnapshot(selected, isSameRevisionReactivation);
        }

        private bool TrySelectNewestSnapshot(
            out NetworkEventLifecycleSnapshot selected)
        {
            selected = default;
            var found = false;
            foreach (var snapshot in snapshotBuffer)
            {
                if (snapshot.EventIdValue != eventIdValue)
                {
                    continue;
                }

                if (!found
                    || snapshot.ChangedAtServerTime
                    > selected.ChangedAtServerTime
                    || (snapshot.ChangedAtServerTime.Equals(
                            selected.ChangedAtServerTime)
                        && snapshot.Revision > selected.Revision))
                {
                    selected = snapshot;
                    found = true;
                }
            }

            return found;
        }

        private void ApplySnapshot(
            NetworkEventLifecycleSnapshot snapshot,
            bool isSameRevisionReactivation)
        {
            terminalHoldActive = false;
            var phaseElapsedSeconds = GetPhaseElapsedSeconds(
                snapshot.ChangedAtServerTime);
            switch (snapshot.State)
            {
                case EventState.Ready:
                    presentationView.ShowTelegraph(
                        phaseElapsedSeconds,
                        !isSameRevisionReactivation);
                    break;
                case EventState.InProgress:
                    presentationView.ShowActive(phaseElapsedSeconds);
                    break;
                case EventState.Resolve:
                    ApplyTerminalPhase(
                        phaseElapsedSeconds,
                        isSameRevisionReactivation,
                        false);
                    break;
                case EventState.Fail:
                    ApplyTerminalPhase(
                        phaseElapsedSeconds,
                        isSameRevisionReactivation,
                        true);
                    break;
                default:
                    presentationView.Cleanup();
                    break;
            }
        }

        private void ApplyTerminalPhase(
            float phaseElapsedSeconds,
            bool isSameRevisionReactivation,
            bool failed)
        {
            var remainingVisualSeconds = Mathf.Max(
                0f,
                terminalVisibleSeconds - phaseElapsedSeconds);
            var allowOneShotAudio =
                !isSameRevisionReactivation && remainingVisualSeconds > 0f;
            var remainingAudioSeconds = failed
                ? presentationView.ShowFailed(
                    phaseElapsedSeconds,
                    allowOneShotAudio)
                : presentationView.ShowResolved(
                    phaseElapsedSeconds,
                    allowOneShotAudio);
            BeginTerminalHold(
                remainingVisualSeconds,
                remainingAudioSeconds);
        }

        private void BeginTerminalHold(
            float remainingVisualSeconds,
            float remainingAudioSeconds)
        {
            var remaining = Mathf.Max(
                remainingVisualSeconds,
                remainingAudioSeconds);
            if (remaining <= 0f)
            {
                presentationView.Cleanup();
                terminalHoldActive = false;
                return;
            }

            terminalHoldActive = true;
            terminalClearAt = Time.unscaledTime + remaining;
        }

        private float GetPhaseElapsedSeconds(double changedAtServerTime)
        {
            var networkManager = boundCoordinator != null
                ? boundCoordinator.NetworkManager
                : null;
            if (networkManager == null)
            {
                return 0f;
            }

            var elapsed = networkManager.ServerTime.Time - changedAtServerTime;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed) || elapsed <= 0d)
            {
                return 0f;
            }

            return elapsed >= float.MaxValue
                ? float.MaxValue
                : (float)elapsed;
        }
    }
}
