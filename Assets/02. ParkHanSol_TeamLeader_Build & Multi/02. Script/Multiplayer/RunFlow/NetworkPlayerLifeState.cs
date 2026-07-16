using LastJumpCrew.Common;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class NetworkPlayerLifeState : NetworkBehaviour, INetworkPlayerLifeState, IDamageable
    {
        [SerializeField, Min(1)] private int maximumHealth = 100;
        [SerializeField, Min(0.1f)] private float automaticRespawnSeconds = 5f;
        [SerializeField, Min(0.1f)] private float missingRespawnPointRetrySeconds = 0.5f;

        private readonly NetworkVariable<int> synchronizedHealth = new(
            100,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedAlive = new(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> synchronizedWarpRevivePending = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> synchronizedDeadZoneSeconds = new(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> synchronizedRespawnSeconds = new(
            -1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkPlayerController playerController;
        private CharacterController characterController;
        private Renderer[] renderers;
        private bool[] rendererStatesBeforeDeath;
        private bool presentationIsDead;
        private float deadZoneDeadline = -1f;
        private float automaticRespawnDeadline = -1f;
        private float nextWarningSyncTime;
        private float nextRespawnSyncTime;

        public bool IsAlive => synchronizedAlive.Value;
        public bool IsWaitingForWarpRevive => synchronizedWarpRevivePending.Value;
        public bool IsWaitingForAutomaticRespawn => !synchronizedAlive.Value
            && !synchronizedWarpRevivePending.Value
            && synchronizedRespawnSeconds.Value >= 0f;
        public float DeadZoneWarningRemainingSeconds => synchronizedDeadZoneSeconds.Value;
        public float RespawnRemainingSeconds => synchronizedRespawnSeconds.Value;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
            characterController = GetComponent<CharacterController>();
            renderers = GetComponentsInChildren<Renderer>(true);
            rendererStatesBeforeDeath = new bool[renderers.Length];
        }

        public override void OnNetworkSpawn()
        {
            synchronizedAlive.OnValueChanged += HandleAliveChanged;
            synchronizedWarpRevivePending.OnValueChanged += HandleWarpRevivePendingChanged;
            synchronizedDeadZoneSeconds.OnValueChanged += HandleWarningChanged;
            synchronizedRespawnSeconds.OnValueChanged += HandleRespawnSecondsChanged;
            if (IsServer)
            {
                synchronizedHealth.Value = maximumHealth;
            }

            ApplyAlivePresentation(synchronizedAlive.Value);
            ApplyWarningPresentation(synchronizedDeadZoneSeconds.Value);
            ApplyRespawnPresentation();
        }

        public override void OnNetworkDespawn()
        {
            synchronizedAlive.OnValueChanged -= HandleAliveChanged;
            synchronizedWarpRevivePending.OnValueChanged -= HandleWarpRevivePendingChanged;
            synchronizedDeadZoneSeconds.OnValueChanged -= HandleWarningChanged;
            synchronizedRespawnSeconds.OnValueChanged -= HandleRespawnSecondsChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            if (synchronizedAlive.Value)
            {
                TickDeadZoneWarning();
                return;
            }

            TickAutomaticRespawn();
        }

        private void TickDeadZoneWarning()
        {
            if (deadZoneDeadline < 0f)
            {
                return;
            }

            var remaining = Mathf.Max(0f, deadZoneDeadline - Time.time);
            if (Time.time >= nextWarningSyncTime || remaining <= 0f)
            {
                nextWarningSyncTime = Time.time + 0.1f;
                synchronizedDeadZoneSeconds.Value = remaining;
            }

            if (remaining > 0f)
            {
                return;
            }

            deadZoneDeadline = -1f;
            Kill("dead_zone", false);
        }

        private void TickAutomaticRespawn()
        {
            if (synchronizedWarpRevivePending.Value || automaticRespawnDeadline < 0f)
            {
                return;
            }

            var remaining = Mathf.Max(0f, automaticRespawnDeadline - Time.time);
            if (Time.time >= nextRespawnSyncTime || remaining <= 0f)
            {
                nextRespawnSyncTime = Time.time + 0.1f;
                synchronizedRespawnSeconds.Value = remaining;
            }

            if (remaining > 0f)
            {
                return;
            }

            automaticRespawnDeadline = -1f;
            if (!TryReviveAtSceneRespawnPoint("automatic"))
            {
                automaticRespawnDeadline = Time.time + missingRespawnPointRetrySeconds;
                synchronizedRespawnSeconds.Value = missingRespawnPointRetrySeconds;
            }
        }

        public void ApplyDamage(int amount, GameObject attacker)
        {
            if (amount <= 0 || !synchronizedAlive.Value)
            {
                return;
            }

            if (IsSpawned && !IsServer)
            {
                Debug.LogError($"PHS_PLAYER_DAMAGE_FAILED reason=server_required player={name} amount={amount}", this);
                return;
            }

            synchronizedHealth.Value = Mathf.Max(0, synchronizedHealth.Value - amount);
            if (synchronizedHealth.Value == 0)
            {
                Kill("damage", false);
            }
        }

        public void BeginDeadZoneWarning(float warningSeconds)
        {
            if (!RequireServer(nameof(BeginDeadZoneWarning)) || !synchronizedAlive.Value)
            {
                return;
            }

            var duration = Mathf.Max(0.1f, warningSeconds);
            deadZoneDeadline = Time.time + duration;
            synchronizedDeadZoneSeconds.Value = duration;
            Debug.Log($"PHS_DEBRIS_DEAD_ZONE_WARNING player={name} clientId={OwnerClientId} seconds={duration:0.0}", this);
        }

        public void CancelDeadZoneWarning()
        {
            if (!RequireServer(nameof(CancelDeadZoneWarning)))
            {
                return;
            }

            deadZoneDeadline = -1f;
            synchronizedDeadZoneSeconds.Value = -1f;
        }

        public void KillForWarp()
        {
            if (!RequireServer(nameof(KillForWarp)))
            {
                return;
            }

            if (!synchronizedAlive.Value)
            {
                automaticRespawnDeadline = -1f;
                synchronizedRespawnSeconds.Value = -1f;
                synchronizedWarpRevivePending.Value = true;
                Debug.Log($"PHS_PLAYER_WARP_REVIVE_QUEUED player={name} clientId={OwnerClientId}", this);
                return;
            }

            Kill("warp_outside_safe_zone", true);
        }

        public bool TryReviveAfterWarp()
        {
            if (!RequireServer(nameof(TryReviveAfterWarp)) || !synchronizedWarpRevivePending.Value)
            {
                return false;
            }

            return TryReviveAtSceneRespawnPoint("warp_complete");
        }

        private void Kill(string reason, bool reviveAfterWarp)
        {
            if (!synchronizedAlive.Value)
            {
                return;
            }

            deadZoneDeadline = -1f;
            automaticRespawnDeadline = reviveAfterWarp
                ? -1f
                : Time.time + automaticRespawnSeconds;
            synchronizedHealth.Value = 0;
            synchronizedDeadZoneSeconds.Value = -1f;
            synchronizedWarpRevivePending.Value = reviveAfterWarp;
            synchronizedRespawnSeconds.Value = reviveAfterWarp
                ? -1f
                : automaticRespawnSeconds;
            synchronizedAlive.Value = false;
            Debug.Log($"PHS_PLAYER_DIED reason={reason} player={name} clientId={OwnerClientId} warpRevive={reviveAfterWarp}", this);
        }

        private bool TryReviveAtSceneRespawnPoint(string reason)
        {
            var activeScene = SceneManager.GetActiveScene();
            var context = GameplaySceneContext.FindForScene(activeScene);
            if (context == null || !context.TryGetRespawnPoint(out var respawnPoint))
            {
                synchronizedRespawnSeconds.Value = -1f;
                Debug.LogError($"PHS_PLAYER_REVIVE_FAILED reason=respawn_point_missing player={name} scene={activeScene.name}", this);
                return false;
            }

            playerController.ResetMovementForRespawn();
            TeleportTo(respawnPoint.position, respawnPoint.rotation);
            synchronizedHealth.Value = maximumHealth;
            synchronizedWarpRevivePending.Value = false;
            synchronizedRespawnSeconds.Value = -1f;
            synchronizedDeadZoneSeconds.Value = -1f;
            synchronizedAlive.Value = true;
            Debug.Log($"PHS_PLAYER_REVIVED reason={reason} player={name} clientId={OwnerClientId}", this);
            return true;
        }

        private void TeleportTo(Vector3 position, Quaternion rotation)
        {
            var wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);
            if (characterController != null)
            {
                characterController.enabled = wasEnabled;
            }
        }

        private void HandleAliveChanged(bool previousValue, bool currentValue)
        {
            ApplyAlivePresentation(currentValue);
        }

        private void HandleWarpRevivePendingChanged(bool previousValue, bool currentValue)
        {
            ApplyRespawnPresentation();
        }

        private void HandleWarningChanged(float previousValue, float currentValue)
        {
            ApplyWarningPresentation(currentValue);
        }

        private void HandleRespawnSecondsChanged(float previousValue, float currentValue)
        {
            ApplyRespawnPresentation();
        }

        private void ApplyAlivePresentation(bool alive)
        {
            playerController.SetLifeInputBlocked(!alive);
            if (!alive && !presentationIsDead)
            {
                presentationIsDead = true;
                if (characterController != null)
                {
                    characterController.enabled = false;
                }

                for (var index = 0; index < renderers.Length; index++)
                {
                    rendererStatesBeforeDeath[index] = renderers[index] != null && renderers[index].enabled;
                    if (renderers[index] != null)
                    {
                        renderers[index].enabled = false;
                    }
                }
            }
            else if (alive && presentationIsDead)
            {
                presentationIsDead = false;
                if (characterController != null)
                {
                    characterController.enabled = true;
                }

                for (var index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] != null)
                    {
                        renderers[index].enabled = rendererStatesBeforeDeath[index];
                    }
                }
            }

            ApplyRespawnPresentation();
        }

        private void ApplyRespawnPresentation()
        {
            if (!IsOwner)
            {
                return;
            }

            if (synchronizedAlive.Value)
            {
                playerController.ClearRespawnStatus();
                return;
            }

            if (synchronizedWarpRevivePending.Value)
            {
                playerController.ShowWarpRespawnPending();
                return;
            }

            if (synchronizedRespawnSeconds.Value >= 0f)
            {
                playerController.ShowRespawnCountdown(synchronizedRespawnSeconds.Value);
                return;
            }

            playerController.ClearRespawnStatus();
        }

        private void ApplyWarningPresentation(float remainingSeconds)
        {
            if (!IsOwner)
            {
                return;
            }

            if (remainingSeconds >= 0f && synchronizedAlive.Value)
            {
                playerController.ShowDeadZoneWarning(remainingSeconds);
            }
            else
            {
                playerController.ClearDeadZoneWarning();
            }
        }

        private bool RequireServer(string operation)
        {
            if (!IsSpawned || IsServer)
            {
                return true;
            }

            Debug.LogError($"PHS_PLAYER_LIFE_FAILED reason=server_required operation={operation} player={name}", this);
            return false;
        }
    }
}
