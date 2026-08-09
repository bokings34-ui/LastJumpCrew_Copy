using LastJumpCrew.ParkHanSol.Interaction;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialHeldItemDropObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private string expectedItemId;
        [SerializeField] private TempPlayerItemHolder itemHolder;
        [SerializeField] private NetworkTutorialActionSource actionSource;

        private bool expectedItemWasHeld;

        private void OnEnable()
        {
            BindActionSource();
        }

        private void OnDisable()
        {
            if (actionSource != null)
            {
                actionSource.ActionSucceeded -= HandleActionSucceeded;
            }
        }

        public override void SetObjectiveActive(bool active)
        {
            base.SetObjectiveActive(active);
            BindActionSource();
            expectedItemWasHeld = active
                && itemHolder != null
                && itemHolder.IsHoldingItem(expectedItemId);
        }

        private void BindActionSource()
        {
            if (actionSource == null && itemHolder != null)
            {
                actionSource = itemHolder.GetComponent<NetworkTutorialActionSource>();
            }

            if (actionSource != null)
            {
                actionSource.ActionSucceeded -= HandleActionSucceeded;
                actionSource.ActionSucceeded += HandleActionSucceeded;
            }
        }

        private void Update()
        {
            if (CanComplete
                && itemHolder != null
                && itemHolder.IsHoldingItem(expectedItemId))
            {
                expectedItemWasHeld = true;
            }
        }

        private void HandleActionSucceeded(TutorialActionKind actionKind)
        {
            if (actionKind != TutorialActionKind.Drop
                || !CanComplete
                || !expectedItemWasHeld
                || itemHolder == null
                || itemHolder.IsHoldingItem(expectedItemId))
            {
                return;
            }

            CompleteObjective();
        }
    }
}
