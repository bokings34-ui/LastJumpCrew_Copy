using UnityEngine;

[RequireComponent (typeof(Rigidbody))]
public class GravityController : MonoBehaviour
{
    [Header("Gravity State")]
    [SerializeField] private GravityState currentState = GravityState.ZeroGravity;

    private Rigidbody rb;

    public GravityState CurrentState => currentState;
    public bool IsGravity => currentState == GravityState.Gravity;
    public bool IsZeroGravity => currentState == GravityState.ZeroGravity;
    // Update is called once per frame
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
    }
    public void SetGravityState(GravityState state)
    {
        currentState = state;

        if (currentState == GravityState.ZeroGravity)
        {
            // 무중력에서는 Unity 기본 중력 OFF
            rb.useGravity = false;
        }
    }
}
