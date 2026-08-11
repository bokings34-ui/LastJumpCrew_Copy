using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using UnityEngine;

namespace SM
{
    [DisallowMultipleComponent]
    public sealed class PowerOffPresentationBridge : MonoBehaviour
    {
        [SerializeField] private NetworkEventCoordinator eventCoordinator;
        [SerializeField] private GameObject powerFailurePresentationPrefab;
        [SerializeField] private Transform[] presentationRoots;

        private readonly List<GameObject> presentationInstances = new();
        private bool setupValid;
        private bool presentationActive;

        private void Awake()
        {
            setupValid = ValidateSetup();
            if (!setupValid)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (!setupValid)
            {
                return;
            }

            SetPresentationActive(eventCoordinator.IsEventActive(EventId.PowerOff));
        }

        private void OnDisable()
        {
            SetPresentationActive(false);
        }

        private bool ValidateSetup()
        {
            if (eventCoordinator == null)
            {
                Debug.LogError("PHS_TEAM_POWER_OFF_PRESENTATION_DISABLED reason=event_coordinator_missing", this);
                return false;
            }

            if (powerFailurePresentationPrefab == null)
            {
                Debug.LogError("PHS_TEAM_POWER_OFF_PRESENTATION_DISABLED reason=presentation_prefab_missing", this);
                return false;
            }

            if (presentationRoots == null || presentationRoots.Length == 0)
            {
                Debug.LogError("PHS_TEAM_POWER_OFF_PRESENTATION_DISABLED reason=presentation_roots_missing", this);
                return false;
            }

            for (var index = 0; index < presentationRoots.Length; index++)
            {
                if (presentationRoots[index] == null)
                {
                    Debug.LogError(
                        $"PHS_TEAM_POWER_OFF_PRESENTATION_DISABLED reason=presentation_root_missing index={index}",
                        this);
                    return false;
                }
            }

            return true;
        }

        private void SetPresentationActive(bool active)
        {
            if (presentationActive == active)
            {
                return;
            }

            presentationActive = active;
            if (active)
            {
                for (var index = 0; index < presentationRoots.Length; index++)
                {
                    var instance = Instantiate(
                        powerFailurePresentationPrefab,
                        presentationRoots[index]);
                    instance.name = $"Team_PowerOff_Presentation_{index}";
                    presentationInstances.Add(instance);
                }

                Debug.Log(
                    $"PHS_TEAM_POWER_OFF_PRESENTATION_SPAWNED roots={presentationInstances.Count}",
                    this);
                return;
            }

            for (var index = 0; index < presentationInstances.Count; index++)
            {
                if (presentationInstances[index] != null)
                {
                    Destroy(presentationInstances[index]);
                }
            }

            presentationInstances.Clear();
            Debug.Log("PHS_TEAM_POWER_OFF_PRESENTATION_CLEARED", this);
        }
    }
}
