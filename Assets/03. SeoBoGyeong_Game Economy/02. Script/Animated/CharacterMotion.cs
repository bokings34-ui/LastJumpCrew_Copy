using UnityEngine;
namespace LastJumpCrew.SeoBoGyeong.animate
{
    public class CharacterMotion : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private Transform headRig;

       
        private float _headDefault;
        private void Awake()
        {
            _headDefault = headRig.rotation.x;
        }

        private void Update()
        {
            if (cameraRoot != null)
            {
                headRig.rotation = new Quaternion(_headDefault + cameraRoot.rotation.x, headRig.rotation.y, headRig.rotation.z, headRig.rotation.w);
            }
        }
    }
}

