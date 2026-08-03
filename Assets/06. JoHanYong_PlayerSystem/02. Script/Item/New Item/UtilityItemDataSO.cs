using LastJumpCrew.ParkHanSol.Items;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


namespace LastJumpCrew.Common
{
    [CreateAssetMenu(fileName = "NewUtilityItemData", menuName = "LastJumpCrew/Items/Utility Item Data")]

    public sealed class UtilityItemDataSO : ScriptableObject
    {
        [Header("Basic Information")]

        [Tooltip("코드에서 아이템을 구분할 때 사용하는 고유 ID")]
        [SerializeField]
        private string itemId;

        [Tooltip("UI와 Inspector에 표시할 아이템 이름")]
        [SerializeField]
        private string displayName;

        [Tooltip("인벤토리 또는 HUD에 표시할 아이콘")]
        [SerializeField]
        private Sprite icon;

        [Tooltip("아이템 가격")]
        [SerializeField, Min(0)]
        private int price;

        [Tooltip("아이템의 사용 방식")]
        [SerializeField]
        private ItemUseType useType;

        [Header("Prefabs")]

        [Tooltip("플레이어가 손에 들었을 때 표시되는 프리팹")]
        [SerializeField]
        private GameObject handPrefab;

        [Tooltip("바닥에 내려놓았을 때 사용한느 월드 프리팹")]
        [SerializeField]
        private GameObject droppedPrefab;

        [Tooltip("투척 아이템이 실제로 날아갈 때 사용하는 프리팹")]
        [SerializeField]
        private GameObject thrownPrefab;

        [Header("Held Presentation")]
        [Tooltip("소유자 1인칭 화면에서 사용하는 아이템 위치")]
        [SerializeField]
        private UtilityHeldItemPose firstPersonHeldPose = new(Vector3.zero, Vector3.zero, 1f);

        [Tooltip("다른 플레이어에게 보이는 아이템 위치")]
        [SerializeField]
        private UtilityHeldItemPose worldHeldPose = new(Vector3.zero, Vector3.zero, 1f);


        [Header("Use Settings")]

        [Tooltip("아이템 사용 후 다시 사용할 수 있을 때까지의 시간")]
        [SerializeField, Min(0f)]
        private float cooldown = 0.5f;

        [Tooltip("근접 또는 분사 판정이 도달할 수 있는 거리")]
        [SerializeField, Min(0f)]
        private float attackRange = 1.5f;

        [Tooltip("OverlapSphere 또는 분사 판정의 반경")]
        [SerializeField, Min(0f)]
        private float attackRadius = 1f;

        [Tooltip("분사형 아이템 부채꼴 전체 각도")]
        [SerializeField, Range(0f, 360f)]
        private float attackAngle = 60f; // 추가 : 소화기 부채꼴 전체 각도

        [Tooltip("공격과 상호작용 대상을 검사할 레이어")]
        [SerializeField]
        private LayerMask targetLayers;

        [Header("Throw Settings")]

        [Tooltip("투척 아이템을 앞으로 날리는 힘")]
        [SerializeField, Min(0f)]
        private float throwForce = 12f;

        [Tooltip("투척할 때 위쪽으로 추가하는 힘")]
        [SerializeField, Min(0f)]
        private float upwardForce = 1.5f;

        [Header("Repair")]

        [Tooltip("한 번의 수리가 성공했을 때 대상에게 적용하는 수리량")]
        [SerializeField, Min(0)]
        private float repairAmount;

        [Header("Durability")]

        [Tooltip("이 아이템이 내구도를 사용하는지 여부")]
        [SerializeField]
        private bool usesDurability;

        [Tooltip("새 아이템의 최대 내구도")]
        [SerializeField, Min(1)]
        private int maxDurability = 100;

        [Tooltip("한 번의 유효한 사용이 성공했을 때 감소하는 내구도")]
        [SerializeField, Min(0)]
        private int durabilityCostPerUse = 1;


        [Header("Hit Effects")]

        [Tooltip("적중한 대상에게 순서대로 적용할 효과 목록")]
        [SerializeField]
        private List<ItemEffectData> hitEffects = new();

        [Header("Event Influence")]

        [Tooltip("이벤트 종류별 작동량과 내구도 소비 설정")]
        [SerializeField]
        private List<UtilityItemActionProfile> actionProfiles = new();

        [Header("Temporary Upgrade")]

        [Tooltip("아이템 사용 시 적용되는 임시 업그레이드 효과")]
        [SerializeField]
        private UtilityItemUpgradeEffect upgradeEffect;

        [Tooltip("업그레이드 효과의 적용량")]
        [SerializeField, Min(0f)]
        private float upgradeAmount;

        // 기본 정보
        public string ItemId => itemId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int Price => price;
        public ItemUseType UseType => useType;

        // 프리팹
        public GameObject HandPrefab => handPrefab;
        public GameObject DroppedPrefab => droppedPrefab;
        public GameObject ThrownPrefab => thrownPrefab;

        public bool HasHandPrefab => handPrefab != null;
        public bool HasDroppedPrefab => droppedPrefab != null;
        public bool HasThrownPrefab => thrownPrefab != null;


        // 사용 설정
        public float Cooldown => cooldown;
        public float AttackRange => attackRange;
        public float AttackRadius => attackRadius;
        public float AttackAngle => attackAngle;
        public LayerMask TargetLayers => targetLayers;

        // 투척 설정
        public float ThrowForce => throwForce;
        public float UpwardForce => upwardForce;

        // 수리
        public float RepairAmount => repairAmount;

        // 내구도 설정
        public bool UsesDurability => usesDurability;
        public int MaxDurability => maxDurability;
        public int DurabilityCostPerUse => durabilityCostPerUse;

        // 효과 목록
        public IReadOnlyList<ItemEffectData> HitEffects => hitEffects;

        // 이벤트 행동
        public IReadOnlyList<UtilityItemActionProfile> ActionProfiles => actionProfiles;

        // 업그레이드
        public UtilityItemUpgradeEffect UpgradeEffect => upgradeEffect;
        public float UpgradeAmount => upgradeAmount;

        public bool IsUpgradeItem => upgradeEffect != UtilityItemUpgradeEffect.None && upgradeAmount > 0f;
        public bool TryGetHeldPose(bool firstPerson, out UtilityHeldItemPose heldPose)
        {
            heldPose = firstPerson ? firstPersonHeldPose : worldHeldPose;

            return heldPose.IsValid;
        }
        public bool TryGetActionProfile(UtilityItemActionKind actionKind, out UtilityItemActionProfile profile)
        {
            profile = default;

            if (actionKind == UtilityItemActionKind.None || actionProfiles == null)
            {
                return false;
            }
            foreach (UtilityItemActionProfile candidate in actionProfiles)
            {
                if (candidate.ActionKind == actionKind &&
                    candidate.IsValid)
                {
                    profile = candidate;
                    return true;
                }
            }
            return false;
        }

        private void OnValidate()
        {
            itemId = itemId?.Trim();

            price = Mathf.Max(0, price);

            cooldown = Mathf.Max(0f, cooldown);
            attackRange = Mathf.Max(0f, attackRange);
            attackRadius = Mathf.Max(0f, attackRadius);

            throwForce = Mathf.Max(0f, throwForce);
            upwardForce = Mathf.Max(0f, upwardForce);

            repairAmount = Mathf.Max(0, repairAmount);
            maxDurability = Mathf.Max(1, maxDurability);
            durabilityCostPerUse = Mathf.Max(0, durabilityCostPerUse);

            upgradeAmount = Mathf.Max(0f, upgradeAmount);

            if (!usesDurability)//내구도를 사용 안하는 아이템은 내구도 소모량을 0으로 설정
            {
                durabilityCostPerUse = 0;
            }
            else
            {
                durabilityCostPerUse = Mathf.Clamp(durabilityCostPerUse, 0, maxDurability);
            }
            ValidateHitEffects();
            ValidateActionProfiles();
        }
        private void ValidateHitEffects()
        {
            if (hitEffects == null)
            {
                hitEffects = new List<ItemEffectData>();

                Debug.LogError($"PHS_ITEM_EFFECT_LIST_INVALID " + $"reason=list_missing item={itemId}",this);
            }
        }
        private void ValidateActionProfiles()
        {
            if (actionProfiles == null)
            {
                Debug.LogError($"PHS_UTILITY_ITEM_PROFILE_INVALID " + $"reason=list_missing item={itemId}", this);

                return;
            }
            var actionKinds = new HashSet<UtilityItemActionKind>();

            foreach (UtilityItemActionProfile profile in actionProfiles)
            {
                if (!profile.IsValid)
                {
                    Debug.LogError($"PHS_UTILITY_ITEM_PROFILE_INVALID " + $"reason=entry_invalid " + $"item={itemId} " + $"action={profile.ActionKind}", this);

                    continue;
                }
                if (!actionKinds.Add(profile.ActionKind))
                {
                    Debug.LogError($"PHS_UTILITY_ITEM_PROFILE_INVALID " + $"reason=duplicate_action " + $"item={itemId} " + $"action={profile.ActionKind}", this);
                }
            }
        }

    }
}
