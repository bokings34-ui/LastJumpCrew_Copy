using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialRoomController : MonoBehaviour
    {
        [Header("Room Contract")]
        [SerializeField] private string roomId = "tutorial_room";
        [SerializeField] private TutorialActionKind requiredAction;
        [SerializeField, Min(1)] private int requiredSuccessCount = 2;
        [SerializeField] private TutorialActionKind[] requiredActionSequence =
            Array.Empty<TutorialActionKind>();
        [SerializeField] private MonoBehaviour[] objectiveSourceBehaviours =
            Array.Empty<MonoBehaviour>();
        [SerializeField] private GameObject roomRoot;
        [SerializeField] private bool manageRoomRootActiveState;

        [Header("Instruction UI")]
        [SerializeField] private GameObject instructionRoot;
        [SerializeField] private Image instructionImage;
        [SerializeField] private Sprite instructionSprite;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private string instruction = "COMPLETE THE ACTION TWICE";
        [SerializeField] private Slider instructionProgressSlider;
        [SerializeField] private GameObject objectiveGuidanceRoot;
        [SerializeField] private GameObject[] objectiveMarkerRoots =
            Array.Empty<GameObject>();

        [Header("Door - Animator Preferred")]
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private string doorOpenTrigger = "Open";

        [Header("Door - Transform Fallback")]
        [SerializeField] private Transform doorTransform;
        [SerializeField] private Collider doorCollider;
        [SerializeField] private Vector3 doorOpenLocalPosition;
        [SerializeField] private Vector3 doorOpenLocalEulerAngles;
        [SerializeField, Min(0.01f)] private float doorOpenDuration = 0.5f;

        private Vector3 doorClosedLocalPosition;
        private Quaternion doorClosedLocalRotation;
        private Coroutine doorRoutine;
        private bool isComplete;
        private readonly List<ITutorialObjectiveSource> objectiveSources = new();
        private readonly HashSet<string> completedObjectiveIds = new();
        private bool objectiveSourcesResolved;
        private bool isCurrent;

        public string RoomId => roomId;
        public TutorialActionKind RequiredAction => requiredAction;
        public int RequiredSuccessCount => RequiredStepCount;
        public int RequiredStepCount => HasObjectiveSources
            ? objectiveSourceBehaviours.Length
            : HasActionSequence
                ? requiredActionSequence.Length
                : Mathf.Max(1, requiredSuccessCount);
        public bool IsComplete => isComplete;
        public bool HasObjectiveSources =>
            objectiveSourceBehaviours != null
            && objectiveSourceBehaviours.Length > 0;

        public event Action<NetworkTutorialRoomController, int>
            ObjectiveProgressChanged;

        private bool HasActionSequence =>
            requiredActionSequence != null
            && requiredActionSequence.Length > 0;

        public TutorialActionKind GetExpectedAction(int completedCount)
        {
            if (!HasActionSequence)
            {
                return requiredAction;
            }

            var index = Mathf.Clamp(
                completedCount,
                0,
                requiredActionSequence.Length - 1);
            return requiredActionSequence[index];
        }

        public bool TryRegisterAction(
            TutorialActionKind actionKind,
            int completedCount,
            out int nextCompletedCount)
        {
            nextCompletedCount = Mathf.Clamp(
                completedCount,
                0,
                RequiredStepCount);
            if (HasObjectiveSources
                || nextCompletedCount >= RequiredStepCount
                || actionKind != GetExpectedAction(nextCompletedCount))
            {
                return false;
            }

            nextCompletedCount++;
            return true;
        }

        private void Awake()
        {
            ResolveObjectiveSources();
            if (doorTransform != null)
            {
                doorClosedLocalPosition = doorTransform.localPosition;
                doorClosedLocalRotation = doorTransform.localRotation;
            }

            ConfigureInstructionUi(0);
        }

        private void OnDestroy()
        {
            foreach (var source in objectiveSources)
            {
                source.Completed -= HandleObjectiveCompleted;
            }
        }

        private void OnDisable()
        {
            if (doorRoutine != null)
            {
                StopCoroutine(doorRoutine);
                doorRoutine = null;
            }
        }

        public void SetCurrent(bool current, int completedCount)
        {
            isCurrent = current;
            ResolveObjectiveSources();
            foreach (var source in objectiveSources)
            {
                source.SetObjectiveActive(current);
            }

            if (manageRoomRootActiveState && roomRoot != null)
            {
                roomRoot.SetActive(current || isComplete);
            }

            if (instructionRoot != null)
            {
                instructionRoot.SetActive(current);
            }
            else if (instructionImage != null)
            {
                instructionImage.gameObject.SetActive(current);
            }

            if (instructionRoot == null && instructionText != null)
            {
                instructionText.gameObject.SetActive(current);
            }

            if (instructionRoot == null && instructionProgressSlider != null)
            {
                instructionProgressSlider.gameObject.SetActive(current);
            }

            if (objectiveGuidanceRoot != null)
            {
                objectiveGuidanceRoot.SetActive(current);
            }

            RefreshObjectiveMarkerVisibility();

            if (current)
            {
                ConfigureInstructionUi(completedCount);
            }
        }

        public void RefreshProgress(int completedCount)
        {
            ConfigureInstructionUi(completedCount);
        }

        public void CompleteRoom()
        {
            if (isComplete)
            {
                return;
            }

            isComplete = true;
            ConfigureInstructionUi(RequiredSuccessCount);
            OpenDoor();
        }

        public bool TryValidateObjectiveSources(out string reason)
        {
            ResolveObjectiveSources();
            if (!HasObjectiveSources)
            {
                reason = null;
                return true;
            }

            if (objectiveSources.Count != objectiveSourceBehaviours.Length)
            {
                reason = "objective_interface_missing";
                return false;
            }

            if (objectiveMarkerRoots == null
                || objectiveMarkerRoots.Length != objectiveSources.Count
                || objectiveMarkerRoots.Any(marker => marker == null))
            {
                reason = "objective_marker_contract_invalid";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in objectiveSources)
            {
                if (string.IsNullOrWhiteSpace(source.ObjectiveId)
                    || !ids.Add(source.ObjectiveId))
                {
                    reason = "objective_id_invalid_or_duplicate";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private void ResolveObjectiveSources()
        {
            if (objectiveSourcesResolved)
            {
                return;
            }

            objectiveSourcesResolved = true;
            objectiveSources.Clear();
            if (!HasObjectiveSources)
            {
                return;
            }

            foreach (var behaviour in objectiveSourceBehaviours)
            {
                if (behaviour is not ITutorialObjectiveSource source)
                {
                    continue;
                }

                objectiveSources.Add(source);
                source.Completed += HandleObjectiveCompleted;
                source.SetObjectiveActive(false);
            }
        }

        private void HandleObjectiveCompleted(ITutorialObjectiveSource source)
        {
            if (source == null
                || !completedObjectiveIds.Add(source.ObjectiveId))
            {
                return;
            }

            var completedCount = completedObjectiveIds.Count;
            RefreshObjectiveMarkerVisibility();

            ConfigureInstructionUi(completedCount);
            ObjectiveProgressChanged?.Invoke(this, completedCount);
        }

        private void RefreshObjectiveMarkerVisibility()
        {
            foreach (var marker in objectiveMarkerRoots)
            {
                if (marker != null)
                {
                    marker.SetActive(false);
                }
            }

            if (!isCurrent)
            {
                return;
            }

            var markerCount = Mathf.Min(
                objectiveMarkerRoots.Length,
                objectiveSources.Count);
            for (var index = 0; index < markerCount; index++)
            {
                if (objectiveSources[index].IsComplete
                    || objectiveMarkerRoots[index] == null)
                {
                    continue;
                }

                objectiveMarkerRoots[index].SetActive(true);
                return;
            }
        }

        private void ConfigureInstructionUi(int completedCount)
        {
            var clampedCount = Mathf.Clamp(
                completedCount,
                0,
                RequiredStepCount);
            if (instructionImage != null)
            {
                instructionImage.sprite = instructionSprite;
                instructionImage.preserveAspect = true;
            }

            if (instructionText != null)
            {
                instructionText.text =
                    $"{instruction}  {clampedCount}/{RequiredStepCount}";
            }

            if (instructionProgressSlider != null)
            {
                instructionProgressSlider.minValue = 0f;
                instructionProgressSlider.maxValue = RequiredStepCount;
                instructionProgressSlider.wholeNumbers = true;
                instructionProgressSlider.value = clampedCount;
            }
        }

        private void OpenDoor()
        {
            if (doorCollider != null)
            {
                doorCollider.enabled = false;
            }

            if (doorAnimator != null
                && !string.IsNullOrWhiteSpace(doorOpenTrigger))
            {
                doorAnimator.SetTrigger(doorOpenTrigger);
                return;
            }

            if (doorTransform == null)
            {
                return;
            }

            if (doorRoutine != null)
            {
                StopCoroutine(doorRoutine);
            }

            doorRoutine = StartCoroutine(AnimateDoorOpen());
        }

        private IEnumerator AnimateDoorOpen()
        {
            var elapsed = 0f;
            var targetRotation = Quaternion.Euler(doorOpenLocalEulerAngles);
            while (elapsed < doorOpenDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / doorOpenDuration);
                t = t * t * (3f - 2f * t);
                doorTransform.localPosition = Vector3.LerpUnclamped(
                    doorClosedLocalPosition,
                    doorOpenLocalPosition,
                    t);
                doorTransform.localRotation = Quaternion.SlerpUnclamped(
                    doorClosedLocalRotation,
                    targetRotation,
                    t);
                yield return null;
            }

            doorTransform.localPosition = doorOpenLocalPosition;
            doorTransform.localRotation = targetRotation;
            doorRoutine = null;
        }
    }
}
