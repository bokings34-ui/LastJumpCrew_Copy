using LastJumpCrew.ParkHanSol.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    public enum NetworkTutorialRoom : byte
    {
        Movement,
        ZeroGravity,
        Grapple,
        ItemPickup,
        ItemDrop,
        ItemSwap,
        Interaction,
        Complete
    }

    [DisallowMultipleComponent]
    public sealed class NetworkTutorialRoomGate :
        MonoBehaviour,
        IInteractable
    {
        [SerializeField] private NetworkTutorialDirector tutorialDirector;
        [SerializeField] private NetworkTutorialRoom room;
        [SerializeField] private bool isTerminal;
        [SerializeField] private Transform doorPanel;
        [SerializeField] private Collider gateBarrier;
        [SerializeField] private Button nextRoomButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text buttonLabelText;
        [SerializeField] private Image buttonImage;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip doorOpenClip;
        [SerializeField] private AudioClip uiConfirmClip;
        [SerializeField] private Vector3 openOffset = new(0f, 5.5f, 0f);
        [SerializeField, Min(0.1f)] private float openSpeed = 4f;

        private static readonly Color LockedColor =
            new(0.28f, 0.1f, 0.01f, 1f);
        private static readonly Color ReadyColor =
            new(1f, 0.38f, 0.03f, 1f);
        private static readonly Color OpenColor =
            new(1f, 0.72f, 0.08f, 1f);
        private static readonly Color OrangeTextColor =
            new(1f, 0.42f, 0.05f, 1f);
        private static readonly Color YellowTextColor =
            new(1f, 0.78f, 0.18f, 1f);

        private Vector3 closedDoorLocalPosition;
        private bool isOpen;

        public string InteractionPrompt => isOpen
            ? "Room Gate Open"
            : isTerminal
                ? "Training Complete"
                : "NEXT ROOM";

        private void Awake()
        {
            if (!TryValidate(out var reason))
            {
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_GATE_SETUP_FAILED " +
                    $"reason={reason} gate={name}",
                    this);
                enabled = false;
                return;
            }

            closedDoorLocalPosition = doorPanel.localPosition;
            nextRoomButton.onClick.AddListener(RequestOpen);
            RefreshMonitor();
        }

        private void OnEnable()
        {
            if (tutorialDirector == null)
            {
                return;
            }

            tutorialDirector.ProgressChanged += RefreshMonitor;
            RefreshMonitor();
        }

        private void OnDisable()
        {
            if (tutorialDirector != null)
            {
                tutorialDirector.ProgressChanged -= RefreshMonitor;
            }
        }

        private void OnDestroy()
        {
            if (nextRoomButton != null)
            {
                nextRoomButton.onClick.RemoveListener(RequestOpen);
            }
        }

        private void Update()
        {
            if (!isOpen)
            {
                return;
            }

            doorPanel.localPosition = Vector3.MoveTowards(
                doorPanel.localPosition,
                closedDoorLocalPosition + openOffset,
                openSpeed * Time.unscaledDeltaTime);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return enabled && !isTerminal && !isOpen;
        }

        public void Interact(IItemHolder itemHolder)
        {
            RequestOpen();
        }

        public bool TryValidate(out string reason)
        {
            if (tutorialDirector == null)
            {
                reason = "director_missing";
                return false;
            }

            if (isTerminal
                && room != NetworkTutorialRoom.Complete)
            {
                reason = "terminal_room_mismatch";
                return false;
            }

            if (!isTerminal
                && room == NetworkTutorialRoom.Complete)
            {
                reason = "complete_room_requires_terminal_gate";
                return false;
            }

            if (doorPanel == null)
            {
                reason = "door_panel_missing";
                return false;
            }

            if (gateBarrier == null)
            {
                reason = "gate_barrier_missing";
                return false;
            }

            if (nextRoomButton == null
                || titleText == null
                || descriptionText == null
                || statusText == null
                || buttonLabelText == null
                || buttonImage == null)
            {
                reason = "monitor_reference_missing";
                return false;
            }

            if (audioSource == null)
            {
                reason = "audio_source_missing";
                return false;
            }

            if (doorOpenClip == null)
            {
                reason = "door_open_clip_missing";
                return false;
            }

            if (uiConfirmClip == null)
            {
                reason = "ui_confirm_clip_missing";
                return false;
            }

            if (openSpeed <= 0f
                || float.IsNaN(openSpeed)
                || float.IsInfinity(openSpeed))
            {
                reason = $"open_speed_invalid:{openSpeed}";
                return false;
            }

            reason = null;
            return true;
        }

        private void RequestOpen()
        {
            if (isOpen)
            {
                return;
            }

            audioSource.PlayOneShot(uiConfirmClip);

            if (!tutorialDirector.IsRoomComplete(room))
            {
                statusText.text =
                    "ERROR: COMPLETE THIS ROOM BEFORE PROCEEDING";
                statusText.color = OrangeTextColor;
                Debug.LogError(
                    $"PHS_NETWORK_TUTORIAL_GATE_OPEN_FAILED " +
                    $"reason=room_incomplete room={room} gate={name}",
                    this);
                return;
            }

            isOpen = true;
            gateBarrier.enabled = false;
            audioSource.PlayOneShot(doorOpenClip);
            RefreshMonitor();
            Debug.Log(
                $"PHS_NETWORK_TUTORIAL_GATE_OPENED room={room} gate={name}",
                this);
        }

        private void RefreshMonitor()
        {
            if (tutorialDirector == null
                || titleText == null
                || descriptionText == null
                || statusText == null
                || buttonLabelText == null
                || buttonImage == null)
            {
                return;
            }

            tutorialDirector.GetRoomDisplay(
                room,
                out var title,
                out var description,
                out var status);
            titleText.text = title;
            titleText.color = YellowTextColor;
            descriptionText.text = description;
            descriptionText.color = OrangeTextColor;
            buttonLabelText.color = Color.black;

            if (isTerminal)
            {
                var isComplete = tutorialDirector.IsRoomComplete(room);
                statusText.text = status;
                statusText.color = isComplete
                    ? YellowTextColor
                    : OrangeTextColor;
                buttonLabelText.text = isComplete
                    ? "TRAINING COMPLETE"
                    : "FINAL ROOM LOCKED";
                buttonImage.color = isComplete ? OpenColor : LockedColor;
                nextRoomButton.interactable = false;
                return;
            }

            if (isOpen)
            {
                statusText.text = "STATUS: OPEN · PROCEED";
                statusText.color = YellowTextColor;
                buttonLabelText.text = "DOOR OPEN";
                buttonImage.color = OpenColor;
                nextRoomButton.interactable = false;
                return;
            }

            var isReady = tutorialDirector.IsRoomComplete(room);
            statusText.text = status;
            statusText.color = isReady
                ? YellowTextColor
                : OrangeTextColor;
            buttonLabelText.text = isReady
                ? "NEXT ROOM"
                : "LOCKED · CLEAR ROOM";
            buttonImage.color = isReady ? ReadyColor : LockedColor;

            // Keep the physical button clickable while locked so it can show
            // the explicit room_incomplete error required by the tutorial.
            nextRoomButton.interactable = true;
        }
    }
}
