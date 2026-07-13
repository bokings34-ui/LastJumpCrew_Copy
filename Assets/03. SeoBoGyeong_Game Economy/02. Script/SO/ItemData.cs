using LastJumpCrew.Common;
using UnityEngine;
namespace LastJumpCrew.SeBoGyeong.Data
{
    [CreateAssetMenu(
        fileName = "Item_New",
        menuName = "LastJumpCrew/SeoBoGyeong/Item Data")]
    public class ItemData : ScriptableObject, IGameData
    {
        [Header("기본 정보")]
        [Tooltip("int로 구성된 ID")]
        [SerializeField] private int id;
        [Tooltip("UI에 표시되는 이름")]
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [TextArea]
        [SerializeField] private string description;
        

        [Header("오브젝트 Prefab")]
        [Tooltip("플레이어가 들었을 때의 오브젝트")]
        [SerializeField] private GameObject heldPrefab;
        [Tooltip("플레이어가 내려놓았을 때의 오브젝트")]
        [SerializeField] private GameObject droppedPrefab;       

        [Header("구매 & 판매")]
        [Tooltip("구매 가능 여부")]
        [SerializeField] private bool canBuy;
        [Tooltip("판매 가능 여부")]
        [SerializeField] private bool canSell;
        [Tooltip("가격")]
        [SerializeField, Min(0)] private int price;

        public int Id => id;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
       
        public int Price => price;
        public GameObject HeldPrefab => heldPrefab;
        public GameObject DroppedPrefab => droppedPrefab;

        public bool CanBuy => canBuy;
        public bool CanSell => canSell;
        public bool HasHeldPrefab => HeldPrefab != null;
        public bool HasDroppedPrefab => DroppedPrefab != null;
    }
}

