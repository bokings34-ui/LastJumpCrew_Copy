using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Data
{
    [CreateAssetMenu(
        fileName = "UtilityItem_New",
        menuName = "LastJumpCrew/SeoBoGyeong/Utility Item Data")]
    public class UtilityItemData : ItemData
    {
        [Header("아이템 타입")]
        [SerializeField] private ItemType type;
        // 조한용님(06)의 전역 ItemType enum 사용

        [Header("내구도")]
        [Tooltip("내구도 사용 여부")]
        [SerializeField] private bool hasDurability;
        [Tooltip("내구도 최대치")]
        [SerializeField, Min(1)] private int maxDurability = 100;

        public bool HasDurability => hasDurability;
        public int MaxDurability => maxDurability;
    }
}
