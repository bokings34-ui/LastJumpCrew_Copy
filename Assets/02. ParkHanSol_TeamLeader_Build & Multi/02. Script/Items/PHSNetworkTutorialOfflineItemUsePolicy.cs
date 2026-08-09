using Unity.Netcode;
using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public class PHSNetworkTutorialOfflineItemUsePolicy : MonoBehaviour
    {
        [SerializeField] private bool allowWrench = true;
        [SerializeField] private bool allowFireExtinguisher = true;

        public bool CanUseOfflineItem(PHSUtilityFamilyActionKind familyKind)
        {
            if (NetworkManager.Singleton != null
                && NetworkManager.Singleton.IsListening)
            {
                return false;
            }

            return familyKind switch
            {
                PHSUtilityFamilyActionKind.Wrench => allowWrench,
                PHSUtilityFamilyActionKind.FireExtinguisher =>
                    allowFireExtinguisher,
                _ => false
            };
        }
    }
}
