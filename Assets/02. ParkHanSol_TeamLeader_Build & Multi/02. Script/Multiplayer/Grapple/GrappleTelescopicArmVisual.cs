using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer
{
    public sealed class GrappleTelescopicArmVisual : MonoBehaviour
    {
        [Header("Telescopic Pieces")]
        [SerializeField] private Transform[] sleeveSegments;
        [SerializeField] private Transform[] jointInsets;
        [SerializeField] private Transform endMount;

        [Header("Shape")]
        [SerializeField, Min(0f)] private float jointGap = 0.045f;
        [SerializeField, Min(0f)] private float taperPerSegment = 0.006f;
        [SerializeField, Range(0.1f, 1f)] private float insetThicknessRatio = 0.62f;

        public bool IsConfigured => sleeveSegments != null
            && sleeveSegments.Length > 0
            && HasNoMissingReferences(sleeveSegments)
            && jointInsets != null
            && jointInsets.Length == sleeveSegments.Length - 1
            && HasNoMissingReferences(jointInsets)
            && endMount != null;

        public void SetLength(float length, float baseThickness)
        {
            if (!IsConfigured)
            {
                return;
            }

            var safeLength = Mathf.Max(0.01f, length);
            var gap = Mathf.Min(
                jointGap,
                safeLength / (sleeveSegments.Length * 4f));
            var sleeveLength = Mathf.Max(
                0.01f,
                (safeLength - gap * jointInsets.Length)
                / sleeveSegments.Length);
            var cursor = 0f;

            for (var index = 0; index < sleeveSegments.Length; index++)
            {
                var sleeve = sleeveSegments[index];
                var thickness = Mathf.Max(
                    baseThickness - taperPerSegment * index,
                    baseThickness * 0.55f);
                sleeve.localPosition = Vector3.up
                    * (cursor + sleeveLength * 0.5f);
                sleeve.localRotation = Quaternion.identity;
                sleeve.localScale = new Vector3(
                    thickness,
                    sleeveLength,
                    thickness);
                cursor += sleeveLength;

                if (index >= jointInsets.Length)
                {
                    continue;
                }

                var inset = jointInsets[index];
                inset.localPosition = Vector3.up * (cursor + gap * 0.5f);
                inset.localRotation = Quaternion.identity;
                inset.localScale = new Vector3(
                    thickness * insetThicknessRatio,
                    gap,
                    thickness * insetThicknessRatio);
                cursor += gap;
            }

            endMount.localPosition = Vector3.up * safeLength;
            endMount.localRotation = Quaternion.identity;
            endMount.localScale = Vector3.one;
        }

        private static bool HasNoMissingReferences(Transform[] transforms)
        {
            for (var index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
