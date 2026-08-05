using System;
using System.Collections.Generic;
using SM;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Doors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PHSNetworkShipDoorCoordinator : NetworkBehaviour
    {
        [Serializable]
        public struct DoorBinding
        {
            public DoorDoubleSlide LegacyDoor;
            public Transform LeftLeaf;
            public Transform RightLeaf;
            public Collider PresenceSensor;
            public Collider SolidBlocker;
            public NavMeshObstacle NavMeshBlocker;
            public PHSShipDoorTarget Target;
            public PHSShipDoorLockButton Button;
            public Vector3 LeftClosedLocalPosition;
            public Vector3 RightClosedLocalPosition;
            public Vector3 OpenDirection;
            public float OpenDistance;
        }

        public struct DoorState : INetworkSerializable, IEquatable<DoorState>
        {
            public int Integrity;
            public bool Locked;
            public bool Destroyed;
            public bool Open;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer)
                where T : IReaderWriter
            {
                serializer.SerializeValue(ref Integrity);
                serializer.SerializeValue(ref Locked);
                serializer.SerializeValue(ref Destroyed);
                serializer.SerializeValue(ref Open);
            }

            public bool Equals(DoorState other)
            {
                return Integrity == other.Integrity
                    && Locked == other.Locked
                    && Destroyed == other.Destroyed
                    && Open == other.Open;
            }
        }

        [SerializeField] private DoorBinding[] doors = Array.Empty<DoorBinding>();
        [SerializeField, Min(1)] private int maximumIntegrity = 30;
        [SerializeField, Min(0.05f)] private float detectionInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float enemyAttackInterval = 1f;
        [SerializeField, Min(0.1f)] private float visualSpeed = 5f;
        [SerializeField, Min(0.5f)] private float buttonInteractionDistance = 3.5f;

        private readonly NetworkList<DoorState> states = new();
        private readonly Collider[] overlapResults = new Collider[32];
        private readonly Dictionary<int, float> nextEnemyAttackAt = new();
        private float nextDetectionAt;

        public int DoorCount => doors.Length;
        public int MaximumIntegrity => maximumIntegrity;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                states.Clear();
                for (var i = 0; i < doors.Length; i++)
                {
                    states.Add(new DoorState { Integrity = maximumIntegrity });
                }
            }

            Debug.Log($"PHS_SHIP_DOORS_READY count={doors.Length} server={IsServer}", this);
        }

        private void Awake()
        {
            for (var i = 0; i < doors.Length; i++)
            {
                var binding = doors[i];
                if (binding.LegacyDoor != null)
                {
                    binding.LegacyDoor.enabled = false;
                }

                binding.Target?.Initialize(this, i);
                binding.Button?.Initialize(this, i);
            }
        }

        private void Update()
        {
            for (var i = 0; i < doors.Length; i++)
            {
                ApplyPresentation(i, GetState(i));
            }

            if (!IsServer || Time.time < nextDetectionAt)
            {
                return;
            }

            nextDetectionAt = Time.time + detectionInterval;
            for (var i = 0; i < doors.Length; i++)
            {
                UpdateDoorServer(i);
            }
        }

        public DoorState GetState(int doorIndex)
        {
            return doorIndex >= 0 && doorIndex < states.Count
                ? states[doorIndex]
                : new DoorState { Integrity = maximumIntegrity };
        }

        public bool CanRepair(int doorIndex)
        {
            var state = GetState(doorIndex);
            return doorIndex >= 0 && doorIndex < doors.Length
                && state.Integrity < maximumIntegrity;
        }

        public bool TryRepairServer(int doorIndex, float amount, GameObject repairer)
        {
            if ((!IsSpawned || IsServer)
                && IsValidDoor(doorIndex)
                && amount > 0f)
            {
                var state = GetState(doorIndex);
                if (state.Integrity >= maximumIntegrity)
                {
                    return false;
                }

                state.Integrity = Mathf.Min(maximumIntegrity,
                    state.Integrity + Mathf.CeilToInt(amount));
                state.Destroyed = false;
                state.Open = false;
                SetState(doorIndex, state);
                Debug.Log($"PHS_SHIP_DOOR_REPAIRED index={doorIndex} integrity={state.Integrity}/{maximumIntegrity}", repairer);
                return true;
            }

            Debug.LogError($"PHS_SHIP_DOOR_REPAIR_FAILED index={doorIndex} reason=server_or_contract", this);
            return false;
        }

        public void ApplyEnemyDamageServer(int doorIndex, int amount, GameObject attacker)
        {
            if ((IsSpawned && !IsServer) || !IsValidDoor(doorIndex) || amount <= 0)
            {
                return;
            }

            var state = GetState(doorIndex);
            if (!state.Locked || state.Destroyed)
            {
                return;
            }

            state.Integrity = Mathf.Max(0, state.Integrity - amount);
            if (state.Integrity == 0)
            {
                state.Destroyed = true;
                state.Locked = false;
                state.Open = true;
                Debug.Log($"PHS_SHIP_DOOR_DESTROYED index={doorIndex}", attacker);
            }
            SetState(doorIndex, state);
        }

        public void RequestToggleLock(int doorIndex)
        {
            if (!IsSpawned)
            {
                ToggleLockServer(doorIndex);
                return;
            }

            if (IsServer)
            {
                ToggleLockServer(doorIndex);
            }
            else
            {
                ToggleLockServerRpc(doorIndex);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleLockServerRpc(int doorIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsValidDoor(doorIndex)
                || NetworkManager == null
                || !NetworkManager.ConnectedClients.TryGetValue(
                    rpcParams.Receive.SenderClientId, out var client)
                || client.PlayerObject == null
                || Vector3.Distance(client.PlayerObject.transform.position,
                    doors[doorIndex].Button.transform.position) > buttonInteractionDistance)
            {
                Debug.LogWarning($"PHS_SHIP_DOOR_LOCK_REJECTED index={doorIndex} client={rpcParams.Receive.SenderClientId}", this);
                return;
            }

            ToggleLockServer(doorIndex);
        }

        private void ToggleLockServer(int doorIndex)
        {
            if (!IsValidDoor(doorIndex))
            {
                Debug.LogError($"PHS_SHIP_DOOR_LOCK_FAILED index={doorIndex} reason=binding", this);
                return;
            }

            var state = GetState(doorIndex);
            if (state.Destroyed)
            {
                Debug.LogWarning($"PHS_SHIP_DOOR_LOCK_FAILED index={doorIndex} reason=destroyed", this);
                return;
            }

            state.Locked = !state.Locked;
            SetState(doorIndex, state);
            Debug.Log($"PHS_SHIP_DOOR_LOCK_CHANGED index={doorIndex} locked={state.Locked}", this);
        }

        private void UpdateDoorServer(int doorIndex)
        {
            var binding = doors[doorIndex];
            if (binding.PresenceSensor is not BoxCollider sensor)
            {
                Debug.LogError($"PHS_SHIP_DOOR_SENSOR_FAILED index={doorIndex} reason=box_collider_required", this);
                return;
            }

            var center = sensor.transform.TransformPoint(sensor.center);
            var halfExtents = Vector3.Scale(sensor.size * 0.5f,
                sensor.transform.lossyScale);
            var count = Physics.OverlapBoxNonAlloc(center, halfExtents,
                overlapResults, sensor.transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            EnemyBase nearestEnemy = null;
            var hasActor = false;
            for (var i = 0; i < count; i++)
            {
                var hit = overlapResults[i];
                if (hit == null || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                var enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy != null && enemy.IsAlive)
                {
                    nearestEnemy = enemy;
                    hasActor = true;
                    break;
                }

                if (hit.GetComponentInParent<NetworkPlayerItemRecord>() != null)
                {
                    hasActor = true;
                }
            }

            var state = GetState(doorIndex);
            if (state.Destroyed)
            {
                return;
            }

            if (!state.Locked)
            {
                if (state.Open != hasActor)
                {
                    state.Open = hasActor;
                    SetState(doorIndex, state);
                }
                return;
            }

            // 잠금은 즉시 적용하되 점유 중인 통로에 충돌체를 생성하지 않는다.
            // 열린 상태의 마지막 점유자가 센서를 벗어난 뒤 닫고 차단한다.
            if (state.Open)
            {
                if (hasActor)
                {
                    return;
                }

                state.Open = false;
                SetState(doorIndex, state);
                return;
            }

            if (nearestEnemy == null
                || nextEnemyAttackAt.TryGetValue(doorIndex, out var nextAttack)
                && Time.time < nextAttack)
            {
                return;
            }

            nextEnemyAttackAt[doorIndex] = Time.time + enemyAttackInterval;
            nearestEnemy.RotateTowards(binding.Target.transform.position,
                detectionInterval);
            if (nearestEnemy.Anim != null)
            {
                nearestEnemy.Anim.Play(EnemyAnimData.Attack, -1, 0f);
            }
            binding.Target.ApplyDamage(
                Mathf.Max(1, Mathf.RoundToInt(nearestEnemy.AttackDamage)),
                nearestEnemy.gameObject);
        }

        private void ApplyPresentation(int doorIndex, DoorState state)
        {
            var binding = doors[doorIndex];
            var visible = !state.Destroyed;
            if (binding.LeftLeaf != null)
            {
                binding.LeftLeaf.gameObject.SetActive(visible);
                var target = binding.LeftClosedLocalPosition
                    + binding.OpenDirection * (state.Open ? binding.OpenDistance : 0f);
                binding.LeftLeaf.localPosition = Vector3.Lerp(
                    binding.LeftLeaf.localPosition, target,
                    Time.deltaTime * visualSpeed);
            }
            if (binding.RightLeaf != null)
            {
                binding.RightLeaf.gameObject.SetActive(visible);
                var target = binding.RightClosedLocalPosition
                    - binding.OpenDirection * (state.Open ? binding.OpenDistance : 0f);
                binding.RightLeaf.localPosition = Vector3.Lerp(
                    binding.RightLeaf.localPosition, target,
                    Time.deltaTime * visualSpeed);
            }

            var blocked = visible && !state.Open;
            if (binding.SolidBlocker != null)
            {
                binding.SolidBlocker.enabled = blocked;
            }
            if (binding.NavMeshBlocker != null)
            {
                binding.NavMeshBlocker.enabled = blocked;
            }
            binding.Button?.SetState(state.Locked, state.Destroyed);
        }

        private bool IsValidDoor(int doorIndex)
        {
            return doorIndex >= 0 && doorIndex < doors.Length
                && doors[doorIndex].Target != null
                && doors[doorIndex].Button != null;
        }

        private void SetState(int doorIndex, DoorState state)
        {
            if (doorIndex >= 0 && doorIndex < states.Count)
            {
                states[doorIndex] = state;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(DoorBinding[] bindings)
        {
            doors = bindings ?? Array.Empty<DoorBinding>();
        }
#endif
    }
}
