using System.Collections.Generic;
using LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Locations;
using LastJumpCrew.ParkHanSol.Multiplayer.ShipAccidents;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    public interface IFireSpreadSurface
    {
        PHSShipIncidentZone IncidentZone { get; }
        PHSShipAccidentAnchor FireAccidentAnchor { get; }
        IReadOnlyList<PHSFirePatch> Patches { get; }
        byte MaximumBurningPatches { get; }
        ushort InitialHeat { get; }
        ushort MaximumHeat { get; }
        ushort MinimumHeatGrowthPerTick { get; }
        ushort MaximumHeatGrowthPerTick { get; }
        float SpreadTickSeconds { get; }
        byte SpreadAttemptsPerTick { get; }
        byte MaximumNewIgnitionsPerTick { get; }
        float BaseSpreadChance { get; }
        float DamageTickSeconds { get; }
        int BaseDamagePerTick { get; }
        ushort SuppressionHeatPerHit { get; }
        float ContainmentGraceSeconds { get; }
        LayerMask DamageableLayers { get; }
        GameObject PatchPresentationPrefab { get; }
        bool IsReady { get; }

        bool TryResolvePatch(
            ushort patchId,
            out PHSFirePatch patch);
        bool TryCopyOrderedPatches(
            List<PHSFirePatch> destination,
            out string reason);
        bool TryCopySpreadCandidates(
            ushort sourcePatchId,
            PHSFireIntensity sourceIntensity,
            List<PHSFirePatchLink> destination,
            out string reason);
        bool TryValidate(out string reason);
    }
}
