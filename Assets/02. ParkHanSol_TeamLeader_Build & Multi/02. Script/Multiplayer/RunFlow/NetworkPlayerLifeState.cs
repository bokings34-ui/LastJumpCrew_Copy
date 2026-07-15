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

        private NetworkPlayerController playerController;
        private CharacterController characterController;
        private Renderer[] renderers;
        private bool[] rendererStatesBeforeDeath;
        private bool characterControllerWasEnabled;
        private bool presentationIsDead;
        private float deadZoneDeadline = -1f;
        private float nextWarningSyncTime;

        public bool IsAlive => synchronizedAlive.Value;
        public bool IsWaitingForWarpRevive => synchronizedWarpRevivePending.Value;

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
            synchronizedDeadZoneSeconds.OnValueChanged += HandleWarningChanged;
            if (IsServer)
            {
                synchronizedHealth.Value = maximumHealth;
            }

            ApplyAlivePresentation(synchronizedAlive.Value);
            ApplyWarningPresentation(synchronizedDeadZoneSeconds.Value);
        }

        public override void OnNetworkDespawn()
        {
            synchronizedAlive.OnValueChanged -= HandleAliveChanged;
            synchronizedDeadZoneSeconds.OnValueChanged -= HandleWarningChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !synchronizedAlive.Value || deadZoneDeadline < 0f)
            {
                return;
            }

            var remaining = Mathf.Max(0f, deadZoneDeadline - Time.time);
            if (Time.time >= nextWarningSyncTime || remaining <= 0f)
            {
                nextWarningSyncTime = Time.time + 0.1f;
                synchronizedDeadZoneSeconds.Value = remaining;
            }

            if (remaining <= 0f)
            {
                deadZoneDeadline = -1f;
                Kill("dead_zone", false);
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
            if (RequireServer(nameof(KillForWarp)))
            {
                Kill("warp_outside_safe_zone", true);
            }
        }

        public bool TryReviveAfterWarp()
        {
            if (!RequireServer(nameof(TryReviveAfterWarp)) || !synchronizedWarpRevivePending.Value)
            {
                return false;
            }

            var context = GameplaySceneContext.FindForScene(SceneManager.GetActiveScene());
            if (context == null || !context.TryGetSpawnPoint(OwnerClientId, out var spawnPoint))
            {
                Debug.LogError($"PHS_PLAYER_REVIVE_FAILED reason=spawn_point_missing player={name} scene={SceneManager.GetActiveScene().name}", this);
                return false;
            }

            TeleportTo(spawnPoint.position, spawnPoint.rotation);
            synchronizedHealth.Value = maximumHealth;
            synchronizedWarpRevivePending.Value = false;
            synchronizedAlive.Value = true;
            synchronizedDeadZoneSeconds.Value = -1f;
            Debug.Log($"PHS_PLAYER_REVIVED reason=warp_complete player={name} clientId={OwnerClientId}", this);
            return true;
        }

        private void Kill(string reason, bool reviveAfterWarp)
        {
            if (!synchronizedAlive.Value)
            {
                return;
            }

            deadZoneDeadline = -1f;
            synchronizedHealth.Value = 0;
            synchronizedDeadZoneSeconds.Value = -1f;
            synchronizedWarpRevivePending.Value = reviveAfterWarp;
            synchronizedAlive.Value = false;
            Debug.Log($"PHS_PLAYER_DIED reason={reason} player={name} clientId={OwnerClientId} warpRevive={reviveAfterWarp}", this);
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

        private void HandleWarningChanged(float previousValue, float currentValue)
        {
            ApplyWarningPresentation(currentValue);
        }

        private void ApplyAlivePresentation(bool alive)
        {
            playerController.SetLifeInputBlocked(!alive);
            if (!alive && !presentationIsDead)
            {
                presentationIsDead = true;
                characterControllerWasEnabled = characterController != null && characterController.enabled;
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
                    characterController.enabled = characterControllerWasEnabled;
                }

                for (var index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] != null)
                    {
                        renderers[index].enabled = rendererStatesBeforeDeath[index];
                    }
                }
            }

            if (IsOwner && !alive)
            {
                playerController.ShowLifeStateMessage(
                    synchronizedWarpRevivePending.Value
                        ? "사망 - 워프 완료 후 자동 부활"
                        : "사망");
            }
            else if (IsOwner && alive)
            {
                playerController.ClearLifeStateMessage();
            }
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
