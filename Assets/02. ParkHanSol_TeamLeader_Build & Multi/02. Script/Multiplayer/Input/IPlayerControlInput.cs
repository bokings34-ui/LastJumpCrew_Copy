using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public interface IPlayerControlInput
    {
        Vector2 Move { get; }
        Vector2 Look { get; }
        float Descend { get; }
        bool JumpPressedThisFrame { get; }
        bool SprintPressed { get; }
        bool InteractPressedThisFrame { get; }
        bool UsePressedThisFrame { get; }
        bool UsePressed { get; }
        bool DropPressedThisFrame { get; }
        bool DropReleasedThisFrame { get; }
        bool GrapplePressedThisFrame { get; }
        bool GrappleReleasedThisFrame { get; }
    }
}
