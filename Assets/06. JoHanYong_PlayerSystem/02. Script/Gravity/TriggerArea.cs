using System.Collections.Generic;
using UnityEngine;

public class TriggerArea : MonoBehaviour
{
    public List<Rigidbody> RigidBodies { get; } = new();

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if (rb == null)
        {
            return; 
        }
        if (RigidBodies.Contains(rb))
        {
            return;
        }

        RigidBodies.Add(rb);
    }
    private void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;

        if(rb == null)
        {
            return;
        }

        RigidBodies.Remove(rb);
    }
}
