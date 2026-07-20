using System.Collections.Generic;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Incidents.Fire
{
    public interface IFireAreaDamageGateway
    {
        bool TryApplyDamageServer(
            IReadOnlyList<FireAreaDamageSample> samples,
            out int damagedTargetCount,
            out string reason);
    }
}
