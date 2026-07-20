using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Multiplayer.Customization
{
    // 코스메틱 한 종류의 상점/장착 데이터를 정의하는 ScriptableObject다.
    // itemId는 저장 데이터와 멀티 동기화에 사용하므로 생성 후 바꾸지 않는다.
    [CreateAssetMenu(
        fileName = "PHS_CosmeticItemData",
        menuName = "LastJumpCrew/ParkHanSol/Customization/Cosmetic Item Data")]
    public sealed class CosmeticItemData : ScriptableObject
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField] private CosmeticSlot slot;
        [SerializeField, Min(0)] private int price;
        [SerializeField] private Sprite icon;

        // 플레이어 장착 슬롯 아래에 생성할 시각 프리팹이다.
        [SerializeField] private GameObject visualPrefab;

        // 각 임시 도형 모델을 슬롯 기준으로 맞추는 값이다.
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public CosmeticSlot Slot => slot;
        public int Price => price;
        public Sprite Icon => icon;
        public GameObject VisualPrefab => visualPrefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale;
    }
}
