using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public interface INetworkPlayerOptionsStore
    {
        float GetMouseSensitivity(float defaultValue);
        void SetMouseSensitivity(float value);
        bool TryGetWindowMode(out FullScreenMode mode);
        void SetWindowMode(FullScreenMode mode);
        IReadOnlyList<Vector2Int> GetSupportedResolutions();
        bool TryGetSavedResolution(out Vector2Int resolution);
        void SetResolution(Vector2Int resolution);
        bool LoadBindingOverrides(InputActionAsset inputActions);
        void SaveBindingOverrides(InputActionAsset inputActions);
        void ResetBindingOverrides(InputActionAsset inputActions);
    }
}
