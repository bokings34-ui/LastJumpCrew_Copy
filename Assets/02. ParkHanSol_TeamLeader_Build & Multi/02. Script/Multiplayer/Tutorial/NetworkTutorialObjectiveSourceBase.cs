using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public abstract class NetworkTutorialObjectiveSourceBase :
        MonoBehaviour,
        ITutorialObjectiveSource
    {
        [SerializeField] private string objectiveId = "tutorial_objective";

        private bool objectiveActive;
        private bool isComplete;

        public string ObjectiveId => objectiveId;
        public bool IsComplete => isComplete;
        protected bool CanComplete => objectiveActive && !isComplete;

        public event Action<ITutorialObjectiveSource> Completed;

        public virtual void SetObjectiveActive(bool active)
        {
            objectiveActive = active && !isComplete;
        }

        protected void CompleteObjective()
        {
            if (!CanComplete)
            {
                return;
            }

            isComplete = true;
            objectiveActive = false;
            Completed?.Invoke(this);
        }
    }
}
