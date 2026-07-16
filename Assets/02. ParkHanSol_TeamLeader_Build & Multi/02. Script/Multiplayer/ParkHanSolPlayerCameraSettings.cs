using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    [RequireComponent(typeof(Camera))]
    public sealed class ParkHanSolPlayerCameraSettings : MonoBehaviour
    {
        public const string FieldOfViewPreferenceKey = "PHS_FieldOfView";
        public const float DefaultFieldOfView = 60f;
        public const float MinimumFieldOfView = 40f;
        public const float MaximumFieldOfView = 120f;

        private Camera targetCamera;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (targetCamera == null)
            {
                Debug.LogError($"PHS_CAMERA_SETTINGS_FAILED reason=camera_missing object={name}");
                return;
            }

            ApplySavedFieldOfView();
        }

        private void OnEnable()
        {
            ApplySavedFieldOfView();
        }

        public static float GetSavedFieldOfView(float fallback = DefaultFieldOfView)
        {
            return Mathf.Clamp(
                PlayerPrefs.GetFloat(FieldOfViewPreferenceKey, fallback),
                MinimumFieldOfView,
                MaximumFieldOfView);
        }

        public static void SaveFieldOfView(float value)
        {
            var fieldOfView = Mathf.Clamp(value, MinimumFieldOfView, MaximumFieldOfView);
            PlayerPrefs.SetFloat(FieldOfViewPreferenceKey, fieldOfView);

            var cameras = FindObjectsByType<ParkHanSolPlayerCameraSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var applied = false;
            foreach (var cameraSettings in cameras)
            {
                if (!cameraSettings.TryApplyFieldOfView(fieldOfView))
                {
                    continue;
                }

                applied = true;
            }

            if (!applied)
            {
                Debug.LogError("PHS_CAMERA_SETTINGS_FAILED reason=local_player_camera_missing");
            }
        }

        private void ApplySavedFieldOfView()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            TryApplyFieldOfView(GetSavedFieldOfView());
        }

        private bool TryApplyFieldOfView(float fieldOfView)
        {
            if (targetCamera == null)
            {
                Debug.LogError($"PHS_CAMERA_SETTINGS_FAILED reason=camera_missing object={name}");
                return false;
            }

            if (!targetCamera.enabled)
            {
                return false;
            }

            targetCamera.fieldOfView = fieldOfView;
            return true;
        }
    }
}
