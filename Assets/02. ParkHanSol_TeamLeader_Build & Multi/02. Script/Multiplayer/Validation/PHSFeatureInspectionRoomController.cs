using System;
using LastJumpCrew.ParkHanSol.Items;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Validation
{
    [Serializable]
    public sealed class PHSFeatureInspectionRoomEntry
    {
        [SerializeField] private string roomId;
        [SerializeField] private GameObject roomRoot;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private GameObject[] additionalActiveObjects = Array.Empty<GameObject>();

        public string RoomId => roomId;
        public GameObject RoomRoot => roomRoot;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public GameObject[] AdditionalActiveObjects => additionalActiveObjects;
    }

    [DisallowMultipleComponent]
    public sealed class PHSFeatureInspectionRoomController : MonoBehaviour
    {
        [Header("Hub")]
        [SerializeField] private Transform hubSpawnPoint;

        [Header("Single Active Room")]
        [SerializeField] private PHSFeatureInspectionRoomEntry[] rooms =
            Array.Empty<PHSFeatureInspectionRoomEntry>();

        public int ActiveRoomIndex { get; private set; } = -1;
        public int RoomCount => rooms?.Length ?? 0;

        private void Awake()
        {
            ApplyRoomVisibility(-1);
        }

        public bool TryOpenRoom(int roomIndex)
        {
            if (!CanControlRooms()
                || rooms == null
                || roomIndex < 0
                || roomIndex >= rooms.Length)
            {
                return false;
            }

            var room = rooms[roomIndex];
            if (room == null || room.RoomRoot == null || room.PlayerSpawnPoint == null)
            {
                Debug.LogError(
                    $"PHS_FEATURE_ROOM_OPEN_FAILED reason=room_reference_missing index={roomIndex}",
                    this);
                return false;
            }

            CleanupRuntimeState();
            ApplyRoomVisibility(roomIndex);
            TeleportHostPlayer(room.PlayerSpawnPoint);
            ActiveRoomIndex = roomIndex;
            Debug.Log(
                $"PHS_FEATURE_ROOM_OPENED index={roomIndex} room={room.RoomId}",
                this);
            return true;
        }

        public bool TryReturnToHub()
        {
            if (!CanControlRooms() || hubSpawnPoint == null)
            {
                return false;
            }

            CleanupRuntimeState();
            ApplyRoomVisibility(-1);
            TeleportHostPlayer(hubSpawnPoint);
            ActiveRoomIndex = -1;
            Debug.Log("PHS_FEATURE_ROOM_HUB_RETURNED", this);
            return true;
        }

        private static bool CanControlRooms()
        {
            var networkManager = NetworkManager.Singleton;
            return networkManager != null
                && networkManager.IsListening
                && networkManager.IsServer;
        }

        private void ApplyRoomVisibility(int activeIndex)
        {
            if (rooms == null)
            {
                return;
            }

            for (var index = 0; index < rooms.Length; index++)
            {
                var room = rooms[index];
                var roomRoot = room?.RoomRoot;
                if (roomRoot != null)
                {
                    roomRoot.SetActive(index == activeIndex);
                }

                if (room?.AdditionalActiveObjects == null)
                {
                    continue;
                }

                foreach (var additionalObject in room.AdditionalActiveObjects)
                {
                    if (additionalObject != null)
                    {
                        additionalObject.SetActive(index == activeIndex);
                    }
                }
            }
        }

        private static void CleanupRuntimeState()
        {
            NetworkEventCoordinator.Instance?.TryTerminateAllServer();
            PHSNetworkShipAccidentCoordinator.Instance?.TryTerminateAllServer(
                "feature_inspection_room_switch",
                out _);

            ResetShipSystems();
            ClearHeldItems();
            DespawnRuntimeDroppedItems();
        }

        private static void ResetShipSystems()
        {
            var shipSystems = NetworkRunSessionRoot.Instance?.ShipSystems;
            if (shipSystems == null)
            {
                return;
            }

            foreach (var moduleId in new[]
                     {
                         NetworkShipModuleId.Power,
                         NetworkShipModuleId.Gravity,
                         NetworkShipModuleId.LifeSupport,
                         NetworkShipModuleId.Engine
                     })
            {
                shipSystems.TryRepairModule(moduleId, 1000, out _);
            }

            if (!shipSystems.IsPowerEnabled)
            {
                shipSystems.TryRestorePowerWithBattery(out _);
            }

            if (!shipSystems.IsGravityEnabled)
            {
                shipSystems.TryRestoreGravityAfterRepair(out _);
            }

            if (shipSystems.CurrentShipHp < shipSystems.MaximumShipHp)
            {
                shipSystems.TryRestoreShipDurabilityAtDock(
                    shipSystems.MaximumShipHp - shipSystems.CurrentShipHp,
                    out _);
            }
        }

        private static void ClearHeldItems()
        {
            var itemRecords = FindObjectsByType<NetworkPlayerItemRecord>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var itemRecord in itemRecords)
            {
                if (itemRecord == null
                    || !itemRecord.IsSpawned
                    || string.IsNullOrEmpty(itemRecord.HeldItemId))
                {
                    continue;
                }

                itemRecord.TryConsumeHeldItemServer(
                    itemRecord.HeldItemId,
                    itemRecord.Revision);
            }
        }

        private static void DespawnRuntimeDroppedItems()
        {
            var itemObjects = FindObjectsByType<UtilityItemObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var itemObject in itemObjects)
            {
                var networkObject = itemObject == null
                    ? null
                    : itemObject.GetComponent<NetworkObject>();
                if (networkObject != null
                    && networkObject.IsSpawned
                    && networkObject.IsSceneObject == false)
                {
                    networkObject.Despawn(true);
                }
            }
        }

        private static void TeleportHostPlayer(Transform target)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null
                || !networkManager.ConnectedClients.TryGetValue(
                    networkManager.LocalClientId,
                    out var client)
                || client.PlayerObject == null)
            {
                return;
            }

            client.PlayerObject.transform.SetPositionAndRotation(
                target.position,
                target.rotation);
        }
    }
}
