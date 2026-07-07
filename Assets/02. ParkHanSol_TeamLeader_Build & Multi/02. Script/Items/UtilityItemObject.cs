using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public sealed class UtilityItemObject : MonoBehaviour
    {
        [SerializeField] private UtilityItemPrefabData itemPrefabData;

        public UtilityItemPrefabData ItemPrefabData => itemPrefabData;
    }
}
