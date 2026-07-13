using UnityEngine;
namespace LastJumpCrew.SeBoGyeong.Data
{
    [CreateAssetMenu(
        fileName = "UtilityItem_New",
        menuName = "LastJumpCrew/SeoBoGyeong/Utility Item Data")]
    public class UtilityItemData : ItemData
    {
        [Header("아이템 타입")]
        [SerializeField] private ItemType type;
        //조한용님의 itemType enum

        [Header("내구도")]
        [Tooltip("내구도의 유무")]
        [SerializeField] private bool hasDurability;
        [Tooltip("내구도 최대치")]
        [SerializeField, Min(1)] private int maxDurability = 100;

        public bool HasDurability => hasDurability;
        public int MaxDurability => maxDurability;
    }
}

