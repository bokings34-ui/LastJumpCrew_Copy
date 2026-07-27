using System;
using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer.Input;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class NetworkTutorialActionSource :
        MonoBehaviour,
        ITutorialActionSource
    {
        [Header("Observed Player Components")]
        [SerializeField] private NetworkPlayerController playerController;
        [SerializeField] private NetworkPlayerGrappleController grappleController;
        [SerializeField] private TempPlayerItemHolder itemHolder;
        [SerializeField] private PlayerControlInput playerControlInput;

        [Header("Success Filtering")]
        [SerializeField, Min(0.1f)] private float moveSuccessDistance = 1.5f;
        [SerializeField, Min(0.02f)] private float thrusterMinimumActiveSeconds = 0.12f;
        [SerializeField, Min(0.1f)] private float throwHoldThreshold = 0.4f;
        [SerializeField, Min(0.05f)] private float dropConfirmationWindow = 0.35f;

        private bool initialized;
        private bool previousMoveInput;
        private bool moveCandidate;
        private bool moveConsumedUntilRelease;
        private Vector3 moveStartPosition;
        private bool previousThrusterActive;
        private bool thrusterCandidate;
        private bool thrusterConsumedUntilRelease;
        private float thrusterCandidateStartTime;
        private bool previousGrappleLatched;
        private bool previousHasItem;
        private string previousItemId;
        private bool dropButtonHeld;
        private float dropButtonPressedTime;
        private float pendingDropConfirmationUntil = float.NegativeInfinity;
        private int lastInteractionReportFrame = -1;

        public event Action<TutorialActionKind> ActionSucceeded;

        public bool IsConfigured =>
            playerController != null
            && grappleController != null
            && itemHolder != null
            && playerControlInput != null;

        public void Configure(
            NetworkPlayerController controller,
            NetworkPlayerGrappleController grapple,
            TempPlayerItemHolder holder,
            float requiredMoveDistance)
        {
            playerController = controller;
            grappleController = grapple;
            itemHolder = holder;
            playerControlInput = controller == null
                ? null
                : controller.GetComponent<PlayerControlInput>();
            moveSuccessDistance = Mathf.Max(0.1f, requiredMoveDistance);
            InitializeBaselines();
        }

        private void Awake()
        {
            ResolveSamePlayerReferences();
            InitializeBaselines();
        }

        private void OnEnable()
        {
            InitializeBaselines();
        }

        private void Update()
        {
            if (!initialized)
            {
                ResolveSamePlayerReferences();
                InitializeBaselines();
                if (!initialized)
                {
                    return;
                }
            }

            ObserveDropInput();
            ObserveMovement();
            ObserveJump();
            ObserveThruster();
            ObserveGrapple();
            ObserveHeldItem();
            ObserveToolUse();
        }

        public void ReportInteractionSuccess()
        {
            if (lastInteractionReportFrame == Time.frameCount)
            {
                return;
            }

            lastInteractionReportFrame = Time.frameCount;
            Publish(TutorialActionKind.Interaction);
        }

        private void ResolveSamePlayerReferences()
        {
            if (playerController == null)
            {
                playerController = GetComponent<NetworkPlayerController>();
            }

            if (grappleController == null)
            {
                grappleController = GetComponent<NetworkPlayerGrappleController>();
            }

            if (itemHolder == null)
            {
                itemHolder = GetComponent<TempPlayerItemHolder>();
            }

            if (playerControlInput == null)
            {
                playerControlInput = GetComponent<PlayerControlInput>();
            }
        }

        private void InitializeBaselines()
        {
            initialized = IsConfigured;
            if (!initialized)
            {
                return;
            }

            previousMoveInput = playerController.HasMoveInput;
            moveCandidate = false;
            moveConsumedUntilRelease = false;
            moveStartPosition = playerController.transform.position;
            previousThrusterActive = IsThrusterActive();
            thrusterCandidate = false;
            thrusterConsumedUntilRelease = false;
            previousGrappleLatched = grappleController.IsPullingPlayer;
            previousHasItem = itemHolder.HasItem;
            previousItemId = ResolveItemId();
            dropButtonHeld = false;
            pendingDropConfirmationUntil = float.NegativeInfinity;
        }

        private void ObserveMovement()
        {
            var hasMoveInput = playerController.HasMoveInput;
            if (!hasMoveInput)
            {
                moveCandidate = false;
                moveConsumedUntilRelease = false;
                previousMoveInput = false;
                return;
            }

            if (!previousMoveInput && !moveConsumedUntilRelease)
            {
                moveCandidate = true;
                moveStartPosition = playerController.transform.position;
            }

            if (moveCandidate && !moveConsumedUntilRelease)
            {
                var displacement = playerController.transform.position
                    - moveStartPosition;
                displacement.y = 0f;
                if (displacement.magnitude >= moveSuccessDistance)
                {
                    moveCandidate = false;
                    moveConsumedUntilRelease = true;
                    Publish(TutorialActionKind.Move);
                }
            }

            previousMoveInput = true;
        }

        private void ObserveThruster()
        {
            var active = IsThrusterActive();
            if (!active)
            {
                thrusterCandidate = false;
                thrusterConsumedUntilRelease = false;
                previousThrusterActive = false;
                return;
            }

            if (!previousThrusterActive && !thrusterConsumedUntilRelease)
            {
                thrusterCandidate = true;
                thrusterCandidateStartTime = Time.time;
            }

            if (thrusterCandidate
                && !thrusterConsumedUntilRelease
                && Time.time - thrusterCandidateStartTime
                    >= thrusterMinimumActiveSeconds)
            {
                thrusterCandidate = false;
                thrusterConsumedUntilRelease = true;
                Publish(TutorialActionKind.Thruster);
            }

            previousThrusterActive = true;
        }

        private void ObserveJump()
        {
            if (playerControlInput.JumpPressedThisFrame
                && playerController.IsGrounded)
            {
                Publish(TutorialActionKind.Jump);
            }
        }

        private bool IsThrusterActive()
        {
            return playerController.GravityMode
                    != NetworkPlayerGravityMode.ShipGravity
                && playerController.HasMoveInput;
        }

        private void ObserveGrapple()
        {
            var latched = grappleController.IsPullingPlayer;
            if (latched && !previousGrappleLatched)
            {
                Publish(TutorialActionKind.Grapple);
            }

            previousGrappleLatched = latched;
        }

        private void ObserveHeldItem()
        {
            var hasItem = itemHolder.HasItem;
            var itemId = ResolveItemId();

            if (!previousHasItem && hasItem)
            {
                Publish(TutorialActionKind.Pickup);
            }
            else if (previousHasItem
                     && hasItem
                     && !string.IsNullOrWhiteSpace(previousItemId)
                     && !string.IsNullOrWhiteSpace(itemId)
                     && !string.Equals(
                         previousItemId,
                         itemId,
                         StringComparison.Ordinal))
            {
                Publish(TutorialActionKind.Swap);
            }
            else if (previousHasItem && !hasItem)
            {
                if (Time.time <= pendingDropConfirmationUntil)
                {
                    Publish(TutorialActionKind.Drop);
                }

                pendingDropConfirmationUntil = float.NegativeInfinity;
            }

            previousHasItem = hasItem;
            previousItemId = itemId;
        }

        private void ObserveToolUse()
        {
            if (itemHolder.HasItem && playerControlInput.UsePressedThisFrame)
            {
                Publish(TutorialActionKind.Use);
            }
        }

        private void ObserveDropInput()
        {
            if (playerControlInput.DropPressedThisFrame)
            {
                dropButtonHeld = true;
                dropButtonPressedTime = Time.time;
            }

            if (!dropButtonHeld || !playerControlInput.DropReleasedThisFrame)
            {
                return;
            }

            dropButtonHeld = false;
            var heldSeconds = Time.time - dropButtonPressedTime;
            pendingDropConfirmationUntil = heldSeconds < throwHoldThreshold
                ? Time.time + dropConfirmationWindow
                : float.NegativeInfinity;
        }

        private string ResolveItemId()
        {
            return itemHolder.CurrentItemPrefabData == null
                ? null
                : itemHolder.CurrentItemPrefabData.ItemId;
        }

        private void Publish(TutorialActionKind actionKind)
        {
            ActionSucceeded?.Invoke(actionKind);
        }
    }
}
