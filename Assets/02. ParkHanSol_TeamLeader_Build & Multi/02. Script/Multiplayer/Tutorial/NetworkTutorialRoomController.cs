using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialRoomController : MonoBehaviour
    {
        private const int KeySlotsPerCommand = 4;

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
        [SerializeField] private string roomTitle;
        [SerializeField] private string[] objectiveInstructions =
            Array.Empty<string>();
        [SerializeField] private GameObject instructionRoot;
        [SerializeField] private Image instructionImage;
        [SerializeField] private Sprite instructionSprite;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private Image[] instructionKeyBadges =
            Array.Empty<Image>();
        [SerializeField] private TMP_Text[] instructionKeyTexts =
            Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[] instructionCommandTexts =
            Array.Empty<TMP_Text>();
        [SerializeField] private Slider instructionProgressSlider;
        [SerializeField] private TMP_Text targetIndicatorText;
        [SerializeField] private Camera guidanceCamera;
        [SerializeField] private GameObject objectiveGuidanceRoot;
        [SerializeField] private GameObject[] objectiveMarkerRoots =
            Array.Empty<GameObject>();

        [Header("Briefing")]
        [SerializeField] private NetworkTutorialBriefingPresenter
            briefingPresenter;
        [SerializeField] private TutorialBriefingPage[] briefingPages =
            Array.Empty<TutorialBriefingPage>();

        [Header("Door - Animator Preferred")]
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private string doorOpenTrigger = "Open";

        [Header("Door - Transform Fallback")]
        [SerializeField] private Transform doorTransform;
        [SerializeField] private Transform doorSecondaryTransform;
        [SerializeField] private Collider doorCollider;
        [SerializeField] private Vector3 doorOpenLocalPosition;
        [SerializeField] private Vector3 doorSecondaryOpenLocalPosition;
        [SerializeField] private Vector3 doorOpenLocalEulerAngles;
        [SerializeField, Min(0.01f)] private float doorOpenDuration = 0.5f;

        private Vector3 doorClosedLocalPosition;
        private Quaternion doorClosedLocalRotation;
        private Vector3 doorSecondaryClosedLocalPosition;
        private Coroutine doorRoutine;
        private bool isComplete;
        private readonly List<ITutorialObjectiveSource> objectiveSources = new();
        private readonly HashSet<string> completedObjectiveIds = new();
        private bool objectiveSourcesResolved;
        private bool isCurrent;
        private bool briefingCompleted;
        private Tween activeMarkerTween;
        private Transform activeMarkerTransform;
        private Vector3 activeMarkerBaseScale;

        private void LateUpdate()
        {
            UpdateObjectiveGuidance();
        }

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
            if (!briefingCompleted
                || HasObjectiveSources
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
            if (!TryValidateInstructionContract(out var reason))
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_ROOM_DISABLED " +
                    $"room={roomId} reason={reason}",
                    this);
                enabled = false;
                return;
            }

            if (doorTransform != null)
            {
                doorClosedLocalPosition = doorTransform.localPosition;
                doorClosedLocalRotation = doorTransform.localRotation;
            }

            if (doorSecondaryTransform != null)
            {
                doorSecondaryClosedLocalPosition =
                    doorSecondaryTransform.localPosition;
            }

            briefingPresenter.Completed += HandleBriefingCompleted;
            ConfigureInstructionUi(0);
        }

        private void OnDestroy()
        {
            foreach (var source in objectiveSources)
            {
                source.Completed -= HandleObjectiveCompleted;
            }

            if (briefingPresenter != null)
            {
                briefingPresenter.Completed -= HandleBriefingCompleted;
            }
        }

        private void OnDisable()
        {
            StopMarkerPulseAndRestore();
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
            var objectivesAvailable = current && briefingCompleted;
            RefreshObjectiveSourceActivation();

            if (manageRoomRootActiveState && roomRoot != null)
            {
                roomRoot.SetActive(current || isComplete);
            }

            if (!current)
            {
                briefingPresenter.Dismiss(this);
            }
            else if (!briefingCompleted
                && !briefingPresenter.TryPresent(
                    this,
                    briefingPages,
                    out var briefingReason))
            {
                Debug.LogError(
                    "PHS_NETWORK_TUTORIAL_ROOM_DISABLED " +
                    $"room={roomId} reason=briefing_present_failed:" +
                    briefingReason,
                    this);
                enabled = false;
                return;
            }

            SetObjectivePresentationVisible(objectivesAvailable);
            RefreshObjectiveMarkerVisibility();

            if (objectivesAvailable)
            {
                ConfigureInstructionUi(completedCount);
            }
        }

        private void SetObjectivePresentationVisible(bool visible)
        {
            if (instructionRoot != null)
            {
                instructionRoot.SetActive(visible);
            }
            else if (instructionImage != null)
            {
                instructionImage.gameObject.SetActive(visible);
            }

            if (instructionRoot == null && instructionText != null)
            {
                instructionText.gameObject.SetActive(visible);
            }

            if (instructionRoot == null && instructionProgressSlider != null)
            {
                instructionProgressSlider.gameObject.SetActive(visible);
            }

            if (objectiveGuidanceRoot != null)
            {
                objectiveGuidanceRoot.SetActive(visible);
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

        public bool TryValidateProgressContract(out string reason)
        {
            ResolveObjectiveSources();
            if (!TryValidateInstructionContract(out reason))
            {
                return false;
            }

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

        private bool TryValidateInstructionContract(out string reason)
        {
            if (string.IsNullOrWhiteSpace(roomTitle))
            {
                reason = "room_title_missing";
                return false;
            }

            if (instructionRoot == null
                || instructionText == null
                || instructionProgressSlider == null
                || targetIndicatorText == null
                || guidanceCamera == null)
            {
                reason = "instruction_ui_reference_missing";
                return false;
            }

            if (instructionKeyBadges == null
                || instructionKeyTexts == null
                || instructionCommandTexts == null
                || instructionCommandTexts.Length == 0
                || instructionKeyBadges.Length != instructionKeyTexts.Length
                || instructionKeyBadges.Length
                    != instructionCommandTexts.Length * KeySlotsPerCommand
                || instructionKeyBadges.Any(item => item == null)
                || instructionKeyTexts.Any(item => item == null)
                || instructionCommandTexts.Any(item => item == null))
            {
                reason = "instruction_command_ui_invalid";
                return false;
            }

            if (objectiveInstructions == null
                || objectiveInstructions.Length != RequiredStepCount)
            {
                reason = "objective_instruction_count_mismatch";
                return false;
            }

            if (objectiveInstructions.Any(string.IsNullOrWhiteSpace))
            {
                reason = "objective_instruction_blank";
                return false;
            }

            if (briefingPresenter == null)
            {
                reason = "briefing_presenter_missing";
                return false;
            }

            if (!briefingPresenter.TryValidatePages(
                briefingPages,
                out var briefingReason))
            {
                reason = $"briefing_contract_invalid:{briefingReason}";
                return false;
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
            RefreshObjectiveSourceActivation();
            RefreshObjectiveMarkerVisibility();

            ConfigureInstructionUi(completedCount);
            ObjectiveProgressChanged?.Invoke(this, completedCount);
        }

        private void HandleBriefingCompleted(
            NetworkTutorialRoomController owner)
        {
            if (owner != this || !isCurrent || briefingCompleted)
            {
                return;
            }

            briefingCompleted = true;
            RefreshObjectiveSourceActivation();

            SetObjectivePresentationVisible(true);
            RefreshObjectiveMarkerVisibility();
            ConfigureInstructionUi(completedObjectiveIds.Count);
        }

        private void RefreshObjectiveMarkerVisibility()
        {
            StopMarkerPulseAndRestore();
            foreach (var marker in objectiveMarkerRoots)
            {
                if (marker != null)
                {
                    marker.SetActive(false);
                }
            }

            if (!isCurrent || !briefingCompleted)
            {
                return;
            }

            var index = GetNextObjectiveIndex();
            if (index >= 0
                && index < objectiveMarkerRoots.Length
                && objectiveMarkerRoots[index] != null)
            {
                objectiveMarkerRoots[index].SetActive(true);
                StartMarkerPulse(objectiveMarkerRoots[index].transform);
            }

            UpdateObjectiveGuidance();
        }

        private void UpdateObjectiveGuidance()
        {
            var index = isCurrent && briefingCompleted
                ? GetNextObjectiveIndex()
                : -1;
            var marker = index >= 0 && index < objectiveMarkerRoots.Length
                ? objectiveMarkerRoots[index]
                : null;
            if (targetIndicatorText != null)
            {
                targetIndicatorText.transform.parent.gameObject.SetActive(
                    marker != null);
            }

            if (marker == null || guidanceCamera == null)
            {
                return;
            }

            foreach (var markerCanvas in marker.GetComponentsInChildren<Canvas>(true))
            {
                if (markerCanvas.name.StartsWith(
                        "FloorObjectiveMarker",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                markerCanvas.transform.rotation = Quaternion.LookRotation(
                    markerCanvas.transform.position
                    - guidanceCamera.transform.position,
                    guidanceCamera.transform.up);
            }

            if (targetIndicatorText == null)
            {
                return;
            }

            var viewport = guidanceCamera.WorldToViewportPoint(
                marker.transform.position);
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            var arrow = viewport.x < 0.08f
                ? "<  "
                : viewport.x > 0.92f
                    ? ">  "
                    : viewport.y < 0.08f
                        ? "V  "
                        : viewport.y > 0.92f
                            ? "^  "
                            : string.Empty;
            var distance = Vector3.Distance(
                guidanceCamera.transform.position,
                marker.transform.position);
            targetIndicatorText.text =
                $"{arrow}목표 {index + 1}  {distance:0}m";
        }

        private void RefreshObjectiveSourceActivation()
        {
            var nextObjectiveIndex = isCurrent
                && briefingCompleted
                && HasObjectiveSources
                ? GetNextObjectiveIndex()
                : -1;
            for (var index = 0; index < objectiveSources.Count; index++)
            {
                objectiveSources[index].SetObjectiveActive(
                    index == nextObjectiveIndex);
            }
        }

        private void StartMarkerPulse(Transform markerTransform)
        {
            if (markerTransform == null)
            {
                return;
            }

            activeMarkerTransform = markerTransform;
            activeMarkerBaseScale = markerTransform.localScale;
            activeMarkerTween = markerTransform
                .DOScale(activeMarkerBaseScale * 1.08f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true)
                .OnComplete(HandleMarkerPulseCompleted);
        }

        private void HandleMarkerPulseCompleted()
        {
            activeMarkerTween = null;
            if (activeMarkerTransform != null)
            {
                activeMarkerTransform.localScale = activeMarkerBaseScale;
                activeMarkerTransform = null;
            }
        }

        private void StopMarkerPulseAndRestore()
        {
            activeMarkerTween?.Kill();
            activeMarkerTween = null;
            if (activeMarkerTransform != null)
            {
                activeMarkerTransform.localScale = activeMarkerBaseScale;
                activeMarkerTransform = null;
            }
        }

        private int GetNextObjectiveIndex()
        {
            if (!HasObjectiveSources)
            {
                return -1;
            }

            var objectiveCount = Mathf.Min(
                objectiveSourceBehaviours.Length,
                objectiveSources.Count);
            for (var index = 0; index < objectiveCount; index++)
            {
                if (!objectiveSources[index].IsComplete)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ConfigureInstructionUi(int completedCount)
        {
            var clampedCount = Mathf.Clamp(
                completedCount,
                0,
                RequiredStepCount);
            var instructionIndex = HasObjectiveSources
                ? GetNextObjectiveIndex()
                : clampedCount;
            if (instructionImage != null)
            {
                instructionImage.sprite = instructionSprite;
                instructionImage.preserveAspect = true;
            }

            if (instructionText != null)
            {
                if (clampedCount >= RequiredStepCount)
                {
                    SetInstructionCommandRootsActive(false);
                    instructionText.text =
                        $"완료  ·  <size=18>{roomTitle}  " +
                        $"{RequiredStepCount}/{RequiredStepCount}</size>";
                }
                else
                {
                    if (instructionIndex < 0
                        || instructionIndex >= objectiveInstructions.Length)
                    {
                        Debug.LogError(
                            "PHS_NETWORK_TUTORIAL_ROOM_DISABLED " +
                            $"room={roomId} reason=objective_instruction_index_invalid " +
                            $"completed={clampedCount} index={instructionIndex}",
                            this);
                        enabled = false;
                        return;
                    }

                    if (!TryConfigureInstructionCommands(
                            objectiveInstructions[instructionIndex],
                            out var commandReason))
                    {
                        Debug.LogError(
                            "PHS_NETWORK_TUTORIAL_ROOM_DISABLED " +
                            $"room={roomId} reason={commandReason}",
                            this);
                        enabled = false;
                        return;
                    }

                    instructionText.text =
                        $"<size=18>{roomTitle}  " +
                        $"{clampedCount}/{RequiredStepCount}</size>";
                }
            }

            if (instructionProgressSlider != null)
            {
                instructionProgressSlider.minValue = 0f;
                instructionProgressSlider.maxValue = RequiredStepCount;
                instructionProgressSlider.wholeNumbers = true;
                instructionProgressSlider.value = clampedCount;
            }
        }

        private bool TryConfigureInstructionCommands(
            string instruction,
            out string reason)
        {
            SetInstructionCommandRootsActive(false);
            var segments = instruction.Split(
                new[] { " · " },
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0
                || segments.Length > instructionCommandTexts.Length)
            {
                reason = "instruction_command_count_invalid";
                return false;
            }

            for (var index = 0; index < segments.Length; index++)
            {
                var segment = segments[index].Trim();
                var closingBracket = segment.IndexOf(']');
                if (!segment.StartsWith("[", StringComparison.Ordinal)
                    || closingBracket <= 1
                    || closingBracket >= segment.Length - 1)
                {
                    reason = $"instruction_command_format_invalid:{segment}";
                    return false;
                }

                var key = segment.Substring(1, closingBracket - 1).Trim();
                var command = segment.Substring(closingBracket + 1).Trim();
                if (string.IsNullOrWhiteSpace(key)
                    || string.IsNullOrWhiteSpace(command))
                {
                    reason = $"instruction_command_blank:{segment}";
                    return false;
                }

                var keyCount = string.Equals(
                    key,
                    "WASD",
                    StringComparison.OrdinalIgnoreCase)
                    ? KeySlotsPerCommand
                    : 1;
                var keyLabel = key switch
                {
                    "LMB" => "M1",
                    "RMB" => "M2",
                    "SHIFT" => "SH",
                    "CTRL" => "CT",
                    "SPACE" => "SPC",
                    _ => key
                };
                var keyOffset = index * KeySlotsPerCommand;
                for (var keyIndex = 0;
                     keyIndex < KeySlotsPerCommand;
                     keyIndex++)
                {
                    var isVisible = keyIndex < keyCount;
                    instructionKeyBadges[keyOffset + keyIndex].gameObject
                        .SetActive(isVisible);
                    if (isVisible)
                    {
                        instructionKeyTexts[keyOffset + keyIndex].text =
                            keyCount == 1
                                ? keyLabel
                                : key[keyIndex].ToString();
                    }
                }

                instructionCommandTexts[index].text = command;
                instructionCommandTexts[index].textWrappingMode =
                    TextWrappingModes.NoWrap;
                var commandWidth = Mathf.Clamp(
                    instructionCommandTexts[index]
                        .GetPreferredValues(command).x + 8f,
                    90f,
                    segments.Length == 1 ? 280f : 220f);
                instructionCommandTexts[index]
                    .GetComponent<LayoutElement>().preferredWidth =
                    commandWidth;
                var commandRoot = instructionCommandTexts[index]
                    .transform.parent.gameObject;
                commandRoot.GetComponent<LayoutElement>().preferredWidth =
                    keyCount * 70f + commandWidth + 12f;
                commandRoot.SetActive(true);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(
                instructionCommandTexts[0].transform.parent.parent
                    as RectTransform);

            reason = null;
            return true;
        }

        private void SetInstructionCommandRootsActive(bool active)
        {
            foreach (var commandText in instructionCommandTexts)
            {
                if (commandText != null)
                {
                    commandText.transform.parent.gameObject.SetActive(active);
                }
            }
        }

        private void OpenDoor()
        {
            if (doorAnimator != null
                && !string.IsNullOrWhiteSpace(doorOpenTrigger))
            {
                doorAnimator.SetTrigger(doorOpenTrigger);
                if (doorCollider != null)
                {
                    doorRoutine = StartCoroutine(
                        DisableDoorColliderAfterDelay());
                }

                return;
            }

            if (doorTransform == null)
            {
                if (doorCollider != null)
                {
                    doorCollider.enabled = false;
                }

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
                if (doorSecondaryTransform != null)
                {
                    doorSecondaryTransform.localPosition =
                        Vector3.LerpUnclamped(
                            doorSecondaryClosedLocalPosition,
                            doorSecondaryOpenLocalPosition,
                            t);
                }
                yield return null;
            }

            doorTransform.localPosition = doorOpenLocalPosition;
            doorTransform.localRotation = targetRotation;
            if (doorSecondaryTransform != null)
            {
                doorSecondaryTransform.localPosition =
                    doorSecondaryOpenLocalPosition;
            }
            if (doorCollider != null)
            {
                doorCollider.enabled = false;
            }

            doorRoutine = null;
        }

        private IEnumerator DisableDoorColliderAfterDelay()
        {
            yield return new WaitForSeconds(doorOpenDuration);
            if (doorCollider != null)
            {
                doorCollider.enabled = false;
            }

            doorRoutine = null;
        }
    }
}
