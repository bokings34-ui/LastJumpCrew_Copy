using UnityEngine;

[RequireComponent (typeof(Rigidbody))]
public class GravityController : MonoBehaviour
{
    [Header("Gravity State")]
    [SerializeField] private GravityState2 currentState = GravityState2.ZeroGravity;

    private Rigidbody rb;

    public GravityState2 CurrentState => currentState;
    public bool IsGravity => currentState == GravityState2.Gravity;
    public bool IsZeroGravity => currentState == GravityState2.ZeroGravity;
    // Update is called once per frame
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
    }
    public void SetGravityState(GravityState2 state)
    {
        currentState = state;

        if (currentState == GravityState2.ZeroGravity)
        {
            // 무중력에서는 Unity 기본 중력 OFF
            rb.useGravity = false;
        }
    }
}
