using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    public readonly struct FireAreaDamageSample
    {
        public FireAreaDamageSample(
            PHSFirePatch patch,
            PHSFireIntensity intensity,
            int baseDamagePerTick,
            LayerMask damageableLayers)
        {
            Patch = patch;
            Intensity = intensity;
            BaseDamagePerTick = baseDamagePerTick;
            DamageableLayers = damageableLayers;
        }

        public PHSFirePatch Patch { get; }
        public PHSFireIntensity Intensity { get; }
        public int BaseDamagePerTick { get; }
        public LayerMask DamageableLayers { get; }
    }
}
