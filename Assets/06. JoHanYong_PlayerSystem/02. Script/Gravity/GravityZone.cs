using UnityEngine;

[RequireComponent(typeof(TriggerArea))]
public class GravityZone : MonoBehaviour
{
    [Header("Gravity")]
    [SerializeField] private float gravityPower = 25f;

    [Header("Rotation")]
    [SerializeField] private bool rotateToGround = true;

    private TriggerArea triggerArea;

    private void Awake()
    {
        triggerArea = GetComponent<TriggerArea>();
    }
    private void FixedUpdate()
    {
        for (int i = 0; i < triggerArea.RigidBodies.Count; i++)
        {
            Rigidbody rb = triggerArea.RigidBodies[i];

            if (rb == null)
            {
                continue;
            }

            ApplyGravity(rb);

            if (rotateToGround)
            {
                RotateToGround(rb);
            }
        }
        
    }
    private void OnTriggerEnter(Collider other) //중력 존 들어오면 중력 상태 부여
    {
        GravityController gravity = other.attachedRigidbody?.GetComponent<GravityController>();

        if (gravity == null)
        {
            return;
        }

        gravity.SetGravityState(GravityState.Gravity);
    }
    private void OnTriggerExit(Collider other) //중력 존 벗어나면 무중력 상태 부여
    {
        GravityController gravity = other.attachedRigidbody?.GetComponent<GravityController> ();
        
        if(gravity == null)
        {
            return;
        }
        gravity.SetGravityState (GravityState.ZeroGravity);
    }
    private void ApplyGravity(Rigidbody rb)
    {
        Vector3 gravityDirection = -transform.up;

        rb.useGravity = false;
        rb.AddForce(gravityDirection * gravityPower, ForceMode.Acceleration);
    }
    private void RotateToGround(Rigidbody rb)
    {
        Quaternion rotationDifference =
            Quaternion.FromToRotation(rb.transform.up, transform.up);

        Quaternion finalRotation =
            rotationDifference * rb.transform.rotation;

        rb.MoveRotation(finalRotation);
    }
}
