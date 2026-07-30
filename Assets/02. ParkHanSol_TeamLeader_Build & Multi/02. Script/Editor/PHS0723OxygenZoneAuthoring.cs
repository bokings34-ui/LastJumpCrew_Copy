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
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/LegacyMigrated/Prefab/Integration0716/PHS_EventRuntimeSystem.prefab";
        private const string NoPlayerInteractLayerName =
            "NoPlayerInteract";
        private const string PlayerLayerName = "Player";

        [MenuItem("Tools/ParkHanSol/Author 0723 Oxygen Zones")]
        public static void AuthorRuntimePrefab()
        {
            ValidateRuntimePrefab();
            Debug.Log(
                "PHS_OXYGEN_AUTHORING_SKIPPED " +
                "reason=manual_pipe_site_contract validation=passed");
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

    }
}
#endif
