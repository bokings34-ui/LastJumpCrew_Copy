using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
namespace LastJumpCrew.SeoBoGyeong
{
    public class RangeItemSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject[] shelfAreas;
        //나중에 데이터 테이블 연결 예정
        [SerializeField] private GameObject[] itemPrefabs;
        [SerializeField] private int totalItemCount = 5;
        //[Tooltip("배치 공간 길이\n외곽선 걸침 방지를 위해 자체적으로 -1 처리됨")]
        //[SerializeField] private float shelfLength = 8f;
        [SerializeField] private float minItemSpacing = 1f;
        [SerializeField] private float[] length;
        private int[] _spawnItemIndices;
        private List<int> _itemsPerShelf = new List<int>();
        private float[] _shelfAreaLengths;

        //일정한 간격이 아닐테니 수정하기


        private void Awake()
        {
            if (shelfAreas == null || itemPrefabs == null)
            {
                Debug.LogError("[ShopSpawn] Shelf areas 나 item prefabs 가 연결되지 않음.");
            }

            _spawnItemIndices = new int[totalItemCount];
            _shelfAreaLengths = length; //new float[shelfAreas.Length];
        }

        private void Start()
        {
            SpawnItem();
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


        private void SpawnItem()
        {
            /*
                        float usableLength = shelfLength - 1f;
                        _itemsPerShelf.Clear();

                        Vector3 center = shelfAreas[0].transform.position;

                        float leftOffset = -usableLength * 0.5f;
                        float itemSpacing = usableLength / (totalItemCount - 1);

                        int maxItemsPerShelf;
                        // 1번 선반에 몰아넣기
                        if ((totalItemCount - 1) * minItemSpacing > usableLength)
                        {
                            maxItemsPerShelf = Mathf.RoundToInt(usableLength / minItemSpacing);
                            int remainingItems = totalItemCount;
                            if (remainingItems > maxItemsPerShelf)
                            {
                                int shelfCount = Mathf.CeilToInt((float)(remainingItems / maxItemsPerShelf));
                                for (int i = 0; i <= shelfCount; i++)
                                {
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
                            else
                            {
                                _itemsPerShelf.Add(remainingItems);
                            }
                        }
                        else
                        {
                            maxItemsPerShelf = totalItemCount;
                            _itemsPerShelf.Add(maxItemsPerShelf);
                        }

                        if (shelfAreas.Length < _itemsPerShelf.Count)
                        {
                            Debug.Log($"선반 개수 부족. 선반/필요: {shelfAreas.Length}/{_itemsPerShelf.Count} || 아이템 : {_itemsPerShelf[0]}개");
                            return;
                        }

            */

            DivideSection(_shelfAreaLengths);

            Vector3 center = shelfAreas[0].transform.position;



            for (int i = 0; i < totalItemCount; i++)
            {
                _spawnItemIndices[i] = Random.Range(0, itemPrefabs.Length);
            }
            Array.Sort(_spawnItemIndices, (a, b) => b.CompareTo(a));

            // 아이템이 하나면 가운데 배치
            if (totalItemCount == 1)
            {
                Instantiate(itemPrefabs[_spawnItemIndices[0]], center, Quaternion.identity);
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
                    Instantiate(itemPrefabs[_spawnItemIndices[spawnIndex++]], pos, rotation);
                }
                Debug.Log($"선반 {i + 1}번: 아이템 {itemsOnShelf + 1}개 배치 완료. 간격: {itemSpacing}");
            }

        }
    }
}
