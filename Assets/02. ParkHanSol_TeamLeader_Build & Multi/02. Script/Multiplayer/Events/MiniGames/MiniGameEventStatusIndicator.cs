using System;
using System.Collections;
using System.Collections.Generic;
using SM;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Events.MiniGames
{
    [DisallowMultipleComponent]
    public sealed class MiniGameEventStatusIndicator : MonoBehaviour
    {
        [Serializable]
        private sealed class VisualSlot
        {
            [SerializeField] private Light statusLight;
            [SerializeField] private Renderer emissiveRenderer;
            [SerializeField, Min(0)] private int materialIndex;

            private MaterialPropertyBlock propertyBlock;

            public bool Validate(Component context, int slotIndex)
            {
                if (statusLight == null && emissiveRenderer == null)
                {
                    Debug.LogError(
                        $"PHS_MINIGAME_INDICATOR_SLOT_INVALID reason=visual_missing slot={slotIndex}",
                        context);
                    return false;
                }

                if (emissiveRenderer == null)
                {
                    return true;
                }

                var materials = emissiveRenderer.sharedMaterials;
                if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
                {
                    Debug.LogError(
                        $"PHS_MINIGAME_INDICATOR_SLOT_INVALID reason=material_index slot={slotIndex} index={materialIndex}",
                        context);
                    return false;
                }

                // NullGfx is used by the headless network validation player.
                // Shader property introspection is not reliable without a graphics device.
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
                    && !materials[materialIndex].HasProperty(EmissionColorId))
                {
                    Debug.LogError(
                        $"PHS_MINIGAME_INDICATOR_SLOT_INVALID reason=emission_property_missing slot={slotIndex} material={materials[materialIndex].name}",
                        context);
                    return false;
                }

                propertyBlock = new MaterialPropertyBlock();
                return true;
            }

            public void Apply(Color color, bool visible, float lightIntensity, float emissionIntensity)
            {
                if (statusLight != null)
                {
                    statusLight.color = color;
                    statusLight.intensity = lightIntensity;
                    statusLight.enabled = visible;
                }

                if (emissiveRenderer == null)
                {
                    return;
                }

                propertyBlock ??= new MaterialPropertyBlock();
                emissiveRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                propertyBlock.SetColor(
                    BaseColorId,
                    visible ? color : Color.black);
                propertyBlock.SetColor(
                    EmissionColorId,
                    visible ? color * emissionIntensity : Color.black);
                emissiveRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Terminal")]
        [SerializeField] private MonoBehaviour terminalSource;

        [Header("Replaceable Visual Slots")]
        [SerializeField] private VisualSlot[] visualSlots = Array.Empty<VisualSlot>();
        [SerializeField, Min(0f)] private float lightIntensity = 2f;
        [SerializeField, Min(0f)] private float emissionIntensity = 2f;

        [Header("State Colors")]
        [SerializeField] private Color activeColor = new(1f, 0.55f, 0f, 1f);
        [SerializeField] private Color successColor = Color.green;
        [SerializeField] private Color failureColor = Color.red;

        [Header("Result Blink")]
        [SerializeField, Min(0.05f)] private float blinkIntervalSeconds = 0.15f;
        [SerializeField, Min(0.1f)] private float blinkDurationSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float bindRetrySeconds = 0.25f;

        private readonly List<NetworkEventLifecycleSnapshot> snapshotBuffer = new();

        private IEventMiniGameTerminal terminal;
        private NetworkEventCoordinator boundCoordinator;
        private Coroutine blinkCoroutine;
        private float nextBindAttemptTime;
        private ulong activeInstanceId;
        private ulong handledTerminalInstanceId;
        private uint handledTerminalRevision;
        private bool setupValid;

        private void Awake()
        {
            setupValid = ValidateSetup();
            ApplyVisual(Color.black, false);
        }

        private void OnEnable()
        {
            if (setupValid)
            {
                TryBindCoordinator();
            }
        }

        private void OnDisable()
        {
            UnbindCoordinator();
            StopBlink();
            ApplyVisual(Color.black, false);
        }

        private void Update()
        {
            if (!setupValid)
            {
                return;
            }

            if (boundCoordinator != null
                && boundCoordinator.IsSpawned
                && NetworkEventCoordinator.Instance == boundCoordinator)
            {
                return;
            }

            UnbindCoordinator();
            if (Time.unscaledTime >= nextBindAttemptTime)
            {
                nextBindAttemptTime = Time.unscaledTime + bindRetrySeconds;
                TryBindCoordinator();
            }
        }

        private bool ValidateSetup()
        {
            terminal = terminalSource as IEventMiniGameTerminal;
            if (terminalSource == null || terminal == null)
            {
                Debug.LogError(
                    $"PHS_MINIGAME_INDICATOR_SETUP_INVALID reason=terminal_interface_missing indicator={name}",
                    this);
                return false;
            }

            if (!terminal.IsConfigured)
            {
                Debug.LogError(
                    $"PHS_MINIGAME_INDICATOR_SETUP_INVALID reason=terminal_not_configured indicator={name}",
                    this);
                return false;
            }

            if (visualSlots == null || visualSlots.Length == 0)
            {
                Debug.LogError(
                    $"PHS_MINIGAME_INDICATOR_SETUP_INVALID reason=visual_slots_missing indicator={name}",
                    this);
                return false;
            }

            var valid = true;
            for (var i = 0; i < visualSlots.Length; i++)
            {
                if (visualSlots[i] == null || !visualSlots[i].Validate(this, i))
                {
                    valid = false;
                }
            }

            return valid;
        }

        private void TryBindCoordinator()
        {
            var coordinator = NetworkEventCoordinator.Instance;
            if (coordinator == null || !coordinator.IsSpawned)
            {
                return;
            }

            if (boundCoordinator == coordinator)
            {
                RefreshFromCoordinator();
                return;
            }

            UnbindCoordinator();
            boundCoordinator = coordinator;
            boundCoordinator.LifecycleSnapshotsChanged += RefreshFromCoordinator;
            RefreshFromCoordinator();
        }

        private void UnbindCoordinator()
        {
            if (boundCoordinator != null)
            {
                boundCoordinator.LifecycleSnapshotsChanged -= RefreshFromCoordinator;
                boundCoordinator = null;
            }

            snapshotBuffer.Clear();
            activeInstanceId = 0UL;
            StopBlink();
            ApplyVisual(Color.black, false);
        }

        private void RefreshFromCoordinator()
        {
            if (boundCoordinator == null || !boundCoordinator.IsSpawned)
            {
                return;
            }

            boundCoordinator.CopySnapshotsTo(snapshotBuffer);
            NetworkEventLifecycleSnapshot? newestActive = null;
            NetworkEventLifecycleSnapshot? newestTerminal = null;

            foreach (var snapshot in snapshotBuffer)
            {
                if (snapshot.EventId != terminal.ConfiguredEventId)
                {
                    continue;
                }

                if (!snapshot.IsTerminal)
                {
                    if (!newestActive.HasValue || IsNewer(snapshot, newestActive.Value))
                    {
                        newestActive = snapshot;
                    }
                }
                else if (!newestTerminal.HasValue || IsNewer(snapshot, newestTerminal.Value))
                {
                    newestTerminal = snapshot;
                }
            }

            if (newestActive.HasValue)
            {
                if (activeInstanceId != newestActive.Value.InstanceId || blinkCoroutine != null)
                {
                    StopBlink();
                    activeInstanceId = newestActive.Value.InstanceId;
                    ApplyVisual(activeColor, true);
                }

                return;
            }

            activeInstanceId = 0UL;
            if (newestTerminal.HasValue && IsUnhandledTerminal(newestTerminal.Value))
            {
                handledTerminalInstanceId = newestTerminal.Value.InstanceId;
                handledTerminalRevision = newestTerminal.Value.Revision;
                StartResultBlink(newestTerminal.Value.State);
                return;
            }

            if (blinkCoroutine == null)
            {
                ApplyVisual(Color.black, false);
            }
        }

        private bool IsUnhandledTerminal(NetworkEventLifecycleSnapshot snapshot)
        {
            return handledTerminalInstanceId != snapshot.InstanceId
                || handledTerminalRevision != snapshot.Revision;
        }

        private void StartResultBlink(EventState state)
        {
            StopBlink();
            switch (state)
            {
                case EventState.Resolve:
                    blinkCoroutine = StartCoroutine(BlinkRoutine(successColor));
                    break;
                case EventState.Fail:
                    blinkCoroutine = StartCoroutine(BlinkRoutine(failureColor));
                    break;
                default:
                    Debug.LogError(
                        $"PHS_MINIGAME_INDICATOR_STATE_INVALID event={terminal.ConfiguredEventId} state={state}",
                        this);
                    ApplyVisual(Color.black, false);
                    break;
            }
        }

        private IEnumerator BlinkRoutine(Color color)
        {
            var elapsed = 0f;
            var visible = true;
            while (elapsed < blinkDurationSeconds)
            {
                ApplyVisual(color, visible);
                yield return new WaitForSecondsRealtime(blinkIntervalSeconds);
                elapsed += blinkIntervalSeconds;
                visible = !visible;
            }

            ApplyVisual(Color.black, false);
            blinkCoroutine = null;
        }

        private void StopBlink()
        {
            if (blinkCoroutine == null)
            {
                return;
            }

            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        private void ApplyVisual(Color color, bool visible)
        {
            if (visualSlots == null)
            {
                return;
            }

            foreach (var slot in visualSlots)
            {
                slot?.Apply(color, visible, lightIntensity, emissionIntensity);
            }
        }

        private static bool IsNewer(
            NetworkEventLifecycleSnapshot candidate,
            NetworkEventLifecycleSnapshot current)
        {
            var timeComparison = candidate.ChangedAtServerTime.CompareTo(current.ChangedAtServerTime);
            if (timeComparison != 0)
            {
                return timeComparison > 0;
            }

            var revisionComparison = candidate.Revision.CompareTo(current.Revision);
            if (revisionComparison != 0)
            {
                return revisionComparison > 0;
            }

            return candidate.InstanceId > current.InstanceId;
        }
    }
}
