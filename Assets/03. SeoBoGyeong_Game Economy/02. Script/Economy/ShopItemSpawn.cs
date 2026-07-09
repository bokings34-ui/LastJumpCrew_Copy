using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShopItemSpawn : MonoBehaviour
{
    [SerializeField] private GameObject[] itemslotPrefabs;
    //나중에 데이터 테이블 연결 예정
    [SerializeField] private GameObject[] itemlist;
    [SerializeField] private int maxItemCount = 5;
    [Tooltip("배치 공간 길이\n외곽선 걸침 방지를 위해 자체적으로 -1 처리됨")]
    [SerializeField] private float shelfLength = 8f;
    [SerializeField] private float spawnInterval = 1f;
    private int[] _spawnId;
    private List<int> stockItem = new List<int>();

    private void Start()
    {
        _spawnId = new int[maxItemCount];
        SpawnItem();
    }


    private void SpawnItem()
    {
        float usableLength = shelfLength - 1f;
        stockItem.Clear();

        Vector3 center = itemslotPrefabs[0].transform.position;

        float left = -usableLength * 0.5f;
        float interval = usableLength / (maxItemCount - 1);
       
        int maxSlot;
        // 1번 선반에 몰아넣기
        if ((maxItemCount-1) * spawnInterval > usableLength)
        {
            maxSlot = Mathf.RoundToInt(usableLength / spawnInterval);
            int amount = maxItemCount;
            if (amount > maxSlot)
            {
                int loopCount = Mathf.CeilToInt((float)(amount / maxSlot));
                for (int i = 0; i <= loopCount; i++)
                {
                    if (amount > maxSlot)
                    {
                        stockItem.Add(maxSlot);
                        amount -= maxSlot;
                    }
                    else
                    {
                        stockItem.Add(amount);
                        break;
                    }
                }
            }
            else
            {
                stockItem.Add(amount);
            }
        }
        else
        {
            maxSlot = maxItemCount;
            stockItem.Add(maxSlot);
        }

        if (itemslotPrefabs.Length < stockItem.Count)
        {
            Debug.Log($"선반 개수 부족. 선반/필요: {itemslotPrefabs.Length}/{stockItem.Count}");
            return;
        }

        for (int i = 0; i < maxItemCount; i++)
        {
            _spawnId[i] = Random.Range(0, itemlist.Length) ;
        }
        Array.Sort(_spawnId, (a, b) => b.CompareTo(a));

        // 아이템이 하나면 가운데 배치
        if (maxItemCount == 1)
        {
            Instantiate(itemlist[_spawnId[0]], center, Quaternion.identity);
            return;
        }

        int spawnIndex = 0;

        for (int i = 0; i < stockItem.Count; i++)
        {

            Vector3 pos = itemslotPrefabs[i].transform.position;

            int spawnCount = maxSlot * i;

            int maxSpawn = stockItem[i];
            Debug.Log($"{maxSpawn} / {i}");

            interval = usableLength / (maxSpawn - 1);
            for (int j = 0; j < maxSpawn; j++)
            {

                float x = itemslotPrefabs[i].transform.position.x + left + interval * j;

                pos.x = x;


                Instantiate(itemlist[_spawnId[spawnIndex++]], pos, Quaternion.identity);

            }
            Debug.Log($"선반 {i}번: 아이템 {maxSpawn}개 배치 완료. 간격: {interval}");
        }

    }
}
