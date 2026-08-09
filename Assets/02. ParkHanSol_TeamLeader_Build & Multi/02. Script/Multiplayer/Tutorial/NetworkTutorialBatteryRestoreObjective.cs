using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialBatteryRestoreObjective :
        NetworkTutorialObjectiveSourceBase
    {
        [SerializeField] private TutorialPowerBatterySocket batterySocket;
        [SerializeField] private GameObject powerDeviceRoot;
        [SerializeField] private Light statusLight;
        public override void SetObjectiveActive(bool active)
        {
            base.SetObjectiveActive(active);
            if (powerDeviceRoot != null)
            {
                powerDeviceRoot.SetActive(active || IsComplete);
            }

            if (statusLight != null)
            {
                statusLight.color = IsComplete ? Color.cyan : Color.red;
            }
        }

        private void Update()
        {
            if (!CanComplete || batterySocket == null || !batterySocket.IsRestored)
            {
                return;
            }

            if (statusLight != null)
            {
                statusLight.color = Color.cyan;
            }

            CompleteObjective();
        }
    }
}
