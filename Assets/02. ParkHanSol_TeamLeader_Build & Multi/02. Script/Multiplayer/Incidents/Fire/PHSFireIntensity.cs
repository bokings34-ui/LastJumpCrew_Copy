namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    public enum PHSFireIntensity : byte
    {
        None = 0,
        Small = 1,
        Medium = 2,
        Large = 3
    }

    public static class PHSFireIntensityUtility
    {
        public const ushort MaximumHeat = 200;
        public const ushort SmallMaximumHeat = 69;
        public const ushort MediumMaximumHeat = 139;

        public static PHSFireIntensity FromHeat(ushort heat)
        {
            if (heat == 0)
            {
                return PHSFireIntensity.None;
            }

            if (heat <= SmallMaximumHeat)
            {
                return PHSFireIntensity.Small;
            }

            if (heat <= MediumMaximumHeat)
            {
                return PHSFireIntensity.Medium;
            }

            return PHSFireIntensity.Large;
        }

        public static bool IsDefined(PHSFireIntensity intensity)
        {
            var value = (byte)intensity;
            return value >= (byte)PHSFireIntensity.None
                && value <= (byte)PHSFireIntensity.Large;
        }
    }
}
