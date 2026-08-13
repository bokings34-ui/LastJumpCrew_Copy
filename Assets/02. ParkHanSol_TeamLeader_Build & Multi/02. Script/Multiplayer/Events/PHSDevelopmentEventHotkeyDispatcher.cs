using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events
{
    /// <summary>
    /// Editor/development-only host shortcut for exercising the canonical team-event
    /// authority. This component is authored on the persistent session root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PHSDevelopmentEventHotkeyDispatcher : MonoBehaviour
    {
        [Header("Development Only")]
        [SerializeField] private bool enableDevelopmentHotkeys = true;

        [Header("Inspector References")]
        [SerializeField] private NetworkRunSessionRoot sessionRoot;
        [SerializeField] private NetworkEventCoordinator eventCoordinator;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (!enableDevelopmentHotkeys)
            {
                return;
            }

            if (!TryGetRequestedEvent(out var eventId, out var keyName))
            {
                return;
            }

            TryDispatch(eventId, keyName);
        }

        private bool TryGetRequestedEvent(out SM.EventId eventId, out string keyName)
        {
            if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.Fire;
                keyName = "2";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.EnemySpawn;
                keyName = "3";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.PowerOff;
                keyName = "4";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.OxygenLeak;
                keyName = "5";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.MicDestroy;
                keyName = "6";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit7Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.EnemyScout;
                keyName = "7";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit8Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.MeteorAttack;
                keyName = "8";
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.digit9Key.wasPressedThisFrame)
            {
                eventId = SM.EventId.EmpAttack;
                keyName = "9";
                return true;
            }

            eventId = default;
            keyName = null;
            return false;
        }

        private void TryDispatch(SM.EventId eventId, string keyName)
        {
            if (sessionRoot == null || eventCoordinator == null)
            {
                Debug.LogError(
                    $"PHS_DEV_EVENT_HOTKEY_REJECTED key={keyName} event={eventId} reason=inspector_reference_missing",
                    this);
                return;
            }

            if (sessionRoot.EventCoordinator != eventCoordinator)
            {
                Debug.LogError(
                    $"PHS_DEV_EVENT_HOTKEY_REJECTED key={keyName} event={eventId} reason=noncanonical_coordinator_reference",
                    this);
                return;
            }

            if (!sessionRoot.IsSpawned || !eventCoordinator.IsSpawned)
            {
                Debug.LogWarning(
                    $"PHS_DEV_EVENT_HOTKEY_REJECTED key={keyName} event={eventId} reason=session_root_not_spawned",
                    this);
                return;
            }

            if (!sessionRoot.IsServer || !eventCoordinator.IsServer)
            {
                Debug.LogWarning(
                    $"PHS_DEV_EVENT_HOTKEY_REJECTED key={keyName} event={eventId} reason=host_only",
                    this);
                return;
            }

            if (!eventCoordinator.TrySpawnEventServer(eventId, out var instanceId))
            {
                Debug.LogWarning(
                    $"PHS_DEV_EVENT_HOTKEY_REJECTED key={keyName} event={eventId} reason=spawn_rejected",
                    this);
                return;
            }

            Debug.Log(
                $"PHS_DEV_EVENT_HOTKEY_SPAWNED key={keyName} event={eventId} instance={instanceId}",
                this);
        }
#endif
    }
}
