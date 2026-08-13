using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public static class TeamRepairToolVisualPalette
    {
        public static readonly Color Wrench = new(0.68f, 0.32f, 1f, 1f);
        public static readonly Color FireExtinguisher = new(1f, 0.22f, 0.24f, 1f);
        public static readonly Color Battery = new(1f, 0.78f, 0.08f, 1f);
        public static readonly Color EnemyOrange = new(1f, 0.52f, 0.06f, 1f);
        public static readonly Color MiniGameCyan = new(0.12f, 0.88f, 0.9f, 1f);
        public static readonly Color MiniGameCyanBright = new(0.42f, 1f, 0.98f, 1f);
        public static readonly Color MiniGameCyanDim = new(0.05f, 0.38f, 0.42f, 1f);

        public static Color GetToolColor(PHSUtilityFamilyActionKind tool)
        {
            return tool switch
            {
                PHSUtilityFamilyActionKind.Wrench => Wrench,
                PHSUtilityFamilyActionKind.FireExtinguisher => FireExtinguisher,
                PHSUtilityFamilyActionKind.Battery => Battery,
                _ => Color.white
            };
        }

        public static Color GetFeedbackRangeColor(PHSItemUseFeedbackKind kind)
        {
            var color = GetFeedbackColor(kind);
            color.a = 0.24f;
            return color;
        }

        public static Color GetFeedbackTargetColor(PHSItemUseFeedbackKind kind)
        {
            var color = GetFeedbackColor(kind);
            color.a = 1f;
            return color;
        }

        public static Color GetMiniGameWireChannelColor(int channel)
        {
            var value = channel switch
            {
                0 => 0.62f,
                1 => 0.72f,
                2 => 0.82f,
                3 => 0.92f,
                _ => 1f
            };
            return Color.Lerp(MiniGameCyanDim, MiniGameCyanBright, value);
        }

        private static Color GetFeedbackColor(PHSItemUseFeedbackKind kind)
        {
            return kind switch
            {
                PHSItemUseFeedbackKind.Wrench => Wrench,
                PHSItemUseFeedbackKind.FireExtinguisher => FireExtinguisher,
                PHSItemUseFeedbackKind.Battery => Battery,
                _ => new Color(0.2f, 0.9f, 1f, 1f)
            };
        }
    }
}
