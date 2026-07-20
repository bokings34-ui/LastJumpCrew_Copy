using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Maps
{
    public interface IMapPresentationConfigurator
    {
        bool TryConfigureMapPresentation(
            Material gameplaySkybox,
            Material arrivalSkybox,
            out string reason);
    }
}
