using LastJumpCrew.SeoBoGyeong.Data;
using LastJumpCrew.SeoBoGyeong.Economy;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastJumpCrew.SeoBoGyeong
{
    /// <summary>
    /// 상점 선반에 판매 아이템을 랜덤 진열하는 스포너.
    /// 스폰 직후 ShopItemTag(itemId)를 부착해 ItemCheckout 이 상품을 인식하게 한다.
    /// </summary>
    public class RangeItemSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] shelfAreas;

        // 데이터 테이블(GameCore.Data.Tools)에서 채운다
        private GameObject[] _itemPrefabs;
        private int[] _itemIds; // _itemPrefabs 와 같은 색인의 판매용 itemId
        [SerializeField] private int totalItemCount = 5;
        //[Tooltip("배치 가능 길이\n단순히 겹침 방지용 — 실제 길이에서 -1 처리됨")]
        //[SerializeField] private float shelfLength = 8f;
        [SerializeField] private float minItemSpacing = 1f;
        [SerializeField] private float[] length;
        private int[] _spawnItemIndices;
        private List<int> _itemsPerShelf = new List<int>();
        private float[] _shelfAreaLengths;
        private DataRepository<UtilityItemData> _tools;



        private void Awake()
        {
            if (shelfAreas == null)
            {
                Debug.LogError("[ShopSpawn] Shelf areas 가 연결되지 않음.");
            }

            _spawnItemIndices = new int[totalItemCount];
            _shelfAreaLengths = length;
        }

        private void Start()
        {
            _tools = GameCore.Instance.Data.Tools;
            FillItem();

            SpawnItem();
        }

        private void FillItem()
        {
            UtilityItemData[] item = _tools.All.Values.ToArray<UtilityItemData>();
            _itemPrefabs = new GameObject[item.Length];
            _itemIds = new int[item.Length];

            for (int i = 0; i < item.Length; i++)
            {
                _itemPrefabs[i] = item[i].DroppedPrefab;
                _itemIds[i] = item[i].Id;
            }

        }
        private void DivideSection(float[] areaLength)
        {
            _itemsPerShelf.Clear();

            int maxItemsPerShelf = totalItemCount;
            int areaMaxItems = 0;
            int shelfCount = 0;
            for (int i = 0; i < areaLength.Length; i++)
            {
                if (areaMaxItems >= totalItemCount)
                {
                    Debug.Log($"사용 선반 {shelfCount}개");
                    break;
                }
                else if (areaMaxItems < totalItemCount)
                {
                    if (areaMaxItems + areaLength[i] <= totalItemCount)
                    {
                        areaMaxItems += Mathf.FloorToInt(areaLength[i]);
                        shelfCount++;
                    }
                }

                float usableLength = areaLength[i];



                maxItemsPerShelf = Mathf.RoundToInt(usableLength / minItemSpacing);
                int remainingItems = totalItemCount;


                if (remainingItems > maxItemsPerShelf)
                {
                    _itemsPerShelf.Add(maxItemsPerShelf);
                    remainingItems -= maxItemsPerShelf;
                }
                else
                {
                    _itemsPerShelf.Add(remainingItems);
                    break;
                }

            }
        }

        /// <summary>진열품 1개 스폰 + 판매용 itemId 태그 부착(프리팹 수정 없이 코드로 연결).</summary>
        private void SpawnDisplayItem(int prefabIndex, Vector3 pos, Quaternion rot)
        {
            var go = Instantiate(_itemPrefabs[prefabIndex], pos, rot);
            go.AddComponent<ShopItemTag>().Init(_itemIds[prefabIndex]);
        }

        [ContextMenu("Spawn Item")]
        private void SpawnItem()
        {
            Vector3 center = shelfAreas[0].transform.position;

            
            // 아이템이 하나면 가운데 배치
            if (totalItemCount == 1)
            {
                SpawnDisplayItem(_spawnItemIndices[0], center, Quaternion.identity);
                return;
            }

            DivideSection(_shelfAreaLengths);           

            for (int i = 0; i < totalItemCount; i++)
            {
                _spawnItemIndices[i] = Random.Range(0, _itemPrefabs.Length);
            }
            Array.Sort(_spawnItemIndices, (a, b) => b.CompareTo(a));

            

            int spawnIndex = 0;

            for (int i = 0; i < shelfAreas.Length; i++)
            {

                Vector3 pos = shelfAreas[i].transform.position;

                int itemsOnShelf = _itemsPerShelf[i];
                //Debug.Log($"{itemsOnShelf} / {i}");
                //Debug.Log($"{spawnIndex} / {_shelfAreaLengths[i]}");
                float usableLength = _shelfAreaLengths[i];// - 1f;

                float itemSpacing = usableLength / (itemsOnShelf - 1);
                float leftOffset = -usableLength * 0.5f;

                for (int j = 0; j < itemsOnShelf; j++)
                {
                    float x = shelfAreas[i].transform.position.x + leftOffset + itemSpacing * j;
                    Quaternion rotation = shelfAreas[i].transform.rotation;
                    pos.x = x;
                    SpawnDisplayItem(_spawnItemIndices[spawnIndex++], pos, rotation);
                }
                Debug.Log($"선반 {i + 1}번: 아이템 {itemsOnShelf}개 배치 완료. 간격: {itemSpacing}");
            }

        }
    }
}
