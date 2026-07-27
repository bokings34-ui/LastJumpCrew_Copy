using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Input
{
    public sealed class NetworkPlayerOptionsStore : INetworkPlayerOptionsStore
    {
        public const string MouseSensitivityPreferenceKey = "PHS_MouseSensitivity";
        public const string BindingOverridesPreferenceKey = "PHS_InputBindingOverrides_v1";
        public const string WindowModePreferenceKey = "PHS_FullScreen";
        public const string ResolutionWidthPreferenceKey = "PHS_ResolutionWidth";
        public const string ResolutionHeightPreferenceKey = "PHS_ResolutionHeight";
        public const float MinimumMouseSensitivity = 0.05f;
        public const float MaximumMouseSensitivity = 5f;

        public static INetworkPlayerOptionsStore Shared { get; } =
            new NetworkPlayerOptionsStore();

        public float GetMouseSensitivity(float defaultValue)
        {
            return Mathf.Clamp(
                PlayerPrefs.GetFloat(MouseSensitivityPreferenceKey, defaultValue),
                MinimumMouseSensitivity,
                MaximumMouseSensitivity);
        }

        public void SetMouseSensitivity(float value)
        {
            PlayerPrefs.SetFloat(
                MouseSensitivityPreferenceKey,
                Mathf.Clamp(value, MinimumMouseSensitivity, MaximumMouseSensitivity));
            PlayerPrefs.Save();
        }

        public bool TryGetWindowMode(out FullScreenMode mode)
        {
            if (!PlayerPrefs.HasKey(WindowModePreferenceKey))
            {
                mode = FullScreenMode.FullScreenWindow;
                return true;
            }

            var savedValue = PlayerPrefs.GetInt(WindowModePreferenceKey);
            if (savedValue == 0)
            {
                mode = FullScreenMode.Windowed;
                return true;
            }

            if (savedValue == 1)
            {
                mode = FullScreenMode.FullScreenWindow;
                return true;
            }

            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_LOAD_FAILED reason=invalid_window_mode value={savedValue}");
            mode = default;
            return false;
        }

        public void SetWindowMode(FullScreenMode mode)
        {
            if (mode != FullScreenMode.Windowed
                && mode != FullScreenMode.FullScreenWindow)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_SAVE_FAILED reason=unsupported_window_mode mode={mode}");
                return;
            }

            Screen.SetResolution(Screen.width, Screen.height, mode);
            PlayerPrefs.SetInt(
                WindowModePreferenceKey,
                mode == FullScreenMode.FullScreenWindow ? 1 : 0);
            PlayerPrefs.Save();
        }

        public IReadOnlyList<Vector2Int> GetSupportedResolutions()
        {
            var supported = Screen.resolutions
                .Select(resolution => new Vector2Int(resolution.width, resolution.height))
                .Distinct()
                .OrderBy(resolution => resolution.x)
                .ThenBy(resolution => resolution.y)
                .ToList();
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!supported.Contains(current))
            {
                supported.Add(current);
            }

            return supported;
        }

        public bool TryGetSavedResolution(out Vector2Int resolution)
        {
            var hasWidth = PlayerPrefs.HasKey(ResolutionWidthPreferenceKey);
            var hasHeight = PlayerPrefs.HasKey(ResolutionHeightPreferenceKey);
            if (!hasWidth && !hasHeight)
            {
                resolution = new Vector2Int(Screen.width, Screen.height);
                return true;
            }

            if (!hasWidth || !hasHeight)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_LOAD_FAILED reason=incomplete_resolution " +
                    $"hasWidth={hasWidth} hasHeight={hasHeight}");
                resolution = default;
                return false;
            }

            resolution = new Vector2Int(
                PlayerPrefs.GetInt(ResolutionWidthPreferenceKey),
                PlayerPrefs.GetInt(ResolutionHeightPreferenceKey));
            if (resolution.x > 0
                && resolution.y > 0
                && GetSupportedResolutions().Contains(resolution))
            {
                return true;
            }

            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_LOAD_FAILED reason=invalid_resolution " +
                $"width={resolution.x} height={resolution.y}");
            resolution = default;
            return false;
        }

        public void SetResolution(Vector2Int resolution)
        {
            if (!GetSupportedResolutions().Contains(resolution))
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_SAVE_FAILED reason=unsupported_resolution " +
                    $"width={resolution.x} height={resolution.y}");
                return;
            }

            if (!TryGetWindowMode(out var windowMode))
            {
                Debug.LogError(
                    "PHS_NETWORK_OPTIONS_SAVE_FAILED reason=window_mode_invalid " +
                    "operation=set_resolution");
                return;
            }

            Screen.SetResolution(resolution.x, resolution.y, windowMode);
            PlayerPrefs.SetInt(ResolutionWidthPreferenceKey, resolution.x);
            PlayerPrefs.SetInt(ResolutionHeightPreferenceKey, resolution.y);
            PlayerPrefs.Save();
        }

        public bool LoadBindingOverrides(InputActionAsset inputActions)
        {
            if (!ValidateInputActions(inputActions, "load"))
            {
                return false;
            }

            if (!PlayerPrefs.HasKey(BindingOverridesPreferenceKey))
            {
                return true;
            }

            var json = PlayerPrefs.GetString(BindingOverridesPreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_BINDINGS_FAILED reason=saved_json_empty key={BindingOverridesPreferenceKey}");
                return false;
            }

            try
            {
                inputActions.LoadBindingOverridesFromJson(json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"PHS_NETWORK_OPTIONS_BINDINGS_FAILED reason=invalid_json " +
                    $"exception={exception.GetType().Name} message={exception.Message}");
                return false;
            }
        }

        public void SaveBindingOverrides(InputActionAsset inputActions)
        {
            if (!ValidateInputActions(inputActions, "save"))
            {
                return;
            }

            PlayerPrefs.SetString(
                BindingOverridesPreferenceKey,
                inputActions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void ResetBindingOverrides(InputActionAsset inputActions)
        {
            if (!ValidateInputActions(inputActions, "reset"))
            {
                return;
            }

            inputActions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(BindingOverridesPreferenceKey);
            PlayerPrefs.Save();
        }

        private static bool ValidateInputActions(InputActionAsset inputActions, string operation)
        {
            if (inputActions != null)
            {
                return true;
            }

            Debug.LogError(
                $"PHS_NETWORK_OPTIONS_BINDINGS_FAILED reason=actions_asset_missing operation={operation}");
            return false;
        }
    }
}
