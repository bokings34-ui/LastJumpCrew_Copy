using LastJumpCrew.Common;
using LastJumpCrew.ParkHanSol.Items;   // UtilityItemObject (영속 아이템 컴포넌트, 읽기 전용)
using LastJumpCrew.SeoBoGyeong.item;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LastJumpCrew.SeoBoGyeong.Economy
{
    public enum TradeType
    {
        None,
        Sell,
        Buy,
    }

    // 스캔 영역을 같은 오브젝트의 BoxCollider 에서 읽으므로 BoxCollider 를 강제한다.
    [RequireComponent(typeof(BoxCollider))]
    public class CheckoutDetector : MonoBehaviour
    {
        public readonly List<ShopItemTag> basket = new();
        private readonly HashSet<GameObject> shopItems = new();      // 바구니 멤버십(O(1) 판정)
        private readonly HashSet<ShopItemTag> _present = new();      // 재조회용 임시 집합(재사용)

        [SerializeField] private TradeType _type;
        [SerializeField] private TMP_Text textUI;
        [Tooltip("아이템 영속 식별(UtilityItemObject.ItemId, string)을 경제 데이터(int id·가격)로 잇는 브릿지. 씬의 UtilityConnect 를 연결한다.")]
        [SerializeField] private UtilityConnect utilityConnect;

        [Header("OverlapBox 스캔")]
        [Tooltip("스캔 주기(초). 이 주기로 박스 안 상품을 전체 재조회한다.")]
        [SerializeField] private float scanInterval = 0.2f;
        [Tooltip("스캔 범위 레이어. 레지스트리/UtilityItemObject 로 상품만 걸러내므로 기본 Everything 로 둬도 정확. 성능용으로 상품이 이미 올라간 '기존' 레이어로 좁히는 것은 선택.")]
        [SerializeField] private LayerMask scanLayerMask = ~0;
        [Tooltip("켜면 매 스캔의 히트/인식/바구니 수를 Console 에 남긴다(진단용).")]
        [SerializeField] private bool debugLog = false;

        private int _totalPrice = 0;
        private string prefix = "Total : $";

        private IAuthority _authority;                              // 서버 권위 게이트(로컬=항상 true)
        private BoxCollider _box;                                   // 스캔 영역 소스(같은 오브젝트 트리거)
        private readonly Collider[] _scanBuffer = new Collider[64]; // NonAlloc 버퍼(GC 0). 상한 64.

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            UpdateTotal();
        }

        private void Start()
        {
            // 규율: Services resolve 는 시작 시 1회 캐싱(매 프레임 조회 금지).
            if (GameCore.Instance != null)
                _authority = GameCore.Instance.Services.Get<IAuthority>();
        }



        // 권위측(서버)만 감지·바구니를 확정한다. 로컬(권위 미주입/LocalAuthority)은 항상 true.
        private bool IsAuthority => _authority == null || _authority.IsServer;

        private void OnEnable()
        {
            StartCoroutine(ScanLoop());
        }

        private IEnumerator ScanLoop()
        {
            var wait = new WaitForSeconds(scanInterval);
            while (true)
            {
                if (_type == TradeType.Buy && IsAuthority) Reconcile();
                yield return wait;
            }
        }

        // 박스 안을 통째로 재조회해 바구니를 실제 상태와 일치시킨다.
        // 트리거 이벤트 타이밍에 의존하지 않으므로 투척·드롭·튕김·터널링 모두 잡는다.
        private void Reconcile()
        {
            if (_box == null) return;

            Vector3 center = transform.TransformPoint(_box.center);
            Vector3 halfExtents = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(
                center, halfExtents, _scanBuffer, transform.rotation, scanLayerMask, QueryTriggerInteraction.Ignore);

            // 1) 지금 박스 안에 '안 들린' 상품 집합을 만든다.
            _present.Clear();
            for (int i = 0; i < count; i++)
            {
                ShopItemTag tag = ResolveTag(_scanBuffer[i]);
                if (tag == null) continue;                                       // 상품 아님/해석 실패
                if (tag.GetComponentInParent<IItemHolder>() != null) continue;   // 들림(플레이어/보관장치)
                _present.Add(tag);
            }

            bool changed = false;

            // 2) 새로 들어온 상품 추가.
            foreach (var tag in _present)
            {
                if (shopItems.Add(tag.gameObject))
                {
                    basket.Add(tag);
                    changed = true;
                }
            }

            // 3) 사라진(빠진·들림·파괴된) 상품 제거.
            for (int i = basket.Count - 1; i >= 0; i--)
            {
                var tag = basket[i];
                if (tag != null && _present.Contains(tag)) continue;

                if (tag != null) shopItems.Remove(tag.gameObject);
                basket.RemoveAt(i);
                changed = true;
            }

            if (changed) UpdateTotal();

            if (debugLog)
                Debug.Log($"[Checkout] hits={count} present={_present.Count} basket={basket.Count} total={_totalPrice}");
        }

        // 히트 Collider 를 ShopItemTag 로 해석한다.
        //  1) 이미 등록된 상품이면 레지스트리에서 즉시(재조회 없음).
        //  2) 미등록이면 영속 컴포넌트 UtilityItemObject 로 인식하고, UtilityConnect 로 경제 데이터를 찾아
        //     그 자리에서 ShopItemTag 를 붙인다 — 픽업→드롭 재생성으로 태그가 날아가도 다시 인식되게 한다.
        private ShopItemTag ResolveTag(Collider col)
        {
            if (ShopItemTag.TryGet(col, out var tag)) return tag;

            var uio = col.GetComponentInParent<UtilityItemObject>();
            if (uio == null) return null;                       // 상품(아이템 오브젝트) 아님

            if (utilityConnect == null)
            {
                Debug.LogError("[Checkout] utilityConnect 가 인스펙터에 연결되지 않음 — 재생성 아이템 인식 불가");
                return null;
            }

            if (!utilityConnect.TryGetData(uio.ItemId, out var data) || data == null) return null;

            if (!uio.TryGetComponent(out tag))
                tag = uio.gameObject.AddComponent<ShopItemTag>();   // OnEnable 이 콜라이더를 레지스트리에 등록

            tag.Init(data.Id, data.Price);
            return tag;
        }

        public bool CheckBasket() => basket.Count > 0;

        public ShopItemTag[] GetBasket() => basket.ToArray();
        // 바구니 합계를 다시 계산해 UI 갱신(비었으면 $0 로 표기).
        public void UpdateTotal()
        {
            int sum = 0;
            for (int i = 0; i < basket.Count; i++)
                if (basket[i] != null) sum += basket[i].ItemPrice;

            _totalPrice = sum;
            if (textUI != null) textUI.text = prefix + _totalPrice.ToString();
        }

        // 외부 호환용(기존 호출부 유지). 내부적으로 UpdateTotal 과 동일 역할.
        public void RefreshTotalPrice(int totalprice = 0) => UpdateTotal();
    }
}
