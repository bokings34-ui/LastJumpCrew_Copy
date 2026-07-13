using LastJumpCrew.SeBoGyeong.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
namespace LastJumpCrew.SeoBoGyeong
{
    public class RangeItemSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] shelfAreas;

        //나중에 데이터 테이블 연결 예정
         private GameObject[] _itemPrefabs;
        [SerializeField] private int totalItemCount = 5;
        //[Tooltip("배치 공간 길이\n외곽선 걸침 방지를 위해 자체적으로 -1 처리됨")]
        //[SerializeField] private float shelfLength = 8f;
        [SerializeField] private float minItemSpacing = 1f;
        [SerializeField] private float[] length;
        private int[] _spawnItemIndices;
        private List<int> _itemsPerShelf = new List<int>();
        private float[] _shelfAreaLengths;
        private DataRepository<UtilityItemData> _tools; 



        private void Awake()
        {
            if (shelfAreas == null )
            {
                Debug.LogError("[ShopSpawn] Shelf areas 가 연결되지 않음.");
            }

            _spawnItemIndices = new int[totalItemCount];
            _shelfAreaLengths = length; //new float[shelfAreas.Length];
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
           
            for (int i = 0; i < item.Length; i++)
            {
                _itemPrefabs[i] = item[i].DroppedPrefab;
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
                    Debug.Log($"공간 충족 {shelfCount}개");
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

                float usableLength = areaLength[i];// - 1f;



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

        [ContextMenu("Spawn Item")]
        private void SpawnItem()
        {
            DivideSection(_shelfAreaLengths);

            Vector3 center = shelfAreas[0].transform.position;

            for (int i = 0; i < totalItemCount; i++)
            {
                _spawnItemIndices[i] = Random.Range(0, _itemPrefabs.Length);
            }
            Array.Sort(_spawnItemIndices, (a, b) => b.CompareTo(a));

            // 아이템이 하나면 가운데 배치
            if (totalItemCount == 1)
            {
                Instantiate(_itemPrefabs[_spawnItemIndices[0]], center, Quaternion.identity);
                return;
            }

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
                    Instantiate(_itemPrefabs[_spawnItemIndices[spawnIndex++]], pos, rotation);
                }
                Debug.Log($"선반 {i + 1}번: 아이템 {itemsOnShelf}개 배치 완료. 간격: {itemSpacing}");
            }

        }
    }
}
