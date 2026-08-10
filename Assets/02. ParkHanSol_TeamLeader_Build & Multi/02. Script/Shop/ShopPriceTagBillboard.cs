using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopPriceTagBillboard : MonoBehaviour
    {
        private Camera localCamera;

        private void LateUpdate()
        {
            if (localCamera == null || !localCamera.isActiveAndEnabled)
            {
                localCamera = Camera.main;
            }

            if (localCamera != null)
            {
                transform.rotation = localCamera.transform.rotation;
            }
        }
    }
}
