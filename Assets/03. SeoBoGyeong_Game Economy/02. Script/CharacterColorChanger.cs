using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong
{
    public class CharacterColorChanger : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer target;
        [SerializeField] private Color baseColor = Color.white;

        private Material[] mats;

        private void Start()
        {
            mats = target.materials;

        }

        [ContextMenu("Change Color")]
        private void ChangeColor()
        {
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].color = baseColor;
            }
        }

        public void SetColor(Color color)
        {
            foreach (Material mat in mats)
            {
                mat.color = color;
            }
        }
    }
}


