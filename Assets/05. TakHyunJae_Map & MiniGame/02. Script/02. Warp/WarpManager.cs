using UnityEngine;

public class WarpManager : MonoBehaviour
{
    public static WarpManager Instance { get; private set; }

    [Header("워프 이펙트 (실린더 메쉬)")]
    public GameObject warpObject; // 스크린샷에 있는 그 원기둥 오브젝트를 연결할 칸

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 게임이 시작될 때는 워프 이펙트를 안 보이게 꺼둡니다.
        if (warpObject != null)
        {
            warpObject.SetActive(false);
        }
    }

    // 워프 켜기
    public void StartWarp()
    {
        if (warpObject != null)
        {
            warpObject.SetActive(true);
        }
    }

    // 워프 끄기
    public void StopWarp()
    {
        if (warpObject != null)
        {
            warpObject.SetActive(false);
        }
    }
}