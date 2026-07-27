using System.Collections;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Interaction
{
    public sealed class ShopCheckoutButtonPressVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Transform buttonVisual;
        [SerializeField] private Renderer buttonRenderer;
        [SerializeField, Min(0.001f)] private float pressDistance = 0.1f;
        [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;
        [SerializeField, Min(0f)] private float holdDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float releaseDuration = 0.12f;
        [SerializeField] private Color idleColor = new(0.08f, 0.7f, 0.78f, 1f);
        [SerializeField] private Color acceptedColor = new(0.15f, 0.9f, 0.35f, 1f);
        [SerializeField] private Color rejectedColor = new(0.95f, 0.25f, 0.18f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private Vector3 restLocalPosition;
        private Coroutine animationRoutine;
        private bool restPositionCaptured;

        private void Awake()
        {
            CaptureRestPosition();
            ApplyColor(idleColor);
        }

        private void OnValidate()
        {
            CaptureRestPosition();
            ApplyColor(idleColor);
        }

        private void OnEnable()
        {
            CaptureRestPosition();
            ResetVisual();
        }

        private void OnDisable()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            ResetVisual();
        }

        public void Play(bool accepted)
        {
            if (buttonVisual == null)
            {
                Debug.LogError($"PHS_SHOP_BUTTON_VISUAL_FAILED reason=button_visual_missing visual={name}", this);
                return;
            }

            CaptureRestPosition();
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimatePress(accepted));
        }

        private IEnumerator AnimatePress(bool accepted)
        {
            var pressedPosition = restLocalPosition + Vector3.down * pressDistance;
            ApplyColor(accepted ? acceptedColor : rejectedColor);
            yield return MoveButton(buttonVisual.localPosition, pressedPosition, pressDuration);

            if (holdDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }

            yield return MoveButton(buttonVisual.localPosition, restLocalPosition, releaseDuration);
            ApplyColor(idleColor);
            animationRoutine = null;
        }

        private IEnumerator MoveButton(Vector3 from, Vector3 to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                buttonVisual.localPosition = Vector3.LerpUnclamped(
                    from,
                    to,
                    progress * progress * (3f - 2f * progress));
                yield return null;
            }

            buttonVisual.localPosition = to;
        }

        private void CaptureRestPosition()
        {
            if (restPositionCaptured || buttonVisual == null)
            {
                return;
            }

            restLocalPosition = buttonVisual.localPosition;
            restPositionCaptured = true;
        }

        private void ResetVisual()
        {
            if (buttonVisual != null && restPositionCaptured)
            {
                buttonVisual.localPosition = restLocalPosition;
            }

            ApplyColor(idleColor);
        }

        private void ApplyColor(Color color)
        {
            if (buttonRenderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            buttonRenderer.GetPropertyBlock(propertyBlock);
            var material = buttonRenderer.sharedMaterial;
            if (material != null && material.HasProperty(BaseColorId))
            {
                propertyBlock.SetColor(BaseColorId, color);
            }

            if (material != null && material.HasProperty(ColorId))
            {
                propertyBlock.SetColor(ColorId, color);
            }

            buttonRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
