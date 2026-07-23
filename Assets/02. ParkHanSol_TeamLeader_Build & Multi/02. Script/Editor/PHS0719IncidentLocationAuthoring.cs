using System;
using System.Collections.Generic;
using System.Linq;
using LastJumpCrew.ParkHanSol.Multiplayer;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using SM;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Editor
{
    /// <summary>
    /// Converts the existing 0715 room fire-point clouds and ship-accident
    /// anchors into the explicit incident-location authoring contract.
    /// The legacy source objects remain untouched except for the added
    /// PHSIncidentLocationAnchor components.
    /// </summary>
    public static class PHS0719IncidentLocationAuthoring
    {
        private const string MapScenePath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/01. Scene/0715/PHS_Map_ver1.unity";
        private const string RuntimeRootName = "PHS_Map_Runtime";
        private const string LayoutRootName = "PHS_IncidentLayout";
        private const float ZoneHeight = 2.5f;
        private const float MinimumHorizontalPadding = 0.25f;

        private static readonly ZoneSpec[] ZoneSpecs =
        {
            new ZoneSpec("room_a", "Room A", 2, 2, 100),
            new ZoneSpec("room_b", "Room B", 3, 2, 200),
            new ZoneSpec("room_c", "Room C", 2, 2, 300),
            new ZoneSpec(
                "central_corridor",
                "Central Corridor",
                2,
                4,
                400)
        };

        private static readonly RouteSpec[] RouteSpecs =
        {
            new RouteSpec(
                "fire_surface_ignition",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Fire,
                1),
            new RouteSpec(
                "device_power_fault",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Power,
                2),
            new RouteSpec(
                "device_engine_fault",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Device,
                3),
            new RouteSpec(
                "hull_impact",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Hull,
                4),
            new RouteSpec(
                "pipe_steam_fault",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Steam,
                5),
            new RouteSpec(
                "life_support_oxygen_fault",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Oxygen,
                6),
            new RouteSpec(
                "gravity_generator_fault",
                NetworkRunIncidentChannel.Internal,
                NetworkRunIncidentPayloadKind.ShipAccident,
                NetworkRunIncidentFamily.Gravity,
                7),
            new RouteSpec(
                "enemy_scout_ingress",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.Enemy,
                7201),
            new RouteSpec(
                "meteor_hull_collision",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.Meteor,
                7202),
            new RouteSpec(
                "emp_device_surge",
                NetworkRunIncidentChannel.External,
                NetworkRunIncidentPayloadKind.EventManagerEvent,
                NetworkRunIncidentFamily.EMP,
                7203)
        };

        private static readonly PHSShipAccidentId[] InternalAccidentIds =
        {
            PHSShipAccidentId.Fire,
            PHSShipAccidentId.PowerFailure,
            PHSShipAccidentId.DeviceFailure,
            PHSShipAccidentId.HullBreach,
            PHSShipAccidentId.SteamLeak,
            PHSShipAccidentId.OxygenFailure,
            PHSShipAccidentId.GravityGeneratorFailure
        };

        private const IncidentLocationCapability ExternalRoomCapabilities =
            IncidentLocationCapability.Presentation
            | IncidentLocationCapability.HazardArea
            | IncidentLocationCapability.Alarm;
        private const IncidentLocationCapability FireSurfaceCapabilities =
            IncidentLocationCapability.Presentation
            | IncidentLocationCapability.Interaction
            | IncidentLocationCapability.HazardArea
            | IncidentLocationCapability.FirePropagation
            | IncidentLocationCapability.Alarm;
        private const IncidentLocationCapability InternalAccidentCapabilities =
            IncidentLocationCapability.Presentation
            | IncidentLocationCapability.Interaction;

        [MenuItem("Tools/ParkHanSol/Migrate 0719 Incident Locations")]
        public static void MigrateIncidentLocations()
        {
            ThrowIfAnyLoadedSceneIsDirty("migration");
            var originalActiveScene = SceneManager.GetActiveScene();
            var mapScene = SceneManager.GetSceneByPath(MapScenePath);
            var openedMapScene = false;

            try
            {
                if (!mapScene.IsValid() || !mapScene.isLoaded)
                {
                    mapScene = EditorSceneManager.OpenScene(
                        MapScenePath,
                        OpenSceneMode.Additive);
                    openedMapScene = true;
                }

                RequireLoadedMapScene(mapScene);
                SceneManager.SetActiveScene(mapScene);
                var result = AuthorMapScene(mapScene);

                Physics.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(mapScene);
                if (!ValidateLoadedMapScene(mapScene, out var validationReason))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=pre_save_validation_failed:{validationReason}");
                }

                if (!EditorSceneManager.SaveScene(mapScene))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=map_scene_save_failed path={MapScenePath}");
                }

                Debug.Log(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_OK " +
                    $"zones={result.ZoneCount} locations={result.LocationCount} " +
                    $"fireZones={result.FireZoneCount} " +
                    $"firePatches={result.FirePatchCount} routes={RouteSpecs.Length} " +
                    $"scene={MapScenePath}");
            }
            finally
            {
                RestoreActiveScene(originalActiveScene);
                if (openedMapScene
                    && mapScene.IsValid()
                    && mapScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(mapScene, true);
                }
            }
        }

        /// <summary>
        /// Read-only validation entry point for the integration validator.
        /// It preserves the active scene and closes the map when this method
        /// had to load it additively.
        /// </summary>
        public static bool ValidateAuthoredScene(out string reason)
        {
            if (!TryGetDirtyLoadedScenes(out var dirtyScenes))
            {
                reason = $"loaded_scene_dirty:{string.Join(",", dirtyScenes)}";
                return false;
            }

            var originalActiveScene = SceneManager.GetActiveScene();
            var mapScene = SceneManager.GetSceneByPath(MapScenePath);
            var openedMapScene = false;
            try
            {
                if (!mapScene.IsValid() || !mapScene.isLoaded)
                {
                    mapScene = EditorSceneManager.OpenScene(
                        MapScenePath,
                        OpenSceneMode.Additive);
                    openedMapScene = true;
                }

                if (!mapScene.IsValid() || !mapScene.isLoaded)
                {
                    reason = $"map_scene_open_failed:{MapScenePath}";
                    return false;
                }

                Physics.SyncTransforms();
                return ValidateLoadedMapScene(mapScene, out reason);
            }
            catch (Exception exception)
            {
                reason =
                    $"validation_exception:{exception.GetType().Name}:" +
                    $"{exception.Message}";
                return false;
            }
            finally
            {
                RestoreActiveScene(originalActiveScene);
                if (openedMapScene
                    && mapScene.IsValid()
                    && mapScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(mapScene, true);
                }
            }
        }

        private static AuthoringResult AuthorMapScene(Scene mapScene)
        {
            var firePresentationPrefab =
                PHS0720FirePresentationAuthoring
                    .EnsurePresentationPrefab();
            var runtimeRoot = FindUniqueNamedTransform(
                mapScene,
                RuntimeRootName);
            var rooms = ResolveSourceRooms(mapScene);
            var accidentAnchors = ResolveAccidentAnchors(mapScene);
            var canonicalAccidentAnchors =
                ResolveCanonicalAccidentAnchors(accidentAnchors);
            var accidentLocationIds = CreateAccidentLocationIds(
                accidentAnchors,
                canonicalAccidentAnchors);
            var fireAccidentAnchors = accidentAnchors
                .Where(
                    pair => pair.Value == PHSShipAccidentId.Fire)
                .Select(pair => pair.Key)
                .ToArray();

            var layoutRoot = EnsureDirectChild(
                runtimeRoot,
                LayoutRootName);
            ResetLocalTransform(layoutRoot);
            var layout = EnsureSingleComponent<PHSShipIncidentLayout>(
                layoutRoot.gameObject);

            var authoredRooms = ZoneSpecs
                .Select(spec => new AuthoredRoom(
                    spec,
                    rooms[spec.ZoneId],
                    CalculateRoomWorldBounds(rooms[spec.ZoneId])))
                .ToArray();
            var zonesById =
                new Dictionary<string, PHSShipIncidentZone>(
                    StringComparer.Ordinal);
            var zoneCollidersById =
                new Dictionary<string, BoxCollider>(
                    StringComparer.Ordinal);
            var locations = new List<PHSIncidentLocationAnchor>();
            var fireZones = new List<PHSFireZone>();
            var firePatchCount = 0;

            foreach (var authoredRoom in authoredRooms)
            {
                var zone = AuthorZone(
                    layoutRoot,
                    authoredRoom,
                    out var zoneCollider);
                zonesById.Add(authoredRoom.Spec.ZoneId, zone);
                zoneCollidersById.Add(
                    authoredRoom.Spec.ZoneId,
                    zoneCollider);
            }

            ConfigureZoneAdjacency(zonesById);
            Physics.SyncTransforms();

            foreach (var authoredRoom in authoredRooms)
            {
                var zone = zonesById[authoredRoom.Spec.ZoneId];
                var zoneCollider =
                    zoneCollidersById[authoredRoom.Spec.ZoneId];
                locations.Add(
                    AuthorExternalRoomLocation(
                        zone,
                        zoneCollider,
                        authoredRoom.Room));

                var fireZone = AuthorFireZone(
                    zone,
                    zoneCollider,
                    SelectNearestAccidentAnchor(
                        zone,
                        fireAccidentAnchors),
                    authoredRoom,
                    firePresentationPrefab,
                    out var fireLocation);
                fireZones.Add(fireZone);
                locations.Add(fireLocation);
                firePatchCount += fireZone.Patches.Count;
            }

            foreach (var pair in accidentAnchors
                         .OrderBy(
                             entry => (ushort)entry.Value)
                         .ThenBy(
                             entry => entry.Key.AnchorId,
                             StringComparer.Ordinal))
            {
                var incidentZone = SelectNearestZone(
                    pair.Key.transform.position,
                    zonesById.Values);
                locations.Add(
                    AuthorAccidentLocation(
                        pair.Key,
                        pair.Value,
                        incidentZone,
                        accidentLocationIds[pair.Key]));
            }

            ConfigureLayout(
                layout,
                ZoneSpecs.Select(spec => zonesById[spec.ZoneId]).ToArray(),
                locations
                    .OrderBy(
                        location => location.LocationId,
                        StringComparer.Ordinal)
                    .ToArray());
            var accidentCoordinator = ConfigureAccidentCoordinator(
                mapScene,
                accidentAnchors
                    .OrderBy(pair => (ushort)pair.Value)
                    .ThenBy(
                        pair => pair.Key.AnchorId,
                        StringComparer.Ordinal)
                    .Select(pair => pair.Key)
                    .ToArray());
            var fireCoordinator = ConfigureFireCoordinator(
                accidentCoordinator,
                fireZones.ToArray());
            ConfigureConsumer(
                mapScene,
                layout,
                fireCoordinator);
            ConfigureRequestGateway(mapScene, runtimeRoot);

            return new AuthoringResult(
                zonesById.Count,
                locations.Count,
                fireZones.Count,
                firePatchCount);
        }

        private static Dictionary<string, ShipRoom> ResolveSourceRooms(
            Scene mapScene)
        {
            var rooms = FindSceneComponents<ShipRoom>(mapScene);
            if (rooms.Length != ZoneSpecs.Length)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=ship_room_count_invalid " +
                    $"expected={ZoneSpecs.Length} actual={rooms.Length}");
            }

            var resolved =
                new Dictionary<string, ShipRoom>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
                {
                    throw new InvalidOperationException(
                        "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        "reason=ship_room_id_missing");
                }

                var zoneId = ResolveNormalizedZoneId(room);
                if (zoneId == null)
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_room_identity_unknown " +
                        $"room={GetHierarchyPath(room.transform)} " +
                        $"roomId={room.RoomId}");
                }

                if (!resolved.TryAdd(zoneId, room))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_room_identity_ambiguous zone={zoneId}");
                }

                ValidateFirePointCloud(room);
            }

            foreach (var spec in ZoneSpecs)
            {
                if (!resolved.ContainsKey(spec.ZoneId))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_room_missing zone={spec.ZoneId}");
                }
            }

            return resolved;
        }

        private static Dictionary<PHSShipAccidentAnchor, PHSShipAccidentId>
            ResolveAccidentAnchors(Scene mapScene)
        {
            var anchors =
                FindSceneComponents<PHSShipAccidentAnchor>(mapScene);
            if (anchors.Length < InternalAccidentIds.Length)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=ship_accident_anchor_count_insufficient " +
                    $"minimum={InternalAccidentIds.Length} " +
                    $"actual={anchors.Length}");
            }

            var resolved =
                new Dictionary<PHSShipAccidentAnchor, PHSShipAccidentId>();
            var coveredIds = new HashSet<PHSShipAccidentId>();
            var uniqueAnchorIds =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (var anchor in anchors)
            {
                var accidentId = ResolveConfiguredAccidentId(anchor);
                if (!InternalAccidentIds.Contains(accidentId))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_accident_identity_unsupported " +
                        $"accident={accidentId}");
                }

                if (string.IsNullOrWhiteSpace(anchor.AnchorId))
                {
                    throw new InvalidOperationException(
                        "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_accident_anchor_id_missing " +
                        $"object={GetHierarchyPath(anchor.transform)}");
                }

                if (!IncidentStableId.IsValid(anchor.AnchorId))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_accident_anchor_id_invalid " +
                        $"anchor={anchor.AnchorId}");
                }

                if (!uniqueAnchorIds.Add(anchor.AnchorId))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=ship_accident_anchor_id_duplicate " +
                        $"anchor={anchor.AnchorId}");
                }

                resolved.Add(anchor, accidentId);
                coveredIds.Add(accidentId);
            }

            if (!InternalAccidentIds.All(coveredIds.Contains))
            {
                throw new InvalidOperationException(
                    "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    "reason=ship_accident_set_incomplete");
            }

            return resolved;
        }

        private static string BuildAdditionalAccidentLocationId(
            string baseLocationId,
            string anchorId)
        {
            var candidate = $"{baseLocationId}_{anchorId}";
            if (IncidentStableId.IsValid(candidate))
            {
                return candidate;
            }

            var hash = CalculateStableHash(anchorId).ToString("x8");
            var suffix = $"_{hash}";
            var maximumPrefixLength = Math.Max(
                baseLocationId.Length + 2,
                60 - suffix.Length);
            var anchorPrefixLength = Math.Min(
                anchorId.Length,
                maximumPrefixLength - baseLocationId.Length - 1);
            var anchorPrefix = anchorId.Substring(
                0,
                anchorPrefixLength);
            candidate = $"{baseLocationId}_{anchorPrefix}{suffix}";
            if (!IncidentStableId.IsValid(candidate))
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=generated_location_id_invalid " +
                    $"anchor={anchorId}");
            }

            return candidate;
        }

        private static uint CalculateStableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261U;
                foreach (var character in value)
                {
                    hash ^= character;
                    hash *= 16777619U;
                }

                return hash;
            }
        }

        private static Dictionary<PHSShipAccidentId, PHSShipAccidentAnchor>
            ResolveCanonicalAccidentAnchors(
                IReadOnlyDictionary<
                    PHSShipAccidentAnchor,
                    PHSShipAccidentId> accidentAnchors)
        {
            var resolved =
                new Dictionary<
                    PHSShipAccidentId,
                    PHSShipAccidentAnchor>();
            foreach (var accidentId in InternalAccidentIds)
            {
                var baseLocationId =
                    $"internal_{GetAccidentIdToken(accidentId)}";
                var candidates = accidentAnchors
                    .Where(pair => pair.Value == accidentId)
                    .Select(pair => pair.Key)
                    .OrderBy(
                        anchor => anchor.AnchorId,
                        StringComparer.Ordinal)
                    .ThenBy(
                        anchor => GetHierarchyPath(anchor.transform),
                        StringComparer.Ordinal)
                    .ToArray();
                var existingBaseOwners = candidates
                    .Where(
                        anchor => anchor
                            .GetComponents<PHSIncidentLocationAnchor>()
                            .Any(
                                location => location.LocationId
                                    == baseLocationId))
                    .ToArray();
                if (existingBaseOwners.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=canonical_accident_location_ambiguous " +
                        $"location={baseLocationId}");
                }

                resolved.Add(
                    accidentId,
                    existingBaseOwners.Length == 1
                        ? existingBaseOwners[0]
                        : candidates[0]);
            }

            return resolved;
        }

        private static Dictionary<PHSShipAccidentAnchor, string>
            CreateAccidentLocationIds(
                IReadOnlyDictionary<
                    PHSShipAccidentAnchor,
                    PHSShipAccidentId> accidentAnchors,
                IReadOnlyDictionary<
                    PHSShipAccidentId,
                    PHSShipAccidentAnchor> canonicalAnchors)
        {
            var resolved =
                new Dictionary<PHSShipAccidentAnchor, string>();
            var uniqueLocationIds =
                CreateExpectedLocationIds(Array.Empty<string>());
            foreach (var pair in accidentAnchors
                         .OrderBy(entry => (ushort)entry.Value)
                         .ThenBy(
                             entry => entry.Key.AnchorId,
                             StringComparer.Ordinal)
                         .ThenBy(
                             entry => GetHierarchyPath(
                                 entry.Key.transform),
                             StringComparer.Ordinal))
            {
                var baseLocationId =
                    $"internal_{GetAccidentIdToken(pair.Value)}";
                var locationId =
                    canonicalAnchors[pair.Value] == pair.Key
                        ? baseLocationId
                        : BuildAdditionalAccidentLocationId(
                            baseLocationId,
                            pair.Key.AnchorId);
                if (!uniqueLocationIds.Add(locationId))
                {
                    throw new InvalidOperationException(
                        $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                        $"reason=generated_location_id_duplicate " +
                        $"location={locationId} " +
                        $"anchor={pair.Key.AnchorId}");
                }

                resolved.Add(pair.Key, locationId);
            }

            return resolved;
        }

        private static PHSShipIncidentZone AuthorZone(
            Transform layoutRoot,
            AuthoredRoom authoredRoom,
            out BoxCollider zoneCollider)
        {
            var zoneRoot = EnsureDirectChild(
                layoutRoot,
                $"Zone_{authoredRoom.Spec.ZoneId}");
            SetWorldTransform(
                zoneRoot,
                authoredRoom.WorldBounds.center,
                Quaternion.identity);

            zoneCollider = EnsureSingleComponent<BoxCollider>(
                zoneRoot.gameObject);
            ConfigureBoxCollider(
                zoneCollider,
                Vector3.zero,
                authoredRoom.WorldBounds.size);

            var alarmRoot = EnsureDirectChild(zoneRoot, "Alarm");
            ResetLocalTransform(alarmRoot);

            var zone = EnsureSingleComponent<PHSShipIncidentZone>(
                zoneRoot.gameObject);
            var serializedZone = new SerializedObject(zone);
            SetString(serializedZone, "zoneId", authoredRoom.Spec.ZoneId);
            SetString(
                serializedZone,
                "displayName",
                string.IsNullOrWhiteSpace(authoredRoom.Room.RoomId)
                    ? authoredRoom.Spec.DisplayName
                    : authoredRoom.Room.RoomId);
            SetObject(serializedZone, "parentZone", null);
            SetEnum(
                serializedZone,
                "primaryModule",
                (int)NetworkShipModuleId.None);
            SetObject(serializedZone, "zoneBounds", zoneCollider);
            SetObjectArray(
                serializedZone,
                "adjacentZones",
                Array.Empty<PHSShipIncidentZone>());
            SetObject(
                serializedZone,
                "alarmPresentationRoot",
                alarmRoot);
            SetFloat(serializedZone, "baseRiskWeight", 1f);
            SetInt(
                serializedZone,
                "maximumIndependentAccidents",
                2);
            SetFloat(serializedZone, "cooldownSeconds", 5f);
            ApplyAndRecord(serializedZone, zone);
            return zone;
        }

        private static void ConfigureZoneAdjacency(
            IReadOnlyDictionary<string, PHSShipIncidentZone> zonesById)
        {
            var corridor = zonesById["central_corridor"];
            var roomZones = ZoneSpecs
                .Where(spec => spec.ZoneId != "central_corridor")
                .Select(spec => zonesById[spec.ZoneId])
                .ToArray();
            foreach (var roomZone in roomZones)
            {
                var serializedRoom = new SerializedObject(roomZone);
                SetObjectArray(
                    serializedRoom,
                    "adjacentZones",
                    new[] { corridor });
                ApplyAndRecord(serializedRoom, roomZone);
            }

            var serializedCorridor = new SerializedObject(corridor);
            SetObjectArray(
                serializedCorridor,
                "adjacentZones",
                roomZones);
            ApplyAndRecord(serializedCorridor, corridor);
        }

        private static PHSIncidentLocationAnchor AuthorExternalRoomLocation(
            PHSShipIncidentZone zone,
            BoxCollider zoneCollider,
            ShipRoom room)
        {
            var locationRoot = EnsureDirectChild(
                zone.transform,
                "ExternalRoomLocation");
            ResetLocalTransform(locationRoot);
            var presentationRoot = EnsureDirectChild(
                locationRoot,
                "PresentationRoot");
            ResetLocalTransform(presentationRoot);

            var location =
                EnsureSingleComponent<PHSIncidentLocationAnchor>(
                    locationRoot.gameObject);
            ConfigureLocation(
                location,
                $"external_room_{zone.ZoneId}",
                zone,
                IncidentLocationKind.Room,
                IncidentLocationCapability.Presentation
                    | IncidentLocationCapability.HazardArea
                    | IncidentLocationCapability.Alarm,
                NetworkShipModuleId.None,
                false,
                new[] { NetworkRunIncidentChannel.External },
                new[]
                {
                    NetworkRunIncidentFamily.Enemy,
                    NetworkRunIncidentFamily.Meteor,
                    NetworkRunIncidentFamily.EMP
                },
                new[] { 7201, 7202, 7203 },
                presentationRoot,
                zoneCollider,
                room);
            return location;
        }

        private static PHSFireZone AuthorFireZone(
            PHSShipIncidentZone zone,
            BoxCollider zoneCollider,
            PHSShipAccidentAnchor fireAccidentAnchor,
            AuthoredRoom authoredRoom,
            GameObject firePresentationPrefab,
            out PHSIncidentLocationAnchor fireLocation)
        {
            var fireZoneRoot = EnsureDirectChild(
                zone.transform,
                "FireZone");
            ResetLocalTransform(fireZoneRoot);
            var patchesRoot = EnsureDirectChild(
                fireZoneRoot,
                "Patches");
            ResetLocalTransform(patchesRoot);

            RemoveStaleGeneratedPatches(
                patchesRoot,
                authoredRoom.Spec.PatchCount);
            var patches = new PHSFirePatch[authoredRoom.Spec.PatchCount];
            var cellSize = new Vector3(
                authoredRoom.WorldBounds.size.x
                    / authoredRoom.Spec.Columns,
                ZoneHeight,
                authoredRoom.WorldBounds.size.z
                    / authoredRoom.Spec.Rows);
            var floorY =
                authoredRoom.WorldBounds.center.y - (ZoneHeight * 0.5f);
            for (var row = 0;
                 row < authoredRoom.Spec.Rows;
                 row++)
            {
                for (var column = 0;
                     column < authoredRoom.Spec.Columns;
                     column++)
                {
                    var index =
                        (row * authoredRoom.Spec.Columns) + column;
                    var patchRoot = EnsureDirectChild(
                        patchesRoot,
                        $"Patch_{index + 1:00}");
                    patchRoot.gameObject.layer =
                        RequireLayer("Interactable");
                    MarkAndRecord(patchRoot.gameObject);
                    var patchPosition = new Vector3(
                        authoredRoom.WorldBounds.min.x
                            + ((column + 0.5f) * cellSize.x),
                        floorY,
                        authoredRoom.WorldBounds.min.z
                            + ((row + 0.5f) * cellSize.z));
                    SetWorldTransform(
                        patchRoot,
                        patchPosition,
                        Quaternion.identity);

                    var hazardBounds =
                        EnsureSingleComponent<BoxCollider>(
                            patchRoot.gameObject);
                    ConfigureBoxCollider(
                        hazardBounds,
                        new Vector3(
                            0f,
                            ZoneHeight * 0.5f,
                            0f),
                        cellSize);
                    var presentationRoot = EnsureDirectChild(
                        patchRoot,
                        "PresentationRoot");
                    ResetLocalTransform(presentationRoot);
                    var visualSocketA = EnsureDirectChild(
                        presentationRoot,
                        "VisualSocket_01");
                    var visualSocketB = EnsureDirectChild(
                        presentationRoot,
                        "VisualSocket_02");
                    var visualSocketC = EnsureDirectChild(
                        presentationRoot,
                        "VisualSocket_03");
                    var visualSocketD = EnsureDirectChild(
                        presentationRoot,
                        "VisualSocket_04");
                    var visualSocketE = EnsureDirectChild(
                        presentationRoot,
                        "VisualSocket_05");
                    visualSocketA.localPosition = new Vector3(
                        -cellSize.x * 0.28f,
                        0f,
                        -cellSize.z * 0.28f);
                    visualSocketB.localPosition = new Vector3(
                        cellSize.x * 0.28f,
                        0f,
                        cellSize.z * 0.28f);
                    visualSocketC.localPosition = Vector3.zero;
                    visualSocketD.localPosition = new Vector3(
                        -cellSize.x * 0.28f,
                        0f,
                        cellSize.z * 0.28f);
                    visualSocketE.localPosition = new Vector3(
                        cellSize.x * 0.28f,
                        0f,
                        -cellSize.z * 0.28f);
                    visualSocketA.localRotation = Quaternion.identity;
                    visualSocketB.localRotation = Quaternion.identity;
                    visualSocketC.localRotation = Quaternion.identity;
                    visualSocketD.localRotation = Quaternion.identity;
                    visualSocketE.localRotation = Quaternion.identity;
                    visualSocketA.localScale = Vector3.one;
                    visualSocketB.localScale = Vector3.one;
                    visualSocketC.localScale = Vector3.one;
                    visualSocketD.localScale = Vector3.one;
                    visualSocketE.localScale = Vector3.one;
                    MarkAndRecord(visualSocketA);
                    MarkAndRecord(visualSocketB);
                    MarkAndRecord(visualSocketC);
                    MarkAndRecord(visualSocketD);
                    MarkAndRecord(visualSocketE);

                    var patch = EnsureSingleComponent<PHSFirePatch>(
                        patchRoot.gameObject);
                    var serializedPatch = new SerializedObject(patch);
                    SetInt(
                        serializedPatch,
                        "patchId",
                        authoredRoom.Spec.PatchIdBase + index + 1);
                    SetObject(
                        serializedPatch,
                        "hazardBounds",
                        hazardBounds);
                    SetObject(
                        serializedPatch,
                        "presentationRoot",
                        presentationRoot);
                    SetFloat(serializedPatch, "flammability", 1f);
                    SetFloat(serializedPatch, "damageMultiplier", 1f);
                    SetObjectArray(
                        serializedPatch,
                        "visualSockets",
                        new[]
                        {
                            visualSocketA,
                            visualSocketB,
                            visualSocketC,
                            visualSocketD,
                            visualSocketE
                        });
                    SetPropertyArraySize(
                        serializedPatch,
                        "neighbors",
                        0);
                    ApplyAndRecord(serializedPatch, patch);

                    var fireLightRoot = EnsureDirectChild(
                        presentationRoot,
                        "FireLight");
                    fireLightRoot.localPosition =
                        new Vector3(0f, 0.45f, 0f);
                    fireLightRoot.localRotation =
                        Quaternion.identity;
                    fireLightRoot.localScale = Vector3.one;
                    var fireLight =
                        EnsureSingleComponent<Light>(
                            fireLightRoot.gameObject);
                    fireLight.type = LightType.Point;
                    fireLight.color =
                        new Color(1f, 0.31f, 0.035f, 1f);
                    fireLight.intensity = 1.65f;
                    fireLight.range = Mathf.Max(
                        3.5f,
                        Mathf.Max(cellSize.x, cellSize.z)
                            * 1.35f);
                    fireLight.shadows = LightShadows.None;
                    fireLight.enabled = false;
                    MarkAndRecord(fireLightRoot);
                    MarkAndRecord(fireLight);

                    var runtimeTarget =
                        EnsureSingleComponent<
                            PHSFirePatchRuntimeTarget>(
                            patchRoot.gameObject);
                    var serializedRuntimeTarget =
                        new SerializedObject(runtimeTarget);
                    SetObject(
                        serializedRuntimeTarget,
                        "patch",
                        patch);
                    SetObject(
                        serializedRuntimeTarget,
                        "fireLight",
                        fireLight);
                    ApplyAndRecord(
                        serializedRuntimeTarget,
                        runtimeTarget);
                    patches[index] = patch;
                }
            }

            ConfigurePatchLinks(
                patches,
                authoredRoom.Spec.Columns,
                authoredRoom.Spec.Rows);

            var fireZone = EnsureSingleComponent<PHSFireZone>(
                fireZoneRoot.gameObject);
            var serializedFireZone = new SerializedObject(fireZone);
            SetObject(serializedFireZone, "incidentZone", zone);
            SetObject(
                serializedFireZone,
                "fireAccidentAnchor",
                fireAccidentAnchor);
            SetObjectArray(serializedFireZone, "patches", patches);
            SetInt(
                serializedFireZone,
                "maximumBurningPatches",
                Math.Min(8, patches.Length));
            SetInt(serializedFireZone, "initialHeat", 85);
            SetInt(serializedFireZone, "maximumHeat", 200);
            SetInt(
                serializedFireZone,
                "minimumHeatGrowthPerTick",
                12);
            SetInt(
                serializedFireZone,
                "maximumHeatGrowthPerTick",
                22);
            SetFloat(serializedFireZone, "spreadTickSeconds", 1.35f);
            SetInt(serializedFireZone, "spreadAttemptsPerTick", 3);
            SetInt(
                serializedFireZone,
                "maximumNewIgnitionsPerTick",
                2);
            SetFloat(serializedFireZone, "baseSpreadChance", 0.52f);
            SetFloat(serializedFireZone, "damageTickSeconds", 1f);
            SetInt(serializedFireZone, "baseDamagePerTick", 2);
            SetInt(serializedFireZone, "damageableLayers", ~0);
            SetFloat(
                serializedFireZone,
                "containmentGraceSeconds",
                2.5f);
            SetObject(
                serializedFireZone,
                "patchPresentationPrefab",
                firePresentationPrefab);
            ApplyAndRecord(serializedFireZone, fireZone);

            fireLocation =
                EnsureSingleComponent<PHSIncidentLocationAnchor>(
                    fireZoneRoot.gameObject);
            ConfigureLocation(
                fireLocation,
                $"fire_surface_{zone.ZoneId}",
                zone,
                IncidentLocationKind.FireSurface,
                IncidentLocationCapability.Presentation
                    | IncidentLocationCapability.Interaction
                    | IncidentLocationCapability.HazardArea
                    | IncidentLocationCapability.FirePropagation
                    | IncidentLocationCapability.Alarm,
                fireAccidentAnchor.ModuleId,
                false,
                new[] { NetworkRunIncidentChannel.Internal },
                new[] { NetworkRunIncidentFamily.Fire },
                new[] { (int)PHSShipAccidentId.Fire },
                fireZoneRoot,
                zoneCollider,
                fireAccidentAnchor);
            return fireZone;
        }

        private static void ConfigurePatchLinks(
            IReadOnlyList<PHSFirePatch> patches,
            int columns,
            int rows)
        {
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var index = (row * columns) + column;
                    var targets = new List<PHSFirePatch>(4);
                    if (column > 0)
                    {
                        targets.Add(patches[index - 1]);
                    }

                    if (column + 1 < columns)
                    {
                        targets.Add(patches[index + 1]);
                    }

                    if (row > 0)
                    {
                        targets.Add(patches[index - columns]);
                    }

                    if (row + 1 < rows)
                    {
                        targets.Add(patches[index + columns]);
                    }

                    targets.Sort(
                        (left, right) =>
                            left.PatchId.CompareTo(right.PatchId));
                    var serializedPatch =
                        new SerializedObject(patches[index]);
                    var links = RequireProperty(
                        serializedPatch,
                        "neighbors");
                    links.arraySize = targets.Count;
                    for (var targetIndex = 0;
                         targetIndex < targets.Count;
                         targetIndex++)
                    {
                        var link =
                            links.GetArrayElementAtIndex(targetIndex);
                        RequireRelativeProperty(link, "target")
                            .objectReferenceValue = targets[targetIndex];
                        RequireRelativeProperty(link, "spreadWeight")
                            .floatValue = 1f;
                        RequireRelativeProperty(
                                link,
                                "minimumSourceIntensity")
                            .intValue = 2;
                        RequireRelativeProperty(link, "oneWay")
                            .boolValue = false;
                    }

                    ApplyAndRecord(
                        serializedPatch,
                        patches[index]);
                }
            }
        }

        private static PHSIncidentLocationAnchor AuthorAccidentLocation(
            PHSShipAccidentAnchor accidentAnchor,
            PHSShipAccidentId accidentId,
            PHSShipIncidentZone zone,
            string locationId)
        {
            var presentationRoot = RequireObjectReference<Transform>(
                new SerializedObject(accidentAnchor),
                "presentationRoot",
                $"ship_accident_presentation_root_missing:" +
                $"{accidentAnchor.AnchorId}");
            var location =
                EnsureSingleComponent<PHSIncidentLocationAnchor>(
                    accidentAnchor.gameObject);
            ConfigureLocation(
                location,
                locationId,
                zone,
                GetAccidentLocationKind(accidentId),
                IncidentLocationCapability.Presentation
                    | IncidentLocationCapability.Interaction,
                accidentAnchor.ModuleId,
                !zone.Contains(accidentAnchor.transform.position),
                new[] { NetworkRunIncidentChannel.Internal },
                new[] { GetIncidentFamily(accidentId) },
                new[] { (int)accidentId },
                presentationRoot,
                null,
                accidentAnchor);
            return location;
        }

        private static void ConfigureLocation(
            PHSIncidentLocationAnchor location,
            string locationId,
            PHSShipIncidentZone zone,
            IncidentLocationKind kind,
            IncidentLocationCapability capabilities,
            NetworkShipModuleId moduleOverride,
            bool allowOutsideZoneBounds,
            NetworkRunIncidentChannel[] channels,
            NetworkRunIncidentFamily[] families,
            int[] contentIds,
            Transform presentationRoot,
            Collider hazardBounds,
            Component runtimeTarget)
        {
            if (!IsLowerSnakeId(locationId))
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=location_id_not_lower_snake id={locationId}");
            }

            var serializedLocation = new SerializedObject(location);
            SetString(serializedLocation, "locationId", locationId);
            SetObject(serializedLocation, "zone", zone);
            SetEnum(serializedLocation, "kind", (int)kind);
            SetEnum(
                serializedLocation,
                "capabilities",
                (int)capabilities);
            SetEnum(
                serializedLocation,
                "moduleOverride",
                (int)moduleOverride);
            SetBool(
                serializedLocation,
                "allowOutsideZoneBounds",
                allowOutsideZoneBounds);
            SetEnumArray(
                serializedLocation,
                "supportedChannels",
                channels.Select(channel => (int)channel).ToArray());
            SetEnumArray(
                serializedLocation,
                "supportedFamilies",
                families.Select(family => (int)family).ToArray());
            SetIntArray(
                serializedLocation,
                "supportedContentIds",
                contentIds);
            SetFloat(serializedLocation, "selectionWeight", 1f);
            SetFloat(serializedLocation, "cooldownSeconds", 10f);
            SetObject(
                serializedLocation,
                "presentationRoot",
                presentationRoot);
            SetObject(
                serializedLocation,
                "hazardBounds",
                hazardBounds);
            SetObject(
                serializedLocation,
                "runtimeTarget",
                runtimeTarget);
            ApplyAndRecord(serializedLocation, location);
        }

        private static void ConfigureLayout(
            PHSShipIncidentLayout layout,
            PHSShipIncidentZone[] zones,
            PHSIncidentLocationAnchor[] locations)
        {
            var serializedLayout = new SerializedObject(layout);
            SetObjectArray(serializedLayout, "zones", zones);
            SetObjectArray(serializedLayout, "locations", locations);
            SetBool(
                serializedLayout,
                "includeChildAuthoringFallback",
                false);
            ApplyAndRecord(serializedLayout, layout);
        }

        private static void ConfigureConsumer(
            Scene mapScene,
            PHSShipIncidentLayout layout,
            PHSNetworkFireCoordinator fireCoordinator)
        {
            var consumers =
                FindSceneComponents<PHSMapIncidentCommandConsumer>(mapScene);
            if (consumers.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=incident_consumer_count_invalid " +
                    $"actual={consumers.Length}");
            }

            var consumer = consumers[0];
            var serializedConsumer = new SerializedObject(consumer);
            SetObject(serializedConsumer, "incidentLayout", layout);
            SetObject(
                serializedConsumer,
                "fireCoordinator",
                fireCoordinator);
            SetBool(
                serializedConsumer,
                "allowLegacyLocationFallback",
                false);
            ApplyAndRecord(serializedConsumer, consumer);
        }

        private static PHSNetworkShipAccidentCoordinator
            ConfigureAccidentCoordinator(
            Scene mapScene,
            PHSShipAccidentAnchor[] anchors)
        {
            var coordinators =
                FindSceneComponents<PHSNetworkShipAccidentCoordinator>(
                    mapScene);
            if (coordinators.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=ship_accident_coordinator_count_invalid " +
                    $"actual={coordinators.Length}");
            }

            if (anchors == null
                || anchors.Length < InternalAccidentIds.Length)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=ship_accident_registration_count_insufficient " +
                    $"actual={anchors?.Length ?? 0}");
            }

            var coordinator = coordinators[0];
            var serializedCoordinator = new SerializedObject(coordinator);
            SetObjectArray(serializedCoordinator, "anchors", anchors);
            ApplyAndRecord(serializedCoordinator, coordinator);
            return coordinator;
        }

        private static PHSNetworkFireCoordinator
            ConfigureFireCoordinator(
                PHSNetworkShipAccidentCoordinator accidentCoordinator,
                PHSFireZone[] fireZones)
        {
            if (accidentCoordinator == null)
            {
                throw new InvalidOperationException(
                    "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    "reason=ship_accident_coordinator_missing");
            }

            if (fireZones == null || fireZones.Length == 0)
            {
                throw new InvalidOperationException(
                    "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    "reason=fire_zones_missing");
            }

            var owner = accidentCoordinator.gameObject;
            var damageGateway =
                EnsureSingleComponent<PHSFireAreaDamageGateway>(
                    owner);
            var serializedDamageGateway =
                new SerializedObject(damageGateway);
            SetInt(
                serializedDamageGateway,
                "maximumDamagePerTargetPerTick",
                12);
            ApplyAndRecord(
                serializedDamageGateway,
                damageGateway);
            var fireCoordinator =
                EnsureSingleComponent<PHSNetworkFireCoordinator>(
                    owner);
            var serializedFireCoordinator =
                new SerializedObject(fireCoordinator);
            SetObject(
                serializedFireCoordinator,
                "accidentCoordinator",
                accidentCoordinator);
            SetObject(
                serializedFireCoordinator,
                "areaDamageGateway",
                damageGateway);
            SetObjectArray(
                serializedFireCoordinator,
                "fireZones",
                fireZones);
            SetFloat(
                serializedFireCoordinator,
                "maximumSuppressionDistance",
                7f);
            ApplyAndRecord(
                serializedFireCoordinator,
                fireCoordinator);

            var serializedAccidentCoordinator =
                new SerializedObject(accidentCoordinator);
            SetObject(
                serializedAccidentCoordinator,
                "fireCoordinator",
                fireCoordinator);
            ApplyAndRecord(
                serializedAccidentCoordinator,
                accidentCoordinator);
            return fireCoordinator;
        }

        private static void ConfigureRequestGateway(
            Scene mapScene,
            Transform runtimeRoot)
        {
            var gateways =
                FindSceneComponents<PHSIncidentRequestGateway>(mapScene);
            if (gateways.Length > 1
                || (gateways.Length == 1
                    && gateways[0].gameObject != runtimeRoot.gameObject))
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=request_gateway_owner_invalid " +
                    $"count={gateways.Length}");
            }

            var gateway = gateways.Length == 1
                ? gateways[0]
                : runtimeRoot.gameObject
                    .AddComponent<PHSIncidentRequestGateway>();
            var serializedGateway = new SerializedObject(gateway);
            var routes = RequireProperty(serializedGateway, "routes");
            routes.arraySize = RouteSpecs.Length;
            for (var index = 0;
                 index < RouteSpecs.Length;
                 index++)
            {
                var spec = RouteSpecs[index];
                var route = routes.GetArrayElementAtIndex(index);
                RequireRelativeProperty(route, "sourceId").stringValue =
                    spec.SourceId;
                RequireRelativeProperty(route, "channel").intValue =
                    (int)spec.Channel;
                RequireRelativeProperty(route, "payloadKind").intValue =
                    (int)spec.PayloadKind;
                RequireRelativeProperty(route, "incidentFamily").intValue =
                    (int)spec.Family;
                RequireRelativeProperty(route, "contentId").intValue =
                    spec.ContentId;
                RequireRelativeProperty(route, "sourceKind").intValue =
                    (int)NetworkRunIncidentSourceKind.Device;
                RequireRelativeProperty(route, "pressureCost").intValue = 1;
                RequireRelativeProperty(
                        route,
                        "warpChargeMultiplier")
                    .floatValue = 1f;
                RequireRelativeProperty(route, "requiresTarget").boolValue =
                    true;
                RequireRelativeProperty(route, "cooldownSeconds").floatValue =
                    1f;
            }

            ApplyAndRecord(serializedGateway, gateway);
        }

        private static bool ValidateLoadedMapScene(
            Scene mapScene,
            out string reason)
        {
            if (!mapScene.IsValid() || !mapScene.isLoaded)
            {
                reason = "map_scene_not_loaded";
                return false;
            }

            var runtimeRoots =
                FindNamedTransforms(mapScene, RuntimeRootName);
            if (runtimeRoots.Length != 1)
            {
                reason =
                    $"runtime_root_count_invalid:{runtimeRoots.Length}";
                return false;
            }

            var layoutRoots = FindDirectChildren(
                runtimeRoots[0],
                LayoutRootName);
            if (layoutRoots.Length != 1)
            {
                reason =
                    $"layout_root_count_invalid:{layoutRoots.Length}";
                return false;
            }

            var layouts =
                layoutRoots[0].GetComponents<PHSShipIncidentLayout>();
            if (layouts.Length != 1)
            {
                reason =
                    $"layout_component_count_invalid:{layouts.Length}";
                return false;
            }

            var layout = layouts[0];
            var serializedLayout = new SerializedObject(layout);
            var childFallback = RequireProperty(
                serializedLayout,
                "includeChildAuthoringFallback");
            if (childFallback.boolValue)
            {
                reason = "layout_child_fallback_must_be_false";
                return false;
            }

            if (layout.Zones.Count != ZoneSpecs.Length)
            {
                reason =
                    $"zone_count_invalid:{layout.Zones.Count}";
                return false;
            }

            var zoneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in layout.Zones)
            {
                if (zone == null
                    || !IsLowerSnakeId(zone.ZoneId)
                    || !zoneIds.Add(zone.ZoneId))
                {
                    reason =
                        $"zone_id_invalid_or_duplicate:{zone?.ZoneId}";
                    return false;
                }
            }

            if (!ZoneSpecs.All(spec => zoneIds.Contains(spec.ZoneId)))
            {
                reason = "expected_zone_set_missing";
                return false;
            }

            Dictionary<PHSShipAccidentAnchor, PHSShipAccidentId>
                accidentAnchorTypes;
            Dictionary<PHSShipAccidentId, PHSShipAccidentAnchor>
                canonicalAccidentAnchors;
            Dictionary<PHSShipAccidentAnchor, string>
                accidentLocationIds;
            PHSShipAccidentAnchor[] accidentAnchors;
            try
            {
                accidentAnchorTypes = ResolveAccidentAnchors(mapScene);
                canonicalAccidentAnchors =
                    ResolveCanonicalAccidentAnchors(
                        accidentAnchorTypes);
                accidentLocationIds = CreateAccidentLocationIds(
                    accidentAnchorTypes,
                    canonicalAccidentAnchors);
                accidentAnchors = accidentAnchorTypes.Keys.ToArray();
            }
            catch (InvalidOperationException exception)
            {
                reason =
                    $"ship_accident_authoring_invalid:" +
                    $"{exception.Message}";
                return false;
            }

            var expectedLocationIds = CreateExpectedLocationIds(
                accidentLocationIds.Values);
            if (layout.Locations.Count != expectedLocationIds.Count)
            {
                reason =
                    $"location_count_invalid:{layout.Locations.Count}:" +
                    $"{expectedLocationIds.Count}";
                return false;
            }

            var locationsById =
                new Dictionary<string, PHSIncidentLocationAnchor>(
                    StringComparer.Ordinal);
            foreach (var location in layout.Locations)
            {
                if (location == null
                    || !IsLowerSnakeId(location.LocationId))
                {
                    reason =
                        $"location_id_not_lower_snake:" +
                        $"{location?.LocationId}";
                    return false;
                }

                if (!locationsById.TryAdd(
                        location.LocationId,
                        location))
                {
                    reason =
                        $"location_id_duplicate:{location.LocationId}";
                    return false;
                }
            }

            if (!expectedLocationIds.SetEquals(locationsById.Keys))
            {
                var missing = expectedLocationIds
                    .Except(locationsById.Keys, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal);
                var unexpected = locationsById.Keys
                    .Except(expectedLocationIds, StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal);
                reason =
                    $"location_id_set_invalid:" +
                    $"missing={string.Join(",", missing)}:" +
                    $"unexpected={string.Join(",", unexpected)}";
                return false;
            }

            if (!layout.TryValidate(out var layoutReason))
            {
                reason = $"layout_invalid:{layoutReason}";
                return false;
            }

            var fireZones =
                FindLayoutRootComponents<PHSFireZone>(layoutRoots[0]);
            if (fireZones.Length != ZoneSpecs.Length)
            {
                reason =
                    $"fire_zone_count_invalid:{fireZones.Length}";
                return false;
            }

            var fireZonesByZoneId =
                new Dictionary<string, PHSFireZone>(
                    StringComparer.Ordinal);
            var totalPatchCount = 0;
            foreach (var fireZone in fireZones)
            {
                if (fireZone.IncidentZone == null
                    || !fireZonesByZoneId.TryAdd(
                        fireZone.IncidentZone.ZoneId,
                        fireZone))
                {
                    reason = "fire_zone_identity_invalid_or_duplicate";
                    return false;
                }

                if (!fireZone.TryValidate(out var fireReason))
                {
                    reason =
                        $"fire_zone_invalid:" +
                        $"{fireZone.IncidentZone.ZoneId}:{fireReason}";
                    return false;
                }

                if (!ValidatePatchGraph(fireZone, out var graphReason))
                {
                    reason =
                        $"fire_graph_invalid:" +
                        $"{fireZone.IncidentZone.ZoneId}:{graphReason}";
                    return false;
                }

                totalPatchCount += fireZone.Patches.Count;
            }

            foreach (var spec in ZoneSpecs)
            {
                if (!fireZonesByZoneId.TryGetValue(
                        spec.ZoneId,
                        out var fireZone))
                {
                    reason = $"fire_zone_missing:{spec.ZoneId}";
                    return false;
                }

                if (fireZone.Patches.Count != spec.PatchCount)
                {
                    reason =
                        $"fire_patch_count_invalid:{spec.ZoneId}:" +
                        $"{fireZone.Patches.Count}:{spec.PatchCount}";
                    return false;
                }
            }

            if (totalPatchCount != 22)
            {
                reason =
                    $"fire_patch_total_invalid:{totalPatchCount}:22";
                return false;
            }

            var registeredLocations =
                new HashSet<PHSIncidentLocationAnchor>(layout.Locations);
            foreach (var accidentAnchor in accidentAnchors)
            {
                var locationComponents =
                    accidentAnchor
                        .GetComponents<PHSIncidentLocationAnchor>();
                if (locationComponents.Length != 1
                    || !registeredLocations.Contains(
                        locationComponents[0])
                    || locationComponents[0].LocationId
                        != accidentLocationIds[accidentAnchor])
                {
                    reason =
                        $"ship_accident_location_invalid:" +
                        $"{accidentAnchor.AnchorId}";
                    return false;
                }
            }

            var accidentCoordinators =
                FindSceneComponents<PHSNetworkShipAccidentCoordinator>(
                    mapScene);
            if (accidentCoordinators.Length != 1)
            {
                reason =
                    $"ship_accident_coordinator_count_invalid:" +
                    $"{accidentCoordinators.Length}";
                return false;
            }

            var serializedAccidentCoordinator =
                new SerializedObject(accidentCoordinators[0]);
            var registeredAnchors = RequireProperty(
                serializedAccidentCoordinator,
                "anchors");
            if (registeredAnchors.arraySize != accidentAnchors.Length)
            {
                reason =
                    $"ship_accident_registered_anchor_count_invalid:" +
                    $"{registeredAnchors.arraySize}:{accidentAnchors.Length}";
                return false;
            }

            var registeredAnchorSet =
                new HashSet<PHSShipAccidentAnchor>();
            for (var index = 0;
                 index < registeredAnchors.arraySize;
                 index++)
            {
                var registeredAnchor = registeredAnchors
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as PHSShipAccidentAnchor;
                if (registeredAnchor == null
                    || !registeredAnchorSet.Add(registeredAnchor))
                {
                    reason =
                        $"ship_accident_registered_anchor_invalid_or_duplicate:" +
                        $"{index}";
                    return false;
                }
            }

            if (!registeredAnchorSet.SetEquals(accidentAnchors))
            {
                reason = "ship_accident_registered_anchor_set_mismatch";
                return false;
            }

            var fireCoordinators =
                FindSceneComponents<PHSNetworkFireCoordinator>(mapScene);
            if (fireCoordinators.Length != 1)
            {
                reason =
                    $"fire_coordinator_count_invalid:" +
                    $"{fireCoordinators.Length}";
                return false;
            }

            var fireCoordinator = fireCoordinators[0];
            var damageGateways =
                FindSceneComponents<PHSFireAreaDamageGateway>(mapScene);
            if (damageGateways.Length != 1
                || damageGateways[0].gameObject
                    != accidentCoordinators[0].gameObject
                || fireCoordinator.gameObject
                    != accidentCoordinators[0].gameObject)
            {
                reason =
                    $"fire_runtime_owner_invalid:" +
                    $"coordinators={fireCoordinators.Length}:" +
                    $"gateways={damageGateways.Length}";
                return false;
            }

            if (!damageGateways[0].TryValidate(
                    out var damageGatewayReason))
            {
                reason =
                    $"fire_damage_gateway_invalid:" +
                    $"{damageGatewayReason}";
                return false;
            }

            var serializedFireCoordinator =
                new SerializedObject(fireCoordinator);
            if (RequireProperty(
                    serializedFireCoordinator,
                    "accidentCoordinator").objectReferenceValue
                    != accidentCoordinators[0]
                || RequireProperty(
                    serializedFireCoordinator,
                    "areaDamageGateway").objectReferenceValue
                    != damageGateways[0])
            {
                reason = "fire_runtime_reference_invalid";
                return false;
            }

            var registeredFireZones = RequireProperty(
                serializedFireCoordinator,
                "fireZones");
            if (registeredFireZones.arraySize != fireZones.Length)
            {
                reason =
                    $"fire_registered_zone_count_invalid:" +
                    $"{registeredFireZones.arraySize}:" +
                    $"{fireZones.Length}";
                return false;
            }

            var registeredFireZoneSet = new HashSet<PHSFireZone>();
            for (var index = 0;
                 index < registeredFireZones.arraySize;
                 index++)
            {
                var registeredFireZone = registeredFireZones
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as PHSFireZone;
                if (registeredFireZone == null
                    || !registeredFireZoneSet.Add(
                        registeredFireZone))
                {
                    reason =
                        $"fire_registered_zone_invalid_or_duplicate:" +
                        $"{index}";
                    return false;
                }
            }

            if (!registeredFireZoneSet.SetEquals(fireZones))
            {
                reason = "fire_registered_zone_set_mismatch";
                return false;
            }

            if (RequireProperty(
                    serializedAccidentCoordinator,
                    "fireCoordinator").objectReferenceValue
                    != fireCoordinator)
            {
                reason =
                    "ship_accident_fire_coordinator_reference_invalid";
                return false;
            }

            if (!fireCoordinator.TryValidate(
                    out var fireCoordinatorReason))
            {
                reason =
                    $"fire_coordinator_invalid:" +
                    $"{fireCoordinatorReason}";
                return false;
            }

            var interactableLayer = RequireLayer("Interactable");
            foreach (var fireZone in fireZones)
            {
                if (!PHS0720FirePresentationAuthoring
                        .ValidatePresentationPrefab(
                            fireZone.PatchPresentationPrefab,
                            out var presentationReason))
                {
                    reason =
                        $"fire_presentation_invalid:" +
                        $"{fireZone.IncidentZone.ZoneId}:" +
                        $"{presentationReason}";
                    return false;
                }

                foreach (var patch in fireZone.Patches)
                {
                    if (patch.gameObject.layer != interactableLayer)
                    {
                        reason =
                            $"fire_patch_layer_invalid:" +
                            $"{fireZone.IncidentZone.ZoneId}:" +
                            $"{patch.PatchId}:" +
                            $"{patch.gameObject.layer}:" +
                            $"{interactableLayer}";
                        return false;
                    }

                    var runtimeTargets =
                        patch.GetComponents<
                            PHSFirePatchRuntimeTarget>();
                    var fireLights =
                        patch.PresentationRoot
                            .GetComponentsInChildren<Light>(true);
                    if (runtimeTargets.Length != 1
                        || fireLights.Length != 1
                        || runtimeTargets[0].FireLight
                            != fireLights[0]
                        || fireLights[0].enabled)
                    {
                        reason =
                            $"fire_patch_presentation_contract_invalid:" +
                            $"{fireZone.IncidentZone.ZoneId}:" +
                            $"{patch.PatchId}:" +
                            $"targets={runtimeTargets.Length}:" +
                            $"lights={fireLights.Length}";
                        return false;
                    }
                }
            }

            var zonesById = layout.Zones.ToDictionary(
                zone => zone.ZoneId,
                zone => zone,
                StringComparer.Ordinal);
            var fireAccidentAnchors = accidentAnchorTypes
                .Where(
                    pair => pair.Value == PHSShipAccidentId.Fire)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (var spec in ZoneSpecs)
            {
                var zone = zonesById[spec.ZoneId];
                if (!ValidateExternalRoomLocation(
                        locationsById[
                            $"external_room_{spec.ZoneId}"],
                        zone,
                        out reason))
                {
                    return false;
                }

                if (!ValidateFireSurfaceLocation(
                        locationsById[
                            $"fire_surface_{spec.ZoneId}"],
                        zone,
                        fireZonesByZoneId[spec.ZoneId],
                        SelectNearestAccidentAnchor(
                            zone,
                            fireAccidentAnchors),
                        out reason))
                {
                    return false;
                }
            }

            foreach (var pair in accidentAnchorTypes)
            {
                if (!ValidateInternalAccidentLocation(
                        locationsById[
                            accidentLocationIds[pair.Key]],
                        pair.Value,
                        pair.Key,
                        out reason))
                {
                    return false;
                }
            }

            var consumers =
                FindSceneComponents<PHSMapIncidentCommandConsumer>(mapScene);
            if (consumers.Length != 1
                || consumers[0].IncidentLayout != layout
                || consumers[0].FireCoordinator != fireCoordinator
                || consumers[0].AccidentCoordinator
                    != accidentCoordinators[0])
            {
                reason =
                    $"consumer_runtime_reference_invalid:" +
                    $"{consumers.Length}";
                return false;
            }

            var serializedConsumer =
                new SerializedObject(consumers[0]);
            if (RequireProperty(
                    serializedConsumer,
                    "allowLegacyLocationFallback").boolValue)
            {
                reason = "consumer_legacy_location_fallback_enabled";
                return false;
            }

            var gateways =
                FindSceneComponents<PHSIncidentRequestGateway>(mapScene);
            if (gateways.Length != 1
                || gateways[0].gameObject
                    != runtimeRoots[0].gameObject)
            {
                reason =
                    $"request_gateway_owner_invalid:{gateways.Length}";
                return false;
            }

            if (!ValidateRequestRoutes(gateways[0], out reason))
            {
                return false;
            }

            reason = null;
            return true;
        }

        private static HashSet<string> CreateExpectedLocationIds(
            IEnumerable<string> accidentLocationIds)
        {
            var expectedIds =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in ZoneSpecs)
            {
                expectedIds.Add($"external_room_{spec.ZoneId}");
                expectedIds.Add($"fire_surface_{spec.ZoneId}");
            }

            foreach (var locationId in accidentLocationIds)
            {
                expectedIds.Add(locationId);
            }

            return expectedIds;
        }

        private static bool ValidateExternalRoomLocation(
            PHSIncidentLocationAnchor location,
            PHSShipIncidentZone expectedZone,
            out string reason)
        {
            var room = location.RuntimeTarget as ShipRoom;
            if (room == null)
            {
                reason =
                    $"external_room_runtime_target_invalid:" +
                    $"{location.LocationId}";
                return false;
            }

            var roomZoneId = ResolveNormalizedZoneId(room);
            if (!string.Equals(
                    roomZoneId,
                    expectedZone.ZoneId,
                    StringComparison.Ordinal))
            {
                reason =
                    $"external_room_zone_target_mismatch:" +
                    $"{location.LocationId}:{roomZoneId}:" +
                    $"{expectedZone.ZoneId}";
                return false;
            }

            return ValidateLocationContract(
                location,
                expectedZone,
                IncidentLocationKind.Room,
                ExternalRoomCapabilities,
                NetworkShipModuleId.None,
                new[] { NetworkRunIncidentChannel.External },
                new[]
                {
                    NetworkRunIncidentFamily.Enemy,
                    NetworkRunIncidentFamily.Meteor,
                    NetworkRunIncidentFamily.EMP
                },
                new[] { 7201, 7202, 7203 },
                room,
                out reason);
        }

        private static bool ValidateFireSurfaceLocation(
            PHSIncidentLocationAnchor location,
            PHSShipIncidentZone expectedZone,
            PHSFireZone fireZone,
            PHSShipAccidentAnchor fireAnchor,
            out string reason)
        {
            if (fireZone == null
                || fireZone.gameObject != location.gameObject
                || fireZone.IncidentZone != expectedZone
                || fireZone.FireAccidentAnchor != fireAnchor)
            {
                reason =
                    $"fire_surface_zone_bridge_invalid:" +
                    $"{location.LocationId}";
                return false;
            }

            return ValidateLocationContract(
                location,
                expectedZone,
                IncidentLocationKind.FireSurface,
                FireSurfaceCapabilities,
                fireAnchor.ModuleId,
                new[] { NetworkRunIncidentChannel.Internal },
                new[] { NetworkRunIncidentFamily.Fire },
                new[] { (int)PHSShipAccidentId.Fire },
                fireAnchor,
                out reason);
        }

        private static bool ValidateInternalAccidentLocation(
            PHSIncidentLocationAnchor location,
            PHSShipAccidentId accidentId,
            PHSShipAccidentAnchor accidentAnchor,
            out string reason)
        {
            if (location.gameObject != accidentAnchor.gameObject)
            {
                reason =
                    $"internal_accident_runtime_owner_invalid:" +
                    $"{location.LocationId}:{accidentAnchor.AnchorId}";
                return false;
            }

            return ValidateLocationContract(
                location,
                location.Zone,
                GetAccidentLocationKind(accidentId),
                InternalAccidentCapabilities,
                accidentAnchor.ModuleId,
                new[] { NetworkRunIncidentChannel.Internal },
                new[] { GetIncidentFamily(accidentId) },
                new[] { (int)accidentId },
                accidentAnchor,
                out reason);
        }

        private static bool ValidateLocationContract(
            PHSIncidentLocationAnchor location,
            PHSShipIncidentZone expectedZone,
            IncidentLocationKind expectedKind,
            IncidentLocationCapability expectedCapabilities,
            NetworkShipModuleId expectedModule,
            NetworkRunIncidentChannel[] expectedChannels,
            NetworkRunIncidentFamily[] expectedFamilies,
            int[] expectedContentIds,
            Component expectedRuntimeTarget,
            out string reason)
        {
            if (expectedZone == null
                || location.Zone != expectedZone)
            {
                reason =
                    $"location_zone_mismatch:{location.LocationId}:" +
                    $"{location.Zone?.ZoneId}:{expectedZone?.ZoneId}";
                return false;
            }

            if (location.Kind != expectedKind)
            {
                reason =
                    $"location_kind_mismatch:{location.LocationId}:" +
                    $"{location.Kind}:{expectedKind}";
                return false;
            }

            if (location.Capabilities != expectedCapabilities)
            {
                reason =
                    $"location_capabilities_mismatch:" +
                    $"{location.LocationId}:" +
                    $"{location.Capabilities}:{expectedCapabilities}";
                return false;
            }

            if (location.ModuleId != expectedModule)
            {
                reason =
                    $"location_module_mismatch:{location.LocationId}:" +
                    $"{location.ModuleId}:{expectedModule}";
                return false;
            }

            if (location.RuntimeTarget != expectedRuntimeTarget)
            {
                reason =
                    $"location_runtime_target_mismatch:" +
                    $"{location.LocationId}";
                return false;
            }

            var serializedLocation = new SerializedObject(location);
            if (!ValidateSerializedIntSet(
                    serializedLocation,
                    "supportedChannels",
                    expectedChannels
                        .Select(value => (int)value)
                        .ToArray(),
                    out var serializedReason)
                || !ValidateSerializedIntSet(
                    serializedLocation,
                    "supportedFamilies",
                    expectedFamilies
                        .Select(value => (int)value)
                        .ToArray(),
                    out serializedReason)
                || !ValidateSerializedIntSet(
                    serializedLocation,
                    "supportedContentIds",
                    expectedContentIds,
                    out serializedReason))
            {
                reason =
                    $"location_compatibility_invalid:" +
                    $"{location.LocationId}:{serializedReason}";
                return false;
            }

            if (expectedChannels.Length != 1
                || expectedFamilies.Length
                    != expectedContentIds.Length)
            {
                reason =
                    $"validator_location_contract_invalid:" +
                    $"{location.LocationId}";
                return false;
            }

            for (var index = 0;
                 index < expectedFamilies.Length;
                 index++)
            {
                var query = new IncidentLocationQuery(
                    expectedChannels[0],
                    expectedFamilies[index],
                    expectedContentIds[index],
                    expectedModule,
                    expectedKind,
                    expectedCapabilities,
                    expectedZone.ZoneId,
                    location.LocationId,
                    0d,
                    false);
                if (!location.Supports(query))
                {
                    reason =
                        $"location_query_not_supported:" +
                        $"{location.LocationId}:" +
                        $"{expectedFamilies[index]}:" +
                        $"{expectedContentIds[index]}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static bool ValidateSerializedIntSet(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyCollection<int> expectedValues,
            out string reason)
        {
            var property = RequireProperty(
                serializedObject,
                propertyName);
            var actualCount = property.isArray
                ? property.arraySize
                : -1;
            if (!property.isArray
                || actualCount != expectedValues.Count)
            {
                reason =
                    $"{propertyName}_count_invalid:" +
                    $"{actualCount}:{expectedValues.Count}";
                return false;
            }

            var actualValues = new HashSet<int>();
            for (var index = 0;
                 index < property.arraySize;
                 index++)
            {
                if (!actualValues.Add(
                        property.GetArrayElementAtIndex(index).intValue))
                {
                    reason =
                        $"{propertyName}_duplicate:" +
                        $"{property.GetArrayElementAtIndex(index).intValue}";
                    return false;
                }
            }

            if (!actualValues.SetEquals(expectedValues))
            {
                reason = $"{propertyName}_set_mismatch";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool ValidatePatchGraph(
            PHSFireZone fireZone,
            out string reason)
        {
            if (fireZone.Patches.Count == 0)
            {
                reason = "patches_empty";
                return false;
            }

            var patchSet =
                new HashSet<PHSFirePatch>(fireZone.Patches);
            var visited = new HashSet<PHSFirePatch>();
            var pending = new Stack<PHSFirePatch>();
            pending.Push(fireZone.Patches[0]);
            var directedLinkCount = 0;
            while (pending.Count > 0)
            {
                var patch = pending.Pop();
                if (!visited.Add(patch))
                {
                    continue;
                }

                foreach (var link in patch.Neighbors)
                {
                    directedLinkCount++;
                    if (link == null
                        || link.OneWay
                        || link.Target == null
                        || !patchSet.Contains(link.Target))
                    {
                        reason =
                            $"link_invalid:{patch.PatchId}";
                        return false;
                    }

                    pending.Push(link.Target);
                }

                if (patch.VisualSockets.Count < 3)
                {
                    reason =
                        $"visual_socket_count_insufficient:" +
                        $"{patch.PatchId}:{patch.VisualSockets.Count}";
                    return false;
                }
            }

            if (visited.Count != patchSet.Count)
            {
                reason =
                    $"graph_disconnected:{visited.Count}:{patchSet.Count}";
                return false;
            }

            var spec = ZoneSpecs.SingleOrDefault(
                candidate =>
                    candidate.ZoneId
                    == fireZone.IncidentZone.ZoneId);
            if (spec == null)
            {
                reason =
                    $"zone_spec_missing:{fireZone.IncidentZone.ZoneId}";
                return false;
            }

            var expectedDirectedLinks = 2
                * (((spec.Columns - 1) * spec.Rows)
                    + ((spec.Rows - 1) * spec.Columns));
            if (directedLinkCount != expectedDirectedLinks)
            {
                reason =
                    $"link_count_invalid:{directedLinkCount}:" +
                    $"{expectedDirectedLinks}";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool ValidateRequestRoutes(
            PHSIncidentRequestGateway gateway,
            out string reason)
        {
            var serializedGateway = new SerializedObject(gateway);
            var routes = RequireProperty(serializedGateway, "routes");
            if (!routes.isArray || routes.arraySize != RouteSpecs.Length)
            {
                reason =
                    $"request_route_count_invalid:{routes.arraySize}";
                return false;
            }

            for (var index = 0;
                 index < RouteSpecs.Length;
                 index++)
            {
                var spec = RouteSpecs[index];
                var route = routes.GetArrayElementAtIndex(index);
                if (RequireRelativeProperty(route, "sourceId").stringValue
                        != spec.SourceId
                    || RequireRelativeProperty(route, "channel").intValue
                        != (int)spec.Channel
                    || RequireRelativeProperty(route, "payloadKind").intValue
                        != (int)spec.PayloadKind
                    || RequireRelativeProperty(
                            route,
                            "incidentFamily").intValue
                        != (int)spec.Family
                    || RequireRelativeProperty(route, "contentId").intValue
                        != spec.ContentId
                    || RequireRelativeProperty(route, "sourceKind").intValue
                        != (int)NetworkRunIncidentSourceKind.Device
                    || RequireRelativeProperty(route, "pressureCost").intValue
                        != 1
                    || !Mathf.Approximately(
                        RequireRelativeProperty(
                                route,
                                "warpChargeMultiplier").floatValue,
                        1f)
                    || !RequireRelativeProperty(
                            route,
                            "requiresTarget").boolValue
                    || !Mathf.Approximately(
                        RequireRelativeProperty(
                                route,
                                "cooldownSeconds").floatValue,
                        1f))
                {
                    reason =
                        $"request_route_invalid:{index}:{spec.SourceId}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private static Bounds CalculateRoomWorldBounds(ShipRoom room)
        {
            var points = room.FireSpawnPoints
                .Where(point => point != null)
                .Select(point => point.position)
                .ToArray();
            var minimum = points[0];
            var maximum = points[0];
            foreach (var point in points)
            {
                minimum = Vector3.Min(minimum, point);
                maximum = Vector3.Max(maximum, point);
            }

            var paddingX = Mathf.Max(
                MinimumHorizontalPadding,
                FindHalfMinimumPositiveSpacing(
                    points.Select(point => point.x)));
            var paddingZ = Mathf.Max(
                MinimumHorizontalPadding,
                FindHalfMinimumPositiveSpacing(
                    points.Select(point => point.z)));
            minimum.x -= paddingX;
            maximum.x += paddingX;
            minimum.z -= paddingZ;
            maximum.z += paddingZ;

            var floorY = points.Average(point => point.y);
            var size = new Vector3(
                Mathf.Max(1f, maximum.x - minimum.x),
                ZoneHeight,
                Mathf.Max(1f, maximum.z - minimum.z));
            var center = new Vector3(
                (minimum.x + maximum.x) * 0.5f,
                floorY + (ZoneHeight * 0.5f),
                (minimum.z + maximum.z) * 0.5f);
            return new Bounds(center, size);
        }

        private static float FindHalfMinimumPositiveSpacing(
            IEnumerable<float> values)
        {
            var ordered = values
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var minimumSpacing = float.PositiveInfinity;
            for (var index = 1; index < ordered.Length; index++)
            {
                var spacing = ordered[index] - ordered[index - 1];
                if (spacing > 0.001f)
                {
                    minimumSpacing =
                        Mathf.Min(minimumSpacing, spacing);
                }
            }

            return float.IsPositiveInfinity(minimumSpacing)
                ? MinimumHorizontalPadding
                : minimumSpacing * 0.5f;
        }

        private static void ValidateFirePointCloud(ShipRoom room)
        {
            if (room.FireSpawnPoints == null
                || room.FireSpawnPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=fire_point_cloud_empty room={room.RoomId}");
            }

            if (room.FireSpawnPoints.Any(point => point == null))
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=fire_point_cloud_contains_null " +
                    $"room={room.RoomId}");
            }
        }

        private static PHSShipIncidentZone SelectNearestZone(
            Vector3 worldPosition,
            IEnumerable<PHSShipIncidentZone> zones)
        {
            var candidates = zones
                .Select(zone => new
                {
                    Zone = zone,
                    ContainsHorizontal =
                        ContainsHorizontal(
                            zone.ZoneBounds.bounds,
                            worldPosition),
                    DistanceSquared =
                        HorizontalDistanceSquared(
                            zone.ZoneBounds.bounds,
                            worldPosition)
                })
                .OrderByDescending(candidate => candidate.ContainsHorizontal)
                .ThenBy(candidate => candidate.DistanceSquared)
                .ThenBy(
                    candidate => candidate.Zone.ZoneId,
                    StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    "reason=incident_zone_missing");
            }

            return candidates[0].Zone;
        }

        private static PHSShipAccidentAnchor SelectNearestAccidentAnchor(
            PHSShipIncidentZone zone,
            IEnumerable<PHSShipAccidentAnchor> anchors)
        {
            var candidates = anchors
                .Where(anchor => anchor != null)
                .OrderBy(
                    anchor => HorizontalDistanceSquared(
                        zone.ZoneBounds.bounds,
                        anchor.transform.position))
                .ThenBy(
                    anchor => (
                        anchor.transform.position
                        - zone.ZoneBounds.bounds.center).sqrMagnitude)
                .ThenBy(
                    anchor => anchor.AnchorId,
                    StringComparer.Ordinal)
                .ThenBy(
                    anchor => GetHierarchyPath(anchor.transform),
                    StringComparer.Ordinal)
                .ToArray();
            if (candidates.Length == 0)
            {
                throw new InvalidOperationException(
                    "PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=fire_accident_anchor_missing " +
                    $"zone={zone.ZoneId}");
            }

            return candidates[0];
        }

        private static bool ContainsHorizontal(
            Bounds bounds,
            Vector3 position)
        {
            return position.x >= bounds.min.x
                && position.x <= bounds.max.x
                && position.z >= bounds.min.z
                && position.z <= bounds.max.z;
        }

        private static float HorizontalDistanceSquared(
            Bounds bounds,
            Vector3 position)
        {
            var nearestX = Mathf.Clamp(
                position.x,
                bounds.min.x,
                bounds.max.x);
            var nearestZ = Mathf.Clamp(
                position.z,
                bounds.min.z,
                bounds.max.z);
            var x = position.x - nearestX;
            var z = position.z - nearestZ;
            return (x * x) + (z * z);
        }

        private static PHSShipAccidentId ResolveConfiguredAccidentId(
            PHSShipAccidentAnchor anchor)
        {
            var serializedAnchor = new SerializedObject(anchor);
            var configuredIds = new HashSet<PHSShipAccidentId>();
            var sceneAccidentId = (PHSShipAccidentId)RequireProperty(
                serializedAnchor,
                "sceneAccidentId").intValue;
            if (sceneAccidentId != PHSShipAccidentId.None)
            {
                configuredIds.Add(sceneAccidentId);
            }

            var supportedAccidents = RequireProperty(
                serializedAnchor,
                "supportedAccidents");
            for (var index = 0;
                 index < supportedAccidents.arraySize;
                 index++)
            {
                var supported = (PHSShipAccidentId)supportedAccidents
                    .GetArrayElementAtIndex(index)
                    .intValue;
                if (supported != PHSShipAccidentId.None)
                {
                    configuredIds.Add(supported);
                }
            }

            if (configuredIds.Count != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=ship_accident_identity_ambiguous " +
                    $"anchor={anchor.AnchorId} count={configuredIds.Count}");
            }

            return configuredIds.Single();
        }

        private static NetworkRunIncidentFamily GetIncidentFamily(
            PHSShipAccidentId accidentId)
        {
            switch (accidentId)
            {
                case PHSShipAccidentId.Fire:
                    return NetworkRunIncidentFamily.Fire;
                case PHSShipAccidentId.PowerFailure:
                    return NetworkRunIncidentFamily.Power;
                case PHSShipAccidentId.DeviceFailure:
                    return NetworkRunIncidentFamily.Device;
                case PHSShipAccidentId.HullBreach:
                    return NetworkRunIncidentFamily.Hull;
                case PHSShipAccidentId.SteamLeak:
                    return NetworkRunIncidentFamily.Steam;
                case PHSShipAccidentId.OxygenFailure:
                    return NetworkRunIncidentFamily.Oxygen;
                case PHSShipAccidentId.GravityGeneratorFailure:
                    return NetworkRunIncidentFamily.Gravity;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(accidentId),
                        accidentId,
                        "Unsupported ship accident.");
            }
        }

        private static IncidentLocationKind GetAccidentLocationKind(
            PHSShipAccidentId accidentId)
        {
            switch (accidentId)
            {
                case PHSShipAccidentId.HullBreach:
                    return IncidentLocationKind.HullSurface;
                case PHSShipAccidentId.SteamLeak:
                    return IncidentLocationKind.Pipe;
                default:
                    return IncidentLocationKind.Device;
            }
        }

        private static string GetAccidentIdToken(
            PHSShipAccidentId accidentId)
        {
            switch (accidentId)
            {
                case PHSShipAccidentId.Fire:
                    return "fire";
                case PHSShipAccidentId.PowerFailure:
                    return "power_failure";
                case PHSShipAccidentId.DeviceFailure:
                    return "device_failure";
                case PHSShipAccidentId.HullBreach:
                    return "hull_breach";
                case PHSShipAccidentId.SteamLeak:
                    return "steam_leak";
                case PHSShipAccidentId.OxygenFailure:
                    return "oxygen_failure";
                case PHSShipAccidentId.GravityGeneratorFailure:
                    return "gravity_generator_failure";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(accidentId),
                        accidentId,
                        "Unsupported ship accident.");
            }
        }

        private static string ResolveNormalizedZoneId(ShipRoom room)
        {
            var labels = new[] { room.RoomId, room.name };
            foreach (var label in labels)
            {
                var compact = CompactIdentity(label);
                if (compact.IndexOf(
                        "centralcorridor",
                        StringComparison.Ordinal) >= 0
                    || compact.IndexOf(
                        "corridor",
                        StringComparison.Ordinal) >= 0
                    || compact.IndexOf(
                        "중앙복도",
                        StringComparison.Ordinal) >= 0)
                {
                    return "central_corridor";
                }
            }

            foreach (var label in labels)
            {
                var compact = CompactIdentity(label);
                if (compact == "a"
                    || compact.EndsWith(
                        "rooma",
                        StringComparison.Ordinal))
                {
                    return "room_a";
                }

                if (compact == "b"
                    || compact.EndsWith(
                        "roomb",
                        StringComparison.Ordinal))
                {
                    return "room_b";
                }

                if (compact == "c"
                    || compact.EndsWith(
                        "roomc",
                        StringComparison.Ordinal))
                {
                    return "room_c";
                }
            }

            return null;
        }

        private static string CompactIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(
                value
                    .Trim()
                    .ToLowerInvariant()
                    .Where(char.IsLetterOrDigit)
                    .ToArray());
        }

        private static bool IsLowerSnakeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value[0] == '_'
                || value[value.Length - 1] == '_')
            {
                return false;
            }

            var previousWasUnderscore = false;
            foreach (var character in value)
            {
                if (character == '_')
                {
                    if (previousWasUnderscore)
                    {
                        return false;
                    }

                    previousWasUnderscore = true;
                    continue;
                }

                if (!(character >= 'a' && character <= 'z')
                    && !(character >= '0' && character <= '9'))
                {
                    return false;
                }

                previousWasUnderscore = false;
            }

            return true;
        }

        private static T EnsureSingleComponent<T>(GameObject owner)
            where T : Component
        {
            var components = owner.GetComponents<T>();
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=component_ambiguous " +
                    $"object={GetHierarchyPath(owner.transform)} " +
                    $"component={typeof(T).Name} count={components.Length}");
            }

            var component = components.Length == 1
                ? components[0]
                : owner.AddComponent<T>();
            MarkAndRecord(component);
            return component;
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string childName)
        {
            var matches = FindDirectChildren(parent, childName);
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=child_ambiguous " +
                    $"parent={GetHierarchyPath(parent)} child={childName} " +
                    $"count={matches.Length}");
            }

            if (matches.Length == 1)
            {
                return matches[0];
            }

            var child = new GameObject(childName).transform;
            child.SetParent(parent, false);
            MarkAndRecord(child);
            return child;
        }

        private static Transform[] FindDirectChildren(
            Transform parent,
            string childName)
        {
            var matches = new List<Transform>();
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == childName)
                {
                    matches.Add(child);
                }
            }

            return matches.ToArray();
        }

        private static void RemoveStaleGeneratedPatches(
            Transform patchesRoot,
            int expectedCount)
        {
            var expectedNames = new HashSet<string>(
                Enumerable
                    .Range(1, expectedCount)
                    .Select(index => $"Patch_{index:00}"),
                StringComparer.Ordinal);
            var stale = new List<GameObject>();
            for (var index = 0;
                 index < patchesRoot.childCount;
                 index++)
            {
                var child = patchesRoot.GetChild(index);
                if (child.name.StartsWith(
                        "Patch_",
                        StringComparison.Ordinal)
                    && !expectedNames.Contains(child.name)
                    && child.GetComponent<PHSFirePatch>() != null)
                {
                    stale.Add(child.gameObject);
                }
            }

            foreach (var staleObject in stale)
            {
                UnityEngine.Object.DestroyImmediate(staleObject);
            }
        }

        private static void ConfigureBoxCollider(
            BoxCollider collider,
            Vector3 center,
            Vector3 size)
        {
            collider.center = center;
            collider.size = size;
            collider.isTrigger = true;
            collider.enabled = true;
            MarkAndRecord(collider);
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            MarkAndRecord(transform);
        }

        private static void SetWorldTransform(
            Transform transform,
            Vector3 position,
            Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = Vector3.one;
            MarkAndRecord(transform);
        }

        private static void MarkAndRecord(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(
                    target);
            }
        }

        private static int RequireLayer(string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                throw new InvalidOperationException(
                    $"required_layer_missing:{layerName}");
            }

            return layer;
        }

        private static void ApplyAndRecord(
            SerializedObject serializedObject,
            UnityEngine.Object target)
        {
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            MarkAndRecord(target);
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            var property =
                serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(
                    serializedObject.targetObject.GetType().FullName,
                    propertyName);
            }

            return property;
        }

        private static SerializedProperty RequireRelativeProperty(
            SerializedProperty parent,
            string propertyName)
        {
            var property =
                parent.FindPropertyRelative(propertyName);
            if (property == null)
            {
                throw new MissingMemberException(
                    parent.propertyPath,
                    propertyName);
            }

            return property;
        }

        private static T RequireObjectReference<T>(
            SerializedObject serializedObject,
            string propertyName,
            string error)
            where T : UnityEngine.Object
        {
            var target = RequireProperty(
                serializedObject,
                propertyName).objectReferenceValue as T;
            if (target == null)
            {
                throw new MissingReferenceException(error);
            }

            return target;
        }

        private static void SetString(
            SerializedObject serializedObject,
            string propertyName,
            string value)
        {
            RequireProperty(serializedObject, propertyName).stringValue =
                value;
        }

        private static void SetBool(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            RequireProperty(serializedObject, propertyName).boolValue =
                value;
        }

        private static void SetInt(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            RequireProperty(serializedObject, propertyName).intValue =
                value;
        }

        private static void SetEnum(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            RequireProperty(serializedObject, propertyName).intValue =
                value;
        }

        private static void SetFloat(
            SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            RequireProperty(serializedObject, propertyName).floatValue =
                value;
        }

        private static void SetObject(
            SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            RequireProperty(serializedObject, propertyName)
                .objectReferenceValue = value;
        }

        private static void SetPropertyArraySize(
            SerializedObject serializedObject,
            string propertyName,
            int size)
        {
            RequireProperty(serializedObject, propertyName).arraySize =
                size;
        }

        private static void SetObjectArray<T>(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<T> values)
            where T : UnityEngine.Object
        {
            var property =
                RequireProperty(serializedObject, propertyName);
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index)
                    .objectReferenceValue = values[index];
            }
        }

        private static void SetEnumArray(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<int> values)
        {
            var property =
                RequireProperty(serializedObject, propertyName);
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).intValue =
                    values[index];
            }
        }

        private static void SetIntArray(
            SerializedObject serializedObject,
            string propertyName,
            IReadOnlyList<int> values)
        {
            SetEnumArray(serializedObject, propertyName, values);
        }

        private static Transform FindUniqueNamedTransform(
            Scene scene,
            string objectName)
        {
            var matches = FindNamedTransforms(scene, objectName);
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=named_object_ambiguous name={objectName} " +
                    $"count={matches.Length}");
            }

            return matches[0];
        }

        private static Transform[] FindNamedTransforms(
            Scene scene,
            string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(
                    root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == objectName)
                .ToArray();
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return Array.Empty<T>();
            }

            return scene.GetRootGameObjects()
                .SelectMany(
                    root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static T[] FindLayoutRootComponents<T>(Transform layoutRoot)
            where T : Component
        {
            return layoutRoot.GetComponentsInChildren<T>(true);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private static void ThrowIfAnyLoadedSceneIsDirty(
            string operation)
        {
            if (TryGetDirtyLoadedScenes(out var dirtyScenes))
            {
                return;
            }

            throw new InvalidOperationException(
                $"PHS_0719_INCIDENT_LOCATION_{operation.ToUpperInvariant()}_" +
                $"FAILED reason=loaded_scene_dirty " +
                $"scenes={string.Join(",", dirtyScenes)}");
        }

        private static bool TryGetDirtyLoadedScenes(
            out string[] dirtyScenes)
        {
            dirtyScenes = Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene =>
                    scene.IsValid()
                    && scene.isLoaded
                    && scene.isDirty)
                .Select(scene =>
                    string.IsNullOrWhiteSpace(scene.path)
                        ? $"<unsaved>:{scene.name}"
                        : scene.path)
                .ToArray();
            return dirtyScenes.Length == 0;
        }

        private static void RequireLoadedMapScene(Scene mapScene)
        {
            if (!mapScene.IsValid() || !mapScene.isLoaded)
            {
                throw new InvalidOperationException(
                    $"PHS_0719_INCIDENT_LOCATION_MIGRATION_FAILED " +
                    $"reason=map_scene_open_failed path={MapScenePath}");
            }
        }

        private static void RestoreActiveScene(Scene originalActiveScene)
        {
            if (originalActiveScene.IsValid()
                && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }
        }

        private sealed class ZoneSpec
        {
            public ZoneSpec(
                string zoneId,
                string displayName,
                int columns,
                int rows,
                int patchIdBase)
            {
                ZoneId = zoneId;
                DisplayName = displayName;
                Columns = columns;
                Rows = rows;
                PatchIdBase = patchIdBase;
            }

            public string ZoneId { get; }
            public string DisplayName { get; }
            public int Columns { get; }
            public int Rows { get; }
            public int PatchIdBase { get; }
            public int PatchCount => Columns * Rows;
        }

        private readonly struct AuthoredRoom
        {
            public AuthoredRoom(
                ZoneSpec spec,
                ShipRoom room,
                Bounds worldBounds)
            {
                Spec = spec;
                Room = room;
                WorldBounds = worldBounds;
            }

            public ZoneSpec Spec { get; }
            public ShipRoom Room { get; }
            public Bounds WorldBounds { get; }
        }

        private readonly struct RouteSpec
        {
            public RouteSpec(
                string sourceId,
                NetworkRunIncidentChannel channel,
                NetworkRunIncidentPayloadKind payloadKind,
                NetworkRunIncidentFamily family,
                int contentId)
            {
                SourceId = sourceId;
                Channel = channel;
                PayloadKind = payloadKind;
                Family = family;
                ContentId = contentId;
            }

            public string SourceId { get; }
            public NetworkRunIncidentChannel Channel { get; }
            public NetworkRunIncidentPayloadKind PayloadKind { get; }
            public NetworkRunIncidentFamily Family { get; }
            public int ContentId { get; }
        }

        private readonly struct AuthoringResult
        {
            public AuthoringResult(
                int zoneCount,
                int locationCount,
                int fireZoneCount,
                int firePatchCount)
            {
                ZoneCount = zoneCount;
                LocationCount = locationCount;
                FireZoneCount = fireZoneCount;
                FirePatchCount = firePatchCount;
            }

            public int ZoneCount { get; }
            public int LocationCount { get; }
            public int FireZoneCount { get; }
            public int FirePatchCount { get; }
        }
    }
}
