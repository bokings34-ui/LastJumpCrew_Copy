using System;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Editor
{
    internal static class PHSItemHeldPresentationSpec
    {
        internal const string CatalogPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/04. Data/UtilityItems/PHS_UtilityItemCatalog_0717.asset";

        internal const string PlayerPrefabPath =
            "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/PlayerPrefab/PHS_CuteWhiteGhost_Player.prefab";

        internal static readonly ItemPoseSpec[] Items =
        {
            Item("auto_repair_kit", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("battery_pack", Pose(0f, 0.02f, 0f, 0.52f), Pose(0f, -0.13f, 0f, 1f)),
            Item("fire_extinguisher", Pose(0f, 0f, 0f, 0.55f), Pose(0f, -0.22f, 0f, 1f)),
            Item("foam_sealant_gun", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("futuristic_adjustable_wrench", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("futuristic_canister", Pose(0f, -0.12f, 0.19f, 0.9f), Pose(0f, -0.09f, 0.14f, 0.9f)),
            Item("tripo_fire_extinguisher", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("wrench", Pose(0f, 0f, 0f, 0.65f), Pose(0f, -0.07f, 0f, 1f)),
            Debris("debris_futuristic_cargo", Pose(0f, -0.14f, 0f, 0.85f), Pose(0f, -0.11f, 0f, 0.85f)),
            Debris("debris_satellite_camera", Pose(0f, -0.15f, 0f, 0.85f), Pose(0f, -0.12f, 0f, 0.85f)),
            Debris("debris_scifi_head", Pose(0f, -0.18f, 0.02f, 0.85f), Pose(0f, -0.14f, 0.01f, 0.85f)),
            Debris("debris_spacecraft_hull", Pose(0f, -0.18f, 0f, 0.85f), Pose(0f, -0.14f, 0f, 0.85f)),
            Debris("debris_worn_scifi_engine", Pose(0f, -0.18f, 0f, 0.85f), Pose(0f, -0.14f, 0f, 0.85f)),
            Item("ship_hp_restore", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("ship_max_hp_upgrade", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("hook_power_upgrade", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("thruster_duration_upgrade", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f)),
            Item("player_max_hp_upgrade", Pose(0f, 0f, 0f, 1f), Pose(0f, 0f, 0f, 1f))
        };

        private static ItemPoseSpec Item(
            string itemId,
            HeldPoseSpec firstPerson,
            HeldPoseSpec world)
        {
            return new ItemPoseSpec(itemId, false, firstPerson, world);
        }

        private static ItemPoseSpec Debris(
            string itemId,
            HeldPoseSpec firstPerson,
            HeldPoseSpec world)
        {
            return new ItemPoseSpec(itemId, true, firstPerson, world);
        }

        private static HeldPoseSpec Pose(
            float x,
            float y,
            float z,
            float scale)
        {
            return new HeldPoseSpec(
                new Vector3(x, y, z),
                Vector3.zero,
                scale);
        }

        internal readonly struct ItemPoseSpec
        {
            internal ItemPoseSpec(
                string itemId,
                bool isDebris,
                HeldPoseSpec firstPerson,
                HeldPoseSpec world)
            {
                ItemId = itemId;
                IsDebris = isDebris;
                FirstPerson = firstPerson;
                World = world;
            }

            internal string ItemId { get; }
            internal bool IsDebris { get; }
            internal HeldPoseSpec FirstPerson { get; }
            internal HeldPoseSpec World { get; }
        }

        internal readonly struct HeldPoseSpec
        {
            internal HeldPoseSpec(
                Vector3 localPosition,
                Vector3 localEulerAngles,
                float scaleMultiplier)
            {
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                ScaleMultiplier = scaleMultiplier;
            }

            internal Vector3 LocalPosition { get; }
            internal Vector3 LocalEulerAngles { get; }
            internal float ScaleMultiplier { get; }
        }
    }
}
