using UnityEngine;


public class DataManager : MonoBehaviour
{
    public ItemRepository Items { get; private set; }
    public EventRepository Events { get; private set; }

    [SerializeField] private ItemDataTest[] ItemDataTest;
    //[SerializeField] private EventData[] eventDatas;

    public void Inint()
    {
        //Items = new ItemRepository(ItemDataTest);
        //Events = new EventRepository(eventDatas);
        Debug.Log("DataManager Inint");
    }
}
