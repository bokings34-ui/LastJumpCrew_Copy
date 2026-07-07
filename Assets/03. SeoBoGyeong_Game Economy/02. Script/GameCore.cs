using UnityEngine;

public class GameCore : MonoBehaviour
{
    public static GameCore Instance { get; private set; }


    //하위클래스 위치
    private DataManager Data;




    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        //순서
        // 데이터 -> 네트워크 -> 플레이어 정보
        Data.Inint();
    }
}
