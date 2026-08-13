using LastJumpCrew.ParkHanSol.Interaction;
using LastJumpCrew.ParkHanSol.Multiplayer;
using SM;
using UnityEngine;
using LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames.Runtime;
using CommonInteraction = LastJumpCrew.Common;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames
{
    [DisallowMultipleComponent]
    public sealed class PHSFinalMiniGameTerminal :
        MonoBehaviour,
        IEventMiniGameTerminal,
        CommonInteraction.IMiniGameTarget,
        IPHSFinalMiniGameSessionOwner,
        LastJumpCrew.ParkHanSol.Interaction.IInteractable,
        CommonInteraction.IInteractable
    {
        [Header("Event Mini Game")]
        [SerializeField] private EventId eventId = EventId.EmpAttack;
        [SerializeField] private PHSMiniGameType miniGameType = PHSMiniGameType.WireFix;

        [Header("Interaction")]
        [SerializeField] private string interactionPrompt = "고장난 장치 수리하기";
        [SerializeField] private Collider interactionRangeCollider;
        [SerializeField, Min(0.1f)] private float interactionRange = 1.5f;

        private NetworkPlayerController activePlayer;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool isMiniGameOpen;
        private bool setupValid;

        public EventId ConfiguredEventId => eventId;
        public PHSMiniGameType ConfiguredMiniGameType => miniGameType;
        public Vector3 WorldPosition => transform.position;
        public bool IsConfigured => IsMatchingPair(eventId, miniGameType);
        public string MiniGameTargetId => eventId.ToString();
        public string InteractionPrompt => interactionPrompt;
        public float InteractionRange => interactionRange;

        private void Awake()
        {
            setupValid = ValidateConfiguration(true);
        }

        private void OnDisable()
        {
            if (isMiniGameOpen)
            {
                RestorePlayerInput();
            }
        }

        private void OnValidate()
        {
            setupValid = ValidateConfiguration(false);
        }

        public bool CanInteract(IItemHolder itemHolder)
        {
            return CanInteractCore(itemHolder as Component);
        }

        public void Interact(IItemHolder itemHolder)
        {
            TryOpenMiniGame(itemHolder as Component);
        }

        bool CommonInteraction.IInteractable.CanInteract(CommonInteraction.IItemHolder itemHolder)
        {
            return CanInteractCore(itemHolder as Component);
        }

        void CommonInteraction.IInteractable.Interact(CommonInteraction.IItemHolder itemHolder)
        {
            TryOpenMiniGame(itemHolder as Component);
        }

        public bool IsWithinInteractionRange(Vector3 playerPosition)
        {
            if (interactionRangeCollider == null)
            {
                return false;
            }

            var closestPoint = interactionRangeCollider.ClosestPoint(playerPosition);
            return (closestPoint - playerPosition).sqrMagnitude
                <= interactionRange * interactionRange;
        }

        public void OnMiniGameSucceeded()
        {
            SubmitResult(true);
        }

        public void OnMiniGameFailed()
        {
            SubmitResult(false);
        }

        public void OnMiniGameSessionClosed()
        {
            RestorePlayerInput();
        }

        private bool CanInteractCore(Component itemHolderComponent)
        {
            if (!setupValid || isMiniGameOpen)
            {
                return false;
            }

            if (itemHolderComponent == null
                || !IsWithinInteractionRange(itemHolderComponent.transform.position))
            {
                return false;
            }

            var coordinator = NetworkEventCoordinator.Instance;
            return coordinator != null
                && coordinator.IsSpawned
                && coordinator.IsEventActive(eventId);
        }

        private void TryOpenMiniGame(Component itemHolderComponent)
        {
            if (!CanInteractCore(itemHolderComponent))
            {
                Debug.LogWarning(
                    $"PHS_FINAL_MINIGAME_OPEN_REJECTED reason=event_not_active_or_terminal_busy event={eventId}",
                    this);
                return;
            }

            if (PHSMiniGameManager.Instance == null)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_OPEN_REJECTED reason=manager_missing event={eventId}",
                    this);
                return;
            }

            if (itemHolderComponent == null)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_OPEN_REJECTED reason=item_holder_component_missing event={eventId}",
                    this);
                return;
            }

            activePlayer = itemHolderComponent.GetComponent<NetworkPlayerController>();
            if (activePlayer == null)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_OPEN_REJECTED reason=network_player_missing event={eventId}",
                    itemHolderComponent);
                return;
            }

            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            activePlayer.SetGameplayInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isMiniGameOpen = true;

            if (!PHSMiniGameManager.Instance.OpenMiniGame(miniGameType, this))
            {
                RestorePlayerInput();
                Debug.LogWarning(
                    $"PHS_FINAL_MINIGAME_OPEN_REJECTED reason=manager_busy event={eventId}",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_FINAL_MINIGAME_OPENED event={eventId} type={miniGameType}",
                this);
        }

        private void SubmitResult(bool succeeded)
        {
            if (!isMiniGameOpen)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_RESULT_REJECTED reason=terminal_not_open event={eventId} succeeded={succeeded}",
                    this);
                return;
            }

            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_RESULT_REJECTED reason=coordinator_missing event={eventId} succeeded={succeeded}",
                    this);
                return;
            }

            if (!coordinator.RequestMiniGameResult(eventId, succeeded))
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_RESULT_REJECTED reason=request_not_accepted event={eventId} succeeded={succeeded}",
                    this);
            }

        }

        private void RestorePlayerInput()
        {
            if (activePlayer != null)
            {
                activePlayer.SetGameplayInputEnabled(true);
                activePlayer = null;
            }

            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            isMiniGameOpen = false;
        }

        private bool ValidateConfiguration(bool logErrors)
        {
            var valid = IsMatchingPair(eventId, miniGameType);
            valid &= interactionRangeCollider != null;
            valid &= interactionRange > 0f;
            if (!valid && logErrors)
            {
                Debug.LogError(
                    $"PHS_FINAL_MINIGAME_SETUP_INVALID event={eventId} type={miniGameType} " +
                    $"collider={(interactionRangeCollider == null ? "missing" : interactionRangeCollider.name)} " +
                    $"range={interactionRange:F2}",
                    this);
            }

            return valid;
        }

        private static bool IsMatchingPair(EventId configuredEventId, PHSMiniGameType configuredMiniGameType)
        {
            return configuredEventId switch
            {
                EventId.EmpAttack => configuredMiniGameType == PHSMiniGameType.WireFix,
                EventId.MeteorAttack => configuredMiniGameType == PHSMiniGameType.Cannon,
                EventId.EnemyScout => configuredMiniGameType == PHSMiniGameType.PowerSync,
                _ => false
            };
        }
    }
}
