using UnityEngine;

namespace LastJumpCrew.ParkHanSol.Items
{
    public enum UtilityItemUpgradeEffect
    {
        None,
        RestoreShipHp,
        IncreaseShipMaximumHp,
        IncreaseHookPower,
        IncreaseThrusterDuration,
        IncreasePlayerMaximumHp
    }

    // 아이템 하나가 런타임에서 어떤 프리팹/아이콘/표시명을 사용할지 정의하는 데이터 asset이다.
    // 실제 아이템 오브젝트에 붙는 컴포넌트는 UtilityItemObject이고, 이 ScriptableObject는 그 오브젝트가 참조하는 설정값이다.
    // 팀원이 새 아이템을 추가할 때는 prefab만 만드는 것으로 끝내지 말고, 이 데이터 asset도 같이 만든 뒤 itemPrefabData에 연결해야 한다.
    [CreateAssetMenu(
        fileName = "PHS_UtilityItemPrefabData",
        menuName = "LastJumpCrew/ParkHanSol/Utility Item Prefab Data")]
    public sealed class UtilityItemPrefabData : ScriptableObject
    {
        // 코드와 로그에서 구분할 때 쓰는 내부 ID다.
        // TryConsumeHeldItem 같은 비교 로직에서 문자열로 비교하므로, 같은 아이템끼리는 같은 ID를 유지해야 한다.
        // 예: wrench, battery_pack, foam_sealant_gun.
        [SerializeField] private string itemId;

        // UI에 그대로 표시되는 이름이다.
        // HUD의 들고 있는 아이템 이름 표시, 추후 상점/툴팁 표시에도 이 값을 재사용한다.
        [SerializeField] private string displayName;

        // HUD에 표시되는 아이콘이다.
        // 최종 아이콘은 04. Data/UtilityItems/Icons 아래 Sprite로 둔다.
        // null이면 HUD 아이콘 Image가 투명 처리된다.
        [SerializeField] private Sprite icon;

        // 상점 계산 구역에서 합산할 테스트용 가격이다.
        // 실제 경제/재화 차감은 나중에 경제 시스템이 붙을 때 연결한다.
        [SerializeField, Min(0)] private int price;

        // 내구도가 있는 아이템만 true로 켠다.
        // 모든 아이템에 내구도를 강제하지 않기 위해 선택형 필드로 둔다.
        // false면 HUD의 내구도 텍스트는 숨겨진다.
        [SerializeField] private bool hasDurability;

        // hasDurability가 true인 아이템의 기본 최대 내구도다.
        // 현재는 표시용 기준값이고, 실제 감소/수리 로직은 아직 붙어 있지 않다.
        [SerializeField, Min(1)] private int maxDurability = 100;

        // 플레이어 손/보관함 시각화에 생성되는 프리팹이다.
        // 이 프리팹의 루트에는 UtilityItemObject가 있어야 한다.
        [SerializeField] private GameObject heldPrefab;

        // 바닥에 떨어뜨릴 때 생성되는 프리팹이다.
        // 현재는 heldPrefab과 같은 프리팹을 넣어도 된다.
        // 별도 드롭 모델이 필요해지면 여기만 다른 프리팹으로 교체한다.
        [SerializeField] private GameObject droppedPrefab;

        [Header("Temporary Upgrade")]
        [SerializeField] private UtilityItemUpgradeEffect upgradeEffect;
        [SerializeField, Min(0f)] private float upgradeAmount;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Price => price;
        public bool HasDurability => hasDurability;
        public int MaxDurability => maxDurability;
        public GameObject HeldPrefab => heldPrefab;
        public GameObject DroppedPrefab => droppedPrefab;
        public UtilityItemUpgradeEffect UpgradeEffect => upgradeEffect;
        public float UpgradeAmount => upgradeAmount;
        public bool IsUpgradeItem => upgradeEffect != UtilityItemUpgradeEffect.None && upgradeAmount > 0f;
        public bool HasHeldPrefab => heldPrefab != null;
        public bool HasDroppedPrefab => droppedPrefab != null;
    }
}
