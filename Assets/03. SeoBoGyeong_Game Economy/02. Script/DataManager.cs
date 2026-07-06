using System;
using UnityEngine;


public class DataManager
{
    public ItemRepository Items { get; private set; }
    public EventRepository Events { get; private set; }

    //[SerializeField] private ItemData[] itemDatas;
    //[SerializeField] private EventData[] eventDatas;

    public void Inint()
    {
        //Items = new ItemRepository(itemDatas);
        //Events = new EventRepository(eventDatas);
        Debug.Log("DataManager Inint");
    }
}
