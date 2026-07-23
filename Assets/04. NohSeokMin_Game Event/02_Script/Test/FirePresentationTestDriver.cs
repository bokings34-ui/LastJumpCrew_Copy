using UnityEngine;
using UnityEngine.InputSystem;

namespace SM
{
    public class FirePresentationTestDriver : MonoBehaviour
    {
        [SerializeField] private FirePresentationController target;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                target.Telegraph();
                Debug.Log("[Test] Telegraph 호출");
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                target.Activate(FireIntensity.Small);
                Debug.Log("[Test] Activate(Small) 호출");
            }

            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                target.SetIntensity(FireIntensity.Medium);
                Debug.Log("[Test] SetIntensity(Medium) 호출");
            }

            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                target.SetIntensity(FireIntensity.Large);
                Debug.Log("[Test] SetIntensity(Large) 호출");
            }

            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            {
                target.Extinguish();
                Debug.Log("[Test] Extinguish 호출");
            }

            if (Keyboard.current.digit6Key.wasPressedThisFrame)
            {
                target.ResetPresentation();
                Debug.Log("[Test] ResetPresentation 호출");
            }
        }
    }
}