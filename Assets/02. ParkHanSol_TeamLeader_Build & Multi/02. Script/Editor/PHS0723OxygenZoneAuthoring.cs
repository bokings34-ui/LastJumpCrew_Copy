#if UNITY_EDITOR
using System;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer.Events;
using SM;
using UnityEditor;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.EditorTools
{
    public static class PHS0723OxygenZoneAuthoring
    {
        private const string RuntimePrefabPath =
            "Assets/01. MainGame/02. Final_Prefab/01. Prefab_ParkHanSol_TeamLeader/Prefab/Integration0716/PHS_EventRuntimeSystem.prefab";
        private const string ZoneObjectName =
            "PHS_OxygenDeprivationZone";
        private const string RepairPointName =
            "RepairPoint";
        private const string NoPlayerInteractLayerName =
            "NoPlayerInteract";
        private const string PlayerLayerName = "Player";

        [MenuItem("Tools/ParkHanSol/Author 0723 Oxygen Zones")]
        public static void AuthorRuntimePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            try
            {
                var rooms = root
                    .GetComponentsInChildren<ShipRoom>(true)
                    .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                    .ToArray();
                if (rooms.Length == 0)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_AUTHORING_FAILED reason=rooms_missing");
                }

                foreach (var room in rooms)
                {
                    AuthorRoom(room);
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    RuntimePrefabPath,
                    out var success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_AUTHORING_FAILED reason=prefab_save_failed");
                }

                AssetDatabase.SaveAssets();
                Debug.Log(
                    $"PHS_OXYGEN_AUTHORING_OK prefab={RuntimePrefabPath} " +
                    $"rooms={rooms.Length} zones={rooms.Length}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/ParkHanSol/Validate 0723 Oxygen Zones")]
        public static void ValidateRuntimePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(RuntimePrefabPath);
            try
            {
                var rooms = root
                    .GetComponentsInChildren<ShipRoom>(true)
                    .OrderBy(room => room.RoomId, StringComparer.Ordinal)
                    .ToArray();
                if (rooms.Length == 0)
                {
                    throw new InvalidOperationException(
                        "PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                        "reason=rooms_missing");
                }

                foreach (var room in rooms)
                {
                    var providers = room
                        .GetComponents<PHSOxygenLeakZoneProvider>();
                    if (providers.Length != 1)
                    {
                        throw new InvalidOperationException(
                            $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                            $"reason=provider_count room={room.RoomId} " +
                            $"count={providers.Length}");
                    }

                    if (!providers[0].TryValidate(out var reason))
                    {
                        throw new InvalidOperationException(
                            $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                            $"reason={reason} room={room.RoomId}");
                    }

                    var roomZones = room
                        .GetComponentsInChildren<PHSOxygenDeprivationZone>(true)
                        .Where(zone => zone.GetComponentInParent<ShipRoom>() == room)
                        .ToArray();
                    if (roomZones.Length != 1)
                    {
                        throw new InvalidOperationException(
                            $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                            $"reason=zone_count room={room.RoomId} " +
                            $"count={roomZones.Length}");
                    }

                    var expectedLayer = LayerMask.NameToLayer(
                        NoPlayerInteractLayerName);
                    if (expectedLayer < 0
                        || roomZones[0].gameObject.layer != expectedLayer)
                    {
                        throw new InvalidOperationException(
                            $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                            $"reason=interaction_layer_invalid room={room.RoomId}");
                    }

                    var expectedPlayerLayer = LayerMask.NameToLayer(
                        PlayerLayerName);
                    var expectedPlayerMask = expectedPlayerLayer < 0
                        ? 0
                        : 1 << expectedPlayerLayer;
                    var zoneSerialized = new SerializedObject(roomZones[0]);
                    var playerLayers = zoneSerialized.FindProperty(
                        "playerLayers");
                    if (expectedPlayerMask == 0
                        || playerLayers == null
                        || playerLayers.intValue != expectedPlayerMask)
                    {
                        throw new InvalidOperationException(
                            $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                            $"reason=player_layer_mask_invalid room={room.RoomId}");
                    }
                }

                var totalZoneCount = root
                    .GetComponentsInChildren<PHSOxygenDeprivationZone>(true)
                    .Length;
                if (totalZoneCount != rooms.Length)
                {
                    throw new InvalidOperationException(
                        $"PHS_OXYGEN_PREFAB_VALIDATION_FAILED " +
                        $"reason=total_zone_count expected={rooms.Length} " +
                        $"actual={totalZoneCount}");
                }

                Debug.Log(
                    $"PHS_OXYGEN_PREFAB_VALIDATION_OK " +
                    $"prefab={RuntimePrefabPath} rooms={rooms.Length} " +
                    $"providers={rooms.Length} zones={totalZoneCount}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AuthorRoom(ShipRoom room)
        {
            if (string.IsNullOrWhiteSpace(room.RoomId))
            {
                throw new InvalidOperationException(
                    $"PHS_OXYGEN_AUTHORING_FAILED " +
                    $"reason=room_id_missing room={room.name}");
            }

            var points = room.FireSpawnPoints
                .Where(point => point != null)
                .ToArray();
            if (points.Length < 2)
            {
                throw new InvalidOperationException(
                    $"PHS_OXYGEN_AUTHORING_FAILED " +
                    $"reason=spawn_points_insufficient room={room.RoomId} " +
                    $"count={points.Length}");
            }

            var providers = room
                .GetComponents<PHSOxygenLeakZoneProvider>();
            if (providers.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_OXYGEN_AUTHORING_FAILED " +
                    $"reason=provider_duplicate room={room.RoomId}");
            }

            var provider = providers.Length == 1
                ? providers[0]
                : room.gameObject.AddComponent<PHSOxygenLeakZoneProvider>();
            var zones = room
                .GetComponentsInChildren<PHSOxygenDeprivationZone>(true)
                .Where(zone => zone.GetComponentInParent<ShipRoom>() == room)
                .ToArray();
            if (zones.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_OXYGEN_AUTHORING_FAILED " +
                    $"reason=zone_duplicate room={room.RoomId}");
            }

            var zone = zones.Length == 1
                ? zones[0]
                : CreateZone(room.transform);
            ConfigureZone(room, points, provider, zone);
        }

        private static PHSOxygenDeprivationZone CreateZone(
            Transform roomTransform)
        {
            var zoneObject = new GameObject(ZoneObjectName);
            zoneObject.transform.SetParent(roomTransform, false);
            zoneObject.transform.localRotation = Quaternion.identity;
            zoneObject.transform.localScale = Vector3.one;
            return zoneObject.AddComponent<PHSOxygenDeprivationZone>();
        }

        private static void ConfigureZone(
            ShipRoom room,
            Transform[] points,
            PHSOxygenLeakZoneProvider provider,
            PHSOxygenDeprivationZone zone)
        {
            var roomTransform = room.transform;
            var pointBounds = new Bounds(
                roomTransform.InverseTransformPoint(points[0].position),
                Vector3.zero);
            for (var index = 1; index < points.Length; index++)
            {
                pointBounds.Encapsulate(
                    roomTransform.InverseTransformPoint(
                        points[index].position));
            }

            var zoneTransform = zone.transform;
            var noPlayerInteractLayer = LayerMask.NameToLayer(
                NoPlayerInteractLayerName);
            if (noPlayerInteractLayer < 0)
            {
                throw new InvalidOperationException(
                    "PHS_OXYGEN_AUTHORING_FAILED " +
                    "reason=no_player_interact_layer_missing");
            }

            var playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            if (playerLayer < 0)
            {
                throw new InvalidOperationException(
                    "PHS_OXYGEN_AUTHORING_FAILED " +
                    "reason=player_layer_missing");
            }

            zone.gameObject.layer = noPlayerInteractLayer;
            zoneTransform.localPosition = new Vector3(
                pointBounds.center.x,
                2f,
                pointBounds.center.z);
            zoneTransform.localRotation = Quaternion.identity;
            zoneTransform.localScale = Vector3.one;

            var zoneBounds = zone.GetComponent<BoxCollider>();
            if (zoneBounds == null)
            {
                throw new InvalidOperationException(
                    $"PHS_OXYGEN_AUTHORING_FAILED " +
                    $"reason=zone_collider_missing room={room.RoomId}");
            }

            zoneBounds.isTrigger = true;
            zoneBounds.center = Vector3.zero;
            zoneBounds.size = new Vector3(
                Mathf.Max(6f, pointBounds.size.x + 3f),
                4f,
                Mathf.Max(6f, pointBounds.size.z + 3f));

            var repairPoint = zoneTransform.Find(RepairPointName);
            if (repairPoint == null)
            {
                var repairObject = new GameObject(RepairPointName);
                repairPoint = repairObject.transform;
                repairPoint.SetParent(zoneTransform, false);
            }

            repairPoint.position = points[0].position + Vector3.up * 0.75f;
            repairPoint.localRotation = Quaternion.identity;
            repairPoint.localScale = Vector3.one;

            var normalizedRoomId = room.RoomId
                .Trim()
                .ToLowerInvariant()
                .Replace(' ', '_');
            var zoneSerialized = new SerializedObject(zone);
            zoneSerialized.FindProperty("zoneId").stringValue =
                $"oxygen_{normalizedRoomId}";
            zoneSerialized.FindProperty("zoneBounds").objectReferenceValue =
                zoneBounds;
            zoneSerialized.FindProperty("repairPoint").objectReferenceValue =
                repairPoint;
            zoneSerialized.FindProperty("activeOnEnable").boolValue = false;
            zoneSerialized.FindProperty("playerLayers").intValue =
                1 << playerLayer;
            zoneSerialized.ApplyModifiedPropertiesWithoutUndo();

            var providerSerialized = new SerializedObject(provider);
            var zoneReferences = providerSerialized.FindProperty("zones");
            zoneReferences.arraySize = 1;
            zoneReferences
                .GetArrayElementAtIndex(0)
                .objectReferenceValue = zone;
            providerSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(provider);
            EditorUtility.SetDirty(zone);
            EditorUtility.SetDirty(zoneBounds);
        }
    }
}
#endif
