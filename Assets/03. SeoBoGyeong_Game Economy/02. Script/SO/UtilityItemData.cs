using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Data
{
    [CreateAssetMenu(
        fileName = "UtilityItem_New",
        menuName = "LastJumpCrew/SeoBoGyeong/Utility Item Data")]
    public class UtilityItemData : ItemData
    {
        [Header("아이템 타입")]
        [SerializeField] private ItemType nomarlType;
        [SerializeField] private ItemType actType;
        // 조한용님(06)의  ItemType enum 사용

        [Header("수리량")]
        [Tooltip("수리에 적용되는 값")]
        [SerializeField] private float value;

        [Header("내구도")]
        [Tooltip("내구도 사용 여부")]
        [SerializeField] private bool hasDurability;
        [Tooltip("내구도 최대치")]
        [SerializeField, Min(1)] private int maxDurability = 100;

        public bool HasDurability => hasDurability;
        public int MaxDurability => maxDurability;

        public float Value => value;
    }
}
